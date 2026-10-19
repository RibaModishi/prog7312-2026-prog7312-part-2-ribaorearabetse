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

