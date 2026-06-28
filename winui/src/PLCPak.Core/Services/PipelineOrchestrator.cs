using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class PipelineOrchestrator
{
    private readonly AppPaths _paths;
    private readonly ConfigService _configService;
    private readonly AdCleanupService _cleanup;
    private readonly PipelineService _pipeline;
    private readonly SevenZipService _sevenZip;
    private readonly CompressService _compress;

    public PipelineOrchestrator(
        AppPaths paths,
        ConfigService configService,
        AdCleanupService cleanup,
        PipelineService pipeline,
        SevenZipService sevenZip,
        CompressService compress)
    {
        _paths = paths;
        _configService = configService;
        _cleanup = cleanup;
        _pipeline = pipeline;
        _sevenZip = sevenZip;
        _compress = compress;
    }

    public static PipelineOrchestrator Create(string appRoot)
    {
        var paths = new AppPaths(appRoot);
        var configService = new ConfigService(paths);
        var samples = new AdSampleRepository(paths);
        var scanCache = new ScanCacheService(paths);
        var cleanup = new AdCleanupService(samples, scanCache);
        var pipeline = new PipelineService(paths, cleanup);
        var sevenZip = new SevenZipService(paths);
        var compress = new CompressService(sevenZip);

        return new PipelineOrchestrator(
            paths, configService, cleanup, pipeline, sevenZip, compress);
    }

    public Task<int> RunCliAsync(
        IEnumerable<string> paths,
        PipelineCliOptions? options = null,
        CompressConfig? config = null,
        Func<string, bool>? confirmDelete = null,
        Action<string>? log = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
        => RunCliAsync(paths, results: null, options, config, confirmDelete, log, percent, cancellationToken);

    public async Task<int> RunCliAsync(
        IEnumerable<string> paths,
        IList<CliPathResult>? results,
        PipelineCliOptions? options = null,
        CompressConfig? config = null,
        Func<string, bool>? confirmDelete = null,
        Action<string>? log = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PipelineCliOptions();
        config ??= _configService.Load();

        var sevenZipPath = _sevenZip.FindSevenZip();
        var exitCode = 0;

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = new CliPathResult { Path = path };

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                pathResult.Exists = false;
                pathResult.Error = "路径不存在";
                results?.Add(pathResult);
                log?.Invoke($"[错误] 不存在: {path}");
                exitCode = 1;
                continue;
            }

            pathResult.Exists = true;
            _pipeline.NewTask(path);
            AdCleanupResult? previewResult = null;

            if (options.Preview)
            {
                previewResult = _cleanup.Invoke(path, config, previewOnly: true, cancellationToken);
                _pipeline.SetTaskFromScan(path, previewResult);
                pathResult.Scanned = previewResult.TotalScanned;
                pathResult.Matched = previewResult.TotalMatched;
                pathResult.MatchedFiles = previewResult.MatchedFiles.ToList();
                log?.Invoke($"[{path}] 扫描={previewResult.TotalScanned} 匹配={previewResult.TotalMatched}");
                foreach (var matched in previewResult.MatchedFiles)
                    log?.Invoke($"  [将删] {matched}");
            }

            if (options.Clean && !options.Preview)
            {
                if (previewResult is null)
                {
                    previewResult = _cleanup.Invoke(path, config, previewOnly: true, cancellationToken);
                    _pipeline.SetTaskFromScan(path, previewResult);
                }

                if (previewResult.TotalMatched >= config.AdConfirmThreshold && !options.Force)
                {
                    var confirmed = confirmDelete?.Invoke(
                        $"匹配 {previewResult.TotalMatched} 项，输入 Y 确认删除") ?? false;

                    if (!confirmed)
                    {
                        log?.Invoke($"[{path}] 已跳过删除");
                        results?.Add(pathResult);
                        continue;
                    }
                }

                var cleanResult = _cleanup.Invoke(path, config, previewOnly: false, cancellationToken);
                _pipeline.WriteSessionLog(path, cleanResult);
                pathResult.Removed = cleanResult.TotalRemoved;
                log?.Invoke($"[{path}] 已删={cleanResult.TotalRemoved}");
            }

            if (options.Compress && Directory.Exists(path) && !string.IsNullOrEmpty(sevenZipPath))
            {
                var name = Path.GetFileName(path);
                var outDir = Path.GetDirectoryName(path)!;
                var format = options.Format ?? "7z";

                if (format.Contains("7z", StringComparison.OrdinalIgnoreCase))
                {
                    var output = Path.Combine(outDir, $"{name}.7z");
                    var args = new List<string>
                    {
                        "a", "-t7z", "-m0=zstd", "-mx=11", "-mmt=4", output, path
                    };

                    if (config.SkipStoreCompress)
                        args.Insert(args.Count - 2, "-ms=on");

                    var result = await Task.Run(
                        () => _sevenZip.RunWithProgress(sevenZipPath, args, log, percent, cancellationToken),
                        cancellationToken);

                    var ok = result.ExitCode == 0
                        && _sevenZip.TestArchiveIntegrity(output, sevenZipPath);

                    pathResult.Compressed = true;
                    pathResult.CompressOutput = output;
                    pathResult.CompressOk = ok;
                    log?.Invoke($"[{path}] 7z => {output} {(ok ? "[校验OK]" : "[校验失败]")}");
                    if (!ok)
                        exitCode = 1;
                }
            }

            results?.Add(pathResult);
        }

        return exitCode;
    }
}