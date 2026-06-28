namespace PLCPak.Core.Models;

public sealed class ForumPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HomeUrl { get; set; } = string.Empty;
}

public sealed class StudioConfig
{
    public string WorkspaceRoot { get; set; } = string.Empty;
    public List<ForumPreset> Forums { get; set; } =
    [
        new() { Id = "laowang", Name = "老王论坛", HomeUrl = "" }
    ];

    /// <summary>解压密码候选列表（按顺序尝试，适用于论坛资源常见密码）。</summary>
    public List<string> ExtractPasswords { get; set; } = [];

    public string DefaultPublishTemplateId { get; set; } = "telegram-default";

    public string TelegramChannelUrl { get; set; } = string.Empty;

    public string TelegramBotToken { get; set; } = string.Empty;

    public bool AutoDownloadThreadAttachments { get; set; }

    public int ForumDownloadMaxSizeMB { get; set; } = 2048;

    /// <summary>三渠道全部标记已发布后自动归档任务。</summary>
    public bool AutoArchiveOnPublished { get; set; }

    /// <summary>兼容旧版配置字段名与 WinUI 绑定。</summary>
    public bool AutoArchiveAfterPublish
    {
        get => AutoArchiveOnPublished;
        set => AutoArchiveOnPublished = value;
    }

    /// <summary>去广告+压缩成功后自动生成发布文案。</summary>
    public bool AutoGenerateCopyAfterProcess { get; set; }

    public bool EnableScheduledBatch { get; set; }

    public int ScheduledBatchHour { get; set; } = 2;

    public int ScheduledBatchMinute { get; set; }

    public string ScheduledBatchFilter { get; set; } = "active";

    public List<string> DefaultJobTags { get; set; } = [];

    public bool ShowWelcomeOnStartup { get; set; } = true;

    /// <summary>夜间全流程脚本中启用 TG 批量自动发送（否则仅注释提示）。</summary>
    public bool NightlyAutoSendTg { get; set; }

    /// <summary>夜间全流程脚本中自动扫描重复任务（否则仅注释提示）。</summary>
    public bool NightlyAutoScanDuplicates { get; set; }

    /// <summary>夜间全流程脚本中自动合并重复任务（否则仅注释提示）。</summary>
    public bool NightlyAutoMergeDuplicates { get; set; }

    /// <summary>夜间全流程脚本中自动清理活动日志（否则仅注释提示）。</summary>
    public bool NightlyAutoTrimActivityLog { get; set; }

    /// <summary>夜间全流程脚本中自动导出活动日志统计 CSV（否则仅注释提示）。</summary>
    public bool NightlyExportActivityLogStats { get; set; }

    /// <summary>夜间全流程脚本中自动导出活动日志统计 HTML（否则仅注释提示；主题跟随 UI 偏好）。</summary>
    public bool NightlyExportActivityLogStatsHtml { get; set; }

    /// <summary>夜间全流程脚本同时导出活动日志统计 CSV 与 HTML（一键开启，覆盖上方两项）。</summary>
    public bool NightlyExportActivityLogStatsBoth { get; set; }

    /// <summary>夜间全流程脚本中自动导出批量操作统计 JSON+CSV（可独立于上方统计项开启）。</summary>
    public bool NightlyExportActivityLogBatchStatsAll { get; set; }

    /// <summary>夜间全流程脚本中自动导出置顶任务 CSV（否则仅注释提示）。</summary>
    public bool NightlyExportPinnedJobsCsv { get; set; }

    /// <summary>活动日志清理前先将超期条目归档至 reports/activity-archive-*.json。</summary>
    public bool ArchiveBeforeTrimActivityLog { get; set; }

    /// <summary>活动日志清理默认保留天数。</summary>
    public int ActivityLogKeepDays { get; set; } = 30;

    /// <summary>活动日志分页默认每页条数。</summary>
    public int ActivityLogPageSize { get; set; } = 20;

    public StudioConfig Clone() => new()
    {
        WorkspaceRoot = WorkspaceRoot,
        ExtractPasswords = [..ExtractPasswords],
        DefaultPublishTemplateId = DefaultPublishTemplateId,
        TelegramChannelUrl = TelegramChannelUrl,
        TelegramBotToken = TelegramBotToken,
        AutoDownloadThreadAttachments = AutoDownloadThreadAttachments,
        ForumDownloadMaxSizeMB = ForumDownloadMaxSizeMB,
        AutoArchiveOnPublished = AutoArchiveOnPublished,
        AutoGenerateCopyAfterProcess = AutoGenerateCopyAfterProcess,
        EnableScheduledBatch = EnableScheduledBatch,
        ScheduledBatchHour = ScheduledBatchHour,
        ScheduledBatchMinute = ScheduledBatchMinute,
        ScheduledBatchFilter = ScheduledBatchFilter,
        DefaultJobTags = [..DefaultJobTags],
        ShowWelcomeOnStartup = ShowWelcomeOnStartup,
        NightlyAutoSendTg = NightlyAutoSendTg,
        NightlyAutoScanDuplicates = NightlyAutoScanDuplicates,
        NightlyAutoMergeDuplicates = NightlyAutoMergeDuplicates,
        NightlyAutoTrimActivityLog = NightlyAutoTrimActivityLog,
        NightlyExportActivityLogStats = NightlyExportActivityLogStats,
        NightlyExportActivityLogStatsHtml = NightlyExportActivityLogStatsHtml,
        NightlyExportActivityLogStatsBoth = NightlyExportActivityLogStatsBoth,
        NightlyExportActivityLogBatchStatsAll = NightlyExportActivityLogBatchStatsAll,
        NightlyExportPinnedJobsCsv = NightlyExportPinnedJobsCsv,
        ArchiveBeforeTrimActivityLog = ArchiveBeforeTrimActivityLog,
        ActivityLogKeepDays = ActivityLogKeepDays,
        ActivityLogPageSize = ActivityLogPageSize,
        Forums = Forums.Select(f => new ForumPreset
        {
            Id = f.Id,
            Name = f.Name,
            HomeUrl = f.HomeUrl
        }).ToList()
    };
}