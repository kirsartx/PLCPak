using System.Net;
using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ThreadTitleService
{
    private readonly HttpClient _http;

    public ThreadTitleService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PLCPak/4.4 (+https://plcpak.local)");
    }

    public async Task<ThreadTitleResult> FetchTitleAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ThreadTitleResult
            {
                Success = false,
                Error = "帖子链接为空"
            };
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not "http" and not "https")
        {
            return new ThreadTitleResult
            {
                Success = false,
                Error = "链接格式无效"
            };
        }

        try
        {
            using var response = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var (title, source) = ExtractTitle(html);
            if (string.IsNullOrWhiteSpace(title))
            {
                return new ThreadTitleResult
                {
                    Success = false,
                    Error = "页面中未找到标题"
                };
            }

            return new ThreadTitleResult
            {
                Success = true,
                Title = title,
                Source = source
            };
        }
        catch (Exception ex)
        {
            return new ThreadTitleResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public static (string? Title, string Source) ExtractTitle(string html)
    {
        var ogTitle = MatchMetaContent(html, "og:title");
        if (!string.IsNullOrWhiteSpace(ogTitle))
            return (CleanTitle(ogTitle), "og:title");

        var twitterTitle = MatchMetaContent(html, "twitter:title");
        if (!string.IsNullOrWhiteSpace(twitterTitle))
            return (CleanTitle(twitterTitle), "twitter:title");

        var titleTag = Regex.Match(html, @"<title[^>]*>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleTag.Success)
            return (CleanTitle(titleTag.Groups["t"].Value), "title");

        var h1 = Regex.Match(html, @"<h1[^>]*>(?<t>.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (h1.Success)
            return (CleanTitle(StripTags(h1.Groups["t"].Value)), "h1");

        return (null, string.Empty);
    }

    public static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = WebUtility.HtmlDecode(StripTags(raw)).Trim();
        text = Regex.Replace(text, @"\s+", " ");

        foreach (var suffix in new[]
                 {
                     " - 老王论坛", " | 老王论坛", " - 论坛", " | 论坛",
                     " - Forum", " | Forum"
                 })
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                text = text[..^suffix.Length].Trim();
        }

        return text.Trim(' ', '-', '|', ':');
    }

    private static string? MatchMetaContent(string html, string propertyName)
    {
        var patterns = new[]
        {
            $@"<meta[^>]+property=[""']{propertyName}[""'][^>]+content=[""'](?<c>[^""']+)[""']",
            $@"<meta[^>]+content=[""'](?<c>[^""']+)[""'][^>]+property=[""']{propertyName}[""']",
            $@"<meta[^>]+name=[""']{propertyName}[""'][^>]+content=[""'](?<c>[^""']+)[""']",
            $@"<meta[^>]+content=[""'](?<c>[^""']+)[""'][^>]+name=[""']{propertyName}[""']"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
                return match.Groups["c"].Value;
        }

        return null;
    }

    private static string StripTags(string value)
        => Regex.Replace(value, "<[^>]+>", string.Empty);
}