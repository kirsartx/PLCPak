namespace PLCPak.Core.Services;

public sealed class ExtractResult
{
    public bool Success { get; init; }
    public string? GameRoot { get; init; }
    public string? Error { get; init; }
    public bool PasswordRequired { get; init; }
    public List<string> ExtractedPaths { get; init; } = [];
}

public sealed class ExtractService
{
    private static readonly string[] GameRootMarkers =
    [
        ".exe", ".apk", ".xapk"
    ];

    private readonly SevenZipService _sevenZip;

    public ExtractService(SevenZipService sevenZip) => _sevenZip = sevenZip;

    public ExtractResult ExtractArchive(
        string archivePath,
        string outputDirectory,
        IEnumerable<string>? passwordCandidates = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var sevenZip = _sevenZip.FindSevenZip();
        if (sevenZip is null)
            return new ExtractResult { Success = false, Error = "未找到 7-Zip，无法解压" };

        if (!File.Exists(archivePath))
            return new ExtractResult { Success = false, Error = $"压缩包不存在: {archivePath}" };

        Directory.CreateDirectory(outputDirectory);
        log?.Invoke($"[解压] {archivePath} ({FormatSize(new FileInfo(archivePath).Length)})");
        log?.Invoke($"[解压] 输出目录: {outputDirectory}");

        var passwords = BuildPasswordList(passwordCandidates);
        ProcessRunResult? lastResult = null;
        string? lastError = null;
        var sawPasswordError = false;

        foreach (var password in passwords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearDirectory(outputDirectory);

            var pwdLabel = string.IsNullOrEmpty(password) ? "(无密码)" : "(尝试密码)";
            log?.Invoke($"[解压] 正在解压 {pwdLabel}...");

            var args = BuildExtractArgs(archivePath, outputDirectory, password);
            var result = _sevenZip.RunProcess(sevenZip, args, cancellationToken, log, extractMode: true);
            lastResult = result;

            if (result.ExitCode == 0 && HasExtractedContent(outputDirectory))
            {
                var gameRoot = DetectGameRoot(outputDirectory);
                log?.Invoke($"[解压] 完成，游戏根目录: {gameRoot ?? outputDirectory}");
                return new ExtractResult
                {
                    Success = true,
                    GameRoot = gameRoot ?? outputDirectory,
                    ExtractedPaths = [outputDirectory]
                };
            }

            lastError = SummarizeExtractFailure(result);
            if (lastError.Contains("password", StringComparison.OrdinalIgnoreCase)
                || lastError.Contains("密码", StringComparison.OrdinalIgnoreCase)
                || lastError.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
            {
                sawPasswordError = true;
            }
        }

        if (sawPasswordError)
        {
            return new ExtractResult
            {
                Success = false,
                PasswordRequired = true,
                Error = "压缩包需要密码。请点「匹配密码」、填写解压密码，或在 data/Password-Samples/password-manifest.json 添加规则后重试。"
            };
        }

        return new ExtractResult
        {
            Success = false,
            Error = lastError ?? $"7z 解压失败 (exit {lastResult?.ExitCode ?? -1})"
        };
    }

    public string? DetectGameRoot(string extractDirectory)
    {
        if (!Directory.Exists(extractDirectory))
            return null;

        var topDirs = Directory.EnumerateDirectories(extractDirectory).ToList();
        if (topDirs.Count == 1)
            return topDirs[0];

        var candidates = Directory.EnumerateDirectories(extractDirectory, "*", SearchOption.AllDirectories)
            .Select(d => new DirectoryInfo(d))
            .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
            .Select(d => new
            {
                Path = d.FullName,
                Score = ScoreDirectory(d.FullName)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        return candidates.FirstOrDefault(x => x.Score > 0)?.Path ?? topDirs.FirstOrDefault();
    }

    public static bool HasExtractedContent(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        return Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories).Any();
    }

    private static List<string> BuildPasswordList(IEnumerable<string>? passwordCandidates)
    {
        var list = new List<string> { string.Empty };
        if (passwordCandidates is null)
            return list;

        foreach (var pwd in passwordCandidates)
        {
            if (string.IsNullOrWhiteSpace(pwd))
                continue;
            if (!list.Contains(pwd))
                list.Add(pwd);
        }

        return list;
    }

    private static List<string> BuildExtractArgs(string archivePath, string outputDirectory, string? password)
    {
        var args = new List<string> { "x", archivePath, $"-o{outputDirectory}", "-y", "-bb1", "-bd" };
        args.Add(string.IsNullOrEmpty(password) ? "-p-" : $"-p{password}");
        return args;
    }

    private static string SummarizeExtractFailure(ProcessRunResult result)
    {
        var lines = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        foreach (var keyword in new[] { "ERROR", "Wrong password", "Data Error", "Can not open", "密码", "Encrypted" })
        {
            var hit = lines.LastOrDefault(l => l.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit;
        }

        return lines.LastOrDefault() ?? $"7z 解压失败 (exit {result.ExitCode})";
    }

    private static void ClearDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            Directory.Delete(dir, recursive: false);

        foreach (var dir in Directory.EnumerateDirectories(directory))
            Directory.Delete(dir, recursive: true);
    }

    private static int ScoreDirectory(string path)
    {
        var score = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (GameRootMarkers.Any(m => ext.Equals(m, StringComparison.OrdinalIgnoreCase)))
                    score += 100;

                score += 1;
            }
        }
        catch
        {
            return 0;
        }

        return score;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}