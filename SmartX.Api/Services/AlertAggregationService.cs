using SmartX.Shared.Contracts;
using SmartX.Shared.Telemetry;

namespace SmartX.Api.Services;

/// <summary>
/// Folds incoming alerts into per-sensor time windows rather than listing every fault as its own
/// row. Two readings that breach a threshold ten seconds apart are almost always the same episode,
/// so they share a group; a breach an hour later is a new one. The window length is the same
/// shared-property-plus-time-window rule argued for in the Task 1 research: group by sensor, then
/// by how close the timestamps sit, and stop there.
/// </summary>
public class AlertAggregationService
{
    /// <summary>
    /// Tunable. A burst of ESP32 polls at the 5-second interval should collapse into one group;
    /// a later, unrelated fault should not. Thirty seconds covers a handful of consecutive polls
    /// without stretching far enough to glue distinct incidents together.
    /// </summary>
    public static readonly TimeSpan GroupingWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Tunable. Groups whose last alert is older than this drop off the dashboard so a resolved
    /// incident does not sit there indefinitely. They remain in memory for the process lifetime.
    /// </summary>
    public static readonly TimeSpan DashboardRetention = TimeSpan.FromMinutes(5);

    public const double PowerWattsThreshold = 1_500;
    public const double EnvironmentalLowerBound = 10;
    public const double EnvironmentalUpperBound = 90;

    private readonly object _gate = new();
    private readonly List<AlertGroup> _groups = [];

    public void RecordAlert(Alert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        lock (_gate)
        {
            var open = _groups
                .Where(group =>
                    string.Equals(group.SensorId, alert.SensorId, StringComparison.OrdinalIgnoreCase)
                    && alert.Timestamp - group.WindowEnd <= GroupingWindow)
                .OrderByDescending(group => group.WindowEnd)
                .FirstOrDefault();

            if (open is null)
            {
                _groups.Add(new AlertGroup
                {
                    SensorId = alert.SensorId,
                    DeploymentPath = alert.DeploymentPath,
                    WindowStart = alert.Timestamp,
                    WindowEnd = alert.Timestamp,
                    Alerts = [alert]
                });
                return;
            }

            open.Alerts.Add(alert);

            if (alert.Timestamp > open.WindowEnd)
            {
                open.WindowEnd = alert.Timestamp;
            }
        }
    }

    public IReadOnlyList<AlertGroup> GetActiveGroups()
    {
        lock (_gate)
        {
            var cutoff = DateTime.UtcNow - DashboardRetention;

            return _groups
                .Where(group => group.WindowEnd >= cutoff)
                .OrderByDescending(group => group.WindowEnd)
                .Select(Clone)
                .ToList();
        }
    }

    /// <summary>
    /// Actuator states have no numeric band worth alerting on — open versus closed is an operator
    /// decision, not an out-of-range reading — so only Environmental and PowerConsumption can
    /// produce a threshold breach from here.
    /// </summary>
    public void CheckThreshold(string sensorId, string deploymentPath, SensorCategory category, double value)
    {
        var message = category switch
        {
            SensorCategory.PowerConsumption when value > PowerWattsThreshold =>
                $"Power draw {value:0.##}W exceeds {PowerWattsThreshold:0.##}W threshold.",
            SensorCategory.Environmental when value < EnvironmentalLowerBound =>
                $"Environmental reading {value:0.##} is below the {EnvironmentalLowerBound:0.##}–{EnvironmentalUpperBound:0.##} operating band.",
            SensorCategory.Environmental when value > EnvironmentalUpperBound =>
                $"Environmental reading {value:0.##} exceeds the {EnvironmentalLowerBound:0.##}–{EnvironmentalUpperBound:0.##} operating band.",
            _ => null
        };

        if (message is null)
        {
            return;
        }

        RecordAlert(new Alert
        {
            SensorId = sensorId,
            DeploymentPath = deploymentPath,
            Category = category,
            Type = AlertType.ThresholdBreach,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private static AlertGroup Clone(AlertGroup group) => new()
    {
        SensorId = group.SensorId,
        DeploymentPath = group.DeploymentPath,
        WindowStart = group.WindowStart,
        WindowEnd = group.WindowEnd,
        Alerts = [.. group.Alerts]
    };
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

