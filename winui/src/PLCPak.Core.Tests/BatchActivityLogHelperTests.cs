using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchActivityLogHelperTests
{
    [Fact]
    public void FormatJobIdSample_returns_empty_for_no_ids()
    {
        Assert.Equal(string.Empty, BatchActivityLogHelper.FormatJobIdSample([]));
    }

    [Fact]
    public void FormatJobIdSample_joins_small_lists()
    {
        Assert.Equal("a, b", BatchActivityLogHelper.FormatJobIdSample(["a", "b"]));
    }

    [Fact]
    public void FormatJobIdSample_truncates_large_lists()
    {
        var sample = BatchActivityLogHelper.FormatJobIdSample(
            ["j1", "j2", "j3", "j4", "j5", "j6"],
            maxSample: 3);

        Assert.Equal("j1, j2, j3 等 6 个", sample);
    }
}