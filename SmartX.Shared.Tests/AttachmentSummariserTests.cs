using System.Text;
using SmartX.Shared.Attachments;

namespace SmartX.Shared.Tests;

public class AttachmentSummariserTests
{
    [Fact]
    public void Log_ReportsLineCountAndTheLastLines()
    {
        var bytes = Encoding.UTF8.GetBytes("boot\nlink up\nbrownout on rail 3\n");

        var summary = AttachmentSummariser.Summarise("node.log", bytes);

        Assert.Equal("log", summary.Kind);
        Assert.Equal(4, summary.LineCount);
        Assert.Contains("brownout on rail 3", summary.Preview);
    }

    [Fact]
    public void Json_CountsObjectFields()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"wifi_ssid":"SmartX-Mesh","poll_interval_ms":5000}""");

        var summary = AttachmentSummariser.Summarise("node.json", bytes);

        Assert.Equal("config", summary.Kind);
        Assert.Equal(2, summary.FieldCount);
        Assert.Contains("2 field", summary.Preview);
    }

    [Fact]
    public void Csv_CountsRowsAndColumns()
    {
        var bytes = Encoding.UTF8.GetBytes("ts,watts\n1,800\n2,910\n");

        var summary = AttachmentSummariser.Summarise("meter.csv", bytes);

        Assert.Equal(2, summary.FieldCount);
        Assert.Contains("column", summary.Preview);
    }

    [Fact]
    public void Png_ReadsWidthAndHeightFromTheIhdr()
    {
        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x10
        };

        var summary = AttachmentSummariser.Summarise("shot.png", png);

        Assert.Equal("photo", summary.Kind);
        Assert.Contains("32×16", summary.Preview);
    }
}
