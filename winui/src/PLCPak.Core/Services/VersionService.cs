using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class VersionService
{
    private readonly AppPaths _paths;

    public VersionService(AppPaths paths) => _paths = paths;

    public VersionInfo ReadVersionInfo()
    {
        if (!File.Exists(_paths.VersionJsonPath))
        {
            return new VersionInfo
            {
                Version = "2.0.0",
                Channel = "stable",
                ManifestUrl = string.Empty,
                ToolUrl = string.Empty
            };
        }

        try
        {
            return JsonHelper.ReadFile<VersionInfo>(_paths.VersionJsonPath) ?? new VersionInfo();
        }
        catch
        {
            return new VersionInfo
            {
                Version = "2.0.0",
                Channel = "stable",
                ManifestUrl = string.Empty,
                ToolUrl = string.Empty
            };
        }
    }

    public UpdateCheckResult TestPlcPakUpdate(string currentVersion)
    {
        var info = ReadVersionInfo();
        var normalizedCurrent = NormalizeVersion(currentVersion);
        var normalizedLatest = NormalizeVersion(info.LatestVersion ?? info.Version);

        if (!string.IsNullOrEmpty(normalizedLatest)
            && !string.Equals(normalizedLatest, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateCheckResult
            {
                HasUpdate = true,
                Latest = info.LatestVersion ?? info.Version,
                Notes = info.ReleaseNotes,
                Url = info.ToolUrl
            };
        }

        return new UpdateCheckResult { HasUpdate = false };
    }

    private static string NormalizeVersion(string version) =>
        version.Trim().TrimStart('v', 'V');
}