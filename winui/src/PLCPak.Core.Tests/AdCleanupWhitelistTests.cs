using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class AdCleanupWhitelistTests
{
    [Theory]
    [InlineData("save/data/file.txt", "save/data/file.txt", true)]
    [InlineData("save/data/file.txt", "other/file.txt", false)]
    [InlineData("mods/foo/bar", "mods/*/bar", true)]
    [InlineData("mods/foo/bar", "mods/foo/*", true)]
    [InlineData("mods/foo/bar", "mods/?oo/bar", true)]
    public void IsWhitelisted_matches_exact_and_wildcard_patterns(string relativePath, string pattern, bool expected)
    {
        var result = AdCleanupService.IsWhitelisted(relativePath, [pattern]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsWhitelisted_normalizes_slashes()
    {
        var result = AdCleanupService.IsWhitelisted("save/data/file.txt", ["save\\data\\file.txt"]);

        Assert.True(result);
    }
}