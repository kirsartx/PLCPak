using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class NightlyAutomationOptions
{
    public string CliExePath { get; set; } = @"..\PLCPak.Cli\app\PLCPak.Cli.exe";
    public string BatchChainFilter { get; set; } = "active";
    public int QueueCopyLimit { get; set; } = 50;
    public bool IncludeTgPreview { get; set; } = true;
    public bool IncludeTgBatchSendComment { get; set; } = true;
    public bool EnableTgBatchSend { get; set; }
    public int TgSendLimit { get; set; } = 50;
    public bool IncludeScanDuplicatesComment { get; set; } = true;
    public bool EnableScanDuplicates { get; set; }
    public bool IncludeBatchMergeDuplicatesComment { get; set; } = true;
    public bool EnableBatchMergeDuplicates { get; set; }
    public bool IncludeActivityLogTrimComment { get; set; } = true;
    public bool EnableActivityLogTrim { get; set; }
    public bool IncludeActivityLogStatsExportComment { get; set; } = true;
    public bool EnableActivityLogStatsExport { get; set; }
    public bool IncludeActivityLogStatsHtmlExportComment { get; set; } = true;
    public bool EnableActivityLogStatsHtmlExport { get; set; }
    public bool IncludeActivityLogBatchStatsAllExportComment { get; set; } = true;
    public bool EnableActivityLogBatchStatsAllExport { get; set; }
    public bool IncludeActivityLogBatchStatsExportComment { get; set; } = true;
    public bool EnableActivityLogBatchStatsExport { get; set; }
    public bool IncludeActivityLogBatchStatsCsvExportComment { get; set; } = true;
    public bool EnableActivityLogBatchStatsCsvExport { get; set; }
    public bool IncludePinnedJobsCsvExportComment { get; set; } = true;
    public bool EnablePinnedJobsCsvExport { get; set; }

    /// <summary>活动日志统计/批量统计导出时附加 -SinceDays（与工作室保留天数联动）。</summary>
    public int? ActivityLogExportSinceDays { get; set; }
}

public sealed class NightlyAutomationExport
{
    public string BatPath { get; set; } = string.Empty;
    public string ReadmeText { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public int? ActivityLogExportSinceDays { get; set; }
    public bool EnableActivityLogBatchStatsAllExport { get; set; }
}

public static class NightlyAutomationScriptService
{
    public static NightlyAutomationExport Export(string workspaceRoot, NightlyAutomationOptions options)
    {
        var filter = NormalizeFilter(options.BatchChainFilter);
        var limit = Math.Max(1, options.QueueCopyLimit);
        var scriptsDir = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var batPath = Path.Combine(scriptsDir, "nightly-full.bat");
        var bat = BuildBatContent(options);
        File.WriteAllText(batPath, bat, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var readme = new StringBuilder();
        readme.AppendLine("PLCPak 夜间全自动流水线");
        readme.AppendLine("========================");
        readme.AppendLine($"BAT: {batPath}");
        readme.AppendLine($"CLI: {options.CliExePath}");
        readme.AppendLine($"批量链筛选: {filter}");
        readme.AppendLine($"队列文案上限: {limit}");
        if (options.ActivityLogExportSinceDays is > 0)
            readme.AppendLine($"活动日志导出筛选: 最近 {options.ActivityLogExportSinceDays} 天（-SinceDays）");
        readme.AppendLine();
        readme.AppendLine("执行顺序:");
        var step = 1;
        readme.AppendLine($"  {step++}. -JobRunBatchChain -Filter {filter} -Force");
        readme.AppendLine($"  {step++}. -JobBatchQueueCopy -Limit {limit}");
        readme.AppendLine($"  {step++}. -JobDailyReport");
        readme.AppendLine($"  {step++}. -JobExportOperationsReport");
        if (options.EnableScanDuplicates)
        {
            readme.AppendLine($"  {step++}. -JobScanDuplicates");
            readme.AppendLine($"  {step++}. -JobExportDuplicateReport");
        }
        else if (options.IncludeScanDuplicatesComment)
        {
            readme.AppendLine($"  {step++}. (可选) -JobScanDuplicates");
            readme.AppendLine($"  {step++}. (可选) -JobExportDuplicateReport");
        }

        if (options.EnableBatchMergeDuplicates)
            readme.AppendLine($"  {step++}. -JobBatchMergeDuplicates -Force");
        else if (options.IncludeBatchMergeDuplicatesComment)
            readme.AppendLine($"  {step++}. (可选) -JobBatchMergeDuplicates -Force");
        if (options.EnableActivityLogTrim)
            readme.AppendLine($"  {step++}. -JobTrimActivityLog");
        else if (options.IncludeActivityLogTrimComment)
            readme.AppendLine($"  {step++}. (可选) -JobTrimActivityLog");
        if (options.EnableActivityLogStatsExport)
            readme.AppendLine($"  {step++}. {FormatReadmeStep("-JobExportActivityLogStatsCsv", options.ActivityLogExportSinceDays)}");
        else if (options.IncludeActivityLogStatsExportComment)
            readme.AppendLine($"  {step++}. (可选) {FormatReadmeStep("-JobExportActivityLogStatsCsv", options.ActivityLogExportSinceDays)}");
        if (options.EnableActivityLogStatsHtmlExport)
            readme.AppendLine($"  {step++}. {FormatReadmeStep("-JobExportActivityLogStatsHtml", options.ActivityLogExportSinceDays)}");
        else if (options.IncludeActivityLogStatsHtmlExportComment)
            readme.AppendLine($"  {step++}. (可选) {FormatReadmeStep("-JobExportActivityLogStatsHtml", options.ActivityLogExportSinceDays)}");
        if (options.EnableActivityLogBatchStatsAllExport)
            readme.AppendLine($"  {step++}. {FormatReadmeStep("-JobExportActivityLogBatchStatsAll", options.ActivityLogExportSinceDays)}");
        else if (options.IncludeActivityLogBatchStatsAllExportComment)
            readme.AppendLine($"  {step++}. (可选) {FormatReadmeStep("-JobExportActivityLogBatchStatsAll", options.ActivityLogExportSinceDays)}");
        if (!options.EnableActivityLogBatchStatsAllExport && options.EnableActivityLogBatchStatsExport)
            readme.AppendLine($"  {step++}. {FormatReadmeStep("-JobExportActivityLogBatchStats", options.ActivityLogExportSinceDays)}");
        else if (!options.EnableActivityLogBatchStatsAllExport && options.IncludeActivityLogBatchStatsExportComment)
            readme.AppendLine($"  {step++}. (可选) {FormatReadmeStep("-JobExportActivityLogBatchStats", options.ActivityLogExportSinceDays)}");
        if (!options.EnableActivityLogBatchStatsAllExport && options.EnableActivityLogBatchStatsCsvExport)
            readme.AppendLine($"  {step++}. {FormatReadmeStep("-JobExportActivityLogBatchStatsCsv", options.ActivityLogExportSinceDays)}");
        else if (!options.EnableActivityLogBatchStatsAllExport && options.IncludeActivityLogBatchStatsCsvExportComment)
            readme.AppendLine($"  {step++}. (可选) {FormatReadmeStep("-JobExportActivityLogBatchStatsCsv", options.ActivityLogExportSinceDays)}");
        if (options.EnablePinnedJobsCsvExport)
            readme.AppendLine($"  {step++}. -JobExportPinnedJobsCsv");
        else if (options.IncludePinnedJobsCsvExportComment)
            readme.AppendLine($"  {step++}. (可选) -JobExportPinnedJobsCsv");
        if (options.IncludeTgPreview)
            readme.AppendLine($"  {step++}. -JobTgPreview -Limit {Math.Max(1, options.TgSendLimit)}");

        if (options.EnableTgBatchSend)
            readme.AppendLine($"  {step++}. -JobBatchSendTg -Limit {Math.Max(1, options.TgSendLimit)} -Force");
        else if (options.IncludeTgBatchSendComment)
            readme.AppendLine($"  {step++}. (可选) -JobBatchSendTg -Limit {Math.Max(1, options.TgSendLimit)} -Force");
        readme.AppendLine();
        readme.AppendLine("运维报告 JSON 含 activity-log-stats / activity-log-batch 等分区（有活动日志时）。");
        readme.AppendLine();
        readme.AppendLine("手动执行: 双击 nightly-full.bat");
        readme.AppendLine("可配合 Windows 计划任务在夜间定时运行。");

        var readmeText = readme.ToString();
        var readmePath = Path.Combine(scriptsDir, "nightly-full-readme.txt");
        File.WriteAllText(readmePath, readmeText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var extras = new List<string>();
        if (options.ActivityLogExportSinceDays is > 0)
            extras.Add($"-SinceDays {options.ActivityLogExportSinceDays}");
        if (options.EnableActivityLogBatchStatsAllExport)
            extras.Add("批量 JSON+CSV");
        var extraHint = extras.Count > 0 ? $"，{string.Join("，", extras)}" : string.Empty;

        return new NightlyAutomationExport
        {
            BatPath = batPath,
            ReadmeText = readmeText,
            ActivityLogExportSinceDays = options.ActivityLogExportSinceDays,
            EnableActivityLogBatchStatsAllExport = options.EnableActivityLogBatchStatsAllExport,
            SummaryText = $"已导出夜间全自动脚本（筛选 {filter}，队列上限 {limit}{extraHint}）"
        };
    }

    public static string ResolveDefaultCliExePath(AppPaths paths)
        => ScheduleBatchScriptService.ResolveDefaultCliExePath(paths);

    private static string NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "active";

        return value.Trim().ToLowerInvariant() switch
        {
            "active" or "pending" or "进行中" or "待处理" => "active",
            "failed" or "失败" => "failed",
            "all" or "全部" => "all",
            _ => value.Trim()
        };
    }

    private static string BuildBatContent(NightlyAutomationOptions options)
    {
        var filter = NormalizeFilter(options.BatchChainFilter);
        var limit = Math.Max(1, options.QueueCopyLimit);
        var tgLimit = Math.Max(1, options.TgSendLimit);

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine("setlocal");
        sb.AppendLine($"set CLI={options.CliExePath}");
        sb.AppendLine("if not exist \"%CLI%\" (");
        sb.AppendLine("  echo 未找到 PLCPak.Cli.exe");
        sb.AppendLine("  exit /b 1");
        sb.AppendLine(")");
        sb.AppendLine($"\"%CLI%\" -JobRunBatchChain -Filter {filter} -Force");
        sb.AppendLine($"\"%CLI%\" -JobBatchQueueCopy -Limit {limit}");
        sb.AppendLine("\"%CLI%\" -JobDailyReport");
        sb.AppendLine("\"%CLI%\" -JobExportOperationsReport");
        if (options.EnableScanDuplicates)
        {
            sb.AppendLine("\"%CLI%\" -JobScanDuplicates");
            sb.AppendLine("\"%CLI%\" -JobExportDuplicateReport");
        }
        else if (options.IncludeScanDuplicatesComment)
        {
            sb.AppendLine("REM 取消下行注释以自动扫描重复任务");
            sb.AppendLine("REM \"%CLI%\" -JobScanDuplicates");
            sb.AppendLine("REM \"%CLI%\" -JobExportDuplicateReport");
        }

        if (options.EnableBatchMergeDuplicates)
            sb.AppendLine("\"%CLI%\" -JobBatchMergeDuplicates -Force");
        else if (options.IncludeBatchMergeDuplicatesComment)
        {
            sb.AppendLine("REM 取消下行注释以自动合并重复任务");
            sb.AppendLine("REM \"%CLI%\" -JobBatchMergeDuplicates -Force");
        }

        if (options.EnableActivityLogTrim)
            sb.AppendLine("\"%CLI%\" -JobTrimActivityLog");
        else if (options.IncludeActivityLogTrimComment)
        {
            sb.AppendLine("REM 取消下行注释以自动清理活动日志（按工作室配置保留天数）");
            sb.AppendLine("REM \"%CLI%\" -JobTrimActivityLog");
        }

        if (options.EnableActivityLogStatsExport)
            sb.AppendLine(FormatCliCommand("-JobExportActivityLogStatsCsv", options.ActivityLogExportSinceDays));
        else if (options.IncludeActivityLogStatsExportComment)
        {
            sb.AppendLine("REM 取消下行注释以自动导出活动日志统计 CSV");
            sb.AppendLine($"REM {FormatCliCommand("-JobExportActivityLogStatsCsv", options.ActivityLogExportSinceDays)}");
        }

        if (options.EnableActivityLogStatsHtmlExport)
            sb.AppendLine(FormatCliCommand("-JobExportActivityLogStatsHtml", options.ActivityLogExportSinceDays));
        else if (options.IncludeActivityLogStatsHtmlExportComment)
        {
            sb.AppendLine("REM 取消下行注释以自动导出活动日志统计 HTML（主题跟随 UI 偏好）");
            sb.AppendLine($"REM {FormatCliCommand("-JobExportActivityLogStatsHtml", options.ActivityLogExportSinceDays)}");
        }

        if (options.EnableActivityLogBatchStatsAllExport)
            sb.AppendLine(FormatCliCommand("-JobExportActivityLogBatchStatsAll", options.ActivityLogExportSinceDays));
        else if (options.IncludeActivityLogBatchStatsAllExportComment)
        {
            sb.AppendLine("REM 取消下行注释以自动导出批量操作统计 JSON+CSV");
            sb.AppendLine($"REM {FormatCliCommand("-JobExportActivityLogBatchStatsAll", options.ActivityLogExportSinceDays)}");
        }

        if (!options.EnableActivityLogBatchStatsAllExport && options.EnableActivityLogBatchStatsExport)
            sb.AppendLine(FormatCliCommand("-JobExportActivityLogBatchStats", options.ActivityLogExportSinceDays));
        else if (!options.EnableActivityLogBatchStatsAllExport && options.IncludeActivityLogBatchStatsExportComment)
        {
            sb.AppendLine("REM 取消下行注释以自动导出批量操作统计 JSON");
            sb.AppendLine($"REM {FormatCliCommand("-JobExportActivityLogBatchStats", options.ActivityLogExportSinceDays)}");
        }

        if (!options.EnableActivityLogBatchStatsAllExport && options.EnableActivityLogBatchStatsCsvExport)
            sb.AppendLine(FormatCliCommand("-JobExportActivityLogBatchStatsCsv", options.ActivityLogExportSinceDays));
        else if (!options.EnableActivityLogBatchStatsAllExport && options.IncludeActivityLogBatchStatsCsvExportComment)
        {
            sb.AppendLine("REM 取消下行注释以自动导出批量操作统计 CSV");
            sb.AppendLine($"REM {FormatCliCommand("-JobExportActivityLogBatchStatsCsv", options.ActivityLogExportSinceDays)}");
        }

        if (options.EnablePinnedJobsCsvExport)
            sb.AppendLine("\"%CLI%\" -JobExportPinnedJobsCsv");
        else if (options.IncludePinnedJobsCsvExportComment)
        {
            sb.AppendLine("REM 取消下行注释以自动导出置顶任务 CSV");
            sb.AppendLine("REM \"%CLI%\" -JobExportPinnedJobsCsv");
        }

        if (options.IncludeTgPreview)
            sb.AppendLine($"\"%CLI%\" -JobTgPreview -Limit {tgLimit}");
        if (options.EnableTgBatchSend)
            sb.AppendLine($"\"%CLI%\" -JobBatchSendTg -Limit {tgLimit} -Force");
        else if (options.IncludeTgBatchSendComment)
        {
            sb.AppendLine("REM 取消下行注释以自动批量发送 TG（需配置 Bot Token 与频道 ID）");
            sb.AppendLine($"REM \"%CLI%\" -JobBatchSendTg -Limit {tgLimit} -Force");
        }

        sb.AppendLine("exit /b %ERRORLEVEL%");
        return sb.ToString();
    }

    private static string FormatCliCommand(string jobFlag, int? sinceDays)
    {
        var sinceSuffix = sinceDays is > 0 ? $" -SinceDays {sinceDays.Value}" : string.Empty;
        return $"\"%CLI%\" {jobFlag}{sinceSuffix}";
    }

    private static string FormatReadmeStep(string jobFlag, int? sinceDays)
    {
        var sinceSuffix = sinceDays is > 0 ? $" -SinceDays {sinceDays.Value}" : string.Empty;
        return $"{jobFlag}{sinceSuffix}";
    }
}