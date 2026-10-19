using SmartX.Shared.Deployment;

namespace SmartX.Shared.Tests;

public class DeploymentValidatorTests
{
    [Fact]
    public void Validate_ReturnsNoViolationsForAWellFormedTree()
    {
        var facility = new DeploymentNode
        {
            Name = "Facility A",
            NodeType = DeploymentNodeType.Facility,
            Children =
            [
                new DeploymentNode
                {
                    Name = "Zone 1",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode { Name = "ESP32-SOIL-01", NodeType = DeploymentNodeType.Sensor }
                    ]
                },
                new DeploymentNode { Name = "Zone 2", NodeType = DeploymentNodeType.Zone }
            ]
        };

        var violations = DeploymentValidator.Validate(facility);

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_WalksAFacilityZoneSubZoneSensorChainWithoutComplaint()
    {
        var facility = new DeploymentNode
        {
            Name = "Facility A",
            NodeType = DeploymentNodeType.Facility,
            Children =
            [
                new DeploymentNode
                {
                    Name = "Zone 1",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode
                        {
                            Name = "Sub-Zone B",
                            NodeType = DeploymentNodeType.SubZone,
                            Children =
                            [
                                new DeploymentNode
                                {
                                    Name = "ESP32-MTR-07",
                                    NodeType = DeploymentNodeType.Sensor
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var violations = DeploymentValidator.Validate(facility);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReportsABlankNamedNodeAgainstItsParentPath(string blankName)
    {
        var facility = new DeploymentNode
        {
            Name = "Facility A",
            NodeType = DeploymentNodeType.Facility,
            Children =
            [
                new DeploymentNode
                {
                    Name = "Zone 1",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode { Name = blankName, NodeType = DeploymentNodeType.SubZone }
                    ]
                }
            ]
        };

        var violations = DeploymentValidator.Validate(facility);

        var violation = Assert.Single(violations);
        Assert.Contains("Facility A > Zone 1", violation);
        Assert.Contains("blank name", violation);
    }

    [Fact]
    public void Validate_ReportsTwoDirectSiblingsSharingAName()
    {
        var zone = new DeploymentNode
        {
            Name = "Zone 1",
            NodeType = DeploymentNodeType.Zone,
            Children =
            [
                new DeploymentNode { Name = "Sub-Zone B", NodeType = DeploymentNodeType.SubZone },
                new DeploymentNode { Name = "Sub-Zone B", NodeType = DeploymentNodeType.SubZone }
            ]
        };

        var violations = DeploymentValidator.Validate(zone);

        var violation = Assert.Single(violations);
        Assert.Contains("Zone 1", violation);
        Assert.Contains("Sub-Zone B", violation);
    }

    [Fact]
    public void Validate_ReportsSiblingsWhoseNamesDifferOnlyByCase()
    {
        var zone = new DeploymentNode
        {
            Name = "Zone 1",
            NodeType = DeploymentNodeType.Zone,
            Children =
            [
                new DeploymentNode { Name = "Sub-Zone B", NodeType = DeploymentNodeType.SubZone },
                new DeploymentNode { Name = "sub-zone b", NodeType = DeploymentNodeType.SubZone }
            ]
        };

        var violations = DeploymentValidator.Validate(zone);

        var violation = Assert.Single(violations);
        Assert.Contains("sibling nodes named", violation);
    }

    [Fact]
    public void Validate_AllowsTheSameNameUnderDifferentParents()
    {
        var facility = new DeploymentNode
        {
            Name = "Facility A",
            NodeType = DeploymentNodeType.Facility,
            Children =
            [
                new DeploymentNode
                {
                    Name = "Zone 1",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode { Name = "Sub-Zone B", NodeType = DeploymentNodeType.SubZone }
                    ]
                },
                new DeploymentNode
                {
                    Name = "Zone 2",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode { Name = "Sub-Zone B", NodeType = DeploymentNodeType.SubZone }
                    ]
                }
            ]
        };

        var violations = DeploymentValidator.Validate(facility);

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsASensorThatContainsChildNodes()
    {
        var zone = new DeploymentNode
        {
            Name = "Zone 1",
            NodeType = DeploymentNodeType.Zone,
            Children =
            [
                new DeploymentNode
                {
                    Name = "ESP32-VLV-03",
                    NodeType = DeploymentNodeType.Sensor,
                    Children =
                    [
                        new DeploymentNode { Name = "Sub-Zone C", NodeType = DeploymentNodeType.SubZone }
                    ]
                }
            ]
        };

        var violations = DeploymentValidator.Validate(zone);

        var violation = Assert.Single(violations);
        Assert.Contains("Zone 1 > ESP32-VLV-03", violation);
        Assert.Contains("must not contain child nodes", violation);
    }

    [Fact]
    public void Validate_ReturnsEveryViolationInTheTreeRatherThanStoppingAtTheFirst()
    {
        var facility = new DeploymentNode
        {
            Name = "Facility A",
            NodeType = DeploymentNodeType.Facility,
            Children =
            [
                new DeploymentNode
                {
                    Name = "Zone 1",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode
                        {
                            Name = "ESP32-SOIL-01",
                            NodeType = DeploymentNodeType.Sensor,
                            Children =
                            [
                                new DeploymentNode { Name = "Sub-Zone X", NodeType = DeploymentNodeType.SubZone }
                            ]
                        },
                        new DeploymentNode { Name = "  ", NodeType = DeploymentNodeType.SubZone }
                    ]
                },
                new DeploymentNode
                {
                    Name = "Zone 2",
                    NodeType = DeploymentNodeType.Zone,
                    Children =
                    [
                        new DeploymentNode { Name = "Sub-Zone A", NodeType = DeploymentNodeType.SubZone },
                        new DeploymentNode { Name = "Sub-Zone A", NodeType = DeploymentNodeType.SubZone }
                    ]
                }
            ]
        };

        var violations = DeploymentValidator.Validate(facility);

        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, v => v.Contains("must not contain child nodes"));
        Assert.Contains(violations, v => v.Contains("blank name"));
        Assert.Contains(violations, v => v.Contains("sibling nodes named 'Sub-Zone A'"));
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

