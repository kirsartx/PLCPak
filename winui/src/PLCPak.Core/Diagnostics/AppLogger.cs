namespace PLCPak.Core.Diagnostics;

public static class AppLogger
{
    public static void Write(string logPath, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}\r\n");
        }
        catch
        {
            // ignore logging failures
        }
    }

    public static void WriteError(string logPath, Exception ex, string context)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(context);
        sb.Append(": ");
        sb.Append(ex.GetType().FullName);
        sb.Append(": ");
        sb.AppendLine(ex.Message);
        sb.Append("HResult=0x");
        sb.Append(ex.HResult.ToString("X8"));
        if (ex.StackTrace is { Length: > 0 } stack)
        {
            sb.AppendLine();
            sb.Append(stack);
        }

        if (ex.InnerException is not null)
        {
            sb.AppendLine("--- Inner ---");
            sb.Append(ex.InnerException);
        }

        Write(logPath, sb.ToString());
    }
}