namespace PLCPak.Core.Models;

public sealed class ImportInboxResult
{
    public int CopiedFiles { get; set; }
    public int SkippedPaths { get; set; }
    public List<string> CopiedPaths { get; set; } = [];
    public PublishJob Job { get; set; } = null!;
}

public sealed class PipelineRunResult
{
    public bool Success { get; set; }
    public List<string> Steps { get; set; } = [];
    public string? Error { get; set; }
    public PublishJob? Job { get; set; }
}

public sealed class JobDeleteResult
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool DeletedFolders { get; set; }
    public bool UseRecycleBin { get; set; }
    public List<string> RemovedPaths { get; set; } = [];
}

public sealed class BatchDeleteJobIdsPreviewResult
{
    public int Count { get; set; }
    public List<string> JobIds { get; set; } = [];
    public List<string> SampleTitles { get; set; } = [];
    public int FolderCandidateCount { get; set; }
    public List<string> SampleFolderPaths { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class BatchDeleteJobIdsResult
{
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public bool DeletedFolders { get; set; }
    public bool UseRecycleBin { get; set; }
    public List<string> DeletedJobIds { get; set; } = [];
    public List<string> RemovedPaths { get; set; } = [];
    public List<string> Messages { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class BatchPipelineResult
{
    public int Success { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Messages { get; set; } = [];
    public string? BatchLogPath { get; set; }
}

public sealed class JobJsonExport
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ExportPath { get; set; } = string.Empty;
}

public sealed class JobJsonImportResult
{
    public PublishJob Job { get; set; } = null!;
    public bool WasExisting { get; set; }
    public string Message { get; set; } = string.Empty;
}