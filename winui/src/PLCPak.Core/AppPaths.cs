namespace PLCPak.Core;

public sealed class AppPaths
{
    public AppPaths(string appRoot)
    {
        if (string.IsNullOrWhiteSpace(appRoot))
            throw new ArgumentException("App root directory is required.", nameof(appRoot));

        AppRoot = Path.GetFullPath(appRoot);
        PackageRoot = Path.GetFullPath(ResolvePackageRoot(AppRoot));
        DataRoot = ResolveDataRoot(AppRoot, PackageRoot);
        LogsRoot = Path.GetFullPath(ResolveDirectory(PackageRoot, "logs", AppRoot));
        ScriptsRoot = Path.GetFullPath(ResolveDirectory(PackageRoot, "scripts", AppRoot));
    }

    /// <summary>Executable directory (typically .../app).</summary>
    public string AppRoot { get; }

    /// <summary>Distribution root containing app/, data/, scripts/.</summary>
    public string PackageRoot { get; }

    /// <summary>User data, tools, and runtime config.</summary>
    public string DataRoot { get; }

    public string LogsRoot { get; }
    public string ScriptsRoot { get; }
    public string BaseDirectory => DataRoot;

    public string ConfigPath => Path.Combine(DataRoot, "compress-config.json");
    public string CompressConfigPath => ConfigPath;
    public string ScanCachePath => Path.Combine(DataRoot, "scan-cache.json");
    public string SessionLogPath => Path.Combine(DataRoot, ".plcpak-session.json");
    public string VersionJsonPath => Path.Combine(DataRoot, "version.json");
    public string StartupLogPath => Path.Combine(LogsRoot, "plcpak-startup.log");
    public string AdSamplesDirectory => Path.Combine(DataRoot, "AD-Samples");
    public string AdSamplesRoot => AdSamplesDirectory;
    public string AdSamplesLegacyRoot => Path.Combine(DataRoot, "AD-simple");
    public string AdManifestPath => Path.Combine(AdSamplesDirectory, "ad-manifest.json");
    public string PasswordSamplesDirectory => Path.Combine(DataRoot, "Password-Samples");
    public string PasswordManifestPath => Path.Combine(PasswordSamplesDirectory, "password-manifest.json");
    public string SevenZipPath => Path.Combine(DataRoot, "7-Zip-Zstandard", "7z.exe");
    public string ReadmeStandardPath => Path.Combine(DataRoot, "资源说明(必读).txt");
    public string ReadmeSelfPurchasePath => Path.Combine(DataRoot, "资源说明(必读-自购).txt");
    public string TransferGuardPath => Path.Combine(DataRoot, "转存防炸.txt");
    public string ZhuancunFangzhaPath => TransferGuardPath;

    public static AppPaths FromExecutableDirectory() => new(AppContext.BaseDirectory);

    public string ResolveAdSamplesRoot()
    {
        if (Directory.Exists(AdSamplesDirectory))
            return AdSamplesDirectory;

        if (Directory.Exists(AdSamplesLegacyRoot))
            return AdSamplesLegacyRoot;

        Directory.CreateDirectory(AdSamplesDirectory);
        return AdSamplesDirectory;
    }

    private static string ResolvePackageRoot(string appRoot)
    {
        var dirName = Path.GetFileName(appRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (dirName.Equals("app", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(Path.Combine(appRoot, ".."));

        return appRoot;
    }

    private static string ResolveDataRoot(string appRoot, string packageRoot)
    {
        foreach (var candidate in EnumerateDataRootCandidates(appRoot, packageRoot))
        {
            if (HasDataLayout(candidate))
                return Path.GetFullPath(candidate);
        }

        var preferred = Path.GetFullPath(Path.Combine(packageRoot, "data"));
        Directory.CreateDirectory(preferred);
        return preferred;
    }

    private static IEnumerable<string> EnumerateDataRootCandidates(string appRoot, string packageRoot)
    {
        // v3.5 共享 dist/data
        yield return Path.Combine(packageRoot, "..", "data");
        yield return Path.Combine(packageRoot, "data");
        yield return Path.Combine(packageRoot, "..", "PLCPak.WinUI", "data");
        yield return Path.Combine(appRoot, "..", "..", "data");
        yield return Path.Combine(appRoot, "..", "..", "PLCPak.WinUI", "data");
        yield return Path.Combine(appRoot, "data");
        yield return Path.Combine(appRoot, "..", "data");
        yield return appRoot;
    }

    private static bool HasDataLayout(string path)
    {
        var full = Path.GetFullPath(path);
        return Directory.Exists(full) && (
            File.Exists(Path.Combine(full, "compress-config.json")) ||
            Directory.Exists(Path.Combine(full, "AD-Samples")) ||
            Directory.Exists(Path.Combine(full, "7-Zip-Zstandard")));
    }

    private static string ResolveDirectory(string packageRoot, string name, string fallbackRoot)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(packageRoot, name),
                     Path.Combine(packageRoot, "..", name)
                 })
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
                return full;
        }

        return Path.GetFullPath(Path.Combine(packageRoot, name));
    }
}