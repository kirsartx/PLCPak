using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PLCPak.Core.Services;

public sealed class SevenZipService
{
    private static readonly Regex PercentRegex = new(@"(\d+)%", RegexOptions.Compiled);

    private readonly AppPaths _paths;

    public SevenZipService(AppPaths paths) => _paths = paths;

    public string? FindSevenZip()
    {
        if (File.Exists(_paths.SevenZipPath))
            return _paths.SevenZipPath;

        var systemPaths = new[]
        {
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe")
        };

        foreach (var path in systemPaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public bool TestTarZstdAvailable(string? sevenZipPath = null)
    {
        sevenZipPath ??= FindSevenZip();
        if (string.IsNullOrEmpty(sevenZipPath) || !File.Exists(sevenZipPath))
            return false;

        try
        {
            var output = RunProcess(sevenZipPath, ["i"], CancellationToken.None).Output;
            return output.Contains("tar", StringComparison.OrdinalIgnoreCase)
                && output.Contains("zstd", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool TestArchiveIntegrity(string archivePath, string? sevenZipPath = null)
    {
        sevenZipPath ??= FindSevenZip();
        if (string.IsNullOrEmpty(sevenZipPath) || !File.Exists(archivePath))
            return false;

        var result = RunProcess(sevenZipPath, ["t", archivePath], CancellationToken.None);
        return result.ExitCode == 0;
    }

    public ProcessRunResult RunWithProgress(
        string sevenZipPath,
        IReadOnlyList<string> arguments,
        Action<string>? log = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        return RunProcess(sevenZipPath, arguments, cancellationToken, log, percent);
    }

    public ProcessRunResult RunProcess(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        Action<string>? log = null,
        Action<int>? percent = null,
        bool extractMode = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;

            output.AppendLine(e.Data);
            HandleLine(e.Data, log, percent, extractMode);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;

            output.AppendLine(e.Data);
            HandleLine(e.Data, log, percent, extractMode);
        };

        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }

            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessRunResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString()
        };
    }

    private static void HandleLine(string line, Action<string>? log, Action<int>? percent, bool extractMode)
    {
        var match = PercentRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var p))
            percent?.Invoke(p);

        if (!extractMode)
        {
            if (line.Contains("Everything is Ok", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Error", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke(line);
            }

            return;
        }

        if (line.Contains("Everything is Ok", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Wrong password", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Can not open", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Data Error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Encrypted", StringComparison.OrdinalIgnoreCase)
            || line.Contains("password", StringComparison.OrdinalIgnoreCase)
            || line.Contains('%'))
        {
            log?.Invoke(line);
        }
    }
}

public sealed class ProcessRunResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}