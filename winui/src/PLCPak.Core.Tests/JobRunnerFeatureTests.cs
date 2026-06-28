using System.Text.Json;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobRunnerFeatureTests : IDisposable
{
    private readonly string _root;
    private readonly string _appRoot;
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;
    private readonly JobRunner _runner;

    public JobRunnerFeatureTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-runner-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        _appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(_appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        File.WriteAllText(Path.Combine(data, "publish-templates.json"), """
            {
              "version": 1,
              "defaultTemplateId": "telegram-default",
              "templates": [
                {
                  "id": "telegram-default",
                  "name": "TG",
                  "channel": "telegram",
                  "body": "{title}\n{baidu_line}\n{quark_line}"
                }
              ]
            }
            """);
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        var appContext = PlcPakAppContext.Create(_appRoot);
        _workspace = appContext.Workspace;
        _store = appContext.Jobs;
        _runner = appContext.JobRunner;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Unarchive_restores_processed_when_output_exists()
    {
        var job = _store.Create("归档恢复");
        job.Status = JobStatus.Archived;
        job.Artifacts.OutputArchives = [Path.Combine(job.Paths.Output, "game.7z")];
        Directory.CreateDirectory(job.Paths.Output);
        File.WriteAllText(job.Artifacts.OutputArchives[0], "fake");
        _store.Save(job);

        var restored = _runner.UnarchiveJob(job.Id);

        Assert.Equal(JobStatus.Processed, restored.Status);
    }

    [Fact]
    public void CopyAllPublishLinksText_formats_links()
    {
        var job = _store.Create("链接复制");
        _runner.SavePublishLinks(job.Id, "https://baidu.test", "abcd", "https://quark.test", "efgh", "https://t.me/test");

        var text = _runner.CopyAllPublishLinksText(job.Id);

        Assert.Contains("百度:", text);
        Assert.Contains("夸克:", text);
        Assert.Contains("TG:", text);
    }

    [Fact]
    public void RetryJob_resets_failed_to_inbox_ready()
    {
        var job = _store.Create("失败重试");
        File.WriteAllText(Path.Combine(job.Paths.Inbox, "game.7z"), "fake");
        job.Status = JobStatus.Failed;
        job.Error = "boom";
        _store.Save(job);

        var retried = _runner.RetryJob(job.Id);

        Assert.Equal(JobStatus.InboxReady, retried.Status);
        Assert.Null(retried.Error);
    }

    [Fact]
    public void SaveNotes_persists_text()
    {
        var job = _store.Create("备注任务");
        var saved = _runner.SaveNotes(job.Id, "测试备注");

        Assert.Equal("测试备注", saved.Notes);
        Assert.Equal("测试备注", _store.Get(job.Id)!.Notes);
    }

    [Fact]
    public void ExportAndImportJobJson_roundtrip()
    {
        var job = _store.Create("导入导出");
        job.Notes = "note";
        _store.Save(job);

        var exportPath = Path.Combine(_root, "export.json");
        var exported = _runner.ExportJobJson(job.Id, exportPath);
        Assert.True(File.Exists(exported.ExportPath));

        var imported = _runner.ImportJobJson(exportPath);
        Assert.Equal("导入导出", imported.Job.Title);
        Assert.Equal("note", imported.Job.Notes);
    }

    [Fact]
    public void MarkAllChannelsPublished_auto_archives_when_enabled()
    {
        var studio = new StudioConfigService(new AppPaths(_appRoot));
        var config = studio.Load();
        config.AutoArchiveOnPublished = true;
        studio.Save(config);

        var job = _store.Create("自动归档");
        _runner.SavePublishLinks(job.Id, "https://baidu.test", "abcd", "https://quark.test", "efgh");
        job = _store.Get(job.Id)!;
        job.Status = JobStatus.Processed;
        _store.Save(job);

        var published = _runner.MarkAllChannelsPublished(job.Id);

        Assert.Equal(JobStatus.Archived, published.Status);
    }

    [Fact]
    public void PreviewMergeJobs_returns_preview_without_persisting_changes()
    {
        var target = _store.Create("合并目标");
        var source = _store.Create("合并来源");
        source.Source.ThreadUrl = "https://forum.test/thread/99";
        source.Publish.Baidu.Link = "https://pan.baidu.com/s/merge";
        _store.Save(source);

        var preview = _runner.PreviewMergeJobs(target.Id, source.Id);

        Assert.True(preview.Success);
        Assert.Equal(string.Empty, _store.Get(target.Id)!.Source.ThreadUrl);
        Assert.Equal(string.Empty, _store.Get(target.Id)!.Publish.Baidu.Link);
        Assert.Contains(nameof(JobSource.ThreadUrl), preview.MergedFields);
    }

    [Fact]
    public void SavePublishLinks_logs_validation_warnings()
    {
        var job = _store.Create("链接校验");
        var saved = _runner.SavePublishLinks(job.Id, "https://bad.example", "ab", "https://also-bad.example", "xyz");

        Assert.Contains(saved.Log, l => l.Contains("[链接校验]"));
    }

    [Fact]
    public void GetPublishWorkflow_returns_fill_links_for_processed_job()
    {
        var job = _store.Create("工作流任务");
        job.Status = JobStatus.Processed;
        _store.Save(job);

        var workflow = _runner.GetPublishWorkflow(10);

        Assert.NotNull(workflow.Primary);
        Assert.Equal(JobNextActionType.FillLinks, workflow.Primary!.Action);
    }

    [Fact]
    public void GetNextActionForJob_returns_fill_links_for_processed_job_without_links()
    {
        var job = _store.Create("下一步建议");
        job.Status = JobStatus.Processed;
        _store.Save(job);

        var next = _runner.GetNextActionForJob(job.Id);

        Assert.NotNull(next);
        Assert.Equal(job.Id, next!.JobId);
        Assert.Equal(JobNextActionType.FillLinks, next.Action);
    }

    [Fact]
    public async Task ExecuteNextActionAsync_generate_copy_succeeds_with_mock_workspace()
    {
        var job = _store.Create("生成文案");
        job.Status = JobStatus.Processed;
        job.Publish.Baidu.Link = "https://pan.baidu.com/s/test";
        job.Publish.Quark.Link = "https://pan.quark.cn/s/test";
        job.Publish.Telegram.Link = "https://t.me/test";
        _store.Save(job);

        var result = await _runner.ExecuteNextActionAsync(job.Id, JobNextActionType.GenerateCopy);

        Assert.True(result.Success);
        Assert.Equal(JobNextActionType.GenerateCopy, result.Action);
        Assert.Equal("已生成发布文案", result.Message);
        Assert.NotNull(result.Job);
        Assert.False(string.IsNullOrWhiteSpace(result.Job!.Publish.GeneratedCopy));
    }

    [Fact]
    public void RegisterScheduleBatch_exports_bat_and_attempts_registration()
    {
        var result = _runner.RegisterScheduleBatch(cliExePath: @"C:\tools\PLCPak.Cli.exe");

        Assert.NotNull(result.Export);
        Assert.True(File.Exists(result.Export!.BatPath));
        Assert.Contains("nightly-batch.bat", result.Export.BatPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task ExecuteNextActionAsync_honors_copy_action_override()
    {
        var job = _store.Create("覆盖文案");
        job.Status = JobStatus.Draft;
        _store.Save(job);

        var result = await _runner.ExecuteNextActionAsync(job.Id, "copy");

        Assert.True(result.Success);
        Assert.Equal(JobNextActionType.GenerateCopy, result.Action);
    }

    [Fact]
    public async Task RunAutomatableChainAsync_stops_at_fill_links_for_processed_job()
    {
        var job = _store.Create("自动链");
        job.Status = JobStatus.Processed;
        _store.Save(job);

        var result = await _runner.RunAutomatableChainAsync(job.Id);

        Assert.True(result.NeedsUserInput);
        Assert.Equal("需要手动回填链接", result.Message);
        Assert.Equal(JobNextActionType.FillLinks, result.Steps[0].Action);
    }

    [Fact]
    public void ExportDailyReport_writes_report_files()
    {
        _store.Create("日报任务");

        var csvPath = _runner.ExportDailyReport();

        Assert.True(File.Exists(csvPath));
        Assert.Contains("reports", csvPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("daily-", Path.GetFileName(csvPath));
        Assert.Contains("jobId,title", File.ReadAllText(csvPath));
    }

    [Fact]
    public async Task RunBatchChainAsync_includes_failed_jobs_and_writes_log()
    {
        var job = _store.Create("批量链失败");
        job.Status = JobStatus.Failed;
        job.Error = "boom";
        _store.Save(job);

        var filtered = JobHealthService.FilterForBatchChain(_store.List(), JobListFilter.Failed);
        Assert.Contains(filtered, j => j.Id == job.Id);

        var result = await _runner.RunBatchChainAsync(JobListFilter.Failed);

        Assert.Single(result.JobResults);
        Assert.Contains(result.Messages, m => m.Contains("批量链失败"));
        Assert.False(string.IsNullOrWhiteSpace(result.BatchLogPath));
    }

    [Fact]
    public void ExportScheduleBatchScript_writes_nightly_batch_file()
    {
        var studio = new StudioConfigService(new AppPaths(_appRoot));
        var config = studio.Load();
        config.ScheduledBatchFilter = "active";
        config.ScheduledBatchHour = 4;
        config.ScheduledBatchMinute = 30;
        studio.Save(config);

        var cliExe = @"C:\tools\PLCPak.Cli.exe";
        var export = _runner.ExportScheduleBatchScript(cliExe);

        Assert.True(File.Exists(export.BatPath));
        var bat = File.ReadAllText(export.BatPath);
        Assert.Contains("-JobBatchPipeline", bat);
        Assert.Contains(cliExe, bat);
        Assert.Contains("/ST 04:30", export.SchTasksCommand);
    }

    [Fact]
    public void ApplyParsedPanLinks_fills_empty_publish_fields()
    {
        var job = _store.Create("解析回填");
        var parsed = PanLinkParseService.ParseShareText("""
            百度网盘链接：https://pan.baidu.com/s/applytest
            提取码：abcd
            夸克网盘链接：https://pan.quark.cn/s/quarktest
            密码：ef12gh
            TG：https://t.me/applytest/1
            """);

        var updated = _runner.ApplyParsedPanLinks(job.Id, parsed);

        Assert.Equal("https://pan.baidu.com/s/applytest", updated.Publish.Baidu.Link);
        Assert.Equal("abcd", updated.Publish.Baidu.Password);
        Assert.Equal("https://pan.quark.cn/s/quarktest", updated.Publish.Quark.Link);
        Assert.Equal("ef12gh", updated.Publish.Quark.Password);
        Assert.Equal("https://t.me/applytest/1", updated.Publish.Telegram.Link);
        Assert.Contains(updated.Log, l => l.Contains("已应用解析链接"));
    }

    [Fact]
    public void ApplyParsedPanLinks_does_not_overwrite_existing_links()
    {
        var job = _store.Create("保留已有");
        _runner.SavePublishLinks(job.Id, "https://pan.baidu.com/s/existing", "keep1", "https://pan.quark.cn/s/existing", "keep2");

        var parsed = PanLinkParseService.ParseShareText("""
            百度网盘链接：https://pan.baidu.com/s/newlink
            提取码：new1
            夸克网盘链接：https://pan.quark.cn/s/newlink
            密码：new2
            """);

        var updated = _runner.ApplyParsedPanLinks(job.Id, parsed);

        Assert.Equal("https://pan.baidu.com/s/existing", updated.Publish.Baidu.Link);
        Assert.Equal("keep1", updated.Publish.Baidu.Password);
        Assert.Contains(updated.Log, l => l.Contains("无需更新"));
    }

    [Fact]
    public void SaveJobTags_parses_comma_separated_values()
    {
        var job = _store.Create("标签任务");
        var updated = _runner.SaveJobTags(job.Id, "热门, PC ,热门");

        Assert.Equal(["热门", "PC"], updated.Tags);
        Assert.Equal(["热门", "PC"], _store.Get(job.Id)!.Tags);
    }

    [Fact]
    public void ToggleJobPin_flips_pinned_state()
    {
        var job = _store.Create("置顶任务");
        Assert.False(job.IsPinned);

        var pinned = _runner.ToggleJobPin(job.Id);
        Assert.True(pinned.IsPinned);

        var unpinned = _runner.ToggleJobPin(job.Id);
        Assert.False(unpinned.IsPinned);
    }

    [Fact]
    public void ExportOperationsCenterReport_writes_json_report()
    {
        _store.Create("运营任务");

        var reportPath = _runner.ExportOperationsCenterReport();

        Assert.True(File.Exists(reportPath));
        Assert.Contains("operations-", Path.GetFileName(reportPath));
        Assert.Contains("reports", reportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quickStatsLine", File.ReadAllText(reportPath));
    }

    [Fact]
    public void GetOperationsCenterSnapshot_includes_activity_log_batch_summary()
    {
        _store.Create("运营任务");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "archive", "批量归档选中: 成功 1");

        var snapshot = _runner.GetOperationsCenterSnapshot();

        Assert.True(snapshot.ActivityLogBatchSummary.TotalCount > 0);
        Assert.Contains("批量操作统计", snapshot.ActivityLogBatchSummary.SummaryText);
        Assert.Contains(snapshot.ActivityLogBatchSummary.SummaryText, snapshot.SummaryText);
        Assert.Contains(snapshot.Sections, section => section.Key == "activity-log-batch");
    }

    [Fact]
    public void GetOperationsCenterSnapshot_includes_activity_log_stats_summary()
    {
        _store.Create("运营任务");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "export", "导出活动日志统计");

        var snapshot = _runner.GetOperationsCenterSnapshot();

        Assert.True(snapshot.ActivityLogStatsSummary.TotalCount > 0);
        Assert.Contains("活动日志统计", snapshot.ActivityLogStatsSummary.SummaryText);
        Assert.Contains(snapshot.ActivityLogStatsSummary.SummaryText, snapshot.SummaryText);
        Assert.Contains(snapshot.Sections, section => section.Key == "activity-log-stats");
    }

    [Fact]
    public void ExportOperationsCenterReport_includes_activity_log_batch_summary()
    {
        _store.Create("运营任务");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "archive", "批量归档选中: 成功 1");

        var reportPath = _runner.ExportOperationsCenterReport();
        var json = File.ReadAllText(reportPath);

        Assert.Contains("activityLogBatchSummary", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("批量操作统计", json);
    }

    [Fact]
    public void ExportMachineProfile_includes_operations_center_summary()
    {
        _store.Create("运营任务");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "export", "机器配置导出测试");

        var export = _runner.ExportMachineProfile();

        Assert.Contains("运维快照", export.SummaryText);
        Assert.Contains("待发布", export.OperationsCenterSummary);
        Assert.Contains("活动日志统计", export.OperationsCenterSummary);

        var bundle = JsonHelper.ReadFile<MachineProfileBundle>(export.ExportPath);
        Assert.NotNull(bundle);
        Assert.Equal(export.OperationsCenterSummary, bundle!.OperationsCenterSummary);
    }

    [Fact]
    public void PreviewMachineProfile_reads_operations_center_summary_from_bundle()
    {
        _store.Create("运营任务");
        var export = _runner.ExportMachineProfile();

        var preview = _runner.PreviewMachineProfile(export.ExportPath);

        Assert.True(preview.Valid);
        Assert.Equal(export.OperationsCenterSummary, preview.OperationsCenterSummary);
        Assert.Contains("导出时运维", preview.SummaryText);
    }

    [Fact]
    public void ImportMachineProfile_invalidates_operations_snapshot_cache()
    {
        _store.Create("运营缓存");
        var first = _runner.GetOperationsCenterSnapshot();
        var export = _runner.ExportMachineProfile();
        var import = _runner.ImportMachineProfile(export.ExportPath, merge: true);

        Assert.True(import.Success);
        var second = _runner.GetOperationsCenterSnapshot();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ExportOperationsCenterReport_includes_activity_log_stats_summary()
    {
        _store.Create("运营任务");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "export", "导出活动日志统计");

        var reportPath = _runner.ExportOperationsCenterReport();
        var json = File.ReadAllText(reportPath);

        Assert.Contains("activityLogStatsSummary", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("活动日志统计", json);
    }

    [Fact]
    public void CreateJob_applies_default_tags_from_studio_config()
    {
        var studio = new StudioConfigService(new AppPaths(_appRoot));
        var config = studio.Load();
        config.DefaultJobTags = ["热门", "PC", "热门"];
        studio.Save(config);

        var job = _runner.CreateJob("默认标签任务");

        Assert.Equal(["热门", "PC"], job.Tags);
        Assert.Equal(["热门", "PC"], _store.Get(job.Id)!.Tags);
    }

    [Fact]
    public void QueryJobsByTag_filters_jobs_by_tag()
    {
        var tagged = _store.Create("热门任务");
        tagged.Tags = ["热门"];
        _store.Save(tagged);
        _store.Create("普通任务");

        var result = _runner.QueryJobsByTag("热门", JobListFilter.All, null);

        Assert.Single(result);
        Assert.Equal("热门任务", result[0].Title);
    }

    [Fact]
    public void ExportNightlyAutomationScript_writes_nightly_full_file()
    {
        var cliExe = @"C:\tools\PLCPak.Cli.exe";
        var export = _runner.ExportNightlyAutomationScript(cliExe);

        Assert.True(File.Exists(export.BatPath));
        Assert.EndsWith("nightly-full.bat", export.BatPath, StringComparison.OrdinalIgnoreCase);

        var bat = File.ReadAllText(export.BatPath);
        Assert.Contains("-JobRunBatchChain", bat);
        Assert.Contains("-JobBatchQueueCopy", bat);
        Assert.Contains("-JobDailyReport", bat);
        Assert.Contains("-JobExportOperationsReport", bat);
        Assert.Contains(cliExe, bat);
    }

    [Fact]
    public void ExportNightlyAutomationScript_includes_batch_stats_all_when_standalone_option_enabled()
    {
        var studio = PlcPakAppContext.Create(_appRoot).StudioConfig.Load();
        studio.NightlyExportActivityLogBatchStatsAll = true;
        studio.NightlyExportActivityLogStatsBoth = false;
        studio.NightlyExportActivityLogStats = false;
        studio.NightlyExportActivityLogStatsHtml = false;
        PlcPakAppContext.Create(_appRoot).StudioConfig.Save(studio);

        var export = _runner.ExportNightlyAutomationScript(@"C:\tools\PLCPak.Cli.exe");
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("\"%CLI%\" -JobExportActivityLogBatchStatsAll", bat);
        Assert.DoesNotContain(GetActiveNightlyBatLines(bat), line => line.Contains("-JobExportActivityLogStatsCsv", StringComparison.Ordinal));
        Assert.DoesNotContain(GetActiveNightlyBatLines(bat), line => line.Contains("-JobExportActivityLogStatsHtml", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportNightlyAutomationScript_appends_since_days_from_activity_log_keep_days()
    {
        var studio = PlcPakAppContext.Create(_appRoot).StudioConfig.Load();
        studio.NightlyExportActivityLogStatsBoth = true;
        studio.ActivityLogKeepDays = 45;
        PlcPakAppContext.Create(_appRoot).StudioConfig.Save(studio);

        var export = _runner.ExportNightlyAutomationScript(@"C:\tools\PLCPak.Cli.exe");
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportActivityLogStatsCsv -SinceDays 45", bat);
        Assert.Contains("-JobExportActivityLogStatsHtml -SinceDays 45", bat);
        Assert.Contains("-JobExportActivityLogBatchStatsAll -SinceDays 45", bat);
        Assert.Contains("-SinceDays 45", export.ReadmeText);
        Assert.Contains("-SinceDays 45", export.SummaryText);
        Assert.True(export.EnableActivityLogBatchStatsAllExport);
        Assert.Equal(45, export.ActivityLogExportSinceDays);
    }

    [Fact]
    public void JobRunner_ExportBatchMergePreview_writes_json()
    {
        _store.Create("重复A");
        var target = _store.Create("重复A");
        target.Publish.Baidu.Link = "https://pan.baidu.com/s/dup";
        _store.Save(target);

        var exportPath = Path.Combine(_root, "batch-merge-preview.json");
        var export = _runner.ExportBatchMergePreview(exportPath);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Equal(exportPath, export.ExportPath);
        Assert.Contains("batch-merge-preview", Path.GetFileName(export.ExportPath));
        Assert.Equal(1, export.GroupCount);
        Assert.Equal(1, export.MergeActionCount);
        Assert.Contains("items", File.ReadAllText(export.ExportPath), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, _store.List().Count);
    }

    [Fact]
    public void JobRunner_ExportActivityLogCsv_respects_since_days()
    {
        WriteActivityEntry(DateTime.Now.AddDays(-40), "old", "stale entry");
        _runner.LogActivity("new", "fresh entry");

        var exportPath = Path.Combine(_root, "activity-since.csv");
        var export = _runner.ExportActivityLogCsv(exportPath, sinceDays: 7);

        Assert.Equal(1, export.EntryCount);
        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("fresh entry", csv);
        Assert.DoesNotContain("stale entry", csv);
    }

    [Fact]
    public void JobRunner_ExportActivityLogCsv_writes_csv_with_header_and_rows()
    {
        _runner.LogActivity("telegram", "send message");
        _runner.LogActivity("import", "csv import");

        var exportPath = Path.Combine(_root, "activity.csv");
        var export = _runner.ExportActivityLogCsv(exportPath);

        Assert.Equal(2, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Equal(exportPath, export.ExportPath);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("timestamp,category,message", csv);
        Assert.Contains("telegram", csv);
        Assert.Contains("send message", csv);
        Assert.Contains("import", csv);
    }

    [Fact]
    public void JobRunner_ExportActivityLogCsv_filters_by_category()
    {
        _runner.LogActivity("telegram", "send");
        _runner.LogActivity("import", "csv");

        var export = _runner.ExportActivityLogCsv(category: "import");

        Assert.Equal(1, export.EntryCount);
        Assert.Equal("import", export.CategoryFilter);
        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("csv", csv);
        Assert.DoesNotContain("telegram", csv);
    }

    [Fact]
    public void JobRunner_GetActivityLogCategoryCounts_returns_category_counts_without_loading_entries()
    {
        _runner.LogActivity("telegram", "send");
        _runner.LogActivity("import", "csv");
        _runner.LogActivity("import", "csv2");

        var counts = _runner.GetActivityLogCategoryCounts();

        Assert.Equal(2, counts.Count);
        Assert.Equal(2, counts["import"]);
        Assert.Equal(1, counts["telegram"]);
    }

    [Fact]
    public void JobRunner_GetActivityLogStats_returns_category_counts()
    {
        _runner.LogActivity("telegram", "send");
        _runner.LogActivity("import", "csv");
        _runner.LogActivity("import", "csv2");

        var stats = _runner.GetActivityLogStats();

        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(2, stats.Items.Count);
        Assert.Equal(2, stats.Items.First(item => item.Category == "import").Count);
        Assert.Contains("3 条", stats.SummaryText);
    }

    [Fact]
    public void JobRunner_ExportActivityLogStats_writes_json_report()
    {
        _runner.LogActivity("export", "stats test");

        var exportPath = Path.Combine(_root, "activity-stats.json");
        var export = _runner.ExportActivityLogStats(exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Equal(1, export.Stats.TotalCount);
        Assert.Contains("totalCount", File.ReadAllText(export.ExportPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JobRunner_ExportActivityLogStatsCsv_writes_csv_report()
    {
        _runner.LogActivity("export", "csv stats test");

        var exportPath = Path.Combine(_root, "activity-stats.csv");
        var export = _runner.ExportActivityLogStatsCsv(exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Equal(1, export.Stats.TotalCount);
        Assert.Contains("category,count,latestTimestamp", File.ReadAllText(export.ExportPath));
        Assert.Contains("已导出活动日志统计 CSV", export.SummaryText);
    }

    [Fact]
    public void JobRunner_ExportActivityLogDailyStatsCsv_writes_daily_csv_report()
    {
        _runner.LogActivity("export", "daily stats test");

        var exportPath = Path.Combine(_root, "activity-daily-stats.csv");
        var export = _runner.ExportActivityLogDailyStatsCsv(exportPath, days: 7);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Equal(1, export.Stats.TotalCount);
        Assert.Equal(7, export.Stats.Days);
        Assert.Contains("date,count", File.ReadAllText(export.ExportPath));
        Assert.Contains("已导出活动日志每日统计 CSV", export.SummaryText);
    }

    [Fact]
    public void JobRunner_ExportActivityLogStatsHtml_writes_html_report()
    {
        _runner.LogActivity("export", "html stats test");

        var exportPath = Path.Combine(_root, "activity-stats.html");
        var export = _runner.ExportActivityLogStatsHtml(outputPath: exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Equal(1, export.Stats.TotalCount);
        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("活动日志统计", html);
        Assert.Contains("已导出活动日志统计 HTML", export.SummaryText);
    }

    [Fact]
    public void JobRunner_BatchApplyTagsToJobIds_appends_tags_to_selected_jobs()
    {
        var jobA = _runner.CreateJob("Alpha", new JobSource());
        var jobB = _runner.CreateJob("Beta", new JobSource());

        var result = _runner.BatchApplyTagsToJobIds([jobA.Id, jobB.Id], "优先, 测试");

        Assert.Equal(2, result.Applied);
        Assert.Equal(0, result.Skipped);
        Assert.Contains("优先", _store.Get(jobA.Id)!.Tags);
        Assert.Contains("测试", _store.Get(jobB.Id)!.Tags);
    }

    [Fact]
    public void JobRunner_BatchArchiveJobIds_archives_selected_jobs()
    {
        var jobA = _runner.CreateJob("Archive A", new JobSource());
        var jobB = _runner.CreateJob("Archive B", new JobSource());

        var result = _runner.BatchArchiveJobIds([jobA.Id, jobB.Id]);

        Assert.Equal(2, result.Archived);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(JobStatus.Archived, _store.Get(jobA.Id)!.Status);
        Assert.Equal(JobStatus.Archived, _store.Get(jobB.Id)!.Status);
    }

    [Fact]
    public void JobRunner_PreviewBatchDeleteJobIds_lists_selected_jobs()
    {
        var job = _store.Create("删除预览");

        var preview = _runner.PreviewBatchDeleteJobIds([job.Id]);

        Assert.Equal(1, preview.Count);
        Assert.Contains(job.Title, preview.SampleTitles);
    }

    [Fact]
    public void JobRunner_BatchDeleteJobIds_deletes_selected_jobs()
    {
        var jobA = _runner.CreateJob("Delete A", new JobSource());
        var jobB = _runner.CreateJob("Delete B", new JobSource());

        var result = _runner.BatchDeleteJobIds([jobA.Id, jobB.Id]);

        Assert.Equal(2, result.Deleted);
        Assert.Null(_store.Get(jobA.Id));
        Assert.Null(_store.Get(jobB.Id));
    }

    [Fact]
    public void JobRunner_BatchUnarchiveJobIds_restores_selected_jobs()
    {
        var jobA = _runner.CreateJob("Restore A", new JobSource());
        var jobB = _runner.CreateJob("Restore B", new JobSource());
        _runner.ArchiveJob(jobA.Id);
        _runner.ArchiveJob(jobB.Id);

        var result = _runner.BatchUnarchiveJobIds([jobA.Id, jobB.Id]);

        Assert.Equal(2, result.Unarchived);
        Assert.Equal(0, result.Skipped);
        Assert.NotEqual(JobStatus.Archived, _store.Get(jobA.Id)!.Status);
        Assert.NotEqual(JobStatus.Archived, _store.Get(jobB.Id)!.Status);
    }

    [Fact]
    public void JobRunner_ExportActivityLogStatsHtml_includes_daily_chart_section()
    {
        _runner.LogActivity("export", "daily chart test");

        var exportPath = Path.Combine(_root, "activity-stats-daily.html");
        var export = _runner.ExportActivityLogStatsHtml(outputPath: exportPath);

        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("近 7 天每日条数", html);
        Assert.Contains("daily-bar", html);
        Assert.Contains("category-chart", html);
        Assert.Contains("renderBars", html);
    }

    [Fact]
    public void JobRunner_ExportSelectedJobsCsv_writes_selected_jobs_csv()
    {
        var first = _store.Create("选中一");
        first.Status = JobStatus.Processed;
        _store.Save(first);

        var second = _store.Create("选中二");
        second.Status = JobStatus.Failed;
        _store.Save(second);

        _store.Create("未选中");

        var exportPath = Path.Combine(_root, "selected-jobs.csv");
        var export = _runner.ExportSelectedJobsCsv([second.Id, first.Id], exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.Equal(2, export.EntryCount);
        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("jobId,title,status,slug,tags,publishSummary,updatedAt,isPinned,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink,hasCopy", csv);
        Assert.Contains("选中一", csv);
        Assert.Contains("选中二", csv);
        Assert.DoesNotContain("未选中", csv);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("选中二", lines[1]);
        Assert.Contains("选中一", lines[2]);
    }

    [Fact]
    public void JobRunner_ExportFilteredJobsCsv_writes_filtered_csv()
    {
        var pinned = _store.Create("筛选置顶");
        pinned.IsPinned = true;
        pinned.Tags = ["hot"];
        pinned.Status = JobStatus.Processed;
        _store.Save(pinned);

        var failed = _store.Create("筛选失败");
        failed.Status = JobStatus.Failed;
        _store.Save(failed);

        var exportPath = Path.Combine(_root, "filtered-jobs.csv");
        var export = _runner.ExportFilteredJobsCsv(
            JobListFilter.Processed,
            searchText: "置顶",
            tagFilter: "hot",
            JobSortOrder.PinnedFirst,
            outputPath: exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.Equal(1, export.EntryCount);
        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("jobId,title,status,slug,tags,publishSummary,updatedAt,isPinned,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink,hasCopy", csv);
        Assert.Contains("筛选置顶", csv);
        Assert.DoesNotContain("筛选失败", csv);
    }

    [Fact]
    public void JobRunner_ExportFilteredJobsJson_writes_filtered_json()
    {
        var pinned = _store.Create("筛选置顶");
        pinned.IsPinned = true;
        pinned.Tags = ["hot"];
        pinned.Status = JobStatus.Processed;
        _store.Save(pinned);

        var failed = _store.Create("筛选失败");
        failed.Status = JobStatus.Failed;
        _store.Save(failed);

        var exportPath = Path.Combine(_root, "filtered-jobs.json");
        var export = _runner.ExportFilteredJobsJson(
            JobListFilter.Processed,
            searchText: "置顶",
            tagFilter: "hot",
            JobSortOrder.PinnedFirst,
            outputPath: exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.Equal(1, export.EntryCount);
        var jobs = JsonHelper.ReadFile<List<PublishJob>>(export.ExportPath);
        Assert.NotNull(jobs);
        Assert.Single(jobs!);
        Assert.Equal("筛选置顶", jobs![0].Title);
    }

    [Fact]
    public void JobRunner_ExportPinnedJobsCsv_writes_only_pinned_jobs_csv()
    {
        var pinned = _store.Create("置顶任务");
        pinned.IsPinned = true;
        pinned.Status = JobStatus.Processed;
        _store.Save(pinned);

        var otherPinned = _store.Create("另一置顶");
        otherPinned.IsPinned = true;
        _store.Save(otherPinned);

        _store.Create("未置顶");

        var exportPath = Path.Combine(_root, "pinned-jobs.csv");
        var export = _runner.ExportPinnedJobsCsv(exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.Equal(2, export.EntryCount);
        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("jobId,title,status,slug,tags,publishSummary,updatedAt,isPinned,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink,hasCopy", csv);
        Assert.Contains("置顶任务", csv);
        Assert.Contains("另一置顶", csv);
        Assert.DoesNotContain("未置顶", csv);
    }

    [Fact]
    public void JobRunner_TrimActivityLog_archives_before_trim_when_configured()
    {
        WriteActivityEntry(DateTime.UtcNow.AddDays(-40), "old", "stale");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "new", "fresh");

        var appContext = PlcPakAppContext.Create(_appRoot);
        var config = appContext.StudioConfig.Load();
        config.ArchiveBeforeTrimActivityLog = true;
        appContext.StudioConfig.Save(config);

        var result = _runner.TrimActivityLog(keepDays: 30);

        Assert.Contains("活动日志归档", result.SummaryText);
        Assert.Contains("活动日志清理", result.SummaryText);
        var remaining = ActivityLogService.ReadAll(_workspace.GetWorkspaceRoot());
        Assert.Contains(remaining, entry => entry.Category == "new" && entry.Message == "fresh");
        Assert.DoesNotContain(remaining, entry => entry.Category == "old");
        Assert.True(Directory.GetFiles(Path.Combine(_workspace.GetWorkspaceRoot(), "reports"), "activity-archive-*.json").Length > 0);
    }

    [Fact]
    public void JobRunner_GetActivityLogDailyStats_returns_recent_daily_counts()
    {
        WriteActivityEntry(DateTime.Today.AddDays(-1), "export", "yesterday");
        ActivityLogService.Append(_workspace.GetWorkspaceRoot(), "export", "today");

        var stats = _runner.GetActivityLogDailyStats(days: 2);

        Assert.Equal(2, stats.Days);
        Assert.Equal(2, stats.TotalCount);
        Assert.Equal(2, stats.Items.Count);
        Assert.Equal(1, stats.Items[^1].Count);
    }

    [Fact]
    public void JobRunner_PreviewBatchArchiveFiltered_returns_archivable_count_for_filter()
    {
        var published = _store.Create("筛选归档");
        published.Status = JobStatus.Published;
        _store.Save(published);
        var draft = _store.Create("草稿");

        var preview = _runner.PreviewBatchArchiveFiltered(
            JobListFilter.Published,
            searchText: null,
            tagFilter: null,
            JobSortOrder.UpdatedDesc);

        Assert.Equal(1, preview.ArchivableCount);
        Assert.Equal(1, preview.TotalMatched);
        Assert.Contains("可归档 1 个", preview.SummaryText);
        Assert.Equal(JobStatus.Draft, _store.Get(draft.Id)!.Status);
    }

    [Fact]
    public void JobRunner_BatchPinFilteredJobs_pins_matching_jobs()
    {
        var unpinned = _store.Create("批量置顶");
        var pinned = _store.Create("已置顶");
        pinned.IsPinned = true;
        _store.Save(pinned);

        var preview = _runner.PreviewBatchPinFiltered(JobListFilter.All, null, null, pin: true);
        Assert.Equal(2, preview.TotalMatched);
        Assert.Equal(1, preview.ApplicableCount);

        var result = _runner.BatchPinFilteredJobs(JobListFilter.All, null, null, pin: true);

        Assert.Equal(1, result.Applied);
        Assert.True(_store.Get(unpinned.Id)!.IsPinned);
        Assert.True(_store.Get(pinned.Id)!.IsPinned);
    }

    [Fact]
    public void JobRunner_PreviewBatchMergeDuplicates_returns_preview_without_persisting()
    {
        var source = _store.Create("重复标题");
        var target = _store.Create("重复标题");
        target.Publish.Baidu.Link = "https://pan.baidu.com/s/keep";
        _store.Save(target);

        var preview = _runner.PreviewBatchMergeDuplicates();

        Assert.Equal(1, preview.GroupCount);
        Assert.Equal(1, preview.MergeActionCount);
        Assert.Single(preview.Items);
        Assert.Equal(target.Id, preview.Items[0].TargetJobId);
        Assert.Equal([source.Id], preview.Items[0].SourceJobIds);
        Assert.Equal(2, _store.List().Count);
    }

    [Fact]
    public void JobRunner_ExportDuplicateMergeSuggestions_writes_json()
    {
        _store.Create("导出合并建议");
        _store.Create("导出合并建议");

        var export = _runner.ExportDuplicateMergeSuggestions();

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("duplicate-merge-suggestions-", Path.GetFileName(export.ExportPath));
        Assert.Equal(1, export.GroupCount);
        Assert.Equal(1, export.MergeActionCount);
        Assert.Contains("suggestions", File.ReadAllText(export.ExportPath), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, _store.List().Count);
    }

    [Fact]
    public void JobRunner_SearchActivityLogPage_uses_studio_default_page_size()
    {
        var studio = new StudioConfigService(new AppPaths(_appRoot));
        var config = studio.Load();
        config.ActivityLogPageSize = 2;
        studio.Save(config);

        for (var i = 1; i <= 4; i++)
            _runner.LogActivity("test", $"page-{i}");

        var page = _runner.SearchActivityLogPage(query: null);

        Assert.Equal(4, page.TotalMatched);
        Assert.Equal(2, page.Entries.Count);
        Assert.Equal(2, page.Limit);
        Assert.Equal(0, page.Offset);
        Assert.True(page.HasMore);
        Assert.Equal("page-1", page.Entries[0].Message);
        Assert.Equal("page-2", page.Entries[1].Message);
    }

    [Fact]
    public void JobRunner_SearchActivityLogPage_second_page_HasMore_and_TotalMatched()
    {
        for (var i = 1; i <= 5; i++)
            _runner.LogActivity("page", $"runner-entry-{i}");

        var secondPage = _runner.SearchActivityLogPage(query: null, limit: 2, offset: 2);

        Assert.Equal(5, secondPage.TotalMatched);
        Assert.Equal(2, secondPage.Offset);
        Assert.Equal(2, secondPage.Limit);
        Assert.Equal(2, secondPage.Entries.Count);
        Assert.True(secondPage.HasMore);
        Assert.Equal("runner-entry-3", secondPage.Entries[0].Message);
        Assert.Equal("runner-entry-4", secondPage.Entries[1].Message);
    }

    [Fact]
    public void JobRunner_GetDashboardSnapshot_matches_individual_calls()
    {
        var failed = _store.Create("对照失败");
        failed.Status = JobStatus.Failed;
        failed.Error = "boom";
        _store.Save(failed);

        var processed = _store.Create("对照待发布");
        processed.Status = JobStatus.Processed;
        processed.Publish.GeneratedCopy = "copy";
        _store.Save(processed);

        var jobs = _store.List();
        var snapshot = _runner.GetDashboardSnapshot(publishQueueLimit: 10, tgLimit: 20, workflowLimit: 3);

        Assert.Equal(PublishDashboardService.ComputeStats(jobs).SummaryText, snapshot.StatsSummary.SummaryText);
        Assert.Equal(_runner.GetJobHealth(jobs).SummaryText, snapshot.JobHealth.SummaryText);
        Assert.Equal(_runner.GetPublishQueue(10, jobs).SummaryText, snapshot.PublishQueue.SummaryText);
        Assert.Equal(_runner.GetPublishWorkflow(3, jobs).SummaryText, snapshot.Workflow.SummaryText);
        Assert.Equal(_runner.GetTgPendingQueue(20, jobs).SummaryText, snapshot.TgPending.SummaryText);
    }

    [Fact]
    public void JobRunner_GetDashboardSnapshot_reflects_created_jobs()
    {
        var failed = _store.Create("看板失败");
        failed.Status = JobStatus.Failed;
        failed.Error = "boom";
        _store.Save(failed);

        var processed = _store.Create("看板待发布");
        processed.Status = JobStatus.Processed;
        _store.Save(processed);

        var snapshot = _runner.GetDashboardSnapshot();

        Assert.Equal(2, snapshot.StatsSummary.Total);
        Assert.Equal(1, snapshot.StatsSummary.Failed);
        Assert.Equal(1, snapshot.PublishQueue.Count);
        Assert.Contains("失败 1", snapshot.SummaryText);
        Assert.Contains("待发布队列 1", snapshot.SummaryText);
        Assert.NotNull(snapshot.Workflow.Primary);
    }

    [Fact]
    public void JobRunner_GetOperationsCenterSnapshot_reflects_created_jobs()
    {
        var failed = _store.Create("失败任务");
        failed.Status = JobStatus.Failed;
        failed.Error = "boom";
        _store.Save(failed);

        var processed = _store.Create("待发布");
        processed.Status = JobStatus.Processed;
        _store.Save(processed);

        var snapshot = _runner.GetOperationsCenterSnapshot();

        Assert.Contains("失败 1", snapshot.QuickStatsLine);
        Assert.Contains("待发布 1", snapshot.QuickStatsLine);
        Assert.Equal(5, snapshot.Sections.Count);
        Assert.Contains("失败 1", snapshot.SummaryText);
    }

    [Fact]
    public void JobRunner_ExportDuplicateReport_writes_json_for_duplicate_groups()
    {
        _store.Create("重复报告A");
        _store.Create("重复报告A");

        var export = _runner.ExportDuplicateReport();

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("duplicates-", Path.GetFileName(export.ExportPath));
        Assert.Equal(1, export.GroupCount);
        Assert.Equal(2, export.DuplicateJobCount);
    }

    private void WriteActivityEntry(DateTime timestamp, string category, string message)
    {
        var entry = new ActivityLogEntry
        {
            Timestamp = timestamp,
            Category = category,
            Message = message
        };
        var path = ActivityLogService.GetLogPath(_workspace.GetWorkspaceRoot());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, JsonSerializer.Serialize(entry) + Environment.NewLine);
    }

    private static IEnumerable<string> GetActiveNightlyBatLines(string bat)
        => bat.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("REM ", StringComparison.OrdinalIgnoreCase));

}