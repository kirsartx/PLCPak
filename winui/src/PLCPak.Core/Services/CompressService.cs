using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class CompressService
{
    private readonly SevenZipService _sevenZip;

    public CompressService(SevenZipService sevenZip) => _sevenZip = sevenZip;

    public static string FormatFileSize(long size)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        if (size > gb)
            return $"{size / (double)gb:N2}GB";
        if (size > mb)
            return $"{size / (double)mb:N2}MB";
        if (size > kb)
            return $"{size / (double)kb:N2}KB";
        return $"{size}B";
    }

    public static long GetFolderSize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f).Length)
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    public static string GetTempRoot(CompressConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.TempDir) && Directory.Exists(config.TempDir))
            return config.TempDir;
        return Path.GetTempPath();
    }

    public bool CompressWith7zZstd(
        string sourcePath,
        string outputName,
        string sevenZipPath,
        CompressConfig config,
        int volumeSizeMb = 1900,
        bool overwriteExisting = true,
        Action<string>? log = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        log?.Invoke("");
        log?.Invoke("开始使用 7z-zstd 压缩...");
        log?.Invoke($"源文件夹: {sourcePath}");
        log?.Invoke($"输出文件: {outputName}");

        var volumeSize = $"{volumeSizeMb}m";
        var folderSize = GetFolderSize(sourcePath);
        var needSplit = folderSize > volumeSizeMb * 1024L * 1024L;

        if (needSplit)
            log?.Invoke($"文件夹较大,将启用分卷压缩 (每卷 {volumeSizeMb}MB)");

        try
        {
            if (File.Exists(outputName))
            {
                if (!overwriteExisting)
                {
                    log?.Invoke("跳过 7z-zstd 压缩");
                    return false;
                }

                log?.Invoke("删除旧文件...");
                TryDeleteOutputArtifacts(outputName);
            }

            var outputDir = Path.GetDirectoryName(outputName)!;
            var drive = Path.GetPathRoot(outputDir);
            if (!string.IsNullOrEmpty(drive))
            {
                try
                {
                    var driveInfo = new DriveInfo(drive);
                    var freeSpaceGb = Math.Round(driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024), 2);
                    var needSpaceGb = Math.Round(folderSize / (1024.0 * 1024 * 1024), 2);
                    log?.Invoke($"磁盘可用空间: {freeSpaceGb}GB, 预计需要: {needSpaceGb}GB");

                    if (driveInfo.AvailableFreeSpace < folderSize * 0.6)
                        log?.Invoke("警告: 磁盘空间可能不足!");
                }
                catch
                {
                    log?.Invoke("警告: 无法检查磁盘空间");
                }
            }

            var folderSizeGb = folderSize / (1024.0 * 1024 * 1024);
            var compressionLevel = 11;
            var memLimit = "2g";
            var threads = 4;

            if (folderSizeGb > 20)
            {
                compressionLevel = 5;
                memLimit = "1g";
                threads = 2;
                log?.Invoke("检测到超大文件夹 (>20GB)，使用优化参数：压缩级别=5, 内存限制=1GB, 线程数=2");
            }
            else if (folderSizeGb > 10)
            {
                compressionLevel = 7;
                memLimit = "1536m";
                threads = 3;
                log?.Invoke("检测到大文件夹 (>10GB)，使用优化参数：压缩级别=7, 内存限制=1.5GB, 线程数=3");
            }
            else
            {
                log?.Invoke("使用标准压缩参数：压缩级别=11, 内存限制=2GB, 线程数=4");
            }

            if (config.CompressThreads > 0)
            {
                threads = config.CompressThreads;
                log?.Invoke($"配置覆盖线程数: {threads}");
            }

            var compressionParams = new List<string>
            {
                "a", "-t7z", "-m0=zstd", $"-mx={compressionLevel}", $"-mmt={threads}", $"-md={memLimit}"
            };

            if (needSplit)
                compressionParams.Add($"-v{volumeSize}");

            if (config.SkipStoreCompress)
            {
                compressionParams.Add("-ms=on");
                log?.Invoke("已启用存储模式(-ms=on)，跳过已压缩文件二次压缩");
            }

            compressionParams.Add(outputName);
            compressionParams.Add(sourcePath);

            log?.Invoke("");
            log?.Invoke("执行压缩命令...");
            log?.Invoke("这可能需要较长时间，请耐心等待...");

            percent?.Invoke(0);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = _sevenZip.RunWithProgress(
                sevenZipPath,
                compressionParams,
                log,
                percent,
                cancellationToken);
            sw.Stop();

            percent?.Invoke(100);
            log?.Invoke($"压缩耗时: {sw.Elapsed:hh\\:mm\\:ss}");

            if (result.ExitCode == 0)
            {
                log?.Invoke("");
                log?.Invoke("[OK] 7z-zstd 压缩完成!");

                var outputFile = Path.GetFileName(outputName);
                var files = Directory.GetFiles(outputDir, $"{outputFile}*")
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 1 && files[0].Name.EndsWith(".001", StringComparison.OrdinalIgnoreCase))
                {
                    var file001 = files[0];
                    if (file001.Length < 1900L * 1024 * 1024)
                    {
                        var newName = file001.Name[..^4];
                        try
                        {
                            File.Move(file001.FullName, Path.Combine(outputDir, newName), overwrite: true);
                            log?.Invoke($"检测到单个小文件,已重命名: {file001.Name} -> {newName}");
                            files = Directory.GetFiles(outputDir, $"{outputFile}*")
                                .Select(f => new FileInfo(f))
                                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                                .ToList();
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke($"警告: 无法重命名文件: {ex.Message}");
                        }
                    }
                }

                log?.Invoke("");
                log?.Invoke("生成的文件:");
                foreach (var file in files)
                    log?.Invoke($"  - {file.Name} ({FormatFileSize(file.Length)})");

                var mainArchive = files.FirstOrDefault(f =>
                    string.Equals(f.Name, outputFile, StringComparison.OrdinalIgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(f.Name, @"\.7z(\.\d+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    ?? files.FirstOrDefault();

                if (mainArchive is not null)
                {
                    var ok = _sevenZip.TestArchiveIntegrity(mainArchive.FullName, sevenZipPath);
                    log?.Invoke($"归档校验: {(ok ? "[OK]" : "[失败]")}");
                    return ok;
                }

                return true;
            }

            log?.Invoke("");
            log?.Invoke($"[X] 7z-zstd 压缩失败! 退出代码: {result.ExitCode}");
            AppendExitCodeDiagnostics(result.ExitCode, log);
            return false;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[X] 7z-zstd 压缩出错: {ex.Message}");
            return false;
        }
    }

    public bool CompressWithTarZst(
        string sourcePath,
        string outputName,
        string sevenZipPath,
        CompressConfig config,
        double totalMemoryGb = 16,
        bool overwriteExisting = true,
        Action<string>? log = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        log?.Invoke("");
        log?.Invoke("开始使用 tar.zst 压缩...");
        log?.Invoke($"源文件夹: {sourcePath}");
        log?.Invoke($"输出文件: {outputName}");

        if (!outputName.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
            outputName = $"{outputName}.tar.zst";

        try
        {
            if (File.Exists(outputName))
            {
                if (!overwriteExisting)
                {
                    log?.Invoke("跳过 tar.zst 压缩");
                    return false;
                }

                log?.Invoke("删除旧文件...");
                File.Delete(outputName);
            }

            var tempRoot = GetTempRoot(config);
            var tempTarFile = Path.Combine(tempRoot, $"plcpak_{Guid.NewGuid():N}.tar");

            log?.Invoke("正在创建 tar 归档...");

            string[] tarArgs = totalMemoryGb < 16
                ? ["a", "-ttar", "-mmt=1", tempTarFile, sourcePath]
                : ["a", "-ttar", "-mmt=off", tempTarFile, sourcePath];

            if (totalMemoryGb < 16)
                log?.Invoke("使用低内存 tar 模式 (单线程)...");

            percent?.Invoke(0);
            var tarResult = _sevenZip.RunWithProgress(sevenZipPath, tarArgs, log, percent, cancellationToken);
            log?.Invoke($"tar 创建退出代码: {tarResult.ExitCode}");

            if (!File.Exists(tempTarFile))
            {
                log?.Invoke("错误: tar 文件未创建");
                return false;
            }

            var tarInfo = new FileInfo(tempTarFile);
            log?.Invoke($"tar 文件已创建: {FormatFileSize(tarInfo.Length)} ({Math.Round(tarInfo.Length / (1024.0 * 1024), 2)} MB)");

            var sourceSizeBytes = GetFolderSize(sourcePath);
            if (tarInfo.Length < 102400 && sourceSizeBytes > 1024 * 1024)
            {
                log?.Invoke($"错误: tar 文件大小异常 ({tarInfo.Length} 字节)");
                log?.Invoke($"源文件夹大小: {FormatFileSize(sourceSizeBytes)}");
                log?.Invoke("可能原因: 内存不足或进程崩溃");
                TryDelete(tempTarFile);
                return false;
            }

            if (tarResult.ExitCode != 0)
            {
                TryDelete(tempTarFile);
                log?.Invoke($"创建 tar 归档失败! 退出代码: {tarResult.ExitCode}");
                return false;
            }

            log?.Invoke("正在使用 zstd 压缩...");
            log?.Invoke($"系统内存: 总计 {totalMemoryGb} GB");

            var tarSizeGb = tarInfo.Length / (1024.0 * 1024 * 1024);
            log?.Invoke($"tar 文件大小: {Math.Round(tarSizeGb, 2)} GB");

            var zstdArgs = BuildZstdArgs(totalMemoryGb, tarSizeGb, outputName, tempTarFile, log);

            percent?.Invoke(0);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var zstdResult = _sevenZip.RunWithProgress(sevenZipPath, zstdArgs, log, percent, cancellationToken);
            sw.Stop();

            percent?.Invoke(100);
            log?.Invoke($"zstd 压缩退出代码: {zstdResult.ExitCode}");
            log?.Invoke($"压缩耗时: {sw.Elapsed:hh\\:mm\\:ss}");

            if (!File.Exists(outputName))
            {
                log?.Invoke("错误: zst 文件未创建");
                TryDelete(tempTarFile);
                return false;
            }

            var zstInfo = new FileInfo(outputName);
            log?.Invoke($"zst 文件已创建: {FormatFileSize(zstInfo.Length)}");

            if (zstInfo.Length < 1024)
            {
                log?.Invoke($"错误: zst 文件大小异常 ({zstInfo.Length} 字节)");
                TryDelete(outputName);
                TryDelete(tempTarFile);
                return false;
            }

            if (File.Exists(tempTarFile))
            {
                log?.Invoke("警告: 临时 tar 文件未被自动删除，手动清理中...");
                TryDelete(tempTarFile);
            }

            if (zstdResult.ExitCode == 0)
            {
                log?.Invoke("");
                log?.Invoke("[OK] tar.zst 压缩完成!");
                log?.Invoke("");
                log?.Invoke("生成的文件:");
                log?.Invoke($"  - {zstInfo.Name} ({FormatFileSize(zstInfo.Length)})");
                return true;
            }

            log?.Invoke($"[X] tar.zst 压缩失败! 退出代码: {zstdResult.ExitCode}");
            if (zstInfo.Length < 1024)
            {
                log?.Invoke($"删除异常文件: {zstInfo.Name} ({zstInfo.Length} 字节)");
                TryDelete(outputName);
            }

            return false;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[X] tar.zst 压缩出错: {ex.Message}");
            return false;
        }
    }

    private static string[] BuildZstdArgs(
        double totalMemoryGb,
        double tarSizeGb,
        string outputFullPath,
        string tempTarFile,
        Action<string>? log)
    {
        string[] zstdArgs;

        if (totalMemoryGb >= 64)
        {
            log?.Invoke($"检测到旗舰级内存 ({totalMemoryGb} GB)，使用极速模式...");
            zstdArgs = tarSizeGb switch
            {
                > 20 => ["a", "-tzstd", "-mx=5", "-mmt=2", "-md=128m", "-sdel", outputFullPath, tempTarFile],
                > 10 => ["a", "-tzstd", "-mx=7", "-mmt=3", "-md=256m", "-sdel", outputFullPath, tempTarFile],
                > 4 => ["a", "-tzstd", "-mx=11", "-mmt=6", "-md=512m", "-sdel", outputFullPath, tempTarFile],
                _ => ["a", "-tzstd", "-mx=11", "-mmt=8", "-md=512m", "-sdel", outputFullPath, tempTarFile]
            };
        }
        else if (totalMemoryGb >= 32)
        {
            log?.Invoke($"检测到高性能内存 ({totalMemoryGb} GB)，使用高效模式...");
            zstdArgs = tarSizeGb switch
            {
                > 20 => ["a", "-tzstd", "-mx=4", "-mmt=2", "-md=64m", "-sdel", outputFullPath, tempTarFile],
                > 10 => ["a", "-tzstd", "-mx=6", "-mmt=3", "-md=128m", "-sdel", outputFullPath, tempTarFile],
                > 4 => ["a", "-tzstd", "-mx=10", "-mmt=6", "-md=256m", "-sdel", outputFullPath, tempTarFile],
                _ => ["a", "-tzstd", "-mx=10", "-mmt=8", "-md=256m", "-sdel", outputFullPath, tempTarFile]
            };
        }
        else if (totalMemoryGb >= 16)
        {
            log?.Invoke($"检测到标准内存 ({totalMemoryGb} GB)，使用平衡模式...");
            zstdArgs = tarSizeGb switch
            {
                > 20 => ["a", "-tzstd", "-mx=3", "-mmt=1", "-md=32m", "-sdel", outputFullPath, tempTarFile],
                > 10 => ["a", "-tzstd", "-mx=5", "-mmt=2", "-md=64m", "-sdel", outputFullPath, tempTarFile],
                > 4 => ["a", "-tzstd", "-mx=8", "-mmt=4", "-md=128m", "-sdel", outputFullPath, tempTarFile],
                > 2 => ["a", "-tzstd", "-mx=9", "-mmt=6", "-md=128m", "-sdel", outputFullPath, tempTarFile],
                _ => ["a", "-tzstd", "-mx=9", "-mmt=8", "-md=128m", "-sdel", outputFullPath, tempTarFile]
            };
        }
        else
        {
            log?.Invoke($"检测到标准内存 ({totalMemoryGb} GB)，使用保守模式...");
            zstdArgs = tarSizeGb switch
            {
                > 20 => ["a", "-tzstd", "-mx=1", "-mmt=1", "-md=8m", "-sdel", outputFullPath, tempTarFile],
                > 10 => ["a", "-tzstd", "-mx=3", "-mmt=1", "-md=16m", "-sdel", outputFullPath, tempTarFile],
                > 4 => ["a", "-tzstd", "-mx=5", "-mmt=2", "-md=16m", "-sdel", outputFullPath, tempTarFile],
                > 2 => ["a", "-tzstd", "-mx=6", "-mmt=2", "-md=32m", "-sdel", outputFullPath, tempTarFile],
                _ => ["a", "-tzstd", "-mx=7", "-mmt=4", "-md=64m", "-sdel", outputFullPath, tempTarFile]
            };
        }

        var level = zstdArgs.FirstOrDefault(a => a.StartsWith("-mx=", StringComparison.Ordinal))?.Replace("-mx=", "") ?? "?";
        var threads = zstdArgs.FirstOrDefault(a => a.StartsWith("-mmt=", StringComparison.Ordinal))?.Replace("-mmt=", "") ?? "?";
        var memory = zstdArgs.FirstOrDefault(a => a.StartsWith("-md=", StringComparison.Ordinal))?.Replace("-md=", "") ?? "?";
        log?.Invoke($"压缩参数: 级别={level}, 线程={threads}, 内存={memory}");

        return zstdArgs;
    }

    private static void AppendExitCodeDiagnostics(int exitCode, Action<string>? log)
    {
        switch (exitCode)
        {
            case 1:
                log?.Invoke("错误原因: 警告（非致命错误）");
                break;
            case 2:
                log?.Invoke("错误原因: 致命错误");
                log?.Invoke("可能原因:");
                log?.Invoke("  1. 输出路径与源路径冲突");
                log?.Invoke("  2. 磁盘空间不足");
                log?.Invoke("  3. 没有写入权限");
                log?.Invoke("  4. 源文件被占用或无法读取");
                log?.Invoke("建议:");
                log?.Invoke("  - 确保输出文件不在源文件夹内");
                log?.Invoke("  - 检查磁盘空间是否充足");
                log?.Invoke("  - 关闭可能占用文件的程序");
                log?.Invoke("  - 以管理员身份运行");
                break;
            case 7:
                log?.Invoke("错误原因: 命令行错误");
                break;
            case 8:
                log?.Invoke("错误原因: 内存不足");
                break;
            case 255:
                log?.Invoke("错误原因: 用户中断");
                break;
            default:
                log?.Invoke("错误原因: 未知错误");
                break;
        }
    }

    private static void TryDeleteOutputArtifacts(string outputName)
    {
        try
        {
            if (File.Exists(outputName))
                File.Delete(outputName);

            var outputDir = Path.GetDirectoryName(outputName)!;
            var outputFile = Path.GetFileName(outputName);
            foreach (var file in Directory.GetFiles(outputDir, $"{outputFile}*"))
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}