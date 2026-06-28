namespace PLCPak.Core.Models;

public sealed class CompressConfig
{
    public int VolumeSizeMB { get; set; } = 1900;
    public int AdScanTimeoutSec { get; set; } = 300;
    public int AdConfirmThreshold { get; set; } = 20;
    public bool UseRecycleBin { get; set; } = true;
    public bool PreviewBeforeClean { get; set; } = true;
    public int ParallelHashWorkers { get; set; } = 4;
    public bool UseScanCache { get; set; } = true;
    public int HashMaxFileMB { get; set; } = 50;
    public int CompressThreads { get; set; }
    public string TempDir { get; set; } = string.Empty;
    public bool SkipStoreCompress { get; set; } = true;
    public int ManifestStaleDays { get; set; } = 7;
    public List<string> Whitelist { get; set; } = [];
    public List<string> RecentProjects { get; set; } = [];
    public List<string> HashSkipExtensions { get; set; } =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".apk", ".dll", ".exe",
        ".mp4", ".avi", ".mkv", ".mp3", ".wav", ".zip", ".7z", ".rar", ".zst",
        ".tar", ".iso", ".unity3d", ".assets", ".bundle"
    ];
    public List<string> AdLikeExtensions { get; set; } =
    [
        ".txt", ".url", ".bat", ".cmd", ".lnk", ".ini", ".html", ".htm",
        ".pdf", ".doc", ".docx", ".zip", ".apk"
    ];

    public CompressConfig Clone() => new()
    {
        VolumeSizeMB = VolumeSizeMB,
        AdScanTimeoutSec = AdScanTimeoutSec,
        AdConfirmThreshold = AdConfirmThreshold,
        UseRecycleBin = UseRecycleBin,
        PreviewBeforeClean = PreviewBeforeClean,
        ParallelHashWorkers = ParallelHashWorkers,
        UseScanCache = UseScanCache,
        HashMaxFileMB = HashMaxFileMB,
        CompressThreads = CompressThreads,
        TempDir = TempDir,
        SkipStoreCompress = SkipStoreCompress,
        ManifestStaleDays = ManifestStaleDays,
        Whitelist = [..Whitelist],
        RecentProjects = [..RecentProjects],
        HashSkipExtensions = [..HashSkipExtensions],
        AdLikeExtensions = [..AdLikeExtensions]
    };
}