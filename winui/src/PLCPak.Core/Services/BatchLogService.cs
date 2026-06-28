using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchLogService
{
    public const int MaxPreviewLines = 5;

    public static string WriteBatchLog(
        string workspaceRoot,
        string operation,
        IEnumerable<string> messages)
    {
        var logsDir = Path.Combine(workspaceRoot, "logs");
        Directory.CreateDirectory(logsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(logsDir, $"batch-{timestamp}.txt");

        var builder = new StringBuilder();
        builder.AppendLine("# PLCPak Batch Log");
        builder.AppendLine($"Operation: {operation}");
        builder.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        foreach (var message in messages)
            builder.AppendLine(message);

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public static string WriteLog(string logsRoot, string operation, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(logsRoot);
        var path = Path.Combine(logsRoot, $"batch-{operation}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public static string FormatProgressText(BatchPipelineResult result, string? logPath = null)
    {
        var sb = new StringBuilder();
        sb.Append($"批量完成：成功 {result.Success}，跳过 {result.Skipped}，失败 {result.Failed}");

        if (result.Messages.Count > 0)
        {
            sb.AppendLine();
            foreach (var line in result.Messages.Take(MaxPreviewLines))
                sb.AppendLine(line);

            if (result.Messages.Count > MaxPreviewLines)
                sb.AppendLine(string.IsNullOrWhiteSpace(logPath) ? "详见日志" : $"详见日志: {logPath}");
        }

        return sb.ToString().TrimEnd();
    }
}