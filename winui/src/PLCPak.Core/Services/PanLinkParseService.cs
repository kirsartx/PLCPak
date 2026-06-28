using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class PanLinkParseService
{
    private static readonly Regex BaiduLinkRegex = new(
        @"https?://pan\.baidu\.com/[^\s""'<>，。；;]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuarkLinkRegex = new(
        @"https?://(?:pan\.)?quark\.cn/[^\s""'<>，。；;]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TelegramLinkRegex = new(
        @"https?://t\.me/[^\s""'<>，。；;]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExtractCodeRegex = new(
        @"(?:提取码|访问码|pwd)\s*[:：]?\s*([A-Za-z0-9]{3,8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PasswordRegex = new(
        @"(?:密码|口令)\s*[:：]?\s*([A-Za-z0-9]{3,16})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LinkLabelRegex = new(
        @"(?:链接|link)\s*[:：]\s*(https?://[^\s""'<>，。；;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static PanLinkParseResult ParseShareText(string text)
    {
        var result = new PanLinkParseResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Messages.Add("分享文本为空");
            return result;
        }

        var normalized = NormalizeText(text);
        result.BaiduLink = FindFirstMatch(BaiduLinkRegex, normalized)
            ?? FindLabeledLink(normalized, "baidu")
            ?? string.Empty;
        result.QuarkLink = FindFirstMatch(QuarkLinkRegex, normalized)
            ?? FindLabeledLink(normalized, "quark")
            ?? string.Empty;

        result.BaiduPassword = FindPasswordForPlatform(normalized, result.BaiduLink, "baidu");
        result.QuarkPassword = FindPasswordForPlatform(normalized, result.QuarkLink, "quark");

        result.TelegramLinks = TelegramLinkRegex.Matches(normalized)
            .Select(m => CleanToken(m.Value))
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(result.BaiduLink))
            result.Messages.Add($"已识别百度链接: {result.BaiduLink}");
        if (!string.IsNullOrWhiteSpace(result.BaiduPassword))
            result.Messages.Add($"已识别百度提取码: {result.BaiduPassword}");
        if (!string.IsNullOrWhiteSpace(result.QuarkLink))
            result.Messages.Add($"已识别夸克链接: {result.QuarkLink}");
        if (!string.IsNullOrWhiteSpace(result.QuarkPassword))
            result.Messages.Add($"已识别夸克密码: {result.QuarkPassword}");
        foreach (var telegram in result.TelegramLinks)
            result.Messages.Add($"已识别 Telegram 链接: {telegram}");

        result.Success = !string.IsNullOrWhiteSpace(result.BaiduLink)
            || !string.IsNullOrWhiteSpace(result.QuarkLink)
            || result.TelegramLinks.Count > 0;

        if (!result.Success)
            result.Messages.Add("未识别到百度、夸克或 Telegram 链接");

        return result;
    }

    private static string FindPasswordForPlatform(string text, string link, string platform)
    {
        if (!string.IsNullOrWhiteSpace(link))
        {
            var nearLink = FindPasswordNearLink(text, link, platform == "baidu");
            if (!string.IsNullOrWhiteSpace(nearLink))
                return nearLink;
        }

        var section = ExtractPlatformSection(text, platform);
        if (!string.IsNullOrWhiteSpace(section))
        {
            var sectionPassword = platform == "baidu"
                ? FindExtractCode(section)
                : FindPassword(section);
            if (!string.IsNullOrWhiteSpace(sectionPassword))
                return sectionPassword;
        }

        return platform == "baidu" ? FindExtractCode(text) : FindPassword(text);
    }

    private static string? ExtractPlatformSection(string text, string platform)
    {
        var markers = platform switch
        {
            "baidu" => new[] { "百度", "baidu", "pan.baidu.com" },
            "quark" => new[] { "夸克", "quark", "quark.cn" },
            _ => Array.Empty<string>()
        };

        foreach (var marker in markers)
        {
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            var end = text.Length;
            foreach (var other in new[] { "百度", "夸克", "telegram", "tg", "t.me" })
            {
                if (other.Equals(marker, StringComparison.OrdinalIgnoreCase))
                    continue;

                var otherIndex = text.IndexOf(other, index + marker.Length, StringComparison.OrdinalIgnoreCase);
                if (otherIndex > index && otherIndex < end)
                    end = otherIndex;
            }

            return text.Substring(index, end - index);
        }

        return null;
    }

    private static string FindPasswordNearLink(string text, string link, bool extractCode)
    {
        var index = text.IndexOf(link, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return string.Empty;

        var afterStart = index + link.Length;
        var afterLength = Math.Min(text.Length - afterStart, 240);
        if (afterLength > 0)
        {
            var afterContext = text.Substring(afterStart, afterLength);
            var afterMatch = extractCode ? FindExtractCode(afterContext) : FindPassword(afterContext);
            if (!string.IsNullOrWhiteSpace(afterMatch))
                return afterMatch;
        }

        var beforeStart = Math.Max(0, index - 120);
        var beforeLength = index - beforeStart;
        if (beforeLength > 0)
        {
            var beforeContext = text.Substring(beforeStart, beforeLength);
            var beforeMatch = extractCode ? FindExtractCode(beforeContext) : FindPassword(beforeContext);
            if (!string.IsNullOrWhiteSpace(beforeMatch))
                return beforeMatch;
        }

        return string.Empty;
    }

    private static string FindExtractCode(string text)
    {
        var match = ExtractCodeRegex.Match(text);
        return match.Success ? CleanToken(match.Groups[1].Value) : string.Empty;
    }

    private static string FindPassword(string text)
    {
        var match = PasswordRegex.Match(text);
        return match.Success ? CleanToken(match.Groups[1].Value) : string.Empty;
    }

    private static string? FindLabeledLink(string text, string platform)
    {
        foreach (Match match in LinkLabelRegex.Matches(text))
        {
            var link = CleanToken(match.Groups[1].Value);
            if (platform == "baidu" && BaiduLinkRegex.IsMatch(link))
                return link;
            if (platform == "quark" && QuarkLinkRegex.IsMatch(link))
                return link;
        }

        return null;
    }

    private static string? FindFirstMatch(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? CleanToken(match.Value) : null;
    }

    private static string NormalizeText(string value)
        => Regex.Replace(value.Replace("\r\n", "\n").Trim(), @"[ \t]+", " ");

    private static string CleanToken(string value)
        => value.Trim().TrimEnd(')', ']', '}', '.', ',', ';', '"', '\'', '，', '。');
}