namespace PLCPak.Core.Models;

public enum WizardStepStatus
{
    Pending,
    Active,
    Done,
    Skipped,
    Blocked
}

public sealed class WizardStep
{
    public int Order { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WizardStepStatus Status { get; set; }
}

public sealed class PublishWizardState
{
    public string? JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<WizardStep> Steps { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
    public JobNextActionType? CurrentAction { get; set; }
}

public sealed class VersionCheckResult
{
    public string LocalVersion { get; set; } = string.Empty;
    public string? RemoteVersion { get; set; }
    public bool HasUpdate { get; set; }
    public string Message { get; set; } = string.Empty;
}