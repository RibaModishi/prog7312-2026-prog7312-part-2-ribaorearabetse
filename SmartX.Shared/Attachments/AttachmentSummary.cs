namespace SmartX.Shared.Attachments;

/// <summary>
/// What the gateway learned from an attachment after saving it. The file is not just stored: logs
/// contribute their last lines, configs their field counts, and photos their size (and dimensions
/// when the header can be read cheaply).
/// </summary>
public sealed class AttachmentSummary
{
    public required string FileName { get; init; }

    public required string Kind { get; init; }

    public required long SizeInBytes { get; init; }

    public required string Preview { get; init; }

    public int? LineCount { get; init; }

    public int? FieldCount { get; init; }
}
