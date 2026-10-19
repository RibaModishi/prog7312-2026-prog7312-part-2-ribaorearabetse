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


// References (IIE Harvard). Accessed: 19 October 2026.
// Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to algorithms. 4th edn. Cambridge, MA: MIT Press.
// East, D. (n.d.) Getting started with Firebase Auth on the Web. Available at: https://www.youtube.com/watch?v=-OKrloDzGpU
// Firebase (n.d.) Firebase Authentication REST API. Available at: https://firebase.google.com/docs/reference/rest/auth
// Firebase (n.d.) Get started with Firebase Authentication on websites. Available at: https://firebase.google.com/docs/auth/web/start
// Firebase (n.d.) Verify ID tokens. Available at: https://firebase.google.com/docs/auth/admin/verify-id-tokens
// Google Cloud (n.d.) Method: accounts.signInWithPassword. Available at: https://docs.cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithPassword
// Janbodnar (n.d.) C# PriorityQueue. Available at: https://zetcode.com/csharp/priority-queue/
// Microsoft (n.d.) Dictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2
// Microsoft (n.d.) HashSet<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1
// Microsoft (n.d.) Overview of ASP.NET Core authentication. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
// Microsoft (n.d.) PriorityQueue<TElement,TPriority> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2
// Microsoft (n.d.) Queue<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1
// Microsoft (n.d.) SortedDictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2
// Microsoft (n.d.) Stack<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1
// Microsoft (n.d.) Use HttpClientFactory to implement resilient HTTP requests. Available at: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory
// NetMentor (2021) PriorityQueue in C#. Available at: https://www.netmentor.es/entrada/en/priorityqueue-csharp
// .NET Core Tutorials (n.d.) PriorityQueue in C# .NET. Available at: https://dotnetcoretutorials.com/priorityqueue-in-net/
// Roy Chowdhury, K. (2024) Learn Advanced C# Part 1: Step by step build PriorityQueue. Available at: https://www.youtube.com/watch?v=ikbp6vph-6s
// YouTube (n.d.) C# Collections tutorial: List, HashSet, Dictionary, Queue and Stack. Available at: https://www.youtube.com/watch?v=4Mi4OuSMo4A
// YouTube (n.d.) C# collections: Stack, Queue and Dictionary. Available at: https://www.youtube.com/watch?v=GmftVtxrKUc
// YouTube (n.d.) C# hands on: queues. Available at: https://www.youtube.com/watch?v=huzE208-ha8
// YouTube (n.d.) Master the PriorityQueue, a collection in .NET. Available at: https://www.youtube.com/watch?v=d262kUQKT0E

