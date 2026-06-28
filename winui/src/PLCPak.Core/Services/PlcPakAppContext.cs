using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class PlcPakAppContext
{
    private PlcPakAppContext(
        AppPaths paths,
        ConfigService config,
        AdCleanupService cleanup,
        PipelineService pipeline,
        SevenZipService sevenZip,
        CompressService compress,
        ManifestService manifest,
        PasswordManifestService passwordManifest,
        PasswordMatchService passwordMatch,
        PublishTemplateService publishTemplates,
        PipelineOrchestrator orchestrator,
        StudioConfigService studioConfig,
        WorkspaceService workspace,
        JobStore jobs,
        ExtractService extract,
        ThreadTitleService threadTitles,
        ThreadParseService threadParse,
        ForumDownloadService forumDownload,
        TelegramBotService telegramBot,
        UiPreferencesService uiPreferences,
        UiModeService uiMode,
        JobRunner? jobRunner,
        WorkspaceHealthService? workspaceHealth,
        VersionService? versionService)
    {
        Paths = paths;
        Config = config;
        Cleanup = cleanup;
        Pipeline = pipeline;
        SevenZip = sevenZip;
        Compress = compress;
        Manifest = manifest;
        PasswordManifest = passwordManifest;
        PasswordMatch = passwordMatch;
        PublishTemplates = publishTemplates;
        Orchestrator = orchestrator;
        StudioConfig = studioConfig;
        Workspace = workspace;
        Jobs = jobs;
        Extract = extract;
        ThreadTitles = threadTitles;
        ThreadParse = threadParse;
        ForumDownload = forumDownload;
        TelegramBot = telegramBot;
        UiPreferences = uiPreferences;
        UiMode = uiMode;
        JobRunner = jobRunner!;
        WorkspaceHealth = workspaceHealth!;
        Version = versionService!;
    }

    public AppPaths Paths { get; }
    public ConfigService Config { get; }
    public AdCleanupService Cleanup { get; }
    public PipelineService Pipeline { get; }
    public SevenZipService SevenZip { get; }
    public CompressService Compress { get; }
    public ManifestService Manifest { get; }
    public PasswordManifestService PasswordManifest { get; }
    public PasswordMatchService PasswordMatch { get; }
    public PublishTemplateService PublishTemplates { get; }
    public PipelineOrchestrator Orchestrator { get; }
    public StudioConfigService StudioConfig { get; }
    public WorkspaceService Workspace { get; }
    public JobStore Jobs { get; }
    public ExtractService Extract { get; }
    public ThreadTitleService ThreadTitles { get; }
    public ThreadParseService ThreadParse { get; }
    public ForumDownloadService ForumDownload { get; }
    public TelegramBotService TelegramBot { get; }
    public UiPreferencesService UiPreferences { get; }
    public UiModeService UiMode { get; }
    public JobRunner JobRunner { get; private set; }
    public WorkspaceHealthService WorkspaceHealth { get; }
    public VersionService Version { get; }

    public static PlcPakAppContext Create(string appRoot)
    {
        var paths = new AppPaths(appRoot);
        var config = new ConfigService(paths);
        var samples = new AdSampleRepository(paths);
        var scanCache = new ScanCacheService(paths);
        var cleanup = new AdCleanupService(samples, scanCache);
        var pipeline = new PipelineService(paths, cleanup);
        var sevenZip = new SevenZipService(paths);
        var compress = new CompressService(sevenZip);
        var manifest = new ManifestService(paths);
        var studioConfig = new StudioConfigService(paths);
        var passwordManifest = new PasswordManifestService(paths);
        var passwordMatch = new PasswordMatchService(passwordManifest, studioConfig, sevenZip);
        var publishTemplates = new PublishTemplateService(paths);
        var orchestrator = new PipelineOrchestrator(paths, config, cleanup, pipeline, sevenZip, compress);
        var workspace = new WorkspaceService(paths, studioConfig);
        var jobs = new JobStore(workspace);
        var extract = new ExtractService(sevenZip);
        var threadTitles = new ThreadTitleService();
        var threadParse = new ThreadParseService();
        var forumDownload = new ForumDownloadService();
        var telegramBot = new TelegramBotService();
        var uiPreferences = new UiPreferencesService(workspace);
        var uiMode = new UiModeService(uiPreferences);
        uiMode.LoadFromPreferences();
        var workspaceHealth = new WorkspaceHealthService(workspace, jobs);
        var versionService = new VersionService(paths);
        var context = new PlcPakAppContext(paths, config, cleanup, pipeline, sevenZip, compress, manifest, passwordManifest, passwordMatch, publishTemplates, orchestrator, studioConfig, workspace, jobs, extract, threadTitles, threadParse, forumDownload, telegramBot, uiPreferences, uiMode, null, workspaceHealth, versionService);
        context.JobRunner = new JobRunner(jobs, workspace, extract, context);
        return context;
    }

    public static PlcPakAppContext FromExecutableDirectory()
        => Create(AppContext.BaseDirectory);

    public string GetSizeSummaryText(IEnumerable<string> sourcePaths)
    {
        long pc = 0, apk = 0;
        foreach (var path in sourcePaths)
        {
            if (Directory.Exists(path))
            {
                var size = CompressService.GetFolderSize(path);
                pc += size;
            }
            else if (File.Exists(path))
            {
                var size = new FileInfo(path).Length;
                if (path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    apk += size;
                else
                    pc += size;
            }
        }

        if (apk > 0 && pc > 0)
            return $"总大小: PC: {CompressService.FormatFileSize(pc)}   安卓: {CompressService.FormatFileSize(apk)}";
        if (apk > 0)
            return $"总大小: 安卓: {CompressService.FormatFileSize(apk)}";
        if (pc > 0)
            return $"总大小: PC: {CompressService.FormatFileSize(pc)}";
        return "总大小: 0 B";
    }

    public (int Files, int Folders) GetAdSampleCounts()
    {
        var manifest = Manifest.Read();
        return (manifest.FileCount > 0 ? manifest.FileCount : manifest.Files.Count, manifest.Folders.Count);
    }

    public int GetPasswordEntryCount() => PasswordManifest.Read().Entries.Count;

    public async Task ExecuteOneClickAsync(
        GuiExecuteRequest request,
        Func<CleanupConfirmation, Task<bool>> confirmCleanupAsync,
        Action<string>? log,
        Action<int>? percent,
        CancellationToken cancellationToken = default)
    {
        var config = Config.Load();
        config.VolumeSizeMB = request.VolumeSizeMB;
        Config.Save(config);

        await Pipeline.WaitForBackgroundScansAsync(TimeSpan.FromSeconds(config.AdScanTimeoutSec), cancellationToken)
            .ConfigureAwait(false);

        foreach (var path in request.SourcePaths.Where(Directory.Exists))
        {
            if (!Pipeline.Tasks.ContainsKey(path))
            {
                var preview = Cleanup.Invoke(path, config, previewOnly: true, cancellationToken);
                Pipeline.SetTaskFromScan(path, preview);
            }
        }

        var confirmation = Pipeline.BuildCleanupConfirmation(config);
        if (confirmation.FolderCount > 0)
        {
            if (confirmation.RequiresConfirmation)
            {
                var approved = await confirmCleanupAsync(confirmation).ConfigureAwait(false);
                if (!approved)
                {
                    log?.Invoke("[取消] 用户未确认清理");
                    return;
                }
            }

            log?.Invoke("--- 广告清理 ---");
            Pipeline.CleanAll(config, cancellationToken);
            log?.Invoke("广告清理完成");
        }

        var sevenZipPath = SevenZip.FindSevenZip();
        if (sevenZipPath is null)
            throw new InvalidOperationException("未找到 7-Zip，无法压缩。");

        var folders = request.SourcePaths.Where(Directory.Exists).ToList();
        var files = request.SourcePaths.Where(File.Exists).ToList();
        var outputDir = !string.IsNullOrWhiteSpace(request.OutputDirectory)
            ? request.OutputDirectory
            : request.SourcePaths
                .Select(p => Path.GetDirectoryName(p))
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? Paths.AppRoot;
        Directory.CreateDirectory(outputDir);

        if (request.Enable7z && folders.Count > 0)
        {
            log?.Invoke("--- 7z-zstd 压缩（仅文件夹）---");
            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyReadmeIfNeeded(folder, request.SelfPurchase, log);
                var folderName = Path.GetFileName(folder);
                var outputName = string.IsNullOrWhiteSpace(request.SevenZOutputName)
                    || request.SevenZOutputName == "默认使用文件夹名称"
                    ? folderName
                    : request.SevenZOutputName.Trim();
                if (!outputName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                    outputName += ".7z";
                var outputPath = Path.Combine(outputDir, outputName);
                Compress.CompressWith7zZstd(folder, outputPath, sevenZipPath, config, request.VolumeSizeMB, true, log, percent, cancellationToken);
            }

            if (files.Count > 0)
                log?.Invoke($"注意: 7z-zstd 模式跳过了 {files.Count} 个单独文件");
        }

        if (request.EnableTarZst && SevenZip.TestTarZstdAvailable(sevenZipPath))
        {
            log?.Invoke("--- tar.zst 压缩（分离PC和安卓）---");
            var tarBase = string.IsNullOrWhiteSpace(request.TarZstOutputName)
                ? DateTime.Now.ToString("yyMMdd_01")
                : request.TarZstOutputName.Trim().Replace(".tar.zst", "", StringComparison.OrdinalIgnoreCase);
            var tarOutputFolder = Path.Combine(outputDir, tarBase);
            if (Directory.Exists(tarOutputFolder))
                Directory.Delete(tarOutputFolder, recursive: true);
            Directory.CreateDirectory(tarOutputFolder);

            var pcItems = request.SourcePaths
                .Where(p => Directory.Exists(p) || !p.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var apkItems = request.SourcePaths
                .Where(p => p.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var tempRoot = CompressService.GetTempRoot(config);
            var memoryGb = GetTotalMemoryGb();
            var pcSuccess = false;
            var azSuccess = false;

            if (pcItems.Count > 0)
            {
                log?.Invoke($"压缩PC部分（{pcItems.Count} 个项目）...");
                var tempPc = Path.Combine(tempRoot, $"plcpak_pc_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempPc);
                try
                {
                    foreach (var item in pcItems)
                    {
                        var dest = Path.Combine(tempPc, Path.GetFileName(item));
                        if (Directory.Exists(item))
                        {
                            CopyDirectory(item, dest);
                            CopyReadmeIfNeeded(dest, request.SelfPurchase, log);
                        }
                        else
                            File.Copy(item, dest, overwrite: true);
                        log?.Invoke($"  添加: {Path.GetFileName(item)}");
                    }

                    var pcTar = Path.Combine(tarOutputFolder, $"{tarBase}(PC).tar.zst");
                    pcSuccess = Compress.CompressWithTarZst(tempPc, pcTar, sevenZipPath, config, memoryGb, true, log, percent, cancellationToken);
                }
                finally
                {
                    if (Directory.Exists(tempPc))
                        Directory.Delete(tempPc, recursive: true);
                }
            }

            if (apkItems.Count > 0)
            {
                log?.Invoke($"压缩安卓部分（{apkItems.Count} 个APK）...");
                var tempAz = Path.Combine(tempRoot, $"plcpak_az_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempAz);
                try
                {
                    foreach (var item in apkItems)
                    {
                        File.Copy(item, Path.Combine(tempAz, Path.GetFileName(item)), overwrite: true);
                        log?.Invoke($"  添加: {Path.GetFileName(item)}");
                    }

                    var azTar = Path.Combine(tarOutputFolder, $"{tarBase}(AZ).tar.zst");
                    azSuccess = Compress.CompressWithTarZst(tempAz, azTar, sevenZipPath, config, memoryGb, true, log, percent, cancellationToken);
                }
                finally
                {
                    if (Directory.Exists(tempAz))
                        Directory.Delete(tempAz, recursive: true);
                }
            }

            if ((pcSuccess || azSuccess) && File.Exists(Paths.TransferGuardPath))
            {
                File.Copy(Paths.TransferGuardPath, Path.Combine(tarOutputFolder, "转存防炸.txt"), overwrite: true);
                log?.Invoke("已添加转存防炸.txt到输出文件夹");
            }

            log?.Invoke($"所有tar.zst文件已保存到: {tarBase}\\");
        }

        Config.SaveRecentProjects(request.SourcePaths);
        log?.Invoke("压缩任务完成!");
    }

    private void CopyReadmeIfNeeded(string folder, bool selfPurchase, Action<string>? log)
    {
        var source = selfPurchase ? Paths.ReadmeSelfPurchasePath : Paths.ReadmeStandardPath;
        if (!File.Exists(source))
        {
            log?.Invoke($"未找到 {Path.GetFileName(source)}, 跳过复制");
            return;
        }

        var target = Path.Combine(folder, "资源说明(必读).txt");
        if (File.Exists(target))
        {
            log?.Invoke("目标文件夹已存在资源说明文件,跳过复制");
            return;
        }

        File.Copy(source, target, overwrite: true);
        log?.Invoke($"已复制 {Path.GetFileName(source)} 到目标文件夹");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, destination));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static double GetTotalMemoryGb()
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return Math.Max(8, gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024));
        }
        catch
        {
            return 16;
        }
    }
}