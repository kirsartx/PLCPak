namespace PLCPak.Core.Models;

public sealed class WorkflowChainStepResult
{
    public string Step { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Stopped { get; set; }
    public string Message { get; set; } = string.Empty;
    public JobNextActionType? Action { get; set; }
}

public sealed class WorkflowChainResult
{
    public bool Success { get; set; }
    public bool NeedsUserInput { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<WorkflowChainStepResult> Steps { get; set; } = [];
    public PublishJob? Job { get; set; }
    public string? StopReason { get; set; }
}

public sealed class BatchChainResult
{
    public int Success { get; set; }
    public int Failed { get; set; }
    public int StoppedForManual { get; set; }
    public int Skipped { get; set; }
    public List<WorkflowChainResult> JobResults { get; set; } = [];
    public List<string> Messages { get; set; } = [];
    public string? BatchLogPath { get; set; }
}