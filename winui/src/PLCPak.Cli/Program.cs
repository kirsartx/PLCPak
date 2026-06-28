using System.Text;
using System.Text.Json;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

const string CliDisplayName = AppVersion.CliDisplayName;

try
{
    var exitCode = await RunAsync(args);
    if (ShouldPause(args, exitCode))
        WaitForExit();

    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"致命错误: {ex}");
    WaitForExit();
    return 1;
}

static async Task<int> RunAsync(string[] args)
{
    var argsList = args.ToList();
    if (argsList.Count == 0 || argsList.Contains("-h") || argsList.Contains("--help"))
    {
        PrintHelp();
        return 0;
    }

    if (HasFlag(argsList, "-JobList", "--job-list")
        || HasFlag(argsList, "-JobCreate", "--job-create")
        || HasFlag(argsList, "-JobScan", "--job-scan")
        || HasFlag(argsList, "-JobExtract", "--job-extract")
        || HasFlag(argsList, "-JobProcess", "--job-process")
        || HasFlag(argsList, "-JobSaveLinks", "--job-save-links")
        || HasFlag(argsList, "-JobCopy", "--job-copy")
        || HasFlag(argsList, "-JobCopyLinks", "--job-copy-links")
        || HasFlag(argsList, "-JobMarkPublished", "--job-mark-published")
        || HasFlag(argsList, "-JobMarkAllPublished", "--job-mark-all-published")
        || HasFlag(argsList, "-JobStats", "--job-stats")
        || HasFlag(argsList, "-JobHealth", "--job-health")
        || HasFlag(argsList, "-JobExportHistory", "--job-export-history")
        || HasFlag(argsList, "-JobExport", "--job-export")
        || HasFlag(argsList, "-JobImport", "--job-import")
        || HasFlag(argsList, "-JobBatchCopy", "--job-batch-copy")
        || HasFlag(argsList, "-JobBatchPipeline", "--job-batch-pipeline")
        || HasFlag(argsList, "-JobSearch", "--job-search")
        || HasFlag(argsList, "-JobCheckDuplicate", "--job-check-duplicate")
        || HasFlag(argsList, "-JobFetchTitle", "--job-fetch-title")
        || HasFlag(argsList, "-JobFetchThreadInfo", "--job-fetch-thread-info")
        || HasFlag(argsList, "-JobCreateFromThread", "--job-create-from-thread")
        || HasFlag(argsList, "-JobDownloadAttachments", "--job-download-attachments")
        || HasFlag(argsList, "-JobSendTelegram", "--job-send-telegram")
        || HasFlag(argsList, "-JobTgPending", "--job-tg-pending")
        || HasFlag(argsList, "-JobBatchSendTg", "--job-batch-send-tg")
        || HasFlag(argsList, "-JobImportPanLinksCsv", "--job-import-pan-links-csv")
        || HasFlag(argsList, "-JobTgPreview", "--job-tg-preview")
        || HasFlag(argsList, "-JobScanDuplicates", "--job-scan-duplicates")
        || HasFlag(argsList, "-JobBatchTags", "--job-batch-tags")
        || HasFlag(argsList, "-JobBatchTagsJobIds", "--job-batch-tags-job-ids")
        || HasFlag(argsList, "-JobBatchArchiveJobIds", "--job-batch-archive-job-ids")
        || HasFlag(argsList, "-JobBatchUnarchiveJobIds", "--job-batch-unarchive-job-ids")
        || HasFlag(argsList, "-JobPreviewBatchDeleteJobIds", "--job-preview-batch-delete-job-ids")
        || HasFlag(argsList, "-JobBatchDeleteJobIds", "--job-batch-delete-job-ids")
        || HasFlag(argsList, "-JobExportActivityLog", "--job-export-activity-log")
        || HasFlag(argsList, "-JobExportMachineProfile", "--job-export-machine-profile")
        || HasFlag(argsList, "-JobPreviewMachineProfile", "--job-preview-machine-profile")
        || HasFlag(argsList, "-JobImportMachineProfile", "--job-import-machine-profile")
        || HasFlag(argsList, "-JobExportShortcutProfile", "--job-export-shortcut-profile")
        || HasFlag(argsList, "-JobImportShortcutProfile", "--job-import-shortcut-profile")
        || HasFlag(argsList, "-JobExportTgPendingCsv", "--job-export-tg-pending-csv")
        || HasFlag(argsList, "-JobExportPublishQueueCsv", "--job-export-publish-queue-csv")
        || HasFlag(argsList, "-JobExportDuplicateReport", "--job-export-duplicate-report")
        || HasFlag(argsList, "-JobExportDuplicateMergeSuggestions", "--job-export-duplicate-merge-suggestions")
        || HasFlag(argsList, "-JobActivityLogSearch", "--job-activity-log-search")
        || HasFlag(argsList, "-JobActivityLogStats", "--job-activity-log-stats")
        || HasFlag(argsList, "-JobActivityLogDailyStats", "--job-activity-log-daily-stats")
        || HasFlag(argsList, "-JobActivityLogBatchStats", "--job-activity-log-batch-stats")
        || HasFlag(argsList, "-JobExportActivityLogBatchStats", "--job-export-activity-log-batch-stats")
        || HasFlag(argsList, "-JobExportActivityLogBatchStatsCsv", "--job-export-activity-log-batch-stats-csv")
        || HasFlag(argsList, "-JobExportActivityLogBatchStatsAll", "--job-export-activity-log-batch-stats-all")
        || HasFlag(argsList, "-JobExportActivityLogStats", "--job-export-activity-log-stats")
        || HasFlag(argsList, "-JobExportActivityLogStatsCsv", "--job-export-activity-log-stats-csv")
        || HasFlag(argsList, "-JobExportActivityLogStatsHtml", "--job-export-activity-log-stats-html")
        || HasFlag(argsList, "-JobExportActivityLogDailyStatsCsv", "--job-export-activity-log-daily-stats-csv")
        || HasFlag(argsList, "-JobPreviewBatchArchiveFiltered", "--job-preview-batch-archive-filtered")
        || HasFlag(argsList, "-JobBatchArchiveFiltered", "--job-batch-archive-filtered")
        || HasFlag(argsList, "-JobBatchPinFiltered", "--job-batch-pin-filtered")
        || HasFlag(argsList, "-JobExportFilteredJobsCsv", "--job-export-filtered-jobs-csv")
        || HasFlag(argsList, "-JobExportFilteredJobsJson", "--job-export-filtered-jobs-json")
        || HasFlag(argsList, "-JobExportSelectedJobsCsv", "--job-export-selected-jobs-csv")
        || HasFlag(argsList, "-JobExportPinnedJobsCsv", "--job-export-pinned-jobs-csv")
        || HasFlag(argsList, "-JobExportOperationsSnapshot", "--job-export-operations-snapshot")
        || HasFlag(argsList, "-JobDashboardSnapshot", "--job-dashboard-snapshot")
        || HasFlag(argsList, "-JobSuggestDuplicateMerges", "--job-suggest-duplicate-merges")
        || HasFlag(argsList, "-JobBatchMergeDuplicates", "--job-batch-merge-duplicates")
        || HasFlag(argsList, "-JobPreviewBatchMergeDuplicates", "--job-preview-batch-merge-duplicates")
        || HasFlag(argsList, "-JobExportBatchMergePreview", "--job-export-batch-merge-preview")
        || HasFlag(argsList, "-JobExportActivityLogCsv", "--job-export-activity-log-csv")
        || HasFlag(argsList, "-JobTrimActivityLog", "--job-trim-activity-log")
        || HasFlag(argsList, "-JobArchiveActivityLog", "--job-archive-activity-log")
        || HasFlag(argsList, "-JobPreviewActivityLogTrim", "--job-preview-activity-log-trim")
        || HasFlag(argsList, "-JobBatchQueueCopy", "--job-batch-queue-copy")
        || HasFlag(argsList, "-JobPreviewMerge", "--job-preview-merge")
        || HasFlag(argsList, "-JobMerge", "--job-merge")
        || HasFlag(argsList, "-JobPublishQueue", "--job-publish-queue")
        || HasFlag(argsList, "-JobArchive", "--job-archive")
        || HasFlag(argsList, "-JobUnarchive", "--job-unarchive")
        || HasFlag(argsList, "-JobRetry", "--job-retry")
        || HasFlag(argsList, "-JobSaveNotes", "--job-save-notes")
        || HasFlag(argsList, "-JobDelete", "--job-delete")
        || HasFlag(argsList, "-JobImportInbox", "--job-import-inbox")
        || HasFlag(argsList, "-JobRunPipeline", "--job-run-pipeline")
        || HasFlag(argsList, "-JobNextAction", "--job-next-action")
        || HasFlag(argsList, "-JobExecuteNextAction", "--job-execute-next-action")
        || HasFlag(argsList, "-JobScheduleExport", "--job-schedule-export")
        || HasFlag(argsList, "-JobScheduleRegister", "--job-schedule-register")
        || HasFlag(argsList, "-JobWizardState", "--job-wizard-state")
        || HasFlag(argsList, "-JobRunChain", "--job-run-chain")
        || HasFlag(argsList, "-JobRunBatchChain", "--job-run-batch-chain")
        || HasFlag(argsList, "-JobDailyReport", "--job-daily-report")
        || HasFlag(argsList, "-JobExportStudioConfig", "--job-export-studio-config")
        || HasFlag(argsList, "-JobImportStudioConfig", "--job-import-studio-config")
        || HasFlag(argsList, "-JobExportAllBackup", "--job-export-all-backup")
        || HasFlag(argsList, "-JobImportAllBackup", "--job-import-all-backup")
        || HasFlag(argsList, "-JobMaintenanceReport", "--job-maintenance-report")
        || HasFlag(argsList, "-JobBulkArchivePublished", "--job-bulk-archive-published")
        || HasFlag(argsList, "-JobOperationsCenter", "--job-operations-center")
        || HasFlag(argsList, "-JobParsePanLinks", "--job-parse-pan-links")
        || HasFlag(argsList, "-JobSaveTags", "--job-save-tags")
        || HasFlag(argsList, "-JobTogglePin", "--job-toggle-pin")
        || HasFlag(argsList, "-JobExportOperationsReport", "--job-export-operations-report")
        || HasFlag(argsList, "-JobExportNightlyAutomation", "--job-export-nightly-automation"))
    {
        return await RunJobAsync(argsList);
    }

    return await RunPipelineAsync(argsList);
}

static async Task<int> RunJobAsync(List<string> argsList)
{
    var json = HasFlag(argsList, "-Json", "--json");
    var app = PlcPakAppContext.FromExecutableDirectory();
    app.Workspace.EnsureLayout();

    if (HasFlag(argsList, "-JobStats", "--job-stats"))
    {
        var stats = PublishDashboardService.ComputeStats(app.Jobs.List());
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(stats, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(stats.SummaryText);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobHealth", "--job-health"))
    {
        var report = app.JobRunner.GetJobHealth();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(report, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(report.SummaryText);
            foreach (var issue in report.Issues)
                Console.WriteLine($"[{issue.Severity}] {issue.Title} ({issue.JobId}): {issue.Message}");
        }
        return report.ErrorCount > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobImport", "--job-import"))
    {
        var paths = GetPaths(argsList);
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobImport 需要 -Path");
            return 1;
        }

        try
        {
            var result = app.JobRunner.ImportJob(paths[0]);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已导入任务: {result.Title} ({result.JobId})");
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobBatchPipeline", "--job-batch-pipeline"))
    {
        var filter = JobHealthService.ParseBatchPipelineFilter(GetOption(argsList, "-Filter", "--filter") ?? "pending");
        var force = HasFlag(argsList, "-Force", "--force");
        Func<CleanupConfirmation, Task<bool>>? confirm = force
            ? null
            : async confirmation =>
            {
                if (!confirmation.RequiresConfirmation)
                    return true;
                Console.WriteLine(confirmation.Summary);
                Console.Write("确认删除? (Y/N): ");
                var a = Console.ReadLine();
                return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
            };

        var batch = await app.JobRunner.RunBatchPipelineAsync(filter, force, confirm);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(batch, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"批量流水线完成：成功 {batch.Success}，跳过 {batch.Skipped}，失败 {batch.Failed}");
            foreach (var line in batch.Messages)
                Console.WriteLine(line);
        }

        return batch.Failed > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobExportHistory", "--job-export-history"))
    {
        var export = app.JobRunner.ExportPublishHistory();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"已导出 {export.Count} 条发布记录");
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobDailyReport", "--job-daily-report"))
    {
        var snapshot = app.JobRunner.ExportDailyReportSnapshot();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(snapshot, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"日报已导出: {snapshot.CsvPath}");
            Console.WriteLine($"JSON: {snapshot.JsonPath}");
            Console.WriteLine($"任务总数: {snapshot.TotalJobs}，待发布: {snapshot.PendingPublishCount}，失败: {snapshot.FailedCount}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobMaintenanceReport", "--job-maintenance-report"))
    {
        var staleDays = ParseOptionalIntWithDefault(GetOption(argsList, "-StaleDays", "--stale-days"), 7);
        var report = app.JobRunner.GetMaintenanceReport(staleDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(report, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(report.SummaryText);
            Console.WriteLine($"陈旧任务 {report.StaleCount} 个，可归档已发布 {report.PublishedReadyToArchive} 个");
            foreach (var entry in report.StaleJobs)
                Console.WriteLine($"  {entry.Title} ({entry.JobId}): {entry.Reason}，{entry.DaysSinceUpdate} 天");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobBulkArchivePublished", "--job-bulk-archive-published"))
    {
        var olderThanDays = ParseOptionalIntWithDefault(GetOption(argsList, "-OlderThanDays", "--older-than-days"), 0);
        var result = app.JobRunner.BulkArchivePublishedJobs(olderThanDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"批量归档完成：成功 {result.Archived}，跳过 {result.Skipped}");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobPreviewBatchArchiveFiltered", "--job-preview-batch-archive-filtered"))
    {
        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var search = GetOption(argsList, "-Search", "--search");
        var tag = GetOption(argsList, "-Tag", "--tag");
        var sort = ParseSortOrder(GetOption(argsList, "-Sort", "--sort"));
        var preview = app.JobRunner.PreviewBatchArchiveFiltered(filter, search, tag, sort);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(preview.SummaryText);
            foreach (var title in preview.SampleTitles)
                Console.WriteLine($"  {title}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobBatchArchiveFiltered", "--job-batch-archive-filtered"))
    {
        var force = HasFlag(argsList, "-Force", "--force");
        if (!force)
        {
            Console.Error.WriteLine("错误: -JobBatchArchiveFiltered 需要 -Force 确认批量归档");
            return 1;
        }

        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var search = GetOption(argsList, "-Search", "--search");
        var tag = GetOption(argsList, "-Tag", "--tag");
        var sort = ParseSortOrder(GetOption(argsList, "-Sort", "--sort"));
        var result = app.JobRunner.BatchArchiveFilteredJobs(filter, search, tag, sort);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"筛选批量归档完成：成功 {result.Archived}，跳过 {result.Skipped}");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobBatchPinFiltered", "--job-batch-pin-filtered"))
    {
        var force = HasFlag(argsList, "-Force", "--force");
        if (!force)
        {
            Console.Error.WriteLine("错误: -JobBatchPinFiltered 需要 -Force 确认批量置顶/取消置顶");
            return 1;
        }

        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var search = GetOption(argsList, "-Search", "--search");
        var tag = GetOption(argsList, "-Tag", "--tag");
        var pin = ParseOptionalBool(GetOption(argsList, "-Pin", "--pin"), defaultValue: true);
        var result = app.JobRunner.BatchPinFilteredJobs(filter, search, tag, pin);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.SummaryText);
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobOperationsCenter", "--job-operations-center")
        || HasFlag(argsList, "-JobExportOperationsSnapshot", "--job-export-operations-snapshot"))
    {
        var snapshot = app.JobRunner.GetOperationsCenterSnapshot();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(snapshot, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(snapshot.SummaryText);
            foreach (var line in OperationsCenterService.FormatSectionLines(snapshot))
                Console.WriteLine(line);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportOperationsReport", "--job-export-operations-report"))
    {
        var exportPath = app.JobRunner.ExportOperationsCenterReport();
        var snapshot = app.JobRunner.GetOperationsCenterSnapshot();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(new { exportPath, snapshot }, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"运维报告已导出: {exportPath}");
            Console.WriteLine(snapshot.SummaryText);
            foreach (var line in OperationsCenterService.FormatSectionLines(snapshot))
                Console.WriteLine(line);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportNightlyAutomation", "--job-export-nightly-automation"))
    {
        var cliPath = GetOption(argsList, "-CliPath", "--cli-path");
        try
        {
            var export = app.JobRunner.ExportNightlyAutomationScript(cliPath);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(export.SummaryText);
                Console.WriteLine(export.ReadmeText);
                Console.WriteLine($"夜间脚本: {export.BatPath}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobParsePanLinks", "--job-parse-pan-links"))
    {
        var shareText = await ReadShareTextAsync(argsList);
        if (string.IsNullOrWhiteSpace(shareText))
        {
            Console.Error.WriteLine("错误: -JobParsePanLinks 需要 -Text 或通过 stdin 传入分享文本");
            return 1;
        }

        var parsed = PanLinkParseService.ParseShareText(shareText);
        var applyJobId = GetOption(argsList, "-JobId", "--job-id");
        if (!string.IsNullOrWhiteSpace(applyJobId))
        {
            var job = app.JobRunner.ApplyParsedPanLinks(applyJobId, parsed);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(new { parse = parsed, job }, JsonHelper.Options));
            }
            else
            {
                foreach (var message in parsed.Messages)
                    Console.WriteLine(message);
                WriteJobResult(job, false);
            }

            return parsed.Success ? 0 : 1;
        }

        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(parsed, JsonHelper.Options));
        }
        else
        {
            foreach (var message in parsed.Messages)
                Console.WriteLine(message);
        }

        return parsed.Success ? 0 : 1;
    }

    if (HasFlag(argsList, "-JobExportStudioConfig", "--job-export-studio-config"))
    {
        var paths = GetPaths(argsList);
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobExportStudioConfig 需要 -Path");
            return 1;
        }

        try
        {
            var exportPath = app.JobRunner.ExportStudioConfig(paths[0]);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(new { exportPath }, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已导出 studio-config: {exportPath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobImportStudioConfig", "--job-import-studio-config"))
    {
        var paths = GetPaths(argsList);
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobImportStudioConfig 需要 -Path");
            return 1;
        }

        var import = app.JobRunner.ImportStudioConfig(paths[0]);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(import, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(import.Message);
        }

        return import.Success ? 0 : 1;
    }

    if (HasFlag(argsList, "-JobExportAllBackup", "--job-export-all-backup"))
    {
        try
        {
            var paths = GetPaths(argsList);
            var export = app.JobRunner.ExportAllBackup(paths.Count > 0 ? paths[0] : null);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已导出全量备份: {export.ExportPath}");
                Console.WriteLine($"任务数: {export.JobCount}，含 studio-config: {(export.IncludesStudioConfig ? "是" : "否")}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobImportAllBackup", "--job-import-all-backup"))
    {
        var paths = GetPaths(argsList);
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobImportAllBackup 需要 -Path");
            return 1;
        }

        var merge = HasFlag(argsList, "-Merge", "--merge");
        var import = app.JobRunner.ImportAllBackup(paths[0], merge);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(import, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(import.Message);
        }

        return import.Success ? 0 : 1;
    }

    if (HasFlag(argsList, "-JobSearch", "--job-search"))
    {
        var query = GetOption(argsList, "-Query", "--query");
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.Error.WriteLine("错误: -JobSearch 需要 -Query");
            return 1;
        }

        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var tag = GetOption(argsList, "-Tag", "--tag");
        var jobs = !string.IsNullOrWhiteSpace(tag)
            ? app.JobRunner.QueryJobsByTag(tag, filter, query)
            : app.JobRunner.QueryJobs(filter, query);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(jobs, JsonHelper.Options));
        }
        else
        {
            foreach (var job in jobs)
                Console.WriteLine($"{job.Id}  {job.Status,-12}  {job.Title}  {job.PublishStatusLabel}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobCheckDuplicate", "--job-check-duplicate"))
    {
        var title = GetOption(argsList, "-Title", "--title") ?? string.Empty;
        var threadUrl = GetOption(argsList, "-ThreadUrl", "--thread-url");
        var check = app.JobRunner.CheckCreateJob(title, threadUrl);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(check, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(check.HasDuplicates ? check.Message : "未发现重复任务");
        }
        return check.Blocked ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobFetchTitle", "--job-fetch-title"))
    {
        var threadUrl = GetOption(argsList, "-ThreadUrl", "--thread-url");
        if (string.IsNullOrWhiteSpace(threadUrl))
        {
            Console.Error.WriteLine("错误: -JobFetchTitle 需要 -ThreadUrl");
            return 1;
        }

        var result = await app.JobRunner.FetchThreadTitleAsync(threadUrl);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else if (result.Success)
        {
            Console.WriteLine(result.Title);
        }
        else
        {
            Console.Error.WriteLine(result.Error ?? "拉取标题失败");
            return 1;
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobFetchThreadInfo", "--job-fetch-thread-info"))
    {
        var threadUrl = GetOption(argsList, "-ThreadUrl", "--thread-url");
        if (string.IsNullOrWhiteSpace(threadUrl))
        {
            Console.Error.WriteLine("错误: -JobFetchThreadInfo 需要 -ThreadUrl");
            return 1;
        }

        var result = await app.JobRunner.FetchThreadInfoAsync(threadUrl);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.Title))
                Console.WriteLine($"标题: {result.Title}");
            if (!string.IsNullOrWhiteSpace(result.BaiduLink))
                Console.WriteLine($"百度: {result.BaiduLink}  密码: {result.BaiduPassword}");
            if (!string.IsNullOrWhiteSpace(result.QuarkLink))
                Console.WriteLine($"夸克: {result.QuarkLink}  密码: {result.QuarkPassword}");
            if (!string.IsNullOrWhiteSpace(result.ArchivePassword))
                Console.WriteLine($"解压密码: {result.ArchivePassword}");
            if (!string.IsNullOrWhiteSpace(result.DownloadHint))
                Console.WriteLine($"下载提示: {result.DownloadHint}");
        }
        else
        {
            Console.Error.WriteLine(result.Error ?? "拉取帖子信息失败");
            return 1;
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobCreateFromThread", "--job-create-from-thread"))
    {
        var threadUrl = GetOption(argsList, "-ThreadUrl", "--thread-url");
        if (string.IsNullOrWhiteSpace(threadUrl))
        {
            Console.Error.WriteLine("错误: -JobCreateFromThread 需要 -ThreadUrl");
            return 1;
        }

        var title = GetOption(argsList, "-Title", "--title");
        var site = GetOption(argsList, "-Site", "--site");
        var downloadAttachments = HasFlag(argsList, "-DownloadAttachments", "--download-attachments");

        try
        {
            var result = await app.JobRunner.CreateJobFromThreadAsync(
                threadUrl,
                title,
                site,
                downloadAttachments);

            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else if (result.Success && result.Job is not null)
            {
                Console.WriteLine(result.Message);
                Console.WriteLine($"  任务 ID: {result.Job.Id}");
                Console.WriteLine($"  inbox: {result.Job.Paths.Inbox}");
                if (result.DownloadResult is not null)
                    Console.WriteLine($"  附件: 下载 {result.DownloadResult.DownloadedCount}，跳过 {result.DownloadResult.SkippedCount}");
            }
            else
            {
                Console.Error.WriteLine(result.Error ?? result.Message);
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobTgPending", "--job-tg-pending"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var queue = app.JobRunner.GetTgPendingQueue(limit);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(queue, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(queue.SummaryText);
            foreach (var entry in queue.List)
                Console.WriteLine($"{entry.JobId}  {entry.Status,-12}  {entry.Title}  {entry.PublishSummary}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobBatchSendTg", "--job-batch-send-tg"))
    {
        var force = HasFlag(argsList, "-Force", "--force");
        if (!force)
        {
            Console.Error.WriteLine("错误: -JobBatchSendTg 需要 -Force 确认批量发送");
            return 1;
        }

        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var result = await app.JobRunner.BatchSendTgPendingAsync(limit);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"批量 TG 发送完成：成功 {result.Success}，跳过 {result.Skipped}，失败 {result.Failed}");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return result.Failed > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobImportPanLinksCsv", "--job-import-pan-links-csv"))
    {
        var csvPath = GetOption(argsList, "-Path", "--path");
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            Console.Error.WriteLine("错误: -JobImportPanLinksCsv 需要 -Path <csv文件>");
            return 1;
        }

        try
        {
            var result = app.JobRunner.ImportPanLinksCsv(csvPath);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"网盘链接 CSV 导入完成：成功 {result.Applied}，失败 {result.Failed}");
                foreach (var line in result.Messages)
                    Console.WriteLine(line);
            }

            return result.Failed > 0 || !result.Success ? 1 : 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobTgPreview", "--job-tg-preview"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var preview = app.JobRunner.GetTgPreviewSnapshot(limit);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(preview.SummaryText);
            foreach (var entry in preview.Entries)
                Console.WriteLine($"{entry.JobId}  {entry.Title}  {entry.PartCount} 条  {entry.OriginalLength} 字符");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobScanDuplicates", "--job-scan-duplicates"))
    {
        var scan = app.JobRunner.ScanDuplicateJobs();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(scan, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(scan.SummaryText);
            foreach (var group in scan.Groups)
            {
                Console.WriteLine($"[{group.Reason}] {group.Key}");
                foreach (var job in group.Jobs)
                    Console.WriteLine($"  {job.JobId}  {job.Title}  {job.ThreadUrl}");
            }
        }
        return scan.GroupCount > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobBatchTags", "--job-batch-tags"))
    {
        var tags = GetOption(argsList, "-Tags", "--tags");
        if (string.IsNullOrWhiteSpace(tags))
        {
            Console.Error.WriteLine("错误: -JobBatchTags 需要 -Tags \"a,b\"");
            return 1;
        }

        var mode = BatchTagService.ParseMode(GetOption(argsList, "-Mode", "--mode"));
        var filter = JobHealthService.ParseBatchPipelineFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var tagFilter = GetOption(argsList, "-Tag", "--tag");
        var search = GetOption(argsList, "-Search", "--search") ?? GetOption(argsList, "-Query", "--query");

        var result = app.JobRunner.BatchApplyTags(tags, mode, filter, tagFilter, search);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.SummaryText);
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobBatchTagsJobIds", "--job-batch-tags-job-ids"))
    {
        var tags = GetOption(argsList, "-Tags", "--tags");
        if (string.IsNullOrWhiteSpace(tags))
        {
            Console.Error.WriteLine("错误: -JobBatchTagsJobIds 需要 -Tags \"a,b\"");
            return 1;
        }

        var jobIds = ParseJobIds(GetOption(argsList, "-JobIds", "--job-ids"));
        if (jobIds.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobBatchTagsJobIds 需要 -JobIds id1,id2");
            return 1;
        }

        var mode = BatchTagService.ParseMode(GetOption(argsList, "-Mode", "--mode"));
        var result = app.JobRunner.BatchApplyTags(tags, mode, jobIds: jobIds);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.SummaryText);
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobBatchArchiveJobIds", "--job-batch-archive-job-ids"))
    {
        var jobIds = ParseJobIds(GetOption(argsList, "-JobIds", "--job-ids"));
        if (jobIds.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobBatchArchiveJobIds 需要 -JobIds id1,id2");
            return 1;
        }

        var result = app.JobRunner.BatchArchiveJobIds(jobIds);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"批量归档完成：成功 {result.Archived}，跳过 {result.Skipped}");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobBatchUnarchiveJobIds", "--job-batch-unarchive-job-ids"))
    {
        var jobIds = ParseJobIds(GetOption(argsList, "-JobIds", "--job-ids"));
        if (jobIds.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobBatchUnarchiveJobIds 需要 -JobIds id1,id2");
            return 1;
        }

        var result = app.JobRunner.BatchUnarchiveJobIds(jobIds);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"批量恢复完成：成功 {result.Unarchived}，跳过 {result.Skipped}");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobPreviewBatchDeleteJobIds", "--job-preview-batch-delete-job-ids"))
    {
        var jobIds = ParseJobIds(GetOption(argsList, "-JobIds", "--job-ids"));
        if (jobIds.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobPreviewBatchDeleteJobIds 需要 -JobIds id1,id2");
            return 1;
        }

        var preview = app.JobRunner.PreviewBatchDeleteJobIds(jobIds);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(preview.SummaryText);
            foreach (var title in preview.SampleTitles)
                Console.WriteLine($"  · {title}");
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobBatchDeleteJobIds", "--job-batch-delete-job-ids"))
    {
        var jobIds = ParseJobIds(GetOption(argsList, "-JobIds", "--job-ids"));
        if (jobIds.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobBatchDeleteJobIds 需要 -JobIds id1,id2");
            return 1;
        }

        if (!HasFlag(argsList, "-Force", "--force"))
        {
            Console.Error.WriteLine("错误: -JobBatchDeleteJobIds 需要 -Force 确认批量删除");
            return 1;
        }

        var deleteFolders = HasFlag(argsList, "-DeleteFolders", "--delete-folders");
        var useRecycleBin = HasFlag(argsList, "-RecycleBin", "--recycle-bin");
        if (useRecycleBin && !deleteFolders)
        {
            Console.Error.WriteLine("错误: -RecycleBin 需要与 -DeleteFolders 一起使用");
            return 1;
        }

        var result = app.JobRunner.BatchDeleteJobIds(jobIds, deleteFolders, useRecycleBin);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            var mode = useRecycleBin ? "删记录+回收站" : deleteFolders ? "删记录+目录" : "仅删记录";
            Console.WriteLine($"{result.SummaryText}（{mode}）");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLog", "--job-export-activity-log"))
    {
        var category = GetOption(argsList, "-Category", "--category");
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportActivityLog(outputPath, category);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"活动日志已导出：{export.EntryCount} 条");
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogCsv", "--job-export-activity-log-csv"))
    {
        var query = GetOption(argsList, "-Query", "--query");
        var category = GetOption(argsList, "-Category", "--category");
        var outputPath = GetOption(argsList, "-Path", "--path");
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var export = app.JobRunner.ExportActivityLogCsv(outputPath, query, category, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else if (!string.IsNullOrWhiteSpace(export.SummaryText))
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        else
        {
            Console.WriteLine($"活动日志 CSV 已导出：{export.EntryCount} 条");
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportMachineProfile", "--job-export-machine-profile"))
    {
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportMachineProfile(outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            if (!string.IsNullOrWhiteSpace(export.OperationsCenterSummary))
                Console.WriteLine($"运维快照: {export.OperationsCenterSummary}");
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobPreviewMachineProfile", "--job-preview-machine-profile"))
    {
        var profilePath = GetOption(argsList, "-Path", "--path");
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            Console.Error.WriteLine("错误: -JobPreviewMachineProfile 需要 -Path <json>");
            return 1;
        }

        var merge = !HasFlag(argsList, "-Replace", "--replace");
        var preview = app.JobRunner.PreviewMachineProfile(profilePath, merge);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(preview.SummaryText);
            if (!string.IsNullOrWhiteSpace(preview.OperationsCenterSummary))
                Console.WriteLine($"导出时运维快照: {preview.OperationsCenterSummary}");
            if (!string.IsNullOrWhiteSpace(preview.Message))
                Console.WriteLine(preview.Message);
        }

        return preview.Valid ? 0 : 1;
    }

    if (HasFlag(argsList, "-JobImportMachineProfile", "--job-import-machine-profile"))
    {
        var profilePath = GetOption(argsList, "-Path", "--path");
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            Console.Error.WriteLine("错误: -JobImportMachineProfile 需要 -Path <json>");
            return 1;
        }

        var merge = !HasFlag(argsList, "-Replace", "--replace");
        var result = app.JobRunner.ImportMachineProfile(profilePath, merge);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.Message);
        }

        return result.Success ? 0 : 1;
    }

    if (HasFlag(argsList, "-JobExportShortcutProfile", "--job-export-shortcut-profile"))
    {
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportShortcutProfile(outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobImportShortcutProfile", "--job-import-shortcut-profile"))
    {
        var profilePath = GetOption(argsList, "-Path", "--path");
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            Console.Error.WriteLine("错误: -JobImportShortcutProfile 需要 -Path <json>");
            return 1;
        }

        var merge = !HasFlag(argsList, "-Replace", "--replace");
        var result = app.JobRunner.ImportShortcutProfile(profilePath, merge);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.Success ? result.SummaryText : result.Message);
        }

        return result.Success ? 0 : 1;
    }

    if (HasFlag(argsList, "-JobExportTgPendingCsv", "--job-export-tg-pending-csv"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportTgPendingCsv(limit, outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportPublishQueueCsv", "--job-export-publish-queue-csv"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportPublishQueueCsv(limit, outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportDuplicateReport", "--job-export-duplicate-report"))
    {
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportDuplicateReport(outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"重复报告已导出：{export.GroupCount} 组，{export.DuplicateJobCount} 个任务");
            Console.WriteLine(export.ExportPath);
        }
        return export.GroupCount > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobExportDuplicateMergeSuggestions", "--job-export-duplicate-merge-suggestions"))
    {
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportDuplicateMergeSuggestions(outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"合并建议已导出：{export.GroupCount} 组，{export.MergeActionCount} 次合并，{export.EntryCount} 条建议");
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportBatchMergePreview", "--job-export-batch-merge-preview"))
    {
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportBatchMergePreview(outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"批量合并预览已导出：{export.GroupCount} 组，{export.MergeActionCount} 次合并，{export.ItemCount} 条");
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobActivityLogSearch", "--job-activity-log-search"))
    {
        var query = GetOption(argsList, "-Query", "--query") ?? GetOption(argsList, "-Search", "--search");
        var category = GetOption(argsList, "-Category", "--category");
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit"));
        var offset = ParseOptionalInt(GetOption(argsList, "-Offset", "--offset")) ?? 0;
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var page = app.JobRunner.SearchActivityLogPage(query, limit, offset, category, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(page, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(page.SummaryText);
            foreach (var entry in page.Entries)
                Console.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.Category}: {entry.Message}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobActivityLogStats", "--job-activity-log-stats"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var stats = app.JobRunner.GetActivityLogStats(sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(stats, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(stats.SummaryText);
            foreach (var item in stats.Items)
                Console.WriteLine($"  {item.Category}: {item.Count}（最近 {item.LatestTimestamp:yyyy-MM-dd HH:mm:ss}）");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobActivityLogDailyStats", "--job-activity-log-daily-stats"))
    {
        var days = ParseOptionalIntWithDefault(GetOption(argsList, "-Days", "--days"), 7);
        var stats = app.JobRunner.GetActivityLogDailyStats(days);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(stats, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(stats.SummaryText);
            foreach (var item in stats.Items)
                Console.WriteLine($"  {item.DateLabel}: {item.Count}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobActivityLogBatchStats", "--job-activity-log-batch-stats"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var stats = app.JobRunner.GetActivityLogBatchSummary(sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(stats, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(stats.SummaryText);
            foreach (var item in stats.Items)
                Console.WriteLine($"  {item.Label} ({item.Category}): {item.Count}");
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogBatchStats", "--job-export-activity-log-batch-stats"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportActivityLogBatchStats(outputPath, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogBatchStatsAll", "--job-export-activity-log-batch-stats-all"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var export = app.JobRunner.ExportActivityLogBatchStatsAll(sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine($"JSON: {export.JsonExportPath}");
            Console.WriteLine($"CSV: {export.CsvExportPath}");
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogBatchStatsCsv", "--job-export-activity-log-batch-stats-csv"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportActivityLogBatchStatsCsv(outputPath, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }

        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogStats", "--job-export-activity-log-stats"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportActivityLogStats(outputPath, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogStatsCsv", "--job-export-activity-log-stats-csv"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = ExportActivityLogStatsCsv(app, outputPath, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogStatsHtml", "--job-export-activity-log-stats-html"))
    {
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportActivityLogStatsHtml(sinceDays: sinceDays, outputPath: outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportActivityLogDailyStatsCsv", "--job-export-activity-log-daily-stats-csv"))
    {
        var days = ParseOptionalIntWithDefault(GetOption(argsList, "-Days", "--days"), 7);
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportActivityLogDailyStatsCsv(outputPath, days);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportFilteredJobsCsv", "--job-export-filtered-jobs-csv"))
    {
        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var search = GetOption(argsList, "-Search", "--search");
        var tag = GetOption(argsList, "-Tag", "--tag");
        var sort = ParseSortOrder(GetOption(argsList, "-Sort", "--sort"));
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportFilteredJobsCsv(filter, search, tag, sort, limit, outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportFilteredJobsJson", "--job-export-filtered-jobs-json"))
    {
        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var search = GetOption(argsList, "-Search", "--search");
        var tag = GetOption(argsList, "-Tag", "--tag");
        var sort = ParseSortOrder(GetOption(argsList, "-Sort", "--sort"));
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit"));
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportFilteredJobsJson(filter, search, tag, sort, limit, outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportSelectedJobsCsv", "--job-export-selected-jobs-csv"))
    {
        var jobIds = ParseJobIds(GetOption(argsList, "-JobIds", "--job-ids"));
        if (jobIds.Count == 0)
        {
            Console.Error.WriteLine("错误: -JobExportSelectedJobsCsv 需要 -JobIds id1,id2");
            return 1;
        }

        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportSelectedJobsCsv(jobIds, outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobExportPinnedJobsCsv", "--job-export-pinned-jobs-csv"))
    {
        var outputPath = GetOption(argsList, "-Path", "--path");
        var export = app.JobRunner.ExportPinnedJobsCsv(outputPath);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(export.SummaryText);
            Console.WriteLine(export.ExportPath);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobDashboardSnapshot", "--job-dashboard-snapshot"))
    {
        var publishQueueLimit = ParseOptionalIntWithDefault(
            GetOption(argsList, "-PublishQueueLimit", "--publish-queue-limit"),
            20);
        var tgLimit = ParseOptionalIntWithDefault(
            GetOption(argsList, "-TgLimit", "--tg-limit"),
            50);
        var workflowLimit = ParseOptionalIntWithDefault(
            GetOption(argsList, "-WorkflowLimit", "--workflow-limit"),
            5);
        var snapshot = app.JobRunner.GetDashboardSnapshot(publishQueueLimit, tgLimit, workflowLimit);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(snapshot, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(snapshot.SummaryText);
            Console.WriteLine($"统计: {snapshot.StatsSummary.SummaryText}");
            Console.WriteLine($"待发布队列 ({snapshot.PublishQueue.Count}): {snapshot.PublishQueue.SummaryText}");
            foreach (var entry in snapshot.PublishQueue.Entries)
                Console.WriteLine($"  {entry.JobId}  {entry.Status,-12}  {entry.Title}");
            Console.WriteLine($"TG待发 ({snapshot.TgPending.Count}): {snapshot.TgPending.SummaryText}");
            foreach (var entry in snapshot.TgPending.List)
                Console.WriteLine($"  {entry.JobId}  {entry.Status,-12}  {entry.Title}");
            Console.WriteLine($"工作流: {snapshot.Workflow.SummaryText}");
            if (snapshot.Workflow.Primary is not null)
                Console.WriteLine($"  首要: {snapshot.Workflow.Primary.ActionLabel} — {snapshot.Workflow.Primary.Title} ({snapshot.Workflow.Primary.Reason})");
            Console.WriteLine($"健康: {snapshot.JobHealth.SummaryText}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobSuggestDuplicateMerges", "--job-suggest-duplicate-merges"))
    {
        var suggestions = app.JobRunner.GetDuplicateMergeSuggestions();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(suggestions, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(suggestions.SummaryText);
            foreach (var item in suggestions.Suggestions)
            {
                Console.WriteLine($"{item.TargetJobId}  {item.TargetTitle}  ← {string.Join(", ", item.SourceJobIds)}");
                Console.WriteLine($"  {item.SummaryText}");
            }
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobPreviewBatchMergeDuplicates", "--job-preview-batch-merge-duplicates"))
    {
        var preview = app.JobRunner.PreviewBatchMergeDuplicates();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(preview.SummaryText);
            foreach (var item in preview.Items)
                Console.WriteLine($"{item.TargetJobId}  {item.TargetTitle}  ← {string.Join(", ", item.SourceJobIds)}  [{item.Reason}]");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobBatchMergeDuplicates", "--job-batch-merge-duplicates"))
    {
        var force = HasFlag(argsList, "-Force", "--force");
        if (!force)
        {
            Console.Error.WriteLine("错误: -JobBatchMergeDuplicates 需要 -Force 确认自动合并");
            return 1;
        }

        var result = app.JobRunner.BatchMergeDuplicateJobs();
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.SummaryText);
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return result.Failed > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobPreviewActivityLogTrim", "--job-preview-activity-log-trim"))
    {
        var keepDays = ParseOptionalInt(GetOption(argsList, "-KeepDays", "--keep-days"));
        var category = GetOption(argsList, "-Category", "--category");
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var preview = app.JobRunner.PreviewActivityLogTrim(keepDays, category, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(preview.SummaryText);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobTrimActivityLog", "--job-trim-activity-log"))
    {
        var keepDays = ParseOptionalInt(GetOption(argsList, "-KeepDays", "--keep-days"));
        var category = GetOption(argsList, "-Category", "--category");
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var result = app.JobRunner.TrimActivityLog(keepDays, category, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.SummaryText);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobArchiveActivityLog", "--job-archive-activity-log"))
    {
        var keepDays = ParseOptionalInt(GetOption(argsList, "-KeepDays", "--keep-days"));
        var category = GetOption(argsList, "-Category", "--category");
        var sinceDays = ParseOptionalInt(GetOption(argsList, "-SinceDays", "--since-days"));
        var result = app.JobRunner.ArchiveActivityLog(keepDays, category, sinceDays);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(result.SummaryText);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobBatchQueueCopy", "--job-batch-queue-copy"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var templateId = GetOption(argsList, "-Template", "--template");
        var result = app.JobRunner.BatchGenerateCopyForQueue(limit, templateId);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine($"队列批量文案完成：成功 {result.Success}，跳过 {result.Skipped}，失败 {result.Failed}");
            foreach (var line in result.Messages)
                Console.WriteLine(line);
        }

        return result.Failed > 0 ? 1 : 0;
    }

    if (HasFlag(argsList, "-JobPublishQueue", "--job-publish-queue"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var queue = app.JobRunner.GetPublishQueue(limit);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(queue, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(queue.SummaryText);
            foreach (var entry in queue.Entries)
                Console.WriteLine($"{entry.JobId}  {entry.Status,-12}  {entry.Title}  {entry.PublishSummary}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobNextAction", "--job-next-action"))
    {
        var limit = ParseOptionalInt(GetOption(argsList, "-Limit", "--limit")) ?? 50;
        var workflow = app.JobRunner.GetPublishWorkflow(limit);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(workflow, JsonHelper.Options));
        }
        else
        {
            Console.WriteLine(workflow.SummaryText);
            if (workflow.Primary is not null)
                WriteWorkflowEntry(workflow.Primary);
            foreach (var entry in workflow.Suggestions)
                WriteWorkflowEntry(entry);
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobWizardState", "--job-wizard-state"))
    {
        var wizardJobId = GetOption(argsList, "-JobId", "--job-id");
        try
        {
            var state = string.IsNullOrWhiteSpace(wizardJobId)
                ? app.JobRunner.GetWizardState()
                : app.JobRunner.GetWizardState(wizardJobId);

            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(state, JsonHelper.Options));
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(state.JobId))
                    Console.WriteLine($"{state.JobId}  {state.Title}");
                Console.WriteLine(state.SummaryText);
                foreach (var step in state.Steps)
                {
                    var marker = step.Status == WizardStepStatus.Active ? "  ← 当前" : string.Empty;
                    Console.WriteLine($"  [{step.Order}] {step.Title}: {step.Status}{marker}");
                }

                if (state.CurrentAction is JobNextActionType action && action != JobNextActionType.None)
                {
                    var entry = string.IsNullOrWhiteSpace(state.JobId)
                        ? null
                        : app.JobRunner.GetNextActionForJob(state.JobId);
                    if (entry is not null)
                        WriteWorkflowEntry(entry);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobScheduleExport", "--job-schedule-export"))
    {
        var cliPath = GetOption(argsList, "-CliPath", "--cli-path");
        var filter = GetOption(argsList, "-Filter", "--filter");
        var hour = ParseOptionalInt(GetOption(argsList, "-Hour", "--hour"));
        var minute = ParseOptionalInt(GetOption(argsList, "-Minute", "--minute"));

        try
        {
            var export = app.JobRunner.ExportScheduleBatchScript(cliPath, filter, hour, minute);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(export, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(export.ReadmeText);
                Console.WriteLine($"批量脚本: {export.BatPath}");
                Console.WriteLine($"计划任务: {export.SchTasksCommand}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobScheduleRegister", "--job-schedule-register"))
    {
        var cliPath = GetOption(argsList, "-CliPath", "--cli-path");
        var filter = GetOption(argsList, "-Filter", "--filter");
        var hour = ParseOptionalInt(GetOption(argsList, "-Hour", "--hour"));
        var minute = ParseOptionalInt(GetOption(argsList, "-Minute", "--minute"));

        try
        {
            var result = app.JobRunner.RegisterScheduleBatch(cliPath, filter, hour, minute);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(result.Message);
                if (result.Export is not null)
                {
                    Console.WriteLine($"批量脚本: {result.Export.BatPath}");
                    Console.WriteLine($"计划任务: {result.Export.SchTasksCommand}");
                }
            }
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobExecuteNextAction", "--job-execute-next-action"))
    {
        var executeJobId = GetOption(argsList, "-JobId", "--job-id");
        if (string.IsNullOrWhiteSpace(executeJobId))
        {
            Console.Error.WriteLine("错误: -JobExecuteNextAction 需要 -JobId");
            return 1;
        }

        var action = GetOption(argsList, "-Action", "--action");
        var force = HasFlag(argsList, "-Force", "--force");
        Func<CleanupConfirmation, Task<bool>>? confirm = force
            ? null
            : async confirmation =>
            {
                if (!confirmation.RequiresConfirmation)
                    return true;
                Console.WriteLine(confirmation.Summary);
                Console.Write("确认删除? (Y/N): ");
                var a = Console.ReadLine();
                return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
            };

        try
        {
            var result = await app.JobRunner.ExecuteNextActionAsync(executeJobId, action, confirm);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(result.Message);
                if (result.Job is not null)
                    WriteJobResult(result.Job, json: false);
                if (!string.IsNullOrWhiteSpace(result.Error))
                    Console.Error.WriteLine(result.Error);
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobRunChain", "--job-run-chain"))
    {
        var chainJobId = GetOption(argsList, "-JobId", "--job-id");
        if (string.IsNullOrWhiteSpace(chainJobId))
        {
            Console.Error.WriteLine("错误: -JobRunChain 需要 -JobId");
            return 1;
        }

        var force = HasFlag(argsList, "-Force", "--force");
        Func<CleanupConfirmation, Task<bool>>? confirm = force
            ? null
            : async confirmation =>
            {
                if (!confirmation.RequiresConfirmation)
                    return true;
                Console.WriteLine(confirmation.Summary);
                Console.Write("确认删除? (Y/N): ");
                var a = Console.ReadLine();
                return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
            };

        try
        {
            var result = await app.JobRunner.RunAutomatableChainAsync(chainJobId, confirm);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(result.Message);
                foreach (var step in result.Steps)
                {
                    var marker = step.Stopped ? " [停止]" : step.Success ? " [完成]" : " [失败]";
                    Console.WriteLine($"  {step.Step}{marker}: {step.Message}");
                }

                if (result.Job is not null)
                    WriteJobResult(result.Job, json: false);
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobRunBatchChain", "--job-run-batch-chain"))
    {
        var filter = JobHealthService.ParseBatchChainFilter(GetOption(argsList, "-Filter", "--filter") ?? "active");
        var force = HasFlag(argsList, "-Force", "--force");
        Func<CleanupConfirmation, Task<bool>>? confirm = force
            ? null
            : async confirmation =>
            {
                if (!confirmation.RequiresConfirmation)
                    return true;
                Console.WriteLine(confirmation.Summary);
                Console.Write("确认删除? (Y/N): ");
                var a = Console.ReadLine();
                return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
            };

        try
        {
            var batch = await app.JobRunner.RunBatchChainAsync(filter, force, confirm);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(batch, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"批量自动链完成：成功 {batch.Success}，待手动 {batch.StoppedForManual}，跳过 {batch.Skipped}，失败 {batch.Failed}");
                foreach (var line in batch.Messages)
                    Console.WriteLine(line);
                if (!string.IsNullOrWhiteSpace(batch.BatchLogPath))
                    Console.WriteLine($"日志: {batch.BatchLogPath}");
            }

            return batch.Failed > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobList", "--job-list"))
    {
        var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter") ?? "all");
        var search = GetOption(argsList, "-Search", "--search");
        var sort = JobQueryService.ParseSort(GetOption(argsList, "-Sort", "--sort"));
        var tag = GetOption(argsList, "-Tag", "--tag");
        var jobs = !string.IsNullOrWhiteSpace(tag)
            ? app.JobRunner.QueryJobsByTag(tag, filter, search, sort)
            : app.JobRunner.QueryJobs(filter, search, sort);
        if (json)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(JsonSerializer.Serialize(jobs, JsonHelper.Options));
        }
        else
        {
            foreach (var job in jobs)
                Console.WriteLine($"{job.Id}  {job.Status,-12}  {job.Title}  {job.PublishStatusLabel}");
        }
        return 0;
    }

    if (HasFlag(argsList, "-JobCreate", "--job-create"))
    {
        var title = GetOption(argsList, "-Title", "--title");
        if (string.IsNullOrWhiteSpace(title))
        {
            Console.Error.WriteLine("错误: -JobCreate 需要 -Title");
            return 1;
        }

        var source = new JobSource
        {
            Site = GetOption(argsList, "-Site", "--site") ?? "老王论坛",
            ThreadUrl = GetOption(argsList, "-ThreadUrl", "--thread-url") ?? string.Empty
        };
        try
        {
            var job = app.JobRunner.CreateJob(title, source);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(job, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已创建任务: {job.Id}");
                Console.WriteLine($"  inbox: {job.Paths.Inbox}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobPreviewMerge", "--job-preview-merge"))
    {
        var targetJobId = GetOption(argsList, "-JobId", "--job-id");
        var sourceJobId = GetOption(argsList, "-SourceJobId", "--source-job-id");
        if (string.IsNullOrWhiteSpace(targetJobId))
        {
            Console.Error.WriteLine("错误: -JobPreviewMerge 需要 -JobId（目标任务）");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(sourceJobId))
        {
            Console.Error.WriteLine("错误: -JobPreviewMerge 需要 -SourceJobId");
            return 1;
        }

        try
        {
            var result = app.JobRunner.PreviewMergeJobs(targetJobId, sourceJobId);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(result.Message);
                if (result.MergedFields.Count > 0)
                    Console.WriteLine($"将合并字段: {string.Join(", ", result.MergedFields)}");
                if (!string.IsNullOrWhiteSpace(result.Error))
                    Console.Error.WriteLine(result.Error);
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    if (HasFlag(argsList, "-JobMerge", "--job-merge"))
    {
        var targetJobId = GetOption(argsList, "-JobId", "--job-id");
        var sourceJobId = GetOption(argsList, "-SourceJobId", "--source-job-id");
        if (string.IsNullOrWhiteSpace(targetJobId))
        {
            Console.Error.WriteLine("错误: -JobMerge 需要 -JobId（目标任务）");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(sourceJobId))
        {
            Console.Error.WriteLine("错误: -JobMerge 需要 -SourceJobId");
            return 1;
        }

        var archiveSource = ParseOptionalBool(GetOption(argsList, "-ArchiveSource", "--archive-source"), defaultValue: true);
        try
        {
            var result = app.JobRunner.MergeJobs(targetJobId, sourceJobId, archiveSource);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(result.Message);
                if (result.MergedFields.Count > 0)
                    Console.WriteLine($"合并字段: {string.Join(", ", result.MergedFields)}");
                if (!string.IsNullOrWhiteSpace(result.Error))
                    Console.Error.WriteLine(result.Error);
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (json)
                WriteJsonReport(1, [], [ex.Message]);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    var jobId = GetOption(argsList, "-JobId", "--job-id");
    var needsJobId = HasFlag(argsList, "-JobDownloadAttachments", "--job-download-attachments")
        || HasFlag(argsList, "-JobSendTelegram", "--job-send-telegram")
        || HasFlag(argsList, "-JobScan", "--job-scan")
        || HasFlag(argsList, "-JobExtract", "--job-extract")
        || HasFlag(argsList, "-JobProcess", "--job-process")
        || HasFlag(argsList, "-JobSaveLinks", "--job-save-links")
        || HasFlag(argsList, "-JobCopy", "--job-copy")
        || HasFlag(argsList, "-JobCopyLinks", "--job-copy-links")
        || HasFlag(argsList, "-JobMarkPublished", "--job-mark-published")
        || HasFlag(argsList, "-JobMarkAllPublished", "--job-mark-all-published")
        || HasFlag(argsList, "-JobArchive", "--job-archive")
        || HasFlag(argsList, "-JobUnarchive", "--job-unarchive")
        || HasFlag(argsList, "-JobRetry", "--job-retry")
        || HasFlag(argsList, "-JobSaveNotes", "--job-save-notes")
        || HasFlag(argsList, "-JobSaveTags", "--job-save-tags")
        || HasFlag(argsList, "-JobTogglePin", "--job-toggle-pin")
        || HasFlag(argsList, "-JobExport", "--job-export")
        || HasFlag(argsList, "-JobDelete", "--job-delete")
        || HasFlag(argsList, "-JobImportInbox", "--job-import-inbox")
        || HasFlag(argsList, "-JobRunPipeline", "--job-run-pipeline");

    if (needsJobId && string.IsNullOrWhiteSpace(jobId))
    {
        Console.Error.WriteLine("错误: 需要 -JobId");
        return 1;
    }

    try
    {
        if (HasFlag(argsList, "-JobDownloadAttachments", "--job-download-attachments"))
        {
            var download = await app.JobRunner.DownloadThreadAttachmentsAsync(jobId);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(download, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"附件下载：成功 {download.DownloadedCount}，跳过 {download.SkippedCount}");
                foreach (var line in download.Messages)
                    Console.WriteLine(line);
                if (!string.IsNullOrWhiteSpace(download.Error))
                    Console.Error.WriteLine(download.Error);
            }

            return download.Success ? 0 : 1;
        }

        if (HasFlag(argsList, "-JobSendTelegram", "--job-send-telegram"))
        {
            var send = await app.JobRunner.SendPublishCopyToTelegramAsync(jobId);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(send, JsonHelper.Options));
            }
            else if (send.Success)
            {
                Console.WriteLine($"已发送到 Telegram (chat={send.ChatId}, message_id={send.MessageId})");
            }
            else
            {
                Console.Error.WriteLine(send.Error ?? "Telegram 发送失败");
            }

            return send.Success ? 0 : 1;
        }

        if (HasFlag(argsList, "-JobScan", "--job-scan"))
        {
            var job = app.JobRunner.ScanInbox(jobId);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobExtract", "--job-extract"))
        {
            var job = app.JobRunner.Extract(jobId);
            WriteJobResult(job, json);
            return job.Status == JobStatus.Failed ? 1 : 0;
        }

        if (HasFlag(argsList, "-JobProcess", "--job-process"))
        {
            var force = HasFlag(argsList, "-Force", "--force");
            Func<CleanupConfirmation, Task<bool>>? confirm = force
                ? null
                : async confirmation =>
                {
                    if (!confirmation.RequiresConfirmation)
                        return true;
                    Console.WriteLine(confirmation.Summary);
                    Console.Write("确认删除? (Y/N): ");
                    var a = Console.ReadLine();
                    return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
                };

            var job = await app.JobRunner.ProcessAsync(jobId, confirm);
            WriteJobResult(job, json);
            return job.Status == JobStatus.Failed ? 1 : 0;
        }

        if (HasFlag(argsList, "-JobSaveLinks", "--job-save-links"))
        {
            var job = app.JobRunner.SavePublishLinks(
                jobId,
                GetOption(argsList, "-BaiduLink", "--baidu-link"),
                GetOption(argsList, "-BaiduPwd", "--baidu-pwd"),
                GetOption(argsList, "-QuarkLink", "--quark-link"),
                GetOption(argsList, "-QuarkPwd", "--quark-pwd"),
                GetOption(argsList, "-TelegramLink", "--telegram-link"));
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobMarkPublished", "--job-mark-published"))
        {
            var channel = GetOption(argsList, "-Channel", "--channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                Console.Error.WriteLine("错误: -JobMarkPublished 需要 -Channel baidu|quark|tg");
                return 1;
            }

            var job = app.JobRunner.MarkChannelPublished(jobId, channel);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobMarkAllPublished", "--job-mark-all-published"))
        {
            var job = app.JobRunner.MarkAllChannelsPublished(jobId);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobBatchCopy", "--job-batch-copy"))
        {
            var filter = PublishDashboardService.ParseFilter(GetOption(argsList, "-Filter", "--filter"));
            var templateId = GetOption(argsList, "-Template", "--template");
            var result = app.JobRunner.BatchGenerateCopy(filter, templateId);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"批量生成完成：成功 {result.Success}，跳过 {result.Skipped}，失败 {result.Failed}");
                foreach (var line in result.Messages)
                    Console.WriteLine(line);
            }

            return result.Failed > 0 ? 1 : 0;
        }

        if (HasFlag(argsList, "-JobCopy", "--job-copy"))
        {
            var templateId = GetOption(argsList, "-Template", "--template");
            var result = app.JobRunner.GeneratePublishCopy(jobId, templateId);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(result.Text);
                if (!string.IsNullOrWhiteSpace(result.ExportPath))
                    Console.Error.WriteLine($"已导出: {result.ExportPath}");
            }

            return 0;
        }

        if (HasFlag(argsList, "-JobCopyLinks", "--job-copy-links"))
        {
            var links = app.JobRunner.GetPublishLinks(jobId);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(links, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine(links.FormattedText);
            }
            return 0;
        }

        if (HasFlag(argsList, "-JobSaveNotes", "--job-save-notes"))
        {
            var notes = GetOption(argsList, "-Notes", "--notes") ?? string.Empty;
            var job = app.JobRunner.SaveNotes(jobId, notes);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobSaveTags", "--job-save-tags"))
        {
            var tags = GetOption(argsList, "-Tags", "--tags") ?? string.Empty;
            var job = app.JobRunner.SaveJobTags(jobId, tags);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobTogglePin", "--job-toggle-pin"))
        {
            var job = app.JobRunner.ToggleJobPin(jobId);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobExport", "--job-export"))
        {
            var outPath = GetOption(argsList, "-Out", "--out");
            if (string.IsNullOrWhiteSpace(outPath))
            {
                Console.Error.WriteLine("错误: -JobExport 需要 -Out");
                return 1;
            }

            var exported = app.JobRunner.ExportJob(jobId, outPath);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(new { jobId, exportPath = exported }, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已导出任务到: {exported}");
            }
            return 0;
        }

        if (HasFlag(argsList, "-JobUnarchive", "--job-unarchive"))
        {
            var job = app.JobRunner.UnarchiveJob(jobId);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobRetry", "--job-retry"))
        {
            var force = HasFlag(argsList, "-Force", "--force");
            Func<CleanupConfirmation, Task<bool>>? confirm = force
                ? null
                : async confirmation =>
                {
                    if (!confirmation.RequiresConfirmation)
                        return true;
                    Console.WriteLine(confirmation.Summary);
                    Console.Write("确认删除? (Y/N): ");
                    var a = Console.ReadLine();
                    return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
                };

            var job = await app.JobRunner.RetryJobAsync(jobId, confirm);
            WriteJobResult(job, json);
            return job.Status == JobStatus.Failed ? 1 : 0;
        }

        if (HasFlag(argsList, "-JobArchive", "--job-archive"))
        {
            var job = app.JobRunner.ArchiveJob(jobId);
            WriteJobResult(job, json);
            return 0;
        }

        if (HasFlag(argsList, "-JobDelete", "--job-delete"))
        {
            var deleteFolders = HasFlag(argsList, "-DeleteFolders", "--delete-folders");
            var useRecycleBin = HasFlag(argsList, "-RecycleBin", "--recycle-bin");
            if (useRecycleBin && !deleteFolders)
            {
                Console.Error.WriteLine("错误: -RecycleBin 需要与 -DeleteFolders 一起使用");
                return 1;
            }

            var result = app.JobRunner.DeleteJob(jobId, deleteFolders, useRecycleBin);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已删除任务: {result.Title}");
                if (result.DeletedFolders)
                    foreach (var path in result.RemovedPaths)
                        Console.WriteLine($"  已删目录: {path}");
            }
            return 0;
        }

        if (HasFlag(argsList, "-JobImportInbox", "--job-import-inbox"))
        {
            var paths = GetPaths(argsList);
            if (paths.Count == 0)
            {
                Console.Error.WriteLine("错误: -JobImportInbox 需要 -Path");
                return 1;
            }

            var result = app.JobRunner.ImportInboxFiles(jobId, paths);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
            }
            else
            {
                Console.WriteLine($"已导入 {result.CopiedFiles} 个文件到 inbox");
                WriteJobResult(result.Job, json: false);
            }
            return result.CopiedFiles > 0 ? 0 : 1;
        }

        if (HasFlag(argsList, "-JobRunPipeline", "--job-run-pipeline"))
        {
            var force = HasFlag(argsList, "-Force", "--force");
            Func<CleanupConfirmation, Task<bool>>? confirm = force
                ? null
                : async confirmation =>
                {
                    if (!confirmation.RequiresConfirmation)
                        return true;
                    Console.WriteLine(confirmation.Summary);
                    Console.Write("确认删除? (Y/N): ");
                    var a = Console.ReadLine();
                    return a is not null && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
                };

            var pipeline = await app.JobRunner.RunFullPipelineAsync(jobId, confirm);
            if (json)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(JsonSerializer.Serialize(pipeline, JsonHelper.Options));
            }
            else if (pipeline.Job is not null)
            {
                WriteJobResult(pipeline.Job, json: false);
                if (!string.IsNullOrWhiteSpace(pipeline.Error))
                    Console.Error.WriteLine(pipeline.Error);
            }
            else if (!string.IsNullOrWhiteSpace(pipeline.Error))
            {
                Console.Error.WriteLine(pipeline.Error);
            }

            return pipeline.Success ? 0 : 1;
        }
    }
    catch (Exception ex)
    {
        if (json)
            WriteJsonReport(1, [], [ex.Message]);
        else
            Console.Error.WriteLine(ex.Message);
        return 1;
    }

    Console.Error.WriteLine("错误: 未知 Job 命令");
    return 1;
}

static void WriteJobResult(PublishJob job, bool json)
{
    if (json)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(JsonSerializer.Serialize(job, JsonHelper.Options));
        return;
    }

    Console.WriteLine($"任务: {job.Title} ({job.Id})");
    Console.WriteLine($"状态: {job.Status}");
    if (!string.IsNullOrWhiteSpace(job.Error))
        Console.WriteLine($"错误: {job.Error}");
    foreach (var line in job.Log.TakeLast(10))
        Console.WriteLine(line);
}

static async Task<int> RunPipelineAsync(List<string> argsList)
{
    var preview = HasFlag(argsList, "-Preview", "--preview");
    var clean = HasFlag(argsList, "-Clean", "--clean");
    var compress = HasFlag(argsList, "-Compress", "--compress");
    var force = HasFlag(argsList, "-Force", "--force");
    var json = HasFlag(argsList, "-Json", "--json");
    var format = GetOption(argsList, "-Format", "--format") ?? "7z";

    var paths = GetPaths(argsList);
    if (paths.Count == 0)
    {
        if (json)
            WriteJsonReport(1, [], ["错误: 需要 -Path 指定至少一个路径"]);
        else
            Console.Error.WriteLine("错误: 需要 -Path 指定至少一个路径");
        return 1;
    }

    if (!preview && !clean && !compress)
    {
        if (json)
            WriteJsonReport(1, [], ["错误: 请指定 -Preview、-Clean 或 -Compress"]);
        else
            Console.Error.WriteLine("错误: 请指定 -Preview、-Clean 或 -Compress");
        return 1;
    }

    var appRoot = AppContext.BaseDirectory;
    var pathsObj = new AppPaths(appRoot);
    var config = new ConfigService(pathsObj).Load();
    var orchestrator = PipelineOrchestrator.Create(appRoot);
    var options = new PipelineCliOptions
    {
        Preview = preview,
        Clean = clean,
        Compress = compress,
        Force = force,
        Format = format,
        VolumeSizeMB = config.VolumeSizeMB
    };

    Func<string, bool>? confirm = null;
    if (clean && !force)
    {
        confirm = msg =>
        {
            if (json)
                return false;

            Console.Write($"{msg}: ");
            var a = Console.ReadLine();
            return a is not null && (a.Equals("Y", StringComparison.OrdinalIgnoreCase) || a.Equals("Yes", StringComparison.OrdinalIgnoreCase));
        };
    }

    var logs = new List<string>();
    var results = new List<CliPathResult>();
    Action<string> log = json ? logs.Add : Console.WriteLine;

    var exitCode = await orchestrator.RunCliAsync(paths, results, options, config, confirm, log);

    if (json)
        WriteJsonReport(exitCode, results, logs);

    return exitCode;
}

static void WriteWorkflowEntry(JobNextActionEntry entry)
    => Console.WriteLine($"{entry.JobId}  P{entry.Priority}  {entry.ActionLabel,-14}  {entry.Title}  {entry.Reason}");

static void WriteJsonReport(int exitCode, List<CliPathResult> paths, List<string> log)
{
    var report = new CliJsonReport
    {
        Version = AppVersion.Current,
        ExitCode = exitCode,
        Paths = paths,
        Log = log
    };

    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine(JsonSerializer.Serialize(report, JsonHelper.Options));
}

static bool ShouldPause(string[] args, int exitCode)
{
    if (args.Length == 0)
        return true;

    if (HasFlag(args.ToList(), "-Json", "--json"))
        return false;

    if (exitCode != 0 && !Console.IsInputRedirected)
        return true;

    return false;
}

static void WaitForExit()
{
    if (Console.IsInputRedirected)
        return;

    Console.WriteLine();
    Console.WriteLine("按 Enter 退出...");
    Console.ReadLine();
}

static bool HasFlag(List<string> a, params string[] names)
{
    foreach (var n in names)
        if (a.Any(x => x.Equals(n, StringComparison.OrdinalIgnoreCase)))
            return true;
    return false;
}

static string? GetOption(List<string> a, params string[] names)
{
    for (var i = 0; i < a.Count - 1; i++)
        foreach (var n in names)
            if (a[i].Equals(n, StringComparison.OrdinalIgnoreCase))
                return a[i + 1];
    return null;
}

static async Task<string?> ReadShareTextAsync(List<string> argsList)
{
    var text = GetOption(argsList, "-Text", "--text");
    if (!string.IsNullOrWhiteSpace(text))
        return text;

    if (Console.IsInputRedirected)
        return await Console.In.ReadToEndAsync();

    return null;
}

static List<string> GetPaths(List<string> a)
{
    var list = new List<string>();
    for (var i = 0; i < a.Count; i++)
    {
        if (a[i].Equals("-Path", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            while (i < a.Count && !a[i].StartsWith('-'))
            {
                list.Add(a[i]);
                i++;
            }
            i--;
        }
    }
    return list;
}

static void PrintHelp()
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine($"""
        {CliDisplayName} (WinUI / .NET 10)

        管道用法:
          PLCPak.Cli -Preview -Path "D:\游戏" -NoGui
          PLCPak.Cli -Clean -Compress -Path "D:\游戏" -Force -Json

        任务用法 (v1.0.55):
          PLCPak.Cli -JobCreate -Title "游戏名" -ThreadUrl "https://..." -Json
          PLCPak.Cli -JobCreateFromThread -ThreadUrl https://... [-Title 游戏名] [-Site 老王论坛] [-DownloadAttachments] [-Json]
          PLCPak.Cli -JobDownloadAttachments -JobId <id> [-Json]
          PLCPak.Cli -JobSendTelegram -JobId <id> [-Json]
          PLCPak.Cli -JobTgPending [-Limit 50] [-Json]
          PLCPak.Cli -JobTgPreview [-Limit 50] [-Json]
          PLCPak.Cli -JobBatchSendTg [-Limit 50] -Force [-Json]
          PLCPak.Cli -JobImportPanLinksCsv -Path path.csv [-Json]
          PLCPak.Cli -JobScanDuplicates [-Json]
          PLCPak.Cli -JobBatchTags -Tags "a,b" [-Mode append|replace] [-Filter all|active|...] [-Tag 标签] [-Search 关键词] [-Json]
          PLCPak.Cli -JobBatchTagsJobIds -JobIds id1,id2 -Tags "a,b" [-Mode append] [-Json]
          PLCPak.Cli -JobBatchArchiveJobIds -JobIds id1,id2 [-Json]
          PLCPak.Cli -JobBatchUnarchiveJobIds -JobIds id1,id2 [-Json]
          PLCPak.Cli -JobPreviewBatchDeleteJobIds -JobIds id1,id2 [-Json]
          PLCPak.Cli -JobBatchDeleteJobIds -JobIds id1,id2 -Force [-DeleteFolders] [-RecycleBin] [-Json]
          PLCPak.Cli -JobExportShortcutProfile [-Path json] [-Json]
          PLCPak.Cli -JobImportShortcutProfile -Path json [-Merge] [-Replace] [-Json]
            （按指定任务 ID 批量追加或替换标签；默认 append）
          PLCPak.Cli -JobExportActivityLog [-Category telegram] [-Path path.json] [-Json]
          PLCPak.Cli -JobExportActivityLogCsv [-Query 关键词] [-Category <分类>] [-SinceDays <N>] [-Path path.csv] [-Json]
          PLCPak.Cli -JobExportMachineProfile [-Path path.json] [-Json]
          PLCPak.Cli -JobPreviewMachineProfile -Path path.json [-Merge] [-Replace] [-Json]
          PLCPak.Cli -JobImportMachineProfile -Path path.json [-Merge] [-Replace] [-Json]
          PLCPak.Cli -JobExportTgPendingCsv [-Limit 50] [-Path path.csv] [-Json]
          PLCPak.Cli -JobExportPublishQueueCsv [-Limit 50] [-Path path.csv] [-Json]
          PLCPak.Cli -JobExportDuplicateReport [-Path path.json] [-Json]
          PLCPak.Cli -JobExportDuplicateMergeSuggestions [-Path path.json] [-Json]
          PLCPak.Cli -JobActivityLogSearch [-Query 关键词] [-Category 分类] [-SinceDays <N>] [-Limit 50] [-Offset <N>] [-Json]
          PLCPak.Cli -JobActivityLogStats [-SinceDays <N>] [-Json]
            （按分类聚合活动日志条数与最近时间；JSON 含 TotalCount / Items：Category、Count、LatestTimestamp）
          PLCPak.Cli -JobActivityLogDailyStats [-Days 7] [-Json]
            （按日历日聚合活动日志条数；JSON 含 Days / TotalCount / Items：Date、Count；缺省日补 0）
          PLCPak.Cli -JobActivityLogBatchStats [-SinceDays <N>] [-Json]
            （统计含「批量」关键词的活动日志，按分类聚合）
          PLCPak.Cli -JobExportActivityLogBatchStats [-SinceDays <N>] [-Path path.json] [-Json]
            （导出批量操作统计 JSON；默认 reports/activity-log-batch-stats-*.json）
          PLCPak.Cli -JobExportActivityLogBatchStatsCsv [-SinceDays <N>] [-Path path.csv] [-Json]
            （导出批量操作统计 CSV：category、label、count）
          PLCPak.Cli -JobExportActivityLogBatchStatsAll [-SinceDays <N>] [-Json]
            （同时导出批量操作统计 JSON 与 CSV 到 reports/；-SinceDays 与运维中心天数筛选一致）
          PLCPak.Cli -JobExportActivityLogStats [-SinceDays <N>] [-Path path.json] [-Json]
            （导出活动日志统计 JSON；默认 reports/activity-log-stats-*.json）
          PLCPak.Cli -JobExportActivityLogStatsCsv [-SinceDays <N>] [-Path path.csv] [-Json]
            （导出 CSV：category、count、latestTimestamp；默认 reports/activity-log-stats-*.csv；-SinceDays 与统计查询一致）
          PLCPak.Cli -JobExportActivityLogStatsHtml [-SinceDays <N>] [-Path path.html] [-Json]
            （导出活动日志统计 HTML 报告，含分类条形图与明细表；默认 reports/activity-log-stats-*.html）
          PLCPak.Cli -JobExportActivityLogDailyStatsCsv [-Days 7] [-Path path.csv] [-Json]
            （按日历日聚合活动日志条数；CSV 列 date、count；默认 reports/activity-log-daily-stats-*.csv）
          PLCPak.Cli -JobExportFilteredJobsCsv [-Filter active|failed|all|pending|published|processed|archived] [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Limit <N>] [-Path path.csv] [-Json]
            （含 jobId、title、status、tags、发布链接等列）
          PLCPak.Cli -JobExportFilteredJobsJson [-Filter active|failed|all|pending|published|processed|archived] [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Limit <N>] [-Path path.json] [-Json]
            （按任务列表同款筛选/排序导出完整任务 JSON 数组；默认 reports/filtered-jobs-*.json）
          PLCPak.Cli -JobExportSelectedJobsCsv -JobIds id1,id2 [-Path path.csv] [-Json]
            （按指定任务 ID 导出 CSV，列与筛选导出相同；默认 reports/selected-jobs-*.csv）
          PLCPak.Cli -JobExportPinnedJobsCsv [-Path path.csv] [-Json]
            （仅导出 IsPinned 任务 CSV；默认 reports/pinned-jobs-*.csv）
          PLCPak.Cli -JobDashboardSnapshot [-PublishQueueLimit 20] [-TgLimit 50] [-WorkflowLimit 5] [-Json]
          PLCPak.Cli -JobSuggestDuplicateMerges [-Json]
          PLCPak.Cli -JobPreviewBatchMergeDuplicates [-Json]
          PLCPak.Cli -JobExportBatchMergePreview [-Path path.json] [-Json]
          PLCPak.Cli -JobBatchMergeDuplicates -Force [-Json]
          PLCPak.Cli -JobPreviewActivityLogTrim [-KeepDays 30] [-Category 分类] [-SinceDays <N>] [-Json]
            （清理预览 SummaryText 含分类与近 N 天范围提示）
          PLCPak.Cli -JobTrimActivityLog [-KeepDays 30] [-Category 分类] [-SinceDays <N>] [-Json]
          PLCPak.Cli -JobArchiveActivityLog [-KeepDays 30] [-Category 分类] [-SinceDays <N>] [-Json]
            （将超过保留期的活动日志移至 reports/activity-archive-*.json 并从主日志删除；支持分类/近 N 天筛选，与 Trim 一致）
          PLCPak.Cli -JobBatchPinFiltered -Pin true|false -Force [-Filter active|failed|all|pending|published|processed|archived] [-Search 关键词] [-Tag 标签] [-Json]
            （按任务列表同款筛选批量置顶或取消置顶；跳过已处于目标状态的任务；需 -Force 确认）
          PLCPak.Cli -JobBatchQueueCopy [-Limit 50] [-Template telegram-default] [-Json]
          PLCPak.Cli -JobList [-Filter all|pending|published|processed|active|failed|archived] [-Search 关键词] [-Tag 标签] [-Sort updated|title]
          PLCPak.Cli -JobSearch -Query 关键词 [-Filter all] [-Tag 标签] [-Json]
          PLCPak.Cli -JobExportNightlyAutomation [-CliPath path\PLCPak.Cli.exe] [-Json]
          PLCPak.Cli -JobFetchTitle -ThreadUrl https://... [-Json]
          PLCPak.Cli -JobFetchThreadInfo -ThreadUrl https://... [-Json]
          PLCPak.Cli -JobPublishQueue [-Limit 20] [-Json]
          PLCPak.Cli -JobNextAction [-Limit 50] [-Json]
          PLCPak.Cli -JobWizardState [-JobId <id>] [-Json]
          PLCPak.Cli -JobExecuteNextAction -JobId <id> [-Action retry|download|pipeline|copy|mark|telegram] [-Force] [-Json]
          PLCPak.Cli -JobRunChain -JobId <id> [-Force] [-Json]
          PLCPak.Cli -JobRunBatchChain [-Filter active|failed|all] [-Force] [-Json]
          PLCPak.Cli -JobDailyReport [-Json]
          PLCPak.Cli -JobMaintenanceReport [-StaleDays 7] [-Json]
          PLCPak.Cli -JobBulkArchivePublished [-OlderThanDays 0] [-Json]
          PLCPak.Cli -JobPreviewBatchArchiveFiltered [-Filter active|failed|all|pending|published|processed|archived] [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Json]
            （预览当前筛选条件下可归档任务；JSON 含 TotalMatched / ArchivableCount / AlreadyArchivedCount / SampleTitles）
          PLCPak.Cli -JobBatchArchiveFiltered -Force [-Filter active|failed|all|pending|published|processed|archived] [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Json]
            （按任务列表同款筛选批量归档；跳过已归档项；需 -Force 确认）
          PLCPak.Cli -JobOperationsCenter [-Json]
          PLCPak.Cli -JobExportOperationsSnapshot [-Json]
            （输出 GetOperationsCenterSnapshot 摘要；JSON 含 SummaryText / Sections / ActivityLogStatsSummary / ActivityLogBatchSummary 等）
          PLCPak.Cli -JobExportOperationsReport [-Json]
          PLCPak.Cli -JobParsePanLinks -Text "分享文本..." [-JobId <id>] [-Json]
          PLCPak.Cli -JobParsePanLinks [-JobId <id>] [-Json]   (从 stdin 读取分享文本)
          PLCPak.Cli -JobSaveTags -JobId <id> -Tags "a,b" [-Json]
          PLCPak.Cli -JobTogglePin -JobId <id> [-Json]
          PLCPak.Cli -JobExportStudioConfig -Path path.json [-Json]
          PLCPak.Cli -JobImportStudioConfig -Path path.json [-Json]
          PLCPak.Cli -JobExportAllBackup [-Path path.json] [-Json]
          PLCPak.Cli -JobImportAllBackup -Path path.json [-Merge] [-Json]
          PLCPak.Cli -JobScheduleExport [-CliPath path\PLCPak.Cli.exe] [-Filter pending|active|failed|all] [-Hour 3] [-Minute 0] [-Json]
          PLCPak.Cli -JobScheduleRegister [-CliPath path\PLCPak.Cli.exe] [-Filter pending|active|failed|all] [-Hour 3] [-Minute 0] [-Json]
          PLCPak.Cli -JobPreviewMerge -JobId <target> -SourceJobId <source> [-Json]
          PLCPak.Cli -JobMerge -JobId <target> -SourceJobId <source> [-ArchiveSource true|false]
          PLCPak.Cli -JobCheckDuplicate -Title "游戏名" [-ThreadUrl https://...] [-Json]
          PLCPak.Cli -JobStats [-Json]
          PLCPak.Cli -JobHealth [-Json]
          PLCPak.Cli -JobExportHistory [-Json]
          PLCPak.Cli -JobExport -JobId <id> -Out path.json
          PLCPak.Cli -JobImport -Path path.json [-Json]
          PLCPak.Cli -JobScan -JobId <id>
          PLCPak.Cli -JobExtract -JobId <id>
          PLCPak.Cli -JobProcess -JobId <id> -Force
          PLCPak.Cli -JobSaveLinks -JobId <id> -BaiduLink ... -BaiduPwd ... -QuarkLink ... -QuarkPwd ... [-TelegramLink ...]
          PLCPak.Cli -JobCopyLinks -JobId <id> [-Json]
          PLCPak.Cli -JobSaveNotes -JobId <id> -Notes "备注内容"
          PLCPak.Cli -JobMarkPublished -JobId <id> -Channel baidu|quark|tg
          PLCPak.Cli -JobMarkAllPublished -JobId <id>
          PLCPak.Cli -JobBatchCopy [-Filter pending] [-Template telegram-default] [-Json]
          PLCPak.Cli -JobBatchPipeline [-Filter pending|active|failed|all] [-Force] [-Json]
          PLCPak.Cli -JobCopy -JobId <id> [-Template telegram-default] [-Json]
          PLCPak.Cli -JobImportInbox -JobId <id> -Path "D:\57.RAR"
          PLCPak.Cli -JobRunPipeline -JobId <id> [-Force]
          PLCPak.Cli -JobRetry -JobId <id> [-Force]
          PLCPak.Cli -JobArchive -JobId <id>
          PLCPak.Cli -JobUnarchive -JobId <id>
          PLCPak.Cli -JobDelete -JobId <id> [-DeleteFolders] [-RecycleBin]

        参数:
          -Path / -Preview / -Clean / -Compress / -Force / -Json / -Out / -Notes / -Text / -Tags / -Tag / -Sort / -Limit / -Offset / -Hour / -Minute / -CliPath / -Action / -DownloadAttachments / -Merge / -Replace / -Mode / -Category / -SinceDays / -Days / -StaleDays / -OlderThanDays / -PublishQueueLimit / -TgLimit / -WorkflowLimit / -Pin
          -JobCreate -Title / -ThreadUrl / -Site
          -JobCreateFromThread / -JobDownloadAttachments / -JobSendTelegram / -JobTgPending / -JobTgPreview / -JobBatchSendTg / -JobImportPanLinksCsv / -JobScanDuplicates / -JobBatchTags / -JobBatchTagsJobIds / -JobBatchArchiveJobIds / -JobBatchUnarchiveJobIds / -JobPreviewBatchDeleteJobIds / -JobBatchDeleteJobIds / -JobExportActivityLog / -JobExportActivityLogCsv / -JobExportMachineProfile / -JobImportMachineProfile / -JobExportShortcutProfile / -JobImportShortcutProfile / -JobExportTgPendingCsv / -JobExportPublishQueueCsv / -JobExportDuplicateReport / -JobExportDuplicateMergeSuggestions / -JobExportBatchMergePreview / -JobActivityLogSearch / -JobActivityLogStats / -JobActivityLogDailyStats / -JobExportActivityLogStats / -JobExportActivityLogStatsCsv / -JobExportActivityLogStatsHtml / -JobExportActivityLogDailyStatsCsv / -JobExportFilteredJobsCsv / -JobExportFilteredJobsJson / -JobExportSelectedJobsCsv / -JobExportPinnedJobsCsv / -JobDashboardSnapshot / -JobSuggestDuplicateMerges / -JobPreviewBatchMergeDuplicates / -JobBatchMergeDuplicates / -JobPreviewActivityLogTrim / -JobTrimActivityLog / -JobArchiveActivityLog / -JobBatchQueueCopy
          -JobList / -JobSearch / -JobFetchTitle / -JobFetchThreadInfo / -JobPublishQueue / -JobNextAction / -JobWizardState / -JobExecuteNextAction / -JobRunChain / -JobRunBatchChain / -JobDailyReport / -JobMaintenanceReport / -JobBulkArchivePublished / -JobPreviewBatchArchiveFiltered / -JobBatchArchiveFiltered / -JobBatchPinFiltered / -JobOperationsCenter / -JobExportOperationsSnapshot / -JobExportOperationsReport / -JobExportNightlyAutomation / -JobParsePanLinks / -JobSaveTags / -JobTogglePin / -JobExportStudioConfig / -JobImportStudioConfig / -JobExportAllBackup / -JobImportAllBackup / -JobScheduleExport / -JobScheduleRegister / -JobPreviewMerge / -JobMerge / -JobCheckDuplicate / -JobStats / -JobHealth / -JobExportHistory / -JobExport / -JobImport / -JobScan / -JobExtract / -JobProcess / -JobImportInbox / -JobRunPipeline / -JobRetry / -JobBatchPipeline / -JobArchive / -JobUnarchive / -JobDelete / -JobSaveLinks / -JobCopyLinks / -JobSaveNotes / -JobMarkPublished / -JobMarkAllPublished / -JobBatchCopy / -JobCopy -JobId / -SourceJobId / -ArchiveSource
        """);
}

static List<string> ParseJobIds(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return [];

    return value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .ToList();
}

static JobSortOrder ParseSortOrder(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return JobSortOrder.UpdatedDesc;

    return value.Trim().ToLowerInvariant() switch
    {
        "updated" or "update" or "更新时间" or "updateddesc" or "updated-desc" => JobSortOrder.UpdatedDesc,
        "updatedasc" or "updated-asc" => JobSortOrder.UpdatedAsc,
        "title" or "标题" or "titleasc" or "title-asc" => JobSortOrder.TitleAsc,
        "titledesc" or "title-desc" => JobSortOrder.TitleDesc,
        "pinned" or "pinnedfirst" or "置顶" => JobSortOrder.PinnedFirst,
        _ => JobSortOrder.UpdatedDesc
    };
}

static int? ParseOptionalInt(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    return int.TryParse(value, out var parsed) ? parsed : null;
}

static int ParseOptionalIntWithDefault(string? value, int defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
        return defaultValue;

    return int.TryParse(value, out var parsed) ? parsed : defaultValue;
}

static bool ParseOptionalBool(string? value, bool defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
        return defaultValue;

    if (value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || value.Equals("y", StringComparison.OrdinalIgnoreCase))
        return true;

    if (value.Equals("false", StringComparison.OrdinalIgnoreCase)
        || value.Equals("0", StringComparison.OrdinalIgnoreCase)
        || value.Equals("no", StringComparison.OrdinalIgnoreCase)
        || value.Equals("n", StringComparison.OrdinalIgnoreCase))
        return false;

    return defaultValue;
}

static ActivityLogStatsCsvExportResult ExportActivityLogStatsCsv(
    PlcPakAppContext app,
    string? outputPath,
    int? sinceDays)
{
    var stats = app.JobRunner.GetActivityLogStats(sinceDays);
    var csv = BuildActivityLogStatsCsv(stats);
    var fullPath = ResolveActivityLogStatsCsvPath(app.Workspace.GetWorkspaceRoot(), outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, csv, Encoding.UTF8);

    return new ActivityLogStatsCsvExportResult
    {
        ExportPath = fullPath,
        Stats = stats,
        SummaryText = $"已导出活动日志统计 CSV：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
    };
}

static string BuildActivityLogStatsCsv(ActivityLogStatsResult stats)
{
    var sb = new StringBuilder();
    sb.AppendLine("category,count,latestTimestamp");
    foreach (var item in stats.Items)
    {
        sb.Append(CsvField(item.Category)).Append(',');
        sb.Append(item.Count).Append(',');
        sb.Append(CsvField(item.LatestTimestamp.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine();
    }

    return sb.ToString();
}

static string ResolveActivityLogStatsCsvPath(string workspaceRoot, string? outputPath)
{
    if (!string.IsNullOrWhiteSpace(outputPath))
        return Path.GetFullPath(outputPath);

    var reportsDir = Path.Combine(workspaceRoot, "reports");
    var fileName = $"activity-log-stats-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
    return Path.Combine(reportsDir, fileName);
}

static string CsvField(string? value)
{
    var text = value ?? string.Empty;
    if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        return $"\"{text.Replace("\"", "\"\"")}\"";
    return text;
}

sealed class ActivityLogStatsCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public ActivityLogStatsResult Stats { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}