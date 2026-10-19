namespace SmartX.Client.Services;

/// <summary>
/// The Firebase ID token for this running app. Pages each receive their own API client, so the
/// token has to live here or only the login screen would be authorised.
/// </summary>
public sealed class OperatorSession
{
    public string? AccessToken { get; set; }
}
