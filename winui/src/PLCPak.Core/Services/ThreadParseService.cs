using System.Net;
using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ThreadParseService
{
    private static readonly Regex BaiduLinkRegex = new(
        @"https?://pan\.baidu\.com/[^\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuarkLinkRegex = new(
        @"https?://(?:pan\.)?quark\.cn/[^\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArchivePasswordRegex = new(
        @"解压密码\s*[:：]\s*(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GenericPasswordRegex = new(
        @"(?:密码|password)\s*[:：]\s*(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExtractCodeRegex = new(
        @"(?:提取码|密码|访问码|pwd)\s*[:：]?\s*([A-Za-z0-9]{3,8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DownloadHintRegex = new(
        @"下载(?:地址|链接)?\s*[:：]\s*(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HrefSrcRegex = new(
        @"(?:href|src)\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UrlInTextRegex = new(
        @"https?://[^\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;

    public ThreadParseService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        ResilientHttpHelper.EnsureUserAgent(_http);
    }

    public async Task<ThreadParseResult> FetchAndParseAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ThreadParseResult
            {
                Success = false,
                Error = "帖子链接为空"
            };
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not "http" and not "https")
        {
            return new ThreadParseResult
            {
                Success = false,
                Error = "链接格式无效"
            };
        }

        try
        {
            using var response = await ResilientHttpHelper.GetAsync(_http, uri.ToString(), cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseHtml(html, url.Trim());
        }
        catch (Exception ex)
        {
            return new ThreadParseResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public static ThreadParseResult ParseHtml(string html, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new ThreadParseResult
            {
                Success = false,
                Error = "页面内容为空"
            };
        }

        var (title, titleSource) = ThreadTitleService.ExtractTitle(html);
        var plainText = NormalizeText(html);
        var allLinks = CollectAllLinks(html);
        var attachmentLinks = CollectAttachmentLinks(html, baseUrl);
        var baiduLink = FindFirstMatch(BaiduLinkRegex, html);
        var quarkLink = FindFirstMatch(QuarkLinkRegex, html);
        var archivePassword = FindArchivePassword(plainText);
        var baiduPassword = FindPasswordNearLink(html, baiduLink, plainText);
        var quarkPassword = FindPasswordNearLink(html, quarkLink, plainText);
        var downloadHint = FindDownloadHint(plainText, baiduLink, quarkLink);

        var hasContent = !string.IsNullOrWhiteSpace(title)
            || allLinks.Count > 0
            || attachmentLinks.Count > 0
            || !string.IsNullOrWhiteSpace(archivePassword);

        return new ThreadParseResult
        {
            Success = hasContent,
            Error = hasContent ? null : "页面中未找到可用下载信息",
            Title = title ?? string.Empty,
            TitleSource = titleSource,
            BaiduLink = baiduLink,
            BaiduPassword = baiduPassword,
            QuarkLink = quarkLink,
            QuarkPassword = quarkPassword,
            ArchivePassword = archivePassword,
            DownloadHint = downloadHint,
            AllLinks = allLinks,
            AttachmentLinks = attachmentLinks
        };
    }

    private static List<string> CollectAllLinks(string html)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<string>();

        foreach (Match match in BaiduLinkRegex.Matches(html))
            AddLink(links, seen, CleanLink(match.Value));

        foreach (Match match in QuarkLinkRegex.Matches(html))
            AddLink(links, seen, CleanLink(match.Value));

        return links;
    }

    private static List<string> CollectAttachmentLinks(string html, string? baseUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<string>();

        foreach (Match match in HrefSrcRegex.Matches(html))
            TryAddAttachmentLink(links, seen, CleanLink(match.Groups[1].Value), baseUrl);

        foreach (Match match in UrlInTextRegex.Matches(html))
            TryAddAttachmentLink(links, seen, CleanLink(match.Value), baseUrl);

        return links;
    }

    private static void TryAddAttachmentLink(List<string> links, HashSet<string> seen, string rawUrl, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return;

        var url = ForumDownloadService.ResolveRelativeUrl(baseUrl ?? string.Empty, rawUrl);
        if (!ForumDownloadService.IsDirectAttachmentUrl(url))
            return;

        AddLink(links, seen, url);
    }

    private static void AddLink(List<string> links, HashSet<string> seen, string link)
    {
        if (string.IsNullOrWhiteSpace(link) || !seen.Add(link))
            return;

        links.Add(link);
    }

    private static string FindFirstMatch(Regex regex, string html)
    {
        var match = regex.Match(html);
        return match.Success ? CleanLink(match.Value) : string.Empty;
    }

    private static string FindArchivePassword(string plainText)
    {
        var match = ArchivePasswordRegex.Match(plainText);
        if (match.Success)
            return CleanToken(match.Groups[1].Value);

        match = GenericPasswordRegex.Match(plainText);
        return match.Success ? CleanToken(match.Groups[1].Value) : string.Empty;
    }

    private static string FindPasswordNearLink(string html, string link, string plainText)
    {
        if (string.IsNullOrWhiteSpace(link))
            return string.Empty;

        var index = html.IndexOf(link, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var afterStart = index + link.Length;
            var afterLength = Math.Min(html.Length - afterStart, 240);
            if (afterLength > 0)
            {
                var afterContext = NormalizeText(html.Substring(afterStart, afterLength));
                var afterMatch = ExtractCodeRegex.Match(afterContext);
                if (afterMatch.Success)
                    return CleanToken(afterMatch.Groups[1].Value);
            }

            var beforeStart = Math.Max(0, index - 120);
            var beforeLength = index - beforeStart;
            if (beforeLength > 0)
            {
                var beforeContext = NormalizeText(html.Substring(beforeStart, beforeLength));
                var beforeMatch = ExtractCodeRegex.Match(beforeContext);
                if (beforeMatch.Success)
                    return CleanToken(beforeMatch.Groups[1].Value);
            }
        }

        return string.Empty;
    }

    private static string FindDownloadHint(string plainText, string baiduLink, string quarkLink)
    {
        var match = DownloadHintRegex.Match(plainText);
        if (match.Success)
            return CleanToken(match.Groups[1].Value);

        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(baiduLink))
            hints.Add($"百度: {baiduLink}");
        if (!string.IsNullOrWhiteSpace(quarkLink))
            hints.Add($"夸克: {quarkLink}");

        return hints.Count == 0 ? string.Empty : string.Join(" | ", hints);
    }

    private static string NormalizeText(string value)
    {
        var decoded = WebUtility.HtmlDecode(value);
        var withoutTags = Regex.Replace(decoded, "<[^>]+>", " ");
        return Regex.Replace(withoutTags, @"\s+", " ").Trim();
    }

    private static string CleanLink(string value)
        => CleanToken(value.TrimEnd(')', ']', '}', '.', ',', ';', '"', '\''));

    private static string CleanToken(string value)
        => value.Trim().TrimEnd(')', ']', '}', '.', ',', ';', '"', '\'');
}