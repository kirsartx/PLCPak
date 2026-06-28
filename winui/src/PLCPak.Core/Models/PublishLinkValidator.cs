namespace PLCPak.Core.Models;

public static class PublishLinkValidator
{
    public static bool ValidateBaiduLink(string? url)
        => !string.IsNullOrWhiteSpace(url)
            && url.Contains("pan.baidu.com", StringComparison.OrdinalIgnoreCase);

    public static bool ValidateQuarkLink(string? url)
        => !string.IsNullOrWhiteSpace(url)
            && (url.Contains("pan.quark.cn", StringComparison.OrdinalIgnoreCase)
                || url.Contains("quark.cn", StringComparison.OrdinalIgnoreCase));

    public static bool ValidateExtractCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return true;

        if (code.Length is < 3 or > 8)
            return false;

        return code.All(c => char.IsAsciiLetterOrDigit(c));
    }

    public static IReadOnlyList<string> ValidateAll(
        string? baiduLink,
        string? baiduPassword,
        string? quarkLink,
        string? quarkPassword)
    {
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(baiduLink) && !ValidateBaiduLink(baiduLink))
            warnings.Add("百度链接应包含 pan.baidu.com");

        if (!string.IsNullOrWhiteSpace(quarkLink) && !ValidateQuarkLink(quarkLink))
            warnings.Add("夸克链接应包含 pan.quark.cn 或 quark.cn");

        if (!string.IsNullOrWhiteSpace(baiduPassword) && !ValidateExtractCode(baiduPassword))
            warnings.Add("百度提取码应为 3-8 位字母或数字");

        if (!string.IsNullOrWhiteSpace(quarkPassword) && !ValidateExtractCode(quarkPassword))
            warnings.Add("夸克提取码应为 3-8 位字母或数字");

        return warnings;
    }

    public static IReadOnlyList<string> Validate(JobPublishState publish)
    {
        var warnings = new List<string>();

        ValidateChannel(publish.Baidu, "百度", expectPassword: true, warnings);
        ValidateChannel(publish.Quark, "夸克", expectPassword: true, warnings);
        ValidateChannel(publish.Telegram, "TG", expectPassword: false, warnings);

        if (!string.IsNullOrWhiteSpace(publish.Baidu.Link)
            && !string.IsNullOrWhiteSpace(publish.Quark.Link)
            && publish.Baidu.Link.Trim().Equals(publish.Quark.Link.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("百度与夸克链接相同，请确认是否填错。");
        }

        return warnings;
    }

    private static void ValidateChannel(
        PublishChannelState channel,
        string label,
        bool expectPassword,
        List<string> warnings)
    {
        var link = channel.Link?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(link))
            return;

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            warnings.Add($"{label}链接格式可能无效。");
        }

        if (expectPassword && string.IsNullOrWhiteSpace(channel.Password))
            warnings.Add($"{label}链接已填但缺少提取码。");

        if (label == "百度" && !ValidateBaiduLink(link))
            warnings.Add("百度链接域名看起来不像网盘地址。");

        if (label == "夸克" && !ValidateQuarkLink(link))
            warnings.Add("夸克链接域名看起来不像夸克网盘地址。");
    }
}