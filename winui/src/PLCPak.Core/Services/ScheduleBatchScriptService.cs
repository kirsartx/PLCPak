using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ScheduleBatchOptions
{
    public string CliExePath { get; set; } = @"..\PLCPak.Cli\app\PLCPak.Cli.exe";
    public string Filter { get; set; } = "active";
    public int Hour { get; set; } = 2;
    public int Minute { get; set; }
    public string TaskName { get; set; } = "PLCPak Nightly Batch";
}

public sealed class ScheduleBatchExport
{
    public string BatPath { get; set; } = string.Empty;
    public string SchTasksCommand { get; set; } = string.Empty;
    public string ReadmeText { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public ScheduleTaskRegisterResult? Registration { get; set; }
}

public sealed class ScheduleTaskRegisterResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SchTasksCommand { get; set; } = string.Empty;
}

public sealed class ScheduleRegisterResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ScheduleBatchExport? Export { get; set; }
    public string? CommandOutput { get; set; }
}

public sealed class ScheduleBatchRegisterResult
{
    public bool Registered { get; set; }
    public int ExitCode { get; set; }
    public string BatPath { get; set; } = string.Empty;
    public string SchTasksCommand { get; set; } = string.Empty;
    public string ReadmeText { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public string? RegisterOutput { get; set; }
    public string? RegisterError { get; set; }
}

public static class ScheduleBatchScriptService
{
    public static ScheduleBatchExport Export(string workspaceRoot, ScheduleBatchOptions options)
    {
        var filter = NormalizeFilter(options.Filter);
        var hour = Clamp(options.Hour, 0, 23);
        var minute = Clamp(options.Minute, 0, 59);
        var scriptsDir = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var batPath = Path.Combine(scriptsDir, "nightly-batch.bat");
        var bat = BuildBatContent(options.CliExePath, filter);
        File.WriteAllText(batPath, bat, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var schTasksCommand =
            $"schtasks /Create /TN \"{options.TaskName}\" /TR \"{batPath}\" /SC DAILY /ST {hour:D2}:{minute:D2}";

        var readme = new StringBuilder();
        readme.AppendLine("PLCPak 定时批量流水线");
        readme.AppendLine("====================");
        readme.AppendLine($"BAT: {batPath}");
        readme.AppendLine($"CLI: {options.CliExePath}");
        readme.AppendLine($"筛选: {filter}");
        readme.AppendLine();
        readme.AppendLine("手动执行: 双击 nightly-batch.bat");
        readme.AppendLine("注册计划任务（管理员 CMD）:");
        readme.AppendLine(schTasksCommand);

        var readmeText = readme.ToString();
        var readmePath = Path.Combine(scriptsDir, "nightly-batch-readme.txt");
        File.WriteAllText(readmePath, readmeText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new ScheduleBatchExport
        {
            BatPath = batPath,
            SchTasksCommand = schTasksCommand,
            ReadmeText = readmeText,
            SummaryText = $"已导出定时批量脚本（{hour:D2}:{minute:D2}，筛选 {filter}）"
        };
    }

    public static string ResolveDefaultCliExePath(AppPaths paths)
        => Path.GetFullPath(Path.Combine(paths.PackageRoot, "PLCPak.Cli", "app", "PLCPak.Cli.exe"));

    public static ScheduleRegisterResult TryRegisterTask(string schTasksCommand)
    {
        if (string.IsNullOrWhiteSpace(schTasksCommand))
        {
            return new ScheduleRegisterResult
            {
                Success = false,
                Message = "计划任务命令为空"
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {schTasksCommand} /F",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 schtasks 进程");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var output = string.Join(Environment.NewLine, new[] { stdout, stderr }
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim()));

            if (process.ExitCode == 0)
            {
                return new ScheduleRegisterResult
                {
                    Success = true,
                    Message = string.IsNullOrWhiteSpace(output) ? "计划任务已注册" : output,
                    CommandOutput = output
                };
            }

            var message = string.IsNullOrWhiteSpace(output)
                ? $"计划任务注册失败（退出码 {process.ExitCode}）"
                : output;
            if (RequiresAdministratorHint(message))
                message += "。请以管理员身份运行。";

            return new ScheduleRegisterResult
            {
                Success = false,
                Message = message,
                CommandOutput = output
            };
        }
        catch (Exception ex)
        {
            return new ScheduleRegisterResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public static ScheduleTaskRegisterResult TryRegisterScheduledTask(ScheduleBatchExport export, string taskName)
    {
        var scheduleTime = ExtractScheduleTime(export.SchTasksCommand);
        var schTasksCommand =
            $"schtasks /Create /TN \"{taskName}\" /TR \"{export.BatPath}\" /SC DAILY /ST {scheduleTime} /F";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("/Create");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(taskName);
            psi.ArgumentList.Add("/TR");
            psi.ArgumentList.Add(export.BatPath);
            psi.ArgumentList.Add("/SC");
            psi.ArgumentList.Add("DAILY");
            psi.ArgumentList.Add("/ST");
            psi.ArgumentList.Add(scheduleTime);
            psi.ArgumentList.Add("/F");

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 schtasks 进程");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var output = string.Join(Environment.NewLine, new[] { stdout, stderr }
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text.Trim()));

            if (process.ExitCode == 0)
            {
                return new ScheduleTaskRegisterResult
                {
                    Success = true,
                    Message = string.IsNullOrWhiteSpace(output) ? "计划任务已注册" : output,
                    SchTasksCommand = schTasksCommand
                };
            }

            var message = string.IsNullOrWhiteSpace(output) ? "注册计划任务失败" : output;
            if (RequiresAdministratorHint(message))
                message += "。请以管理员身份运行。";

            return new ScheduleTaskRegisterResult
            {
                Success = false,
                Message = message,
                SchTasksCommand = schTasksCommand
            };
        }
        catch (Exception ex)
        {
            return new ScheduleTaskRegisterResult
            {
                Success = false,
                Message = $"{ex.Message}。请以管理员身份运行。",
                SchTasksCommand = schTasksCommand
            };
        }
    }

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

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    private static string ExtractScheduleTime(string schTasksCommand)
    {
        var match = Regex.Match(schTasksCommand, @"/ST\s+(\d{2}:\d{2})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "02:00";
    }

    private static bool RequiresAdministratorHint(string message)
        => message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || message.Contains("denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("拒绝", StringComparison.OrdinalIgnoreCase)
            || message.Contains("权限", StringComparison.OrdinalIgnoreCase);

    private static string BuildBatContent(string cliExe, string filter)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine("setlocal");
        sb.AppendLine($"set CLI={cliExe}");
        sb.AppendLine("if not exist \"%CLI%\" (");
        sb.AppendLine("  echo 未找到 PLCPak.Cli.exe");
        sb.AppendLine("  exit /b 1");
        sb.AppendLine(")");
        sb.AppendLine($"\"%CLI%\" -JobBatchPipeline -Filter {filter} -Force -NoGui");
        sb.AppendLine("exit /b %ERRORLEVEL%");
        return sb.ToString();
    }
}