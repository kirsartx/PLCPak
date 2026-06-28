using PLCPak.Core.Models;

namespace PLCPak.Core.Tests;

public sealed class PublishLinkValidatorTests
{
    [Theory]
    [InlineData("https://pan.baidu.com/s/abc", true)]
    [InlineData("https://example.com/share", false)]
    [InlineData("", false)]
    public void ValidateBaiduLink_checks_domain(string url, bool expected)
    {
        Assert.Equal(expected, PublishLinkValidator.ValidateBaiduLink(url));
    }

    [Theory]
    [InlineData("https://pan.quark.cn/s/abc", true)]
    [InlineData("https://quark.cn/s/abc", true)]
    [InlineData("https://example.com/share", false)]
    public void ValidateQuarkLink_checks_domain(string url, bool expected)
    {
        Assert.Equal(expected, PublishLinkValidator.ValidateQuarkLink(url));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("abcd1234", true)]
    [InlineData("ab", false)]
    [InlineData("abcdefghi", false)]
    [InlineData("abc!", false)]
    public void ValidateExtractCode_allows_optional_alphanumeric(string? code, bool expected)
    {
        Assert.Equal(expected, PublishLinkValidator.ValidateExtractCode(code));
    }

    [Fact]
    public void ValidateAll_returns_warnings_without_blocking()
    {
        var warnings = PublishLinkValidator.ValidateAll(
            "https://bad.example/a",
            "ab",
            "https://bad.example/b",
            "toolongcode");

        Assert.Equal(4, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("百度"));
        Assert.Contains(warnings, w => w.Contains("夸克"));
        Assert.Contains(warnings, w => w.Contains("百度提取码"));
        Assert.Contains(warnings, w => w.Contains("夸克提取码"));
    }

    [Fact]
    public void ValidateAll_returns_empty_for_valid_links()
    {
        var warnings = PublishLinkValidator.ValidateAll(
            "https://pan.baidu.com/s/abc",
            "abcd",
            "https://pan.quark.cn/s/xyz",
            "ef12");

        Assert.Empty(warnings);
    }
}