using System.Text;
using System.Text.Json;

namespace SmartX.Shared.Attachments;

/// <summary>
/// Reads the bytes of an already-validated attachment and turns them into a short summary the
/// dashboard can show. This is the "use the file" step the Part 1 rubric asked for.
/// </summary>
public static class AttachmentSummariser
{
    private const int PreviewLines = 3;

    public static AttachmentSummary Summarise(string fileName, ReadOnlySpan<byte> bytes)
    {
        var extension = Path.GetExtension(fileName);
        var size = bytes.Length;

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return SummariseJson(fileName, bytes, size);
        }

        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return SummariseCsv(fileName, bytes, size);
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
        {
            return SummariseText(fileName, bytes, size, kind: extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ? "log" : "text");
        }

        return SummariseImage(fileName, bytes, size);
    }

    private static AttachmentSummary SummariseText(string fileName, ReadOnlySpan<byte> bytes, int size, string kind)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var last = lines.Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(PreviewLines);
        var preview = string.Join(" | ", last);
        if (string.IsNullOrWhiteSpace(preview))
        {
            preview = $"{lines.Length} line(s), no visible content.";
        }

        return new AttachmentSummary
        {
            FileName = fileName,
            Kind = kind,
            SizeInBytes = size,
            LineCount = lines.Length,
            Preview = preview
        };
    }

    private static AttachmentSummary SummariseJson(string fileName, ReadOnlySpan<byte> bytes, int size)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray());
            var fields = document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Count()
                : document.RootElement.GetArrayLength();

            return new AttachmentSummary
            {
                FileName = fileName,
                Kind = "config",
                SizeInBytes = size,
                FieldCount = fields,
                Preview = $"JSON {document.RootElement.ValueKind.ToString().ToLowerInvariant()} with {fields} field(s)."
            };
        }
        catch (JsonException ex)
        {
            return new AttachmentSummary
            {
                FileName = fileName,
                Kind = "config",
                SizeInBytes = size,
                Preview = $"JSON could not be parsed: {ex.Message}"
            };
        }
    }

    private static AttachmentSummary SummariseCsv(string fileName, ReadOnlySpan<byte> bytes, int size)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var columns = lines.Length == 0 ? 0 : lines[0].Split(',').Length;

        return new AttachmentSummary
        {
            FileName = fileName,
            Kind = "config",
            SizeInBytes = size,
            LineCount = lines.Length,
            FieldCount = columns,
            Preview = $"CSV with {lines.Length} row(s) and {columns} column(s)."
        };
    }

    private static AttachmentSummary SummariseImage(string fileName, ReadOnlySpan<byte> bytes, int size)
    {
        var preview = TryReadPngSize(bytes, out var width, out var height) ||
                      TryReadJpegSize(bytes, out width, out height)
            ? $"Photo {width}×{height}, {FormatKb(size)}."
            : $"Photo attached, {FormatKb(size)}.";

        return new AttachmentSummary
        {
            FileName = fileName,
            Kind = "photo",
            SizeInBytes = size,
            Preview = preview
        };
    }

    private static string FormatKb(int size) =>
        size >= 1024 ? $"{size / 1024.0:0.0} KB" : $"{size} bytes";

    private static bool TryReadPngSize(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 24)
        {
            return false;
        }

        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (!bytes[..8].SequenceEqual(signature))
        {
            return false;
        }

        width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return width > 0 && height > 0;
    }

    private static bool TryReadJpegSize(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return false;
        }

        var i = 2;
        while (i + 8 < bytes.Length)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = bytes[i + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                height = (bytes[i + 5] << 8) | bytes[i + 6];
                width = (bytes[i + 7] << 8) | bytes[i + 8];
                return width > 0 && height > 0;
            }

            if (i + 3 >= bytes.Length)
            {
                return false;
            }

            var length = (bytes[i + 2] << 8) | bytes[i + 3];
            i += 2 + length;
        }

        return false;
    }
}
