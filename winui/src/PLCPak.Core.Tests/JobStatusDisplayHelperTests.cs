using PLCPak.Core.Models;

namespace PLCPak.Core.Tests;

public sealed class JobStatusDisplayHelperTests
{
    [Theory]
    [InlineData(JobStatus.Draft, "草稿", "草")]
    [InlineData(JobStatus.InboxReady, "待解压", "待解")]
    [InlineData(JobStatus.Extracting, "解压中", "解压")]
    [InlineData(JobStatus.Extracted, "已解压", "已解")]
    [InlineData(JobStatus.Processing, "处理中", "处理")]
    [InlineData(JobStatus.Processed, "待发布", "待发")]
    [InlineData(JobStatus.Published, "已发布", "已发")]
    [InlineData(JobStatus.Failed, "失败", "败")]
    [InlineData(JobStatus.Archived, "已归档", "档")]
    public void Maps_status_to_chinese_and_short_labels(JobStatus status, string chinese, string shortLabel)
    {
        Assert.Equal(chinese, JobStatusDisplayHelper.ToChinese(status));
        Assert.Equal(shortLabel, JobStatusDisplayHelper.ToShortLabel(status));
    }

    [Fact]
    public void ToLocalized_returns_english_labels()
    {
        Assert.Equal("Ready to publish", JobStatusDisplayHelper.ToLocalized(JobStatus.Processed, "en"));
        Assert.Equal("Rd", JobStatusDisplayHelper.ToShortLocalized(JobStatus.Processed, "en"));
    }
}