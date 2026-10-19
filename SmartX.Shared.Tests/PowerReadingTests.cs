using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Tests;

public class PowerReadingTests
{
    [Fact]
    public void Addition_AggregatesTheLoadOfTwoMeters()
    {
        var meter1 = new PowerReading(240.5);
        var meter2 = new PowerReading(112.25);

        var meter3 = meter1 + meter2;

        Assert.Equal(352.75, meter3.Watts, 3);
    }

    [Fact]
    public void Subtraction_ProducesTheDeltaBetweenTwoReadings()
    {
        var current = new PowerReading(400);
        var previous = new PowerReading(325);

        var delta = current - previous;

        Assert.Equal(75, delta.Watts, 3);
    }

    [Fact]
    public void Subtraction_ReportsNegativeDeltaWhenConsumptionDrops()
    {
        var current = new PowerReading(150);
        var previous = new PowerReading(210);

        var delta = current - previous;

        Assert.Equal(-60, delta.Watts, 3);
    }

    [Fact]
    public void GreaterThan_DetectsAReadingAboveAnAlertThreshold()
    {
        var reading = new PowerReading(1_450);
        var threshold = new PowerReading(1_000);

        Assert.True(reading > threshold);
        Assert.False(reading < threshold);
    }

    [Fact]
    public void LessThan_DetectsAReadingBelowAnAlertThreshold()
    {
        var reading = new PowerReading(320);
        var threshold = new PowerReading(1_000);

        Assert.True(reading < threshold);
        Assert.False(reading > threshold);
    }

    [Fact]
    public void Comparisons_AreFalseInBothDirectionsForEqualReadings()
    {
        var left = new PowerReading(500);
        var right = new PowerReading(500);

        Assert.False(left > right);
        Assert.False(left < right);
    }

    [Fact]
    public void GreaterThanOrEqual_TriggersOnAReadingSittingExactlyOnTheThreshold()
    {
        var reading = new PowerReading(1_000);
        var threshold = new PowerReading(1_000);

        Assert.True(reading >= threshold);
        Assert.True(reading <= threshold);
    }

    [Fact]
    public void InclusiveComparisons_StillRespectReadingsEitherSideOfTheThreshold()
    {
        var threshold = new PowerReading(1_000);

        Assert.True(new PowerReading(1_001) >= threshold);
        Assert.False(new PowerReading(999) >= threshold);
        Assert.True(new PowerReading(999) <= threshold);
        Assert.False(new PowerReading(1_001) <= threshold);
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

