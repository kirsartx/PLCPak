using System.Text.Json;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class VersionCheckService
{
    private readonly AppPaths _paths;
    private readonly HttpClient _http;

    public VersionCheckService(AppPaths paths, HttpClient? httpClient = null)
    {
        _paths = paths;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        ResilientHttpHelper.EnsureUserAgent(_http);
    }

    public string LoadLocalVersion()
    {
        var info = JsonHelper.ReadFile<VersionInfo>(_paths.VersionJsonPath);
        if (info is null)
            return AppVersion.Current;

        return string.IsNullOrWhiteSpace(info.LatestVersion)
            ? info.Version
            : info.LatestVersion;
    }

    public async Task<VersionCheckResult> CheckAsync(
        string? manifestUrl,
        CancellationToken cancellationToken = default)
    {
        var current = AppVersion.Current;
        var result = new VersionCheckResult { LocalVersion = current };
        var local = JsonHelper.ReadFile<VersionInfo>(_paths.VersionJsonPath);

        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            result.Message = "未配置远程版本地址";
            if (local is not null)
            {
                var latest = string.IsNullOrWhiteSpace(local.LatestVersion)
                    ? local.Version
                    : local.LatestVersion;
                if (IsRemoteNewer(current, latest))
                {
                    result.HasUpdate = true;
                    result.RemoteVersion = latest;
                    result.Message = BuildUpdateMessage(latest, current, local.ReleaseNotes, local.ToolUrl);
                }
            }

            return result;
        }

        try
        {
            using var response = await ResilientHttpHelper
                .GetAsync(_http, manifestUrl.Trim(), cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var remote = JsonSerializer.Deserialize<VersionInfo>(json, JsonHelper.Options);
            if (remote is null)
            {
                result.Message = "远程版本清单解析失败";
                return result;
            }

            var latest = string.IsNullOrWhiteSpace(remote.LatestVersion)
                ? remote.Version
                : remote.LatestVersion;

            if (IsRemoteNewer(current, latest))
            {
                result.HasUpdate = true;
                result.RemoteVersion = latest;
                var notes = string.IsNullOrWhiteSpace(remote.ReleaseNotes)
                    ? local?.ReleaseNotes
                    : remote.ReleaseNotes;
                var toolUrl = string.IsNullOrWhiteSpace(remote.ToolUrl)
                    ? local?.ToolUrl
                    : remote.ToolUrl;
                result.Message = BuildUpdateMessage(latest, current, notes, toolUrl);
            }
        }
        catch (Exception ex)
        {
            result.Message = $"版本检查失败: {ex.Message}";
        }

        return result;
    }

    public static bool IsRemoteNewer(string local, string remote)
    {
        if (string.IsNullOrWhiteSpace(remote))
            return false;

        if (IsNewSchemeVersion(local) && IsLegacySchemeVersion(remote))
            return false;

        if (Version.TryParse(NormalizeVersion(remote), out var remoteVersion)
            && Version.TryParse(NormalizeVersion(local), out var localVersion))
        {
            return remoteVersion > localVersion;
        }

        return string.Compare(remote.Trim(), local.Trim(), StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static bool IsNewSchemeVersion(string version)
        => Version.TryParse(NormalizeVersion(version), out var parsed) && parsed.Major == 1;

    private static bool IsLegacySchemeVersion(string version)
        => Version.TryParse(NormalizeVersion(version), out var parsed) && parsed.Major >= 10;

    private static string BuildUpdateMessage(string latest, string current, string? notes, string? toolUrl)
    {
        var message = $"发现新版本 {latest}（当前 {current}）";
        if (!string.IsNullOrWhiteSpace(notes))
            message += Environment.NewLine + notes;
        if (!string.IsNullOrWhiteSpace(toolUrl))
            message += Environment.NewLine + $"下载: {toolUrl}";
        return message;
    }

    private static string NormalizeVersion(string value)
    {
        var trimmed = value.Trim();
        var parts = trimmed.Split('-', 2);
        return parts[0];
    }
}