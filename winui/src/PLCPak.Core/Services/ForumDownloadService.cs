using System.Net;
using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ForumDownloadService
{
    private static readonly Regex ArchiveExtensionRegex = new(
        @"\.(7z|zip|rar|tar|zst)(?:\?|#|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;

    public ForumDownloadService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        ResilientHttpHelper.EnsureUserAgent(_http);
    }

    public static bool IsDirectAttachmentUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (url.Contains("pan.baidu.com", StringComparison.OrdinalIgnoreCase)
            || url.Contains("quark.cn", StringComparison.OrdinalIgnoreCase))
            return false;

        if (ArchiveExtensionRegex.IsMatch(url))
            return true;

        if (url.Contains("mod=attachment", StringComparison.OrdinalIgnoreCase))
            return true;

        if (url.Contains("attachment.php", StringComparison.OrdinalIgnoreCase))
            return true;

        return url.Contains("/attachments/", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveRelativeUrl(string baseUrl, string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return string.Empty;

        if (Uri.TryCreate(relativeUrl.Trim(), UriKind.Absolute, out var absolute)
            && absolute.Scheme is "http" or "https")
            return absolute.ToString();

        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri))
            return relativeUrl.Trim();

        if (Uri.TryCreate(baseUri, relativeUrl.Trim(), out var resolved))
            return resolved.ToString();

        return relativeUrl.Trim();
    }

    public async Task<ForumDownloadResult> DownloadToInboxAsync(
        PublishJob job,
        IEnumerable<string> urls,
        int maxSizeMB = 2048,
        CancellationToken cancellationToken = default)
    {
        var result = new ForumDownloadResult { Job = job };
        var maxBytes = Math.Max(1, maxSizeMB) * 1024L * 1024L;
        Directory.CreateDirectory(job.Paths.Inbox);

        foreach (var rawUrl in urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = rawUrl.Trim();

            if (!IsDirectAttachmentUrl(url))
            {
                result.SkippedCount++;
                result.Messages.Add($"[跳过] 非直链附件: {url}");
                continue;
            }

            var item = new ForumDownloadItem { Url = url };

            try
            {
                var fileName = GetFileNameFromUrl(url);
                var existingPath = Path.Combine(job.Paths.Inbox, fileName);
                if (File.Exists(existingPath))
                {
                    item.FileName = fileName;
                    item.LocalPath = existingPath;
                    item.Success = true;
                    result.SkippedCount++;
                    result.Messages.Add($"[跳过] 文件已存在: {fileName}");
                    result.Items.Add(item);
                    continue;
                }

                using var response = await ResilientHttpHelper.GetAsync(
                        _http,
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                fileName = GetFileName(response, url);
                item.FileName = fileName;

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > maxBytes)
                {
                    item.Error = $"文件过大: {CompressService.FormatFileSize(contentLength.Value)} (上限 {maxSizeMB} MB)";
                    result.Messages.Add($"[失败] {fileName}: {item.Error}");
                    result.Items.Add(item);
                    continue;
                }

                var destPath = InboxImportService.ResolveUniquePath(job.Paths.Inbox, fileName);
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = File.Create(destPath);

                var buffer = new byte[81920];
                long totalBytes = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    totalBytes += read;
                    if (totalBytes > maxBytes)
                    {
                        output.Close();
                        if (File.Exists(destPath))
                            File.Delete(destPath);

                        item.Error = $"下载超过大小上限 {maxSizeMB} MB";
                        result.Messages.Add($"[失败] {fileName}: {item.Error}");
                        result.Items.Add(item);
                        goto nextUrl;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                item.Bytes = totalBytes;
                item.LocalPath = destPath;
                item.Success = true;
                result.DownloadedCount++;
                result.Messages.Add($"[成功] 已下载: {fileName} ({CompressService.FormatFileSize(totalBytes)})");
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                result.Messages.Add($"[失败] {url}: {ex.Message}");
            }

            result.Items.Add(item);
            nextUrl: ;
        }

        result.Success = result.DownloadedCount > 0 || result.SkippedCount > 0;
        if (result.DownloadedCount == 0 && result.Items.All(i => !i.Success))
            result.Error = result.Items.LastOrDefault(i => i.Error is not null)?.Error ?? "未下载任何附件";

        return result;
    }

    private static string GetFileNameFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "attachment.bin";

        var name = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(name) ? "attachment.bin" : WebUtility.UrlDecode(name);
    }

    private static string GetFileName(HttpResponseMessage response, string url)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName;
        if (!string.IsNullOrWhiteSpace(fileName))
            return WebUtility.UrlDecode(fileName.Trim('"'));

        return GetFileNameFromUrl(url);
    }
}