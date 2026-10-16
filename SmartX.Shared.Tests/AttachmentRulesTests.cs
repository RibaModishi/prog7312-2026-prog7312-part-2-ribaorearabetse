using SmartX.Shared.Attachments;

namespace SmartX.Shared.Tests;

public class AttachmentRulesTests
{
    [Fact]
    public void AcceptsALogUnderTheSizeCap()
    {
        Assert.True(AttachmentRules.TryValidate("node.log", "text/plain", 128, out var error));
        Assert.Empty(error);
    }

    [Fact]
    public void RejectsAnEmptyFile()
    {
        Assert.False(AttachmentRules.TryValidate("node.log", "text/plain", 0, out var error));
        Assert.Contains("at least one byte", error);
    }

    [Fact]
    public void RejectsAFileOverTwoMegabytes()
    {
        Assert.False(AttachmentRules.TryValidate("node.log", "text/plain", AttachmentRules.MaxSizeBytes + 1, out var error));
        Assert.Contains("2 MB", error);
    }

    [Fact]
    public void RejectsAnExecutableExtension()
    {
        Assert.False(AttachmentRules.TryValidate("payload.exe", "application/octet-stream", 64, out var error));
        Assert.Contains(".exe", error);
    }

    [Fact]
    public void RejectsAMismatchedContentType()
    {
        Assert.False(AttachmentRules.TryValidate("shot.png", "text/plain", 64, out var error));
        Assert.Contains("text/plain", error);
    }
}
