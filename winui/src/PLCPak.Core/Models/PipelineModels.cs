using PLCPak.Core;

namespace PLCPak.Core.Models;

public enum PipelineTaskState
{
    PendingScan,
    PendingConfirm,
    NoAds,
    Cleaned,
    Compressed
}

public sealed class PipelineTask
{
    public string Path { get; set; } = string.Empty;
    public PipelineTaskState State { get; set; } = PipelineTaskState.PendingScan;
    public int Matched { get; set; }
    public int Scanned { get; set; }
    public AdCleanupResult? ScanResult { get; set; }
    public bool Cleaned { get; set; }
    public bool Compressed { get; set; }
}

public sealed class PipelineCliOptions
{
    public bool Preview { get; set; }
    public bool Clean { get; set; }
    public bool Compress { get; set; }
    public bool Force { get; set; }
    public string Format { get; set; } = "7z";
    public int VolumeSizeMB { get; set; } = 1900;
}

public sealed class SessionLogEntry
{
    public string Time { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public List<string> Removed { get; set; } = [];
    public int Count { get; set; }
}

public sealed class ImportSampleStats
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Pruned { get; set; }
    public int Copied { get; set; }
    public List<string> Errors { get; } = [];
}

public sealed class VersionInfo
{
    public string Version { get; set; } = "2.0.0";
    public string? LatestVersion { get; set; }
    public string Channel { get; set; } = "stable";
    public string? ReleaseNotes { get; set; }
    public string ManifestUrl { get; set; } = string.Empty;
    public string ToolUrl { get; set; } = string.Empty;
    public string? ManifestVersion { get; set; }
}

public sealed class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string? Latest { get; set; }
    public string? Notes { get; set; }
    public string? Url { get; set; }
}

public sealed class CleanupConfirmation
{
    public int FolderCount { get; init; }
    public int TotalMatched { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class GuiExecuteRequest
{
    public List<string> SourcePaths { get; init; } = [];
    public bool Enable7z { get; init; } = true;
    public bool EnableTarZst { get; init; } = true;
    public bool SelfPurchase { get; init; }
    public string SevenZOutputName { get; init; } = string.Empty;
    public string TarZstOutputName { get; init; } = string.Empty;
    public int VolumeSizeMB { get; init; } = 1900;
    public string? OutputDirectory { get; init; }
}

public sealed class CliPathResult
{
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public int Scanned { get; set; }
    public int Matched { get; set; }
    public int Removed { get; set; }
    public List<string> MatchedFiles { get; set; } = [];
    public bool Compressed { get; set; }
    public string? CompressOutput { get; set; }
    public bool CompressOk { get; set; }
    public string? Error { get; set; }
}

public sealed class CliJsonReport
{
    public string Version { get; set; } = AppVersion.Current;
    public int ExitCode { get; set; }
    public List<CliPathResult> Paths { get; set; } = [];
    public List<string> Log { get; set; } = [];
}