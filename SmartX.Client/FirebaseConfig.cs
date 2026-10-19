namespace SmartX.Client;

/// <summary>
/// Public Firebase web config. Paste the class project's apiKey and projectId here.
/// These values are not passwords; Firebase expects client apps to ship them.
/// </summary>
public static class FirebaseConfig
{
    public const string ApiKey = "AIzaSyAYdCG4NCufZsJ9GwB6siZakK7Olt22BkU";

    public const string ProjectId = "smart-x-741d7";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ProjectId);
}
