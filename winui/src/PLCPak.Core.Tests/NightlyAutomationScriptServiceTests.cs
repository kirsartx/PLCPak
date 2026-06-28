using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class NightlyAutomationScriptServiceTests : IDisposable
{
    private readonly string _workspaceRoot;

    public NightlyAutomationScriptServiceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "plcpak-nightly-full-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
            Directory.Delete(_workspaceRoot, recursive: true);
    }

    [Fact]
    public void Export_writes_bat_with_all_nightly_commands_including_tg_preview()
    {
        var options = new NightlyAutomationOptions
        {
            CliExePath = @"..\PLCPak.Cli\app\PLCPak.Cli.exe",
            BatchChainFilter = "active",
            QueueCopyLimit = 50
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);

        Assert.True(File.Exists(export.BatPath));
        Assert.EndsWith("nightly-full.bat", export.BatPath, StringComparison.OrdinalIgnoreCase);

        var bat = File.ReadAllText(export.BatPath);
        Assert.Contains("-JobRunBatchChain", bat);
        Assert.Contains("-Filter active", bat);
        Assert.Contains("-Force", bat);
        Assert.Contains("-JobBatchQueueCopy", bat);
        Assert.Contains("-Limit 50", bat);
        Assert.Contains("-JobDailyReport", bat);
        Assert.Contains("-JobExportOperationsReport", bat);
        Assert.Contains("-JobTgPreview", bat);
        Assert.Contains("-JobBatchSendTg", bat);
        Assert.Contains("REM", bat);
        Assert.Contains(@"..\PLCPak.Cli\app\PLCPak.Cli.exe", bat);
    }

    [Fact]
    public void Export_writes_readme_with_sequence_summary()
    {
        var export = NightlyAutomationScriptService.Export(_workspaceRoot, new NightlyAutomationOptions());

        var readmePath = Path.Combine(_workspaceRoot, "scripts", "nightly-full-readme.txt");
        Assert.True(File.Exists(readmePath));
        Assert.Contains("-JobRunBatchChain", export.ReadmeText);
        Assert.Contains("-JobBatchQueueCopy", export.ReadmeText);
        Assert.Contains("-JobDailyReport", export.ReadmeText);
        Assert.Contains("-JobExportOperationsReport", export.ReadmeText);
        Assert.Contains("-JobTgPreview", export.ReadmeText);
        Assert.Contains("夜间全自动流水线", export.ReadmeText);
        Assert.Contains("activity-log-stats", export.ReadmeText);
    }

    [Fact]
    public void Export_includes_batch_merge_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableBatchMergeDuplicates = true,
            IncludeBatchMergeDuplicatesComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobBatchMergeDuplicates", bat);
        Assert.Contains("-Force", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobBatchMergeDuplicates", bat);
        Assert.Contains("-JobBatchMergeDuplicates -Force", export.ReadmeText);

        var exportIndex = bat.IndexOf("-JobExportOperationsReport", StringComparison.Ordinal);
        var scanIndex = bat.IndexOf("-JobScanDuplicates", StringComparison.Ordinal);
        var mergeIndex = bat.IndexOf("-JobBatchMergeDuplicates", StringComparison.Ordinal);
        var trimIndex = bat.IndexOf("-JobTrimActivityLog", StringComparison.Ordinal);
        Assert.True(exportIndex >= 0);
        Assert.True(scanIndex < 0 || scanIndex > exportIndex);
        Assert.True(mergeIndex > exportIndex);
        Assert.True(trimIndex > mergeIndex);
    }

    [Fact]
    public void Export_includes_scan_duplicates_steps_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableScanDuplicates = true,
            IncludeScanDuplicatesComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobScanDuplicates", bat);
        Assert.Contains("-JobExportDuplicateReport", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobScanDuplicates", bat);

        var exportIndex = bat.IndexOf("-JobExportOperationsReport", StringComparison.Ordinal);
        var scanIndex = bat.IndexOf("-JobScanDuplicates", StringComparison.Ordinal);
        var reportIndex = bat.IndexOf("-JobExportDuplicateReport", StringComparison.Ordinal);
        var mergeIndex = bat.IndexOf("-JobBatchMergeDuplicates", StringComparison.Ordinal);
        Assert.True(exportIndex >= 0);
        Assert.True(scanIndex > exportIndex);
        Assert.True(reportIndex > scanIndex);
        Assert.True(mergeIndex > reportIndex);
        Assert.Contains("-JobScanDuplicates", export.ReadmeText);
        Assert.Contains("-JobExportDuplicateReport", export.ReadmeText);
    }

    [Fact]
    public void Export_comments_scan_duplicates_steps_when_disabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableScanDuplicates = false,
            IncludeScanDuplicatesComment = true
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("REM \"%CLI%\" -JobScanDuplicates", bat);
        Assert.Contains("REM \"%CLI%\" -JobExportDuplicateReport", bat);
        Assert.DoesNotContain(Environment.NewLine + "\"%CLI%\" -JobScanDuplicates" + Environment.NewLine, bat);
        Assert.Contains("(可选) -JobScanDuplicates", export.ReadmeText);
        Assert.Contains("(可选) -JobExportDuplicateReport", export.ReadmeText);
    }

    [Fact]
    public void Export_comments_batch_merge_step_when_disabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableBatchMergeDuplicates = false,
            IncludeBatchMergeDuplicatesComment = true
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("REM \"%CLI%\" -JobBatchMergeDuplicates -Force", bat);
        Assert.DoesNotContain(Environment.NewLine + "\"%CLI%\" -JobBatchMergeDuplicates -Force" + Environment.NewLine, bat);
        Assert.Contains("(可选) -JobBatchMergeDuplicates -Force", export.ReadmeText);
    }

    [Fact]
    public void Export_includes_activity_log_trim_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogTrim = true,
            IncludeActivityLogTrimComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobTrimActivityLog", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobTrimActivityLog", bat);
        Assert.Contains("-JobExportOperationsReport", bat);

        var mergeIndex = bat.IndexOf("-JobBatchMergeDuplicates", StringComparison.Ordinal);
        var trimIndex = bat.IndexOf("-JobTrimActivityLog", StringComparison.Ordinal);
        Assert.True(mergeIndex >= 0 || bat.Contains("REM \"%CLI%\" -JobBatchMergeDuplicates"));
        Assert.True(trimIndex > mergeIndex || trimIndex > bat.IndexOf("REM \"%CLI%\" -JobBatchMergeDuplicates", StringComparison.Ordinal));
        Assert.Contains("-JobTrimActivityLog", export.ReadmeText);
    }

    [Fact]
    public void Export_includes_activity_log_stats_export_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogStatsExport = true,
            IncludeActivityLogStatsExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportActivityLogStatsCsv", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogStatsCsv", bat);
        Assert.Contains("-JobExportActivityLogStatsCsv", export.ReadmeText);

        var trimIndex = bat.IndexOf("-JobTrimActivityLog", StringComparison.Ordinal);
        var statsIndex = bat.IndexOf("-JobExportActivityLogStatsCsv", StringComparison.Ordinal);
        var previewIndex = bat.IndexOf("-JobTgPreview", StringComparison.Ordinal);
        Assert.True(trimIndex >= 0 || bat.Contains("REM \"%CLI%\" -JobTrimActivityLog"));
        Assert.True(statsIndex > trimIndex || statsIndex > bat.IndexOf("REM \"%CLI%\" -JobTrimActivityLog", StringComparison.Ordinal));
        Assert.True(previewIndex > statsIndex);
    }

    [Fact]
    public void Export_includes_activity_log_stats_html_export_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogStatsHtmlExport = true,
            IncludeActivityLogStatsHtmlExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportActivityLogStatsHtml", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogStatsHtml", bat);
        Assert.Contains("-JobExportActivityLogStatsHtml", export.ReadmeText);

        var csvIndex = bat.IndexOf("-JobExportActivityLogStatsCsv", StringComparison.Ordinal);
        var htmlIndex = bat.IndexOf("-JobExportActivityLogStatsHtml", StringComparison.Ordinal);
        var pinnedIndex = bat.IndexOf("-JobExportPinnedJobsCsv", StringComparison.Ordinal);
        Assert.True(csvIndex >= 0 || bat.Contains("REM \"%CLI%\" -JobExportActivityLogStatsCsv"));
        Assert.True(htmlIndex > csvIndex || htmlIndex > bat.IndexOf("REM \"%CLI%\" -JobExportActivityLogStatsCsv", StringComparison.Ordinal));
        Assert.True(pinnedIndex > htmlIndex);
    }

    [Fact]
    public void Export_comments_activity_log_stats_html_export_step_when_disabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogStatsHtmlExport = false,
            IncludeActivityLogStatsHtmlExportComment = true
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("REM \"%CLI%\" -JobExportActivityLogStatsHtml", bat);
        Assert.Contains("(可选) -JobExportActivityLogStatsHtml", export.ReadmeText);
    }

    [Fact]
    public void Export_includes_both_csv_and_html_when_both_export_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogStatsExport = true,
            IncludeActivityLogStatsExportComment = false,
            EnableActivityLogStatsHtmlExport = true,
            IncludeActivityLogStatsHtmlExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportActivityLogStatsCsv", bat);
        Assert.Contains("-JobExportActivityLogStatsHtml", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogStatsCsv", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogStatsHtml", bat);

        var csvIndex = bat.IndexOf("-JobExportActivityLogStatsCsv", StringComparison.Ordinal);
        var htmlIndex = bat.IndexOf("-JobExportActivityLogStatsHtml", StringComparison.Ordinal);
        Assert.True(csvIndex > 0);
        Assert.True(htmlIndex > csvIndex);
    }

    [Fact]
    public void Export_includes_batch_stats_json_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogBatchStatsAllExport = false,
            IncludeActivityLogBatchStatsAllExportComment = false,
            EnableActivityLogBatchStatsExport = true,
            IncludeActivityLogBatchStatsExportComment = false,
            EnableActivityLogBatchStatsCsvExport = false,
            IncludeActivityLogBatchStatsCsvExportComment = false,
            EnableActivityLogStatsHtmlExport = true,
            IncludeActivityLogStatsHtmlExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("\"%CLI%\" -JobExportActivityLogBatchStats", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogBatchStats", bat);

        var htmlIndex = bat.IndexOf("-JobExportActivityLogStatsHtml", StringComparison.Ordinal);
        var batchIndex = bat.IndexOf("-JobExportActivityLogBatchStats", StringComparison.Ordinal);
        Assert.True(htmlIndex > 0);
        Assert.True(batchIndex > htmlIndex);
    }

    [Fact]
    public void Export_includes_batch_stats_csv_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogBatchStatsAllExport = false,
            IncludeActivityLogBatchStatsAllExportComment = false,
            EnableActivityLogBatchStatsCsvExport = true,
            IncludeActivityLogBatchStatsCsvExportComment = false,
            EnableActivityLogBatchStatsExport = true,
            IncludeActivityLogBatchStatsExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportActivityLogBatchStatsCsv", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogBatchStatsCsv", bat);

        var jsonIndex = bat.IndexOf("-JobExportActivityLogBatchStats", StringComparison.Ordinal);
        var csvIndex = bat.IndexOf("-JobExportActivityLogBatchStatsCsv", StringComparison.Ordinal);
        Assert.True(jsonIndex > 0);
        Assert.True(csvIndex > jsonIndex);
    }

    [Fact]
    public void Export_appends_since_days_to_activity_log_exports_when_configured()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogStatsExport = true,
            IncludeActivityLogStatsExportComment = false,
            EnableActivityLogStatsHtmlExport = true,
            IncludeActivityLogStatsHtmlExportComment = false,
            EnableActivityLogBatchStatsAllExport = true,
            IncludeActivityLogBatchStatsAllExportComment = false,
            ActivityLogExportSinceDays = 30
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportActivityLogStatsCsv -SinceDays 30", bat);
        Assert.Contains("-JobExportActivityLogStatsHtml -SinceDays 30", bat);
        Assert.Contains("-JobExportActivityLogBatchStatsAll -SinceDays 30", bat);
        Assert.Contains("-JobExportActivityLogStatsCsv -SinceDays 30", export.ReadmeText);
        Assert.Contains("-JobExportActivityLogBatchStatsAll -SinceDays 30", export.ReadmeText);
        Assert.Contains("活动日志导出筛选: 最近 30 天", export.ReadmeText);
        Assert.Contains("-SinceDays 30", export.SummaryText);
        Assert.Contains("批量 JSON+CSV", export.SummaryText);
        Assert.Equal(30, export.ActivityLogExportSinceDays);
        Assert.True(export.EnableActivityLogBatchStatsAllExport);
    }

    [Fact]
    public void Export_includes_batch_stats_all_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogBatchStatsAllExport = true,
            IncludeActivityLogBatchStatsAllExportComment = false,
            EnableActivityLogStatsHtmlExport = true,
            IncludeActivityLogStatsHtmlExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("\"%CLI%\" -JobExportActivityLogBatchStatsAll", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportActivityLogBatchStatsAll", bat);
        Assert.DoesNotContain("-JobExportActivityLogBatchStatsCsv", bat);

        var htmlIndex = bat.IndexOf("-JobExportActivityLogStatsHtml", StringComparison.Ordinal);
        var allIndex = bat.IndexOf("-JobExportActivityLogBatchStatsAll", StringComparison.Ordinal);
        Assert.True(htmlIndex > 0);
        Assert.True(allIndex > htmlIndex);
    }

    [Fact]
    public void Export_includes_pinned_jobs_csv_export_step_when_enabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnablePinnedJobsCsvExport = true,
            IncludePinnedJobsCsvExportComment = false
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("-JobExportPinnedJobsCsv", bat);
        Assert.DoesNotContain("REM \"%CLI%\" -JobExportPinnedJobsCsv", bat);
        Assert.Contains("-JobExportPinnedJobsCsv", export.ReadmeText);

        var statsIndex = bat.IndexOf("-JobExportActivityLogStatsCsv", StringComparison.Ordinal);
        var pinnedIndex = bat.IndexOf("-JobExportPinnedJobsCsv", StringComparison.Ordinal);
        var previewIndex = bat.IndexOf("-JobTgPreview", StringComparison.Ordinal);
        Assert.True(statsIndex >= 0 || bat.Contains("REM \"%CLI%\" -JobExportActivityLogStatsCsv"));
        Assert.True(pinnedIndex > statsIndex || pinnedIndex > bat.IndexOf("REM \"%CLI%\" -JobExportActivityLogStatsCsv", StringComparison.Ordinal));
        Assert.True(previewIndex > pinnedIndex);
    }

    [Fact]
    public void Export_comments_pinned_jobs_csv_export_step_when_disabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnablePinnedJobsCsvExport = false,
            IncludePinnedJobsCsvExportComment = true
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("REM \"%CLI%\" -JobExportPinnedJobsCsv", bat);
        Assert.DoesNotContain(Environment.NewLine + "\"%CLI%\" -JobExportPinnedJobsCsv" + Environment.NewLine, bat);
        Assert.Contains("(可选) -JobExportPinnedJobsCsv", export.ReadmeText);
    }

    [Fact]
    public void Export_comments_activity_log_stats_export_step_when_disabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogStatsExport = false,
            IncludeActivityLogStatsExportComment = true
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("REM \"%CLI%\" -JobExportActivityLogStatsCsv", bat);
        Assert.DoesNotContain(Environment.NewLine + "\"%CLI%\" -JobExportActivityLogStatsCsv" + Environment.NewLine, bat);
        Assert.Contains("(可选) -JobExportActivityLogStatsCsv", export.ReadmeText);
    }

    [Fact]
    public void Export_comments_activity_log_trim_step_when_disabled()
    {
        var options = new NightlyAutomationOptions
        {
            EnableActivityLogTrim = false,
            IncludeActivityLogTrimComment = true
        };

        var export = NightlyAutomationScriptService.Export(_workspaceRoot, options);
        var bat = File.ReadAllText(export.BatPath);

        Assert.Contains("REM \"%CLI%\" -JobTrimActivityLog", bat);
        Assert.DoesNotContain(Environment.NewLine + "\"%CLI%\" -JobTrimActivityLog" + Environment.NewLine, bat);
        Assert.Contains("(可选) -JobTrimActivityLog", export.ReadmeText);
    }

    [Fact]
    public void ResolveDefaultCliExePath_points_to_dist_cli_layout()
    {
        var appRoot = Path.Combine(_workspaceRoot, "dist", "PLCPak.WinUI", "app");
        Directory.CreateDirectory(appRoot);

        var paths = new AppPaths(appRoot);
        var cliPath = NightlyAutomationScriptService.ResolveDefaultCliExePath(paths);

        Assert.EndsWith(Path.Combine("PLCPak.Cli", "app", "PLCPak.Cli.exe"), cliPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(cliPath.Contains("dist", StringComparison.OrdinalIgnoreCase));
    }
}