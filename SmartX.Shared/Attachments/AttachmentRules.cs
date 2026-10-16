namespace SmartX.Shared.Attachments;

/// <summary>
/// Type and size gates for a sensor attachment. The file is only opened after these pass, so a
/// huge or unexpected payload never reaches the summariser.
/// </summary>
public static class AttachmentRules
{
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string[]> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ["image/jpeg", "image/jpg", "application/octet-stream"],
        [".jpeg"] = ["image/jpeg", "image/jpg", "application/octet-stream"],
        [".png"] = ["image/png", "application/octet-stream"],
        [".txt"] = ["text/plain", "text/txt", "application/octet-stream"],
        [".log"] = ["text/plain", "text/x-log", "application/octet-stream"],
        [".json"] = ["application/json", "text/json", "text/plain", "application/octet-stream"],
        [".csv"] = ["text/csv", "text/plain", "application/csv", "application/octet-stream"]
    };

    public static IReadOnlyCollection<string> AllowedExtensions => AllowedTypes.Keys;

    public static bool TryValidate(string? fileName, string? contentType, long sizeBytes, out string error)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "An attachment must have a file name.";
            return false;
        }

        if (sizeBytes <= 0)
        {
            error = "An attachment must contain at least one byte.";
            return false;
        }

        if (sizeBytes > MaxSizeBytes)
        {
            error = $"Attachments are limited to {MaxSizeBytes / (1024 * 1024)} MB.";
            return false;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedTypes.TryGetValue(extension, out var acceptedTypes))
        {
            error = $"'{extension}' is not an allowed attachment type. Use {string.Join(", ", AllowedTypes.Keys)}.";
            return false;
        }

        var media = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Split(';')[0].Trim();
        if (!acceptedTypes.Contains(media, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Content type '{media}' does not match '{extension}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
