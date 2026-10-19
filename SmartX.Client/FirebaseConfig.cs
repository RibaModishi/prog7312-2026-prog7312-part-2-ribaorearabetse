namespace SmartX.Client;

/// <summary>
/// Public Firebase web config. Paste the class project's apiKey and projectId here.
/// These values are not passwords; Firebase expects client apps to ship them.
/// </summary>
public static class FirebaseConfig
{
    public const string ApiKey = "";

    public const string ProjectId = "";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ProjectId);
}
