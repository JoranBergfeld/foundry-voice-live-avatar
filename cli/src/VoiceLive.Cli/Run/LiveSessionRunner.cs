using System.Diagnostics;
using Azure;
using Azure.AI.VoiceLive;
using Azure.Identity;
using NAudio.Wave;
using VoiceLive.Cli.Config;
using VoiceLive.Cli.Session;

namespace VoiceLive.Cli.Run;

public sealed record RunOptions(
    string ConfigDir,
    string Mode,
    string? Text,
    string? AudioFile,
    int Seconds,
    string? Instructions);

public static class LiveSessionRunner
{
    private const string DefaultInstructions = "You are a helpful avatar assistant. Reply clearly and concisely.";

    public static async Task<int> RunAsync(string[] args, TextWriter outw, TextWriter errw)
    {
        var options = Parse(args);
        if (options is null)
        {
            errw.WriteLine("usage: voicelive-cli run --config <dir> [--mode model|agent] [--text <prompt>] [--audio-file <path>] [--seconds <n>] [--instructions <text>]");
            return 2;
        }

        try
        {
            var config = ConfigLoader.Load(options.ConfigDir);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.Seconds));
            var client = CreateClient(config, errw);

            if (options.Mode.Equals("agent", StringComparison.OrdinalIgnoreCase))
                return await RunAgentAsync(client, config, options, outw, errw, cts.Token);

            if (!options.Mode.Equals("model", StringComparison.OrdinalIgnoreCase))
            {
                errw.WriteLine($"run: unsupported mode '{options.Mode}'. Use 'model' or 'agent'.");
                return 2;
            }

            var instructions = ResolveInstructions(options.ConfigDir, options.Instructions);
            await using var liveSession = await client.StartSessionAsync(config.Session.Model, cts.Token);
            var sessionOptions = SessionOptionsBuilder.Build(config, instructions);
            await liveSession.ConfigureSessionAsync(sessionOptions, cts.Token);

            if (options.Text is not null)
                return await RunTextAsync(liveSession, options.Text, outw, errw, cts.Token);
            if (options.AudioFile is not null)
                return await RunAudioFileAsync(liveSession, options.AudioFile, outw, errw, cts.Token);

            return await RunMicAsync(liveSession, outw, errw, cts.Token);
        }
        catch (ConfigValidationException ex)
        {
            errw.WriteLine(ex.Message);
            return 1;
        }
        catch (RequestFailedException ex)
        {
            errw.WriteLine($"Voice Live request failed: status={ex.Status} code={ex.ErrorCode}");
            errw.WriteLine(ex.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            errw.WriteLine($"Voice Live run timed out after {options?.Seconds ?? 0} seconds. Increase --seconds or retry after checking connectivity.");
            return 1;
        }
        catch (Exception ex)
        {
            errw.WriteLine($"Voice Live run failed: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException is not null) errw.WriteLine($"Inner error: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            return 1;
        }
    }

    private static VoiceLiveClient CreateClient(AppConfig config, TextWriter errw)
    {
        var serviceVersion = ServiceVersionMapper.Map(config.Session.ApiVersion, errw);
        var clientOptions = new VoiceLiveClientOptions(serviceVersion);
        return new VoiceLiveClient(new Uri(config.Session.Endpoint), new DefaultAzureCredential(), clientOptions);
    }

    private static async Task<int> RunAgentAsync(VoiceLiveClient client, AppConfig config, RunOptions options, TextWriter outw, TextWriter errw, CancellationToken ct)
    {
        var agent = new AgentSessionConfig(config.Agent.AgentName, config.Agent.AgentProjectName);
        if (!string.IsNullOrWhiteSpace(config.Agent.AgentVersion)) agent.AgentVersion = config.Agent.AgentVersion;

        try
        {
            await using var liveSession = await client.StartSessionAsync(SessionTarget.FromAgent(agent), ct);
            await liveSession.ConfigureSessionAsync(SessionOptionsBuilder.BuildForAgent(config), ct);
            if (options.Text is not null) return await RunTextAsync(liveSession, options.Text, outw, errw, ct);
            errw.WriteLine("Agent mode connected, but this project does not yet have a live-verified Foundry agent. Use --text or create/sync the agent in a later phase.");
            return 1;
        }
        catch (Exception ex) when (ex is RequestFailedException or InvalidOperationException)
        {
            errw.WriteLine("Agent mode requires a Foundry agent created by the later tools/sync-agent flow and is not yet live-verified.");
            errw.WriteLine($"Agent mode failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunTextAsync(VoiceLiveSession liveSession, string prompt, TextWriter outw, TextWriter errw, CancellationToken ct)
    {
        var transcript = new System.Text.StringBuilder();
        var audioBytes = 0;
        long? firstAudioMs = null;
        var responseClock = new Stopwatch();
        var requested = false;

        await foreach (var update in liveSession.GetUpdatesAsync(ct))
        {
            switch (update)
            {
                case SessionUpdateSessionUpdated:
                    if (!requested)
                    {
                        requested = true;
                        await liveSession.AddItemAsync(new UserMessageItem(prompt), ct);
                        responseClock.Restart();
                        await liveSession.StartResponseAsync(ct);
                    }
                    break;
                case SessionUpdateResponseAudioTranscriptDelta delta:
                    transcript.Append(delta.Delta);
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateResponseTextDelta delta:
                    transcript.Append(delta.Delta);
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateResponseAudioDelta delta:
                    firstAudioMs ??= responseClock.ElapsedMilliseconds;
                    audioBytes += delta.Delta?.ToArray().Length ?? 0;
                    break;
                case SessionUpdateResponseDone:
                    if (transcript.Length == 0 && audioBytes == 0)
                    {
                        errw.WriteLine("Voice Live response completed without transcript or audio.");
                        return 1;
                    }
                    outw.WriteLine();
                    outw.WriteLine($"Final transcript: {transcript.ToString().Trim()}");
                    outw.WriteLine($"Audio bytes: {audioBytes}");
                    outw.WriteLine($"First audio latency: {(firstAudioMs.HasValue ? firstAudioMs.Value + " ms" : "n/a")}");
                    return 0;
                case SessionUpdateError error:
                    errw.WriteLine($"Voice Live error: type={error.Error?.Type} code={error.Error?.Code} message={error.Error?.Message}");
                    return 1;
            }
        }

        errw.WriteLine("Voice Live update stream ended before a response completed.");
        return 1;
    }

    private static async Task<int> RunAudioFileAsync(VoiceLiveSession liveSession, string path, TextWriter outw, TextWriter errw, CancellationToken ct)
    {
        var pcm = ReadPcm16Mono24kWav(path);
        var transcript = new System.Text.StringBuilder();
        var audioBytes = 0;
        long? firstAudioMs = null;
        var responseClock = new Stopwatch();
        var sent = false;

        await foreach (var update in liveSession.GetUpdatesAsync(ct))
        {
            switch (update)
            {
                case SessionUpdateSessionUpdated when !sent:
                    sent = true;
                    foreach (var chunk in pcm.Chunk(4800))
                        await liveSession.SendInputAudioAsync(chunk, ct);
                    responseClock.Restart();
                    await liveSession.CommitInputAudioAsync(ct);
                    await liveSession.StartResponseAsync(ct);
                    break;
                case SessionUpdateInputAudioBufferSpeechStopped:
                    responseClock.Restart();
                    break;
                case SessionUpdateConversationItemInputAudioTranscriptionDelta delta:
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateConversationItemInputAudioTranscriptionCompleted completed:
                    outw.WriteLine();
                    outw.WriteLine($"Input transcript: {completed.Transcript}");
                    break;
                case SessionUpdateResponseAudioTranscriptDelta delta:
                    transcript.Append(delta.Delta);
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateResponseTextDelta delta:
                    transcript.Append(delta.Delta);
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateResponseAudioDelta delta:
                    firstAudioMs ??= responseClock.ElapsedMilliseconds;
                    audioBytes += delta.Delta?.ToArray().Length ?? 0;
                    break;
                case SessionUpdateResponseDone:
                    outw.WriteLine();
                    outw.WriteLine($"Final transcript: {transcript.ToString().Trim()}");
                    outw.WriteLine($"Audio bytes: {audioBytes}");
                    outw.WriteLine($"First audio latency: {(firstAudioMs.HasValue ? firstAudioMs.Value + " ms" : "n/a")}");
                    return 0;
                case SessionUpdateError error:
                    errw.WriteLine($"Voice Live error: type={error.Error?.Type} code={error.Error?.Code} message={error.Error?.Message}");
                    return 1;
            }
        }

        errw.WriteLine("Voice Live update stream ended before a response completed.");
        return 1;
    }

    private static async Task<int> RunMicAsync(VoiceLiveSession liveSession, TextWriter outw, TextWriter errw, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            errw.WriteLine("Live microphone mode requires Windows because this CLI uses NAudio WaveInEvent/WaveOutEvent. On this platform, use --text or --audio-file.");
            return 1;
        }

        using var waveIn = new WaveInEvent { WaveFormat = new WaveFormat(24000, 16, 1), BufferMilliseconds = 50 };
        var playback = new BufferedWaveProvider(new WaveFormat(24000, 16, 1));
        using var waveOut = new WaveOutEvent();
        waveOut.Init(playback);
        waveOut.Play();
        waveIn.DataAvailable += async (_, e) =>
        {
            try
            {
                var buffer = e.Buffer.Take(e.BytesRecorded).ToArray();
                await liveSession.SendInputAudioAsync(buffer, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errw.WriteLine($"Microphone audio send failed: {ex.Message}");
            }
        };

        waveIn.StartRecording();
        outw.WriteLine("Microphone streaming started. Speak now; stop with Ctrl+C or --seconds timeout.");
        await foreach (var update in liveSession.GetUpdatesAsync(ct))
        {
            switch (update)
            {
                case SessionUpdateResponseAudioDelta delta:
                    var bytes = delta.Delta?.ToArray() ?? [];
                    playback.AddSamples(bytes, 0, bytes.Length);
                    break;
                case SessionUpdateResponseAudioTranscriptDelta delta:
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateResponseTextDelta delta:
                    outw.Write(delta.Delta);
                    break;
                case SessionUpdateError error:
                    errw.WriteLine($"Voice Live error: type={error.Error?.Type} code={error.Error?.Code} message={error.Error?.Message}");
                    return 1;
            }
        }

        return 0;
    }

    private static byte[] ReadPcm16Mono24kWav(string path)
    {
        using var reader = new WaveFileReader(path);
        if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Channels != 1 || reader.WaveFormat.SampleRate != 24000)
            throw new ConfigValidationException($"--audio-file must be a PCM16 mono 24kHz WAV. Actual format: {reader.WaveFormat}.");
        using var ms = new MemoryStream();
        reader.CopyTo(ms);
        return ms.ToArray();
    }

    private static string ResolveInstructions(string configDir, string? overrideInstructions)
    {
        if (!string.IsNullOrWhiteSpace(overrideInstructions)) return overrideInstructions;
        var grounding = Path.Combine(configDir, "grounding", "company-direction.md");
        return File.Exists(grounding) ? File.ReadAllText(grounding) : DefaultInstructions;
    }

    private static RunOptions? Parse(string[] args)
    {
        string configDir = "config";
        string mode = "model";
        string? text = null;
        string? audioFile = null;
        string? instructions = null;
        var seconds = 30;

        for (var i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (args[i])
            {
                case "--config":
                    configDir = Next() ?? "";
                    if (configDir.Length == 0) return null;
                    break;
                case "--mode":
                    mode = Next() ?? "";
                    if (mode.Length == 0) return null;
                    break;
                case "--text":
                    text = Next();
                    if (text is null) return null;
                    break;
                case "--audio-file":
                    audioFile = Next();
                    if (audioFile is null) return null;
                    break;
                case "--seconds":
                    var raw = Next();
                    if (!int.TryParse(raw, out seconds) || seconds <= 0) return null;
                    break;
                case "--instructions":
                    instructions = Next();
                    if (instructions is null) return null;
                    break;
                default: return null;
            }
        }

        if (text is not null && audioFile is not null) return null;
        return new RunOptions(configDir, mode, text, audioFile, seconds, instructions);
    }
}
