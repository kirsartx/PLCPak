using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class QuickStatsServiceTests
{
    [Fact]
    public void BuildOneLiner_formats_pending_failed_active_counts()
    {
        var jobs = new[]
        {
            new PublishJob { Status = JobStatus.Processed },
            new PublishJob { Status = JobStatus.Processed },
            new PublishJob { Status = JobStatus.Processed },
            new PublishJob { Status = JobStatus.Failed },
            new PublishJob { Status = JobStatus.Extracted },
            new PublishJob { Status = JobStatus.InboxReady },
            new PublishJob { Status = JobStatus.InboxReady },
            new PublishJob { Status = JobStatus.Processing },
            new PublishJob { Status = JobStatus.Draft }
        };

        var line = QuickStatsService.BuildOneLiner(jobs);

        Assert.Contains("待发布 3", line);
        Assert.Contains("失败 1", line);
        Assert.Contains("TG待发", line);
        Assert.Contains("重复", line);
        Assert.Contains("陈旧", line);
    }

    [Fact]
    public void BuildOneLiner_returns_zeros_when_empty()
    {
        var line = QuickStatsService.BuildOneLiner([]);
        Assert.Contains("待发布 0", line);
        Assert.Contains("失败 0", line);
        Assert.Contains("TG待发 0", line);
    }
}