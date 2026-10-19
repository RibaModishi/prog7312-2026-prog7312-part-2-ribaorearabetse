namespace SmartX.Shared.Deployment;

/// <summary>
/// Recursively checks a deployment tree for structural faults. Every violation found anywhere in
/// the subtree is collected rather than stopping at the first one, so a single pass tells an
/// engineer everything that is wrong with a configuration profile.
/// </summary>
public static class DeploymentValidator
{
    public static List<string> Validate(DeploymentNode node, string parentPath = "")
    {
        var violations = new List<string>();
        var isNamed = !string.IsNullOrWhiteSpace(node.Name);
        var path = BuildPath(parentPath, isNamed ? node.Name : UnnamedLabel);

        if (!isNamed)
        {
            violations.Add(string.IsNullOrEmpty(parentPath)
                ? "The root node has a missing or blank name."
                : $"'{parentPath}' contains a node with a missing or blank name.");
        }

        if (node.NodeType == DeploymentNodeType.Sensor && node.Children.Count > 0)
        {
            violations.Add($"Sensor '{path}' must not contain child nodes.");
        }

        // Grouping only the immediate children keeps this check to one level, so nodes that share
        // a name under different parents elsewhere in the tree are left alone. Casing is ignored
        // because two sibling zones differing only in case would be indistinguishable on a
        // dashboard and almost certainly a configuration mistake.
        var duplicateSiblings = node.Children
            .Where(child => !string.IsNullOrWhiteSpace(child.Name))
            .GroupBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .Where(siblings => siblings.Count() > 1);

        foreach (var siblings in duplicateSiblings)
        {
            violations.Add($"'{path}' contains {siblings.Count()} sibling nodes named '{siblings.Key}'.");
        }

        foreach (var child in node.Children)
        {
            violations.AddRange(Validate(child, path));
        }

        return violations;
    }

    private const string UnnamedLabel = "(unnamed)";

    private static string BuildPath(string parentPath, string name) =>
        string.IsNullOrEmpty(parentPath) ? name : $"{parentPath} > {name}";
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

