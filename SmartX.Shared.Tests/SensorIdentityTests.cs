using SmartX.Shared.Contracts;

namespace SmartX.Shared.Tests;

public class SensorIdentityTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("A4:CF:12:01:AA:01")]
    public void AcceptsAnIeeeMacAddress(string value) => Assert.True(SensorIdentity.IsValid(value));

    [Fact]
    public void AcceptsADocumentedSmartXUniqueId() => Assert.True(SensorIdentity.IsValid("SX-NODE01"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("11:22:33")]
    [InlineData("CI:PWR:01")]
    [InlineData("sx-node01")]
    [InlineData("SX-AB")]
    public void RejectsJunkAndPartialAddresses(string? value) => Assert.False(SensorIdentity.IsValid(value));

    [Fact]
    public void DescribeViolation_NamesTheOffendingValue()
    {
        var message = SensorIdentity.DescribeViolation("abc");

        Assert.Contains("abc", message);
        Assert.Contains("IEEE MAC", message);
    }
}
