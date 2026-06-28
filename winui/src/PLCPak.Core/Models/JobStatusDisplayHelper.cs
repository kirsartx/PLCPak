using PLCPak.Core.Services;

namespace PLCPak.Core.Models;

public static class JobStatusDisplayHelper
{
    public static string ToChinese(JobStatus status) => ToLocalized(status, "zh");

    public static string ToLocalized(JobStatus status, string? language = null)
    {
        var key = status switch
        {
            JobStatus.Draft => "job.status.draft",
            JobStatus.InboxReady => "job.status.inboxReady",
            JobStatus.Extracting => "job.status.extracting",
            JobStatus.Extracted => "job.status.extracted",
            JobStatus.Processing => "job.status.processing",
            JobStatus.Processed => "job.status.processed",
            JobStatus.Published => "job.status.published",
            JobStatus.Failed => "job.status.failed",
            JobStatus.Archived => "job.status.archived",
            _ => null
        };

        return key is null
            ? status.ToString()
            : UiStringTable.Get(key, language ?? UiDisplayContext.CurrentLanguage);
    }

    public static string ToShortLabel(JobStatus status) => ToShortLocalized(status, "zh");

    public static string ToShortLocalized(JobStatus status, string? language = null)
    {
        var key = status switch
        {
            JobStatus.Draft => "job.status.short.draft",
            JobStatus.InboxReady => "job.status.short.inboxReady",
            JobStatus.Extracting => "job.status.short.extracting",
            JobStatus.Extracted => "job.status.short.extracted",
            JobStatus.Processing => "job.status.short.processing",
            JobStatus.Processed => "job.status.short.processed",
            JobStatus.Published => "job.status.short.published",
            JobStatus.Failed => "job.status.short.failed",
            JobStatus.Archived => "job.status.short.archived",
            _ => null
        };

        return key is null
            ? status.ToString()
            : UiStringTable.Get(key, language ?? UiDisplayContext.CurrentLanguage);
    }
}