namespace VoiceLive.Web.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
