namespace PLCPak.Core.Models;

public enum PublishWizardTab
{
    Prepare = 0,
    Links = 1,
    Copy = 2,
    Publish = 3
}

public sealed class PublishWizardTabState
{
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public bool IsCurrent { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class JobWizardStateSnapshot
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentTabIndex { get; set; }
    public string CurrentTabId { get; set; } = string.Empty;
    public string CurrentTabLabel { get; set; } = string.Empty;
    public List<PublishWizardTabState> Tabs { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
    public JobNextActionEntry? NextAction { get; set; }
}

public sealed class JobWizardStateListSnapshot
{
    public List<JobWizardStateSnapshot> Jobs { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}