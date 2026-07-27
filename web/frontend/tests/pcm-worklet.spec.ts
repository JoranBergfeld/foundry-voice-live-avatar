import { expect, Page, test } from "@playwright/test";

async function countPostedSamples(page: Page, sourceRate: number, inputSamples: number) {
  await page.goto("/");

  return page.evaluate(
    async ({ sourceRate, inputSamples }) => {
      let processorConstructor:
        | (new () => { process(inputs: Float32Array[][]): boolean })
        | undefined;
      let postedSamples = 0;

      class MockAudioWorkletProcessor {
        port = {
          postMessage(message: ArrayBuffer, transfer: Transferable[]) {
            postedSamples += new Int16Array(message).length;
            structuredClone(message, { transfer });
          },
        };
      }

      Object.assign(globalThis, {
        AudioWorkletProcessor: MockAudioWorkletProcessor,
        registerProcessor: (
          _name: string,
          constructor: new () => { process(inputs: Float32Array[][]): boolean },
        ) => {
          processorConstructor = constructor;
        },
        sampleRate: sourceRate,
      });

      const script = await (await fetch("/pcm-worklet.js")).text();
      (0, eval)(script);
      if (!processorConstructor) throw new Error("pcm16-worklet was not registered");

      const processor = new processorConstructor();
      for (let offset = 0; offset < inputSamples; offset += 128) {
        const chunkLength = Math.min(128, inputSamples - offset);
        processor.process([[new Float32Array(chunkLength)]]);
      }

      return postedSamples;
    },
    { sourceRate, inputSamples },
  );
}

test("resamples one second from 44.1 kHz to exactly 24,000 samples", async ({ page }) => {
  expect(await countPostedSamples(page, 44_100, 44_100)).toBe(24_000);
});

test("resamples one second from 48 kHz to exactly 24,000 samples", async ({ page }) => {
  expect(await countPostedSamples(page, 48_000, 48_000)).toBe(24_000);
});

test("passes through one second at 24 kHz as exactly 24,000 samples", async ({ page }) => {
  expect(await countPostedSamples(page, 24_000, 24_000)).toBe(24_000);
});
