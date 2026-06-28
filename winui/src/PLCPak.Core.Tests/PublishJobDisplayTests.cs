using PLCPak.Core.Models;

namespace PLCPak.Core.Tests;

public sealed class PublishJobDisplayTests
{
    [Fact]
    public void StatusLabel_returns_chinese_label()
    {
        var job = new PublishJob { Status = JobStatus.Processed };
        Assert.Equal("待发布", job.StatusLabel);
    }
}