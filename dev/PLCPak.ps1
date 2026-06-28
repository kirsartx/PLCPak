# PLCPak v2.0.0 - 7z-zstd + tar.zst 双格式压缩打包工具
param(
    [switch]$Clean,
    [switch]$Compress,
    [string[]]$Path,
    [switch]$Preview,
    [switch]$NoGui,
    [switch]$Force,
    [string]$Format = '7z'
)

$script:PlcPakVersion = 'v2.0.0'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic

# 跟踪是否执行过压缩（用于拖入新文件时清空列表）
$script:hasCompressed = $false

# 存储后台Job列表
$script:backgroundJobs = @()

# 配置文件路径（兼容 .ps1 和 .exe）
if ($PSScriptRoot) {
    $scriptDir = $PSScriptRoot
} elseif ($MyInvocation.MyCommand.Path) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $scriptDir = [System.IO.Path]::GetDirectoryName([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
}
$configPath = Join-Path $scriptDir "compress-config.json"
$adEngineModule = Join-Path $scriptDir "PLCPak-AdEngine.ps1"
if (Test-Path -LiteralPath $adEngineModule) { . $adEngineModule }

$pipelineModule = Join-Path $scriptDir "PLCPak-Pipeline.ps1"
if (Test-Path -LiteralPath $pipelineModule) { . $pipelineModule }

$settingsModule = Join-Path $scriptDir "PLCPak-Settings.ps1"
if (Test-Path -LiteralPath $settingsModule) { . $settingsModule }

$adRemoveModule = Join-Path $scriptDir "Remove-AdFiles.ps1"
if (Test-Path -LiteralPath $adRemoveModule) { . $adRemoveModule }

function Load-Config {
    if (Test-Path -LiteralPath $configPath) {
        try {
            $json = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
            return Merge-AdEngineConfig -Json $json
        } catch {}
    }
    return Get-AdEngineDefaultConfig
}

function Save-Config {
    param([hashtable]$Config)
    try {
        $Config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8 -Force
    } catch {}
}

function Save-RecentProject {
    param([string[]]$Projects)
    $cfg = Load-Config
    $list = New-Object System.Collections.ArrayList
    foreach ($p in $Projects) {
        if ($p -and (Test-Path -LiteralPath $p)) { [void]$list.Add($p) }
    }
    $cfg.RecentProjects = @($list | Select-Object -Unique | Select-Object -First 10)
    Save-Config -Config $cfg
}

function Wait-ForAdJobs {
    param([int]$TimeoutSec = 300)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($script:backgroundJobs.Count -gt 0 -or $script:jobQueue.Count -gt 0) {
        if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) {
            Log-Message "[超时] 等待广告扫描超过 ${TimeoutSec}s" "Red"
            break
        }
        Start-Sleep -Milliseconds 250
        [System.Windows.Forms.Application]::DoEvents()
    }
}

function Export-PlcPakLog {
    param([string]$LogText)
    $dlg = New-Object System.Windows.Forms.SaveFileDialog
    $dlg.Filter = '日志文件 (*.txt)|*.txt'
    $dlg.FileName = "PLCPak-log-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    if ($dlg.ShowDialog() -eq 'OK') {
        [IO.File]::WriteAllText($dlg.FileName, $LogText, [Text.UTF8Encoding]::new($false))
        Log-Message "[日志] 已导出: $($dlg.FileName)" "Blue"
    }
}

# 任务队列
$script:jobQueue = [System.Collections.Queue]::new()
$script:maxConcurrentJobs = 5
$script:appConfig = Load-Config

# 广告扫描后台脚本块（v2.0 仅预览，不立即删除）
$adScanScriptBlock = {
    param($targetPath, $scriptDir, $previewOnly)
    . (Join-Path $scriptDir 'PLCPak-AdEngine.ps1')
    $cfg = Get-AdEngineDefaultConfig
    $cp = Join-Path $scriptDir 'compress-config.json'
    if (Test-Path -LiteralPath $cp) {
        try {
            $json = Get-Content -LiteralPath $cp -Raw -Encoding UTF8 | ConvertFrom-Json
            $cfg = Merge-AdEngineConfig -Json $json
        } catch {}
    }
    $result = Invoke-AdCleanup -TargetPath $targetPath -ScriptDir $scriptDir -Config $cfg -PreviewOnly:([bool]$previewOnly)
    return @{ Path = $targetPath; Result = $result; Preview = [bool]$previewOnly }
}

# 设置编码为 UTF-8（仅在有控制台时设置）
try {
    $OutputEncoding = [System.Text.Encoding]::UTF8
    if ([Console]::OutputEncoding) {
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    }
} catch {
    # 在 GUI 模式下忽略控制台编码错误
}

# 检查 7-Zip 是否安装
function Test-7ZipInstalled {
    # 获取脚本目录（支持 .exe 和 .ps1）
    if ($PSScriptRoot) {
        $scriptDir = $PSScriptRoot
    } elseif ($MyInvocation.MyCommand.Path) {
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    } else {
        $scriptDir = Get-Location
    }
    $local7z = Join-Path $scriptDir "7-Zip-Zstandard\7z.exe"
    
    if (Test-Path $local7z) {
        return $local7z
    }
    
    $7zipPaths = @(
        "C:\Program Files\7-Zip\7z.exe",
        "C:\Program Files (x86)\7-Zip\7z.exe",
        "$env:ProgramFiles\7-Zip\7z.exe"
    )
    
    foreach ($path in $7zipPaths) {
        if (Test-Path $path) {
            return $path
        }
    }
    
    $7zCmd = Get-Command 7z -ErrorAction SilentlyContinue
    if ($7zCmd) {
        return $7zCmd.Source
    }
    
    return $null
}

# 检查 7z 是否支持 tar.zst（7-Zip-Zstandard 支持）
function Test-TarZstdAvailable {
    param([string]$SevenZipPath)
    
    if (-not $SevenZipPath -or -not (Test-Path $SevenZipPath)) {
        return $false
    }
    
    try {
        # 检查 7z 是否支持 tar 和 zstd
        $formats = & "$SevenZipPath" i 2>&1 | Out-String
        if ($formats -match 'tar' -and $formats -match 'zstd') {
            return $true
        }
    } catch {
        return $false
    }
    
    return $false
}

# 格式化文件大小
function Format-FileSize {
    param([long]$Size)
    
    if ($Size -gt 1000MB) {
        return "{0:N2}GB" -f ($Size / 1GB)
    } elseif ($Size -gt 1MB) {
        return "{0:N2}MB" -f ($Size / 1MB)
    } elseif ($Size -gt 1KB) {
        return "{0:N2}KB" -f ($Size / 1KB)
    } else {
        return "{0}B" -f $Size
    }
}

# 计算文件夹大小
function Get-FolderSize {
    param([string]$Path)
    
    try {
        $totalSize = 0
        Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Length) {
                $totalSize += $_.Length
            }
        }
        return $totalSize
    } catch {
        return 0
    }
}

# 使用 7z-zstd 压缩
function Compress-With7zZstd {
    param(
        [string]$SourcePath,
        [string]$OutputName,
        [string]$SevenZipPath,
        [System.Windows.Forms.RichTextBox]$LogBox,
        [System.Windows.Forms.ProgressBar]$ProgressBar,
        [int]$VolumeSizeMB = 1900
    )
    
    $LogBox.AppendText("`r`n开始使用 7z-zstd 压缩...`r`n")
    $LogBox.AppendText("源文件夹: $SourcePath`r`n")
    $LogBox.AppendText("输出文件: $OutputName`r`n")
    
    $volumeSize = "${VolumeSizeMB}m"
    
    # 预估是否需要分卷
    $folderSize = Get-FolderSize -Path $SourcePath
    $needSplit = $folderSize -gt ($VolumeSizeMB * 1MB)
    
    if ($needSplit) {
        $LogBox.AppendText("文件夹较大,将启用分卷压缩 (每卷 ${VolumeSizeMB}MB)`r`n")
    }
    
    try {
        # 检查输出文件是否已存在
        if (Test-Path -LiteralPath $OutputName) {
            $result = [System.Windows.Forms.MessageBox]::Show(
                "输出文件已存在: $OutputName`r`n`r`n是否覆盖?",
                "文件已存在",
                [System.Windows.Forms.MessageBoxButtons]::YesNo,
                [System.Windows.Forms.MessageBoxIcon]::Warning
            )
            
            if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
                $LogBox.AppendText("删除旧文件...`r`n")
                Remove-Item -LiteralPath $OutputName -Force -ErrorAction SilentlyContinue
                
                $outputDir = Split-Path $OutputName
                $outputFile = Split-Path $OutputName -Leaf
                Get-ChildItem -LiteralPath $outputDir -Filter "$outputFile*" | Remove-Item -Force -ErrorAction SilentlyContinue
            } else {
                $LogBox.AppendText("跳过 7z-zstd 压缩`r`n")
                return $false
            }
        }
        
        $sourceParent = Split-Path $SourcePath -Parent
        $sourceName = Split-Path $SourcePath -Leaf
        $outputFileName = Split-Path $OutputName -Leaf
        
        # 使用绝对路径避免特殊字符问题（如方括号）
        $sourceFullPath = $SourcePath
        $outputFullPath = $OutputName
        
        # 检查输出目录权限和磁盘空间
        $outputDir = Split-Path $OutputName
        $drive = Split-Path $outputDir -Qualifier
        if ($drive) {
            try {
                $driveInfo = Get-PSDrive $drive.TrimEnd(':')
                $freeSpaceGB = [math]::Round($driveInfo.Free / 1GB, 2)
                $needSpaceGB = [math]::Round($folderSize / 1GB, 2)
                $LogBox.AppendText("磁盘可用空间: ${freeSpaceGB}GB, 预计需要: ${needSpaceGB}GB`r`n")
                
                if ($driveInfo.Free -lt ($folderSize * 0.6)) {
                    $LogBox.AppendText("警告: 磁盘空间可能不足!`r`n")
                }
            } catch {
                $LogBox.AppendText("警告: 无法检查磁盘空间`r`n")
            }
        }
        
        # 在有控制台时设置代码页
        try { $null = chcp 65001 2>$null } catch { }
        
        # 根据文件夹大小优化压缩参数
        $folderSizeGB = $folderSize / 1GB
        $compressionLevel = 11
        $memLimit = "2g"
        $threads = 4
        
        $cfgThreads = 0
        if ($script:appConfig -and $script:appConfig.CompressThreads) {
            $cfgThreads = [int]$script:appConfig.CompressThreads
        }
        if ($folderSizeGB -gt 20) {
            $compressionLevel = 5
            $memLimit = "1g"
            $threads = 2
            $LogBox.AppendText("检测到超大文件夹 (>20GB)，使用优化参数：压缩级别=5, 内存限制=1GB, 线程数=2`r`n")
        } elseif ($folderSizeGB -gt 10) {
            $compressionLevel = 7
            $memLimit = "1536m"
            $threads = 3
            $LogBox.AppendText("检测到大文件夹 (>10GB)，使用优化参数：压缩级别=7, 内存限制=1.5GB, 线程数=3`r`n")
        } else {
            $LogBox.AppendText("使用标准压缩参数：压缩级别=11, 内存限制=2GB, 线程数=4`r`n")
        }
        if ($cfgThreads -gt 0) { $threads = $cfgThreads; $LogBox.AppendText("配置覆盖线程数: $threads`r`n") }
        
        # 构建压缩参数
        $compressionParams = @(
            "-t7z"
            "-m0=zstd"
            "-mx=$compressionLevel"
            "-mmt=$threads"
            "-md=$memLimit"
        )
        
        if ($needSplit) {
            $compressionParams += "-v$volumeSize"
        }
        if ($script:appConfig -and $script:appConfig.SkipStoreCompress) {
            $compressionParams += "-ms=on"
            $LogBox.AppendText("已启用存储模式(-ms=on)，跳过已压缩文件二次压缩`r`n")
        }
        
        # 使用绝对路径而不是相对路径，避免 Push-Location 对特殊字符的影响
        $LogBox.AppendText("`r`n执行压缩命令...`r`n")
        $LogBox.AppendText("这可能需要较长时间，请耐心等待...`r`n")
        
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        if ($ProgressBar) { 
            $ProgressBar.Value = 0
            $ProgressBar.Style = "Continuous"
        }
        
        & "$SevenZipPath" a @compressionParams "$outputFullPath" "$sourceFullPath" 2>&1 | ForEach-Object { 
            $line = $_.ToString()
            [System.Windows.Forms.Application]::DoEvents()
            
            if ($line -match '(\d+)%') {
                $p = [int]$matches[1]
                if ($ProgressBar) { 
                    $ProgressBar.Value = $p
                    $script:labelPercent.Text = "$p%"
                    if ($p -lt 30) {
                        $script:labelPercent.ForeColor = [System.Drawing.Color]::Red
                    } elseif ($p -lt 70) {
                        $script:labelPercent.ForeColor = [System.Drawing.Color]::DarkOrange
                    } else {
                        $script:labelPercent.ForeColor = [System.Drawing.Color]::Green
                    }
                }
            }
            
            if ($line -match 'Everything is Ok' -or $line -match 'Error') {
                Log-Message "$line"
            }
        }
        $sw.Stop()
        if ($ProgressBar) { 
            $ProgressBar.Value = 100
            $script:labelPercent.Text = "100%"
            $script:labelPercent.ForeColor = [System.Drawing.Color]::Green
        }
        $exitCode = $LASTEXITCODE
        
        $LogBox.AppendText("压缩耗时: $($sw.Elapsed.ToString('hh\:mm\:ss'))`r`n")
        
        if ($exitCode -eq 0) {
            $LogBox.AppendText("`r`n[OK] 7z-zstd 压缩完成!`r`n")
            
            $outputDir = Split-Path $OutputName
            $outputFile = Split-Path $OutputName -Leaf
            $files = Get-ChildItem -LiteralPath $outputDir -Filter "$outputFile*" | Sort-Object Name
            
            # 检查是否只有一个 .001 文件且小于 1900MB
            if ($files.Count -eq 1 -and $files[0].Name -match '\.001$') {
                $file001 = $files[0]
                if ($file001.Length -lt (1900 * 1MB)) {
                    $newName = $file001.Name -replace '\.001$', ''
                    
                    try {
                        Rename-Item -LiteralPath $file001.FullName -NewName $newName -Force
                        $LogBox.AppendText("检测到单个小文件,已重命名: $($file001.Name) -> $newName`r`n")
                        $files = Get-ChildItem -LiteralPath $outputDir -Filter "$outputFile*" | Sort-Object Name
                    } catch {
                        $LogBox.AppendText("警告: 无法重命名文件: $_`r`n")
                    }
                }
            }
            
            $LogBox.AppendText("`r`n生成的文件:`r`n")
            foreach ($file in $files) {
                $size = Format-FileSize $file.Length
                $LogBox.AppendText("  - $($file.Name) ($size)`r`n")
            }
            if (Get-Command Test-ArchiveIntegrity -ErrorAction SilentlyContinue) {
                $mainArchive = $files | Where-Object { $_.Name -eq (Split-Path $OutputName -Leaf) -or $_.Name -match '\.7z(\.\d+)?$' } | Select-Object -First 1
                if (-not $mainArchive) { $mainArchive = $files | Select-Object -First 1 }
                if ($mainArchive) {
                    $ok = Test-ArchiveIntegrity -SevenZipPath $SevenZipPath -ArchivePath $mainArchive.FullName
                    $LogBox.AppendText("归档校验: $(if ($ok) { '[OK]' } else { '[失败]' })`r`n")
                    if (-not $ok) { return $false }
                }
            }
            
            return $true
        } else {
            $LogBox.AppendText("`r`n[X] 7z-zstd 压缩失败! 退出代码: $exitCode`r`n")
            
            # 提供详细的错误诊断
            switch ($exitCode) {
                1 { $LogBox.AppendText("错误原因: 警告（非致命错误）`r`n") }
                2 { 
                    $LogBox.AppendText("错误原因: 致命错误`r`n")
                    $LogBox.AppendText("可能原因:`r`n")
                    $LogBox.AppendText("  1. 输出路径与源路径冲突`r`n")
                    $LogBox.AppendText("  2. 磁盘空间不足`r`n")
                    $LogBox.AppendText("  3. 没有写入权限`r`n")
                    $LogBox.AppendText("  4. 源文件被占用或无法读取`r`n")
                    $LogBox.AppendText("`r`n建议:`r`n")
                    $LogBox.AppendText("  - 确保输出文件不在源文件夹内`r`n")
                    $LogBox.AppendText("  - 检查磁盘空间是否充足`r`n")
                    $LogBox.AppendText("  - 关闭可能占用文件的程序`r`n")
                    $LogBox.AppendText("  - 以管理员身份运行`r`n")
                }
                7 { $LogBox.AppendText("错误原因: 命令行错误`r`n") }
                8 { $LogBox.AppendText("错误原因: 内存不足`r`n") }
                255 { $LogBox.AppendText("错误原因: 用户中断`r`n") }
                default { $LogBox.AppendText("错误原因: 未知错误`r`n") }
            }
            
            return $false
        }
    } catch {
        Pop-Location
        $LogBox.AppendText("`r`n[X] 7z-zstd 压缩出错: $_`r`n")
        return $false
    }
}

# 使用 7z 创建 tar.zst 压缩
function Compress-WithTarZst {
    param(
        [string]$SourcePath,
        [string]$OutputName,
        [string]$SevenZipPath,
        [System.Windows.Forms.RichTextBox]$LogBox,
        [System.Windows.Forms.ProgressBar]$ProgressBar
    )
    
    $LogBox.AppendText("`r`n开始使用 tar.zst 压缩...`r`n")
    $LogBox.AppendText("源文件夹: $SourcePath`r`n")
    $LogBox.AppendText("输出文件: $OutputName`r`n")
    
    if (-not $OutputName.EndsWith(".tar.zst")) {
        $OutputName = "$OutputName.tar.zst"
    }
    
    $sourceName = Split-Path $SourcePath -Leaf
    $sourceParent = Split-Path $SourcePath
    $outputDir = Split-Path $OutputName
    $outputFileName = Split-Path $OutputName -Leaf
    
    try {
        # 检查输出文件是否已存在
        if (Test-Path -LiteralPath $OutputName) {
            $result = [System.Windows.Forms.MessageBox]::Show(
                "输出文件已存在: $OutputName`r`n`r`n是否覆盖?",
                "文件已存在",
                [System.Windows.Forms.MessageBoxButtons]::YesNo,
                [System.Windows.Forms.MessageBoxIcon]::Warning
            )
            
            if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
                $LogBox.AppendText("删除旧文件...`r`n")
                Remove-Item -LiteralPath $OutputName -Force -ErrorAction SilentlyContinue
            } else {
                $LogBox.AppendText("跳过 tar.zst 压缩`r`n")
                return $false
            }
        }
        
        # 使用绝对路径避免特殊字符问题（如方括号）
        $sourceFullPath = $SourcePath
        $outputFullPath = $OutputName
        
        # 在有控制台时设置代码页
        try { $null = chcp 65001 2>$null } catch { }
        
        $outputFileName = Split-Path $OutputName -Leaf
        $tempRoot = if (Get-Command Get-PlcPakTempRoot -ErrorAction SilentlyContinue) {
            Get-PlcPakTempRoot -Config $script:appConfig
        } else { $outputDir }
        $tempTarFile = Join-Path $tempRoot "plcpak_$([guid]::NewGuid().ToString('N')).tar"
        
        # 第一步：创建 tar 文件（根据内存调整参数）
        $LogBox.AppendText("正在创建 tar 归档...`r`n")
        
        # 根据系统内存选择 tar 创建参数
        if ($totalMemoryGB -lt 16) {
            # 16GB以下：极简模式，使用最小内存
            $LogBox.AppendText("使用低内存 tar 模式 (单线程)...`r`n")
            # 不使用 -ms 参数（可能不兼容），只用最基础的参数
            $tarArgs = @("a", "-ttar", "-mmt=1", "$tempTarFile", "$sourceFullPath")
        } else {
            # 16GB+：标准模式
            $tarArgs = @("a", "-ttar", "-mmt=off", "$tempTarFile", "$sourceFullPath")
        }
        
        try {
            # 使用 & 操作符代替 Start-Process，获取标准输出
            Log-Message "执行命令: $SevenZipPath $($tarArgs -join ' ')"
            if ($ProgressBar) { 
                $ProgressBar.Value = 0
                $ProgressBar.Style = "Continuous"
                $script:labelPercent.Text = "0%"
            }
            
            & "$SevenZipPath" @tarArgs 2>&1 | ForEach-Object {
                [System.Windows.Forms.Application]::DoEvents()
                $line = $_.ToString()
                if ($line -match '(\d+)%') {
                    $p = [int]$matches[1]
                    if ($ProgressBar) { 
                        $ProgressBar.Value = $p
                        $script:labelPercent.Text = "$p%"
                        if ($p -lt 30) {
                            $script:labelPercent.ForeColor = [System.Drawing.Color]::Red
                        } elseif ($p -lt 70) {
                            $script:labelPercent.ForeColor = [System.Drawing.Color]::DarkOrange
                        } else {
                            $script:labelPercent.ForeColor = [System.Drawing.Color]::Green
                        }
                    }
                }
                if ($line -match 'Error' -or $line -match 'Everything is Ok') {
                    Log-Message $line
                }
            }
            $exitCode1 = $LASTEXITCODE
            $LogBox.AppendText("tar 创建退出代码: $exitCode1`r`n")
            
            $tarFilePath = $tempTarFile
            
            if (-not (Test-Path -LiteralPath $tarFilePath)) {
                $exitCode1 = 1
                $LogBox.AppendText("错误: tar 文件未创建`r`n")
            } else {
                $tarFileInfo = Get-Item -LiteralPath $tarFilePath
                $tarSizeMB = [math]::Round($tarFileInfo.Length / 1MB, 2)
                $LogBox.AppendText("tar 文件已创建: $(Format-FileSize $tarFileInfo.Length) ($tarSizeMB MB)`r`n")
                
                # 检查文件大小是否合理
                $measureResult = Get-ChildItem -LiteralPath $SourcePath -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
                $sourceSizeBytes = if ($measureResult -and $measureResult.Sum) { $measureResult.Sum } else { 0 }
                $expectedMinSize = $sourceSizeBytes * 0.5  # tar 文件至少应该是源文件的 50%
                
                if ($tarFileInfo.Length -lt 102400 -and $sourceSizeBytes -gt 1MB) {
                    $exitCode1 = 1
                    $LogBox.AppendText("错误: tar 文件大小异常 ($($tarFileInfo.Length) 字节)`r`n")
                    $LogBox.AppendText("源文件夹大小: $(Format-FileSize $sourceSizeBytes)`r`n")
                    $LogBox.AppendText("可能原因: 内存不足或进程崩溃`r`n")
                    $LogBox.AppendText("建议: `r`n")
                    $LogBox.AppendText("  1. 关闭浏览器等占内存的程序`r`n")
                    $LogBox.AppendText("  2. 重启电脑释放内存`r`n")
                    $LogBox.AppendText("  3. 或者只使用 7z-zstd 格式`r`n")
                } elseif ($tarFileInfo.Length -lt $expectedMinSize * 0.1) {
                    $exitCode1 = 1
                    $LogBox.AppendText("警告: tar 文件可能不完整`r`n")
                }
            }
        } catch {
            $exitCode1 = 1
            $LogBox.AppendText("创建 tar 时出错: $_`r`n")
            $LogBox.AppendText("异常详情: $($_.Exception.Message)`r`n")
        }
        
        if ($exitCode1 -ne 0) {
            Pop-Location
            if (Test-Path -LiteralPath (Join-Path $sourceParent $tempTarFile)) {
                Remove-Item -LiteralPath (Join-Path $sourceParent $tempTarFile) -Force -ErrorAction SilentlyContinue
            }
            $LogBox.AppendText("创建 tar 归档失败! 退出代码: $exitCode1`r`n")
            return $false
        }
        
        # 第二步：压缩 tar 文件为 zstd（智能内存管理）
        $LogBox.AppendText("正在使用 zstd 压缩...`r`n")
        
        # 获取系统内存信息
        try {
            $os = Get-WmiObject -Class Win32_OperatingSystem
            $totalMemoryGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
            $freeMemoryGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
            $LogBox.AppendText("系统内存: 总计 $totalMemoryGB GB, 可用 $freeMemoryGB GB`r`n")
        } catch {
            # 如果无法获取内存信息，假设为低内存系统
            $totalMemoryGB = 4
            $freeMemoryGB = 2
            $LogBox.AppendText("无法检测内存，使用保守设置`r`n")
        }
        
        # 检查 tar 文件大小
        $tarFileInfo = Get-Item -LiteralPath $tempTarFile
        $tarSizeGB = [math]::Round($tarFileInfo.Length / 1GB, 2)
        $LogBox.AppendText("tar 文件大小: $tarSizeGB GB`r`n")
        
        # 根据系统内存和文件大小智能选择压缩参数
        if ($totalMemoryGB -ge 64) {
            # 64GB+ 旗舰级内存：最高性能模式
            $LogBox.AppendText("检测到旗舰级内存 ($totalMemoryGB GB)，使用极速模式...`r`n")
            if ($tarSizeGB -gt 20) {
                # 超大文件：降低压缩级别和线程，避免卡死
                $zstdArgs = @("a", "-tzstd", "-mx=5", "-mmt=2", "-md=128m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 10) {
                $zstdArgs = @("a", "-tzstd", "-mx=7", "-mmt=3", "-md=256m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 4) {
                $zstdArgs = @("a", "-tzstd", "-mx=11", "-mmt=6", "-md=512m", "-sdel", "$outputFullPath", "$tempTarFile")
            } else {
                $zstdArgs = @("a", "-tzstd", "-mx=11", "-mmt=8", "-md=512m", "-sdel", "$outputFullPath", "$tempTarFile")
            }
        } elseif ($totalMemoryGB -ge 32) {
            # 32GB 高性能内存：高效模式
            $LogBox.AppendText("检测到高性能内存 ($totalMemoryGB GB)，使用高效模式...`r`n")
            if ($tarSizeGB -gt 20) {
                # 超大文件：降低压缩级别
                $zstdArgs = @("a", "-tzstd", "-mx=4", "-mmt=2", "-md=64m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 10) {
                $zstdArgs = @("a", "-tzstd", "-mx=6", "-mmt=3", "-md=128m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 4) {
                $zstdArgs = @("a", "-tzstd", "-mx=10", "-mmt=6", "-md=256m", "-sdel", "$outputFullPath", "$tempTarFile")
            } else {
                $zstdArgs = @("a", "-tzstd", "-mx=10", "-mmt=8", "-md=256m", "-sdel", "$outputFullPath", "$tempTarFile")
            }
        } elseif ($totalMemoryGB -ge 16) {
            # 16GB 标准内存：平衡模式
            $LogBox.AppendText("检测到标准内存 ($totalMemoryGB GB)，使用平衡模式...`r`n")
            if ($tarSizeGB -gt 20) {
                # 超大文件：极低资源占用
                $zstdArgs = @("a", "-tzstd", "-mx=3", "-mmt=1", "-md=32m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 10) {
                # 大文件：低资源占用
                $zstdArgs = @("a", "-tzstd", "-mx=5", "-mmt=2", "-md=64m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 4) {
                $zstdArgs = @("a", "-tzstd", "-mx=8", "-mmt=4", "-md=128m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 2) {
                $zstdArgs = @("a", "-tzstd", "-mx=9", "-mmt=6", "-md=128m", "-sdel", "$outputFullPath", "$tempTarFile")
            } else {
                $zstdArgs = @("a", "-tzstd", "-mx=9", "-mmt=8", "-md=128m", "-sdel", "$outputFullPath", "$tempTarFile")
            }
        } else {
            # 16GB以下：保守模式
            $LogBox.AppendText("检测到标准内存 ($totalMemoryGB GB)，使用保守模式...`r`n")
            if ($tarSizeGB -gt 20) {
                # 超大文件：极限保守
                $zstdArgs = @("a", "-tzstd", "-mx=1", "-mmt=1", "-md=8m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 10) {
                # 大文件：超低内存
                $zstdArgs = @("a", "-tzstd", "-mx=3", "-mmt=1", "-md=16m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 4) {
                # 中等文件：低内存
                $zstdArgs = @("a", "-tzstd", "-mx=5", "-mmt=2", "-md=16m", "-sdel", "$outputFullPath", "$tempTarFile")
            } elseif ($tarSizeGB -gt 2) {
                # 中等文件：适中
                $zstdArgs = @("a", "-tzstd", "-mx=6", "-mmt=2", "-md=32m", "-sdel", "$outputFullPath", "$tempTarFile")
            } else {
                # 小文件：正常
                $zstdArgs = @("a", "-tzstd", "-mx=7", "-mmt=4", "-md=64m", "-sdel", "$outputFullPath", "$tempTarFile")
            }
        }
        
        $LogBox.AppendText("压缩参数: 级别=$($zstdArgs[2] -replace '-mx=',''), 线程=$($zstdArgs[3] -replace '-mmt=',''), 内存=$($zstdArgs[4] -replace '-md=','')`r`n")
        
        try {
            Log-Message "执行 zstd 压缩命令..."
            Log-Message "这可能需要较长时间，请耐心等待..."
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            if ($ProgressBar) { 
                $ProgressBar.Value = 0
                $ProgressBar.Style = "Continuous"
                $script:labelPercent.Text = "0%"
            }
            
            & "$SevenZipPath" @zstdArgs 2>&1 | ForEach-Object {
                [System.Windows.Forms.Application]::DoEvents()
                $line = $_.ToString()
                if ($line -match '(\d+)%') {
                    $p = [int]$matches[1]
                    if ($ProgressBar) { 
                        $ProgressBar.Value = $p
                        $script:labelPercent.Text = "$p%"
                        if ($p -lt 30) {
                            $script:labelPercent.ForeColor = [System.Drawing.Color]::Red
                        } elseif ($p -lt 70) {
                            $script:labelPercent.ForeColor = [System.Drawing.Color]::DarkOrange
                        } else {
                            $script:labelPercent.ForeColor = [System.Drawing.Color]::Green
                        }
                    }
                }
                if ($line -match 'Everything is Ok' -or $line -match 'Error') {
                    Log-Message $line
                }
            }
            $sw.Stop()
            if ($ProgressBar) {
                $ProgressBar.Value = 100
                $script:labelPercent.Text = "100%"
                $script:labelPercent.ForeColor = [System.Drawing.Color]::Green
            }
            $exitCode = $LASTEXITCODE
            $LogBox.AppendText("zstd 压缩退出代码: $exitCode`r`n")
            $LogBox.AppendText("压缩耗时: $($sw.Elapsed.ToString('hh\:mm\:ss'))`r`n")
            
            # 检查输出文件是否创建成功
            if (-not (Test-Path -LiteralPath $outputFullPath)) {
                $exitCode = 1
                $LogBox.AppendText("错误: zst 文件未创建`r`n")
            } else {
                $zstFileInfo = Get-Item -LiteralPath $outputFullPath
                $LogBox.AppendText("zst 文件已创建: $(Format-FileSize $zstFileInfo.Length)`r`n")
                
                # 检查文件大小是否合理（至少应该大于 1KB）
                if ($zstFileInfo.Length -lt 1024) {
                    $exitCode = 1
                    $LogBox.AppendText("错误: zst 文件大小异常 ($($zstFileInfo.Length) 字节)`r`n")
                }
            }
            
            # 检查临时 tar 文件是否被删除（-sdel 参数）
            if (Test-Path -LiteralPath $tempTarFile) {
                $LogBox.AppendText("警告: 临时 tar 文件未被自动删除，手动清理中...`r`n")
                Remove-Item -LiteralPath $tempTarFile -Force -ErrorAction SilentlyContinue
            }
        } catch {
            $exitCode = 1
            $LogBox.AppendText("zstd 压缩时出错: $_`r`n")
            # 清理临时文件
            if (Test-Path -LiteralPath $tempTarFile) {
                Remove-Item -LiteralPath $tempTarFile -Force -ErrorAction SilentlyContinue
            }
        }
        
        # 最终验证
        if ($exitCode -eq 0 -and (Test-Path -LiteralPath $OutputName)) {
            $finalFile = Get-Item -LiteralPath $OutputName
            $finalSize = $finalFile.Length
            
            # 验证最终文件大小
            if ($finalSize -lt 1024) {
                $LogBox.AppendText("`r`n[X] tar.zst 压缩失败! 文件大小异常: $finalSize 字节`r`n")
                $LogBox.AppendText("可能原因: 内存不足或磁盘空间不足`r`n")
                $LogBox.AppendText("建议: 尝试只使用 7z-zstd 格式压缩`r`n")
                
                # 删除异常的文件
                Remove-Item -LiteralPath $OutputName -Force -ErrorAction SilentlyContinue
                return $false
            }
            
            $LogBox.AppendText("`r`n[OK] tar.zst 压缩完成!`r`n")
            
            $outputDir = Split-Path $OutputName
            $outputBase = Split-Path $OutputName -Leaf
            $files = Get-ChildItem -LiteralPath $outputDir -Filter $outputBase | Sort-Object Name
            
            $LogBox.AppendText("`r`n生成的文件:`r`n")
            foreach ($file in $files) {
                $size = Format-FileSize $file.Length
                $LogBox.AppendText("  - $($file.Name) ($size)`r`n")
            }
            
            return $true
        } else {
            $LogBox.AppendText("`r`n[X] tar.zst 压缩失败! 退出代码: $exitCode`r`n")
            
            # 如果生成了异常文件，删除它
            if ((Test-Path -LiteralPath $OutputName)) {
                $badFile = Get-Item -LiteralPath $OutputName
                if ($badFile.Length -lt 1024) {
                    $LogBox.AppendText("删除异常文件: $($badFile.Name) ($($badFile.Length) 字节)`r`n")
                    Remove-Item -LiteralPath $OutputName -Force -ErrorAction SilentlyContinue
                }
            }
            
            return $false
        }
    } catch {
        Pop-Location
        $LogBox.AppendText("`r`n[X] tar.zst 压缩出错: $_`r`n")
        return $false
    }
}

# 创建主窗体
$form = New-Object System.Windows.Forms.Form
$form.Text = "PLCPak $script:PlcPakVersion"
$form.Size = New-Object System.Drawing.Size(680, 650)
$form.AllowDrop = $true
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false

# 检查工具
$7zPath = Test-7ZipInstalled
$tarZstdAvailable = Test-TarZstdAvailable -SevenZipPath $7zPath

# 源项目选择区域（支持多个文件/文件夹）
$labelSource = New-Object System.Windows.Forms.Label
$labelSource.Location = New-Object System.Drawing.Point(10, 20)
$labelSource.Size = New-Object System.Drawing.Size(660, 20)
$labelSource.Text = "源项目（可添加多个文件夹和文件）:"
$form.Controls.Add($labelSource)

# 项目列表框
$listBoxSources = New-Object System.Windows.Forms.ListBox
$listBoxSources.Location = New-Object System.Drawing.Point(10, 45)
$listBoxSources.Size = New-Object System.Drawing.Size(560, 80)
$listBoxSources.SelectionMode = [System.Windows.Forms.SelectionMode]::MultiExtended
$listBoxSources.AllowDrop = $true
$form.Controls.Add($listBoxSources)

# 拖拽事件处理
$listBoxSources.Add_DragEnter({
    param($sender, $e)
    if ($e.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
        $e.Effect = [System.Windows.Forms.DragDropEffects]::Copy
    } else {
        $e.Effect = [System.Windows.Forms.DragDropEffects]::None
    }
})

$listBoxSources.Add_DragDrop({
    param($sender, $e)
    try {
        $files = $e.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
        Add-SourcePaths -Paths @($files)
    } catch {
        Log-Message "[错误] 拖拽添加失败: $_" "Red"
    }
})

# 创建定时器监控后台Job和队列
$jobMonitorTimer = New-Object System.Windows.Forms.Timer
$jobMonitorTimer.Interval = 200  
$jobMonitorTimer.Add_Tick({
    # 1. 处理队列：如果并发未满且队列有任务
    while ($script:backgroundJobs.Count -lt $script:maxConcurrentJobs -and $script:jobQueue.Count -gt 0) {
        $task = $script:jobQueue.Dequeue()
        $job = Start-Job -ScriptBlock $adScanScriptBlock -ArgumentList $task.Path, $task.ScriptDir, $true
        $jobObj = [PSCustomObject]@{ Job = $job; Path = $task.Path }
        $script:backgroundJobs += $jobObj
        Log-Message "[开始] 后台预览扫描: $($task.Path)" "DarkOrange"
    }
    
    # 无任务时更新进度条状态
    if ($script:backgroundJobs.Count -eq 0) {
        $progressBar.Style = "Continuous"
        $progressBar.Value = 0
        $script:labelPercent.Text = "就绪"
        $script:labelPercent.ForeColor = [System.Drawing.Color]::Gray
        return
    }
    
    $progressBar.Style = "Marquee" # 扫描时显示忙碌状态
    $script:labelPercent.Text = "扫描中..."
    $script:labelPercent.ForeColor = [System.Drawing.Color]::DarkOrange
    
    # 2. 检查运行中Job
    $completedJobs = @()
    foreach ($jobInfo in $script:backgroundJobs) {
        $job = $jobInfo.Job
        $path = $jobInfo.Path
        
        if ($job.State -eq 'Completed') {
            try {
                $output = Receive-Job -Job $job -ErrorAction SilentlyContinue
                if ($output -and $output.Result) {
                    $result = $output.Result
                    Set-PipelineTaskFromScan -Path $output.Path -Result $result
                    Update-PipelineStatusUI
                    Log-Message "[完成] 预览扫描: $($output.Path)" "Green"
                    
                    if ($result.Errors -and @($result.Errors).Count -gt 0) {
                        foreach ($err in @($result.Errors)) {
                            Log-Message "  [警告] $err" "DarkRed"
                        }
                    }

                    if ($result.TotalMatched -gt 0) {
                        Log-Message "[预览] 匹配 $($result.TotalMatched) 项 (扫描 $($result.TotalScanned) 个文件)" "Magenta"
                        foreach ($matched in @($result.MatchedFiles)) {
                            Log-Message "  [将删] $matched" "Gray"
                        }
                    } else {
                        Log-Message "  [安全] 未检测到广告 (扫描 $($result.TotalScanned) 个文件)" "Green"
                    }
                }
            } catch {
                Log-Message "[错误] Job处理失败: $_" "Red"
            } finally {
                Remove-Job -Job $job -Force
                $completedJobs += $jobInfo
            }
        } elseif ($job.State -eq 'Failed') {
             Log-Message "[失败] 扫描失败: $path" "Red"
             Remove-Job -Job $job -Force
             $completedJobs += $jobInfo
        } elseif ($job.State -eq 'Running') {
            $scanTimeout = if ($script:appConfig.AdScanTimeoutSec) { [int]$script:appConfig.AdScanTimeoutSec } else { 300 }
            if ((Get-Date) - $job.PSBeginTime -gt (New-TimeSpan -Seconds $scanTimeout)) {
                Log-Message "[超时] 扫描超时(${scanTimeout}s)，已终止: $path" "Red"
                Stop-Job -Job $job
                Remove-Job -Job $job -Force
                $completedJobs += $jobInfo
            }
        }
    }
    
    foreach ($c in $completedJobs) {
        $script:backgroundJobs = $script:backgroundJobs | Where-Object { $_ -ne $c }
    }
})
$jobMonitorTimer.Start()

# 添加文件夹按钮
$buttonAddFolder = New-Object System.Windows.Forms.Button
$buttonAddFolder.Location = New-Object System.Drawing.Point(580, 45)
$buttonAddFolder.Size = New-Object System.Drawing.Size(90, 25)
$buttonAddFolder.Text = "添加文件夹"
$form.Controls.Add($buttonAddFolder)

# 添加文件按钮
$buttonAddFile = New-Object System.Windows.Forms.Button
$buttonAddFile.Location = New-Object System.Drawing.Point(580, 75)
$buttonAddFile.Size = New-Object System.Drawing.Size(90, 25)
$buttonAddFile.Text = "添加文件"
$form.Controls.Add($buttonAddFile)

# 移除按钮
$buttonRemove = New-Object System.Windows.Forms.Button
$buttonRemove.Location = New-Object System.Drawing.Point(580, 105)
$buttonRemove.Size = New-Object System.Drawing.Size(90, 25)
$buttonRemove.Text = "移除选中"
$form.Controls.Add($buttonRemove)

$labelRecent = New-Object System.Windows.Forms.Label
$labelRecent.Location = New-Object System.Drawing.Point(10, 128)
$labelRecent.Size = New-Object System.Drawing.Size(70, 20)
$labelRecent.Text = "最近项目:"
$form.Controls.Add($labelRecent)

$comboRecent = New-Object System.Windows.Forms.ComboBox
$comboRecent.Location = New-Object System.Drawing.Point(80, 125)
$comboRecent.Size = New-Object System.Drawing.Size(400, 22)
$comboRecent.DropDownStyle = 'DropDownList'
foreach ($rp in @($script:appConfig.RecentProjects)) {
    if ($rp) { [void]$comboRecent.Items.Add($rp) }
}
$form.Controls.Add($comboRecent)

$buttonSettings = New-Object System.Windows.Forms.Button
$buttonSettings.Location = New-Object System.Drawing.Point(490, 123)
$buttonSettings.Size = New-Object System.Drawing.Size(80, 25)
$buttonSettings.Text = "设置"
$form.Controls.Add($buttonSettings)

$labelTaskStatus = New-Object System.Windows.Forms.Label
$labelTaskStatus.Location = New-Object System.Drawing.Point(10, 150)
$labelTaskStatus.Size = New-Object System.Drawing.Size(70, 20)
$labelTaskStatus.Text = "任务状态:"
$form.Controls.Add($labelTaskStatus)

$textBoxTaskStatus = New-Object System.Windows.Forms.TextBox
$textBoxTaskStatus.Location = New-Object System.Drawing.Point(80, 148)
$textBoxTaskStatus.Size = New-Object System.Drawing.Size(490, 20)
$textBoxTaskStatus.ReadOnly = $true
$textBoxTaskStatus.BorderStyle = 'FixedSingle'
$textBoxTaskStatus.Text = "拖入文件夹后将自动预览扫描"
$form.Controls.Add($textBoxTaskStatus)

# 总大小显示（使用只读文本框以支持复制）
$labelTotalSize = New-Object System.Windows.Forms.TextBox
$labelTotalSize.Location = New-Object System.Drawing.Point(10, 172)
$labelTotalSize.Size = New-Object System.Drawing.Size(560, 20)
$labelTotalSize.Text = "总大小: 0 B"
$labelTotalSize.ForeColor = [System.Drawing.Color]::Gray
$labelTotalSize.ReadOnly = $true
$labelTotalSize.BorderStyle = [System.Windows.Forms.BorderStyle]::None
$labelTotalSize.BackColor = $form.BackColor
$labelTotalSize.Cursor = [System.Windows.Forms.Cursors]::Hand
$labelTotalSize.TabStop = $false
$form.Controls.Add($labelTotalSize)

$form.Controls.Add($labelTotalSize)

# 总大小点击复制事件
$labelTotalSize.Add_MouseDown({
    param($s, $e)
    try {
        $txt = $labelTotalSize.Text
        if (-not $txt -or $txt -eq "总大小: 0 B") {
            return
        }
        
        # 仅复制冒号后面的内容
        $copyText = $txt
        if ($txt -match ':\s*(.+)$') {
            $copyText = $matches[1].Trim()
        }
        
        if (-not $copyText) {
            return
        }
        
        [System.Windows.Forms.Clipboard]::SetText($copyText)
        
        $script:totalSizeOriginalColor = $labelTotalSize.ForeColor
        $labelTotalSize.ForeColor = [System.Drawing.Color]::Green
        
        if ($script:totalSizeTimer) {
            try {
                $script:totalSizeTimer.Stop()
                $script:totalSizeTimer.Dispose()
            } catch { }
        }
        
        $script:totalSizeTimer = New-Object System.Windows.Forms.Timer
        $script:totalSizeTimer.Interval = 500
        $script:totalSizeTimer.Add_Tick({
            try {
                if ($labelTotalSize -and $script:totalSizeOriginalColor) {
                    $labelTotalSize.ForeColor = $script:totalSizeOriginalColor
                }
            } catch { }
            try {
                $script:totalSizeTimer.Stop()
                $script:totalSizeTimer.Dispose()
            } catch { }
        })
        $script:totalSizeTimer.Start()
    } catch { }
})

# 计算总大小函数
function Update-TotalSize {
    try {
        $totalSize = 0
        $pcSize = 0
        $apkSize = 0
        
        foreach ($item in $listBoxSources.Items) {
            if (Test-Path -LiteralPath $item -PathType Container) {
                $size = Get-FolderSize -Path $item
                $totalSize += $size
                $pcSize += $size
            } elseif (Test-Path -LiteralPath $item -PathType Leaf) {
                $size = (Get-Item -LiteralPath $item).Length
                $totalSize += $size
                if ($item -match '\.apk$') {
                    $apkSize += $size
                } else {
                    $pcSize += $size
                }
            }
        }
        
        # 根据是否有APK文件决定显示格式
        if ($apkSize -gt 0 -and $pcSize -gt 0) {
            # 有PC和APK，分别显示
            $pcStr = Format-FileSize $pcSize
            $apkStr = Format-FileSize $apkSize
            $labelTotalSize.Text = "总大小: PC: $pcStr   安卓: $apkStr"
        } elseif ($apkSize -gt 0) {
            # 只有APK
            $labelTotalSize.Text = "总大小: 安卓: $(Format-FileSize $apkSize)"
        } elseif ($pcSize -gt 0) {
            # 只有PC
            $labelTotalSize.Text = "总大小: PC: $(Format-FileSize $pcSize)"
        } else {
            $labelTotalSize.Text = "总大小: 0B"
        }
        
        $labelTotalSize.ForeColor = [System.Drawing.Color]::Black
    } catch {
        $labelTotalSize.Text = "总大小: 计算错误"
        $labelTotalSize.ForeColor = [System.Drawing.Color]::Red
    }
}

# 添加文件夹按钮事件
$buttonAddFolder.Add_Click({
    $folderBrowser = New-Object System.Windows.Forms.FolderBrowserDialog
    $folderBrowser.Description = "选择要添加的文件夹"
    
    if ($folderBrowser.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Add-SourcePaths -Paths @($folderBrowser.SelectedPath)
    }
})

# 添加文件按钮事件
$buttonAddFile.Add_Click({
    # 添加新文件时，如果上次执行过压缩，清空待压缩列表
    if ($script:hasCompressed) {
        $listBoxSources.Items.Clear()
        $script:hasCompressed = $false
        $textBoxLog.AppendText("[清空] 已清空上次的待压缩列表`r`n")
    }
    
    $fileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $fileDialog.Title = "选择要添加的文件"
    $fileDialog.Multiselect = $true
    
    if ($fileDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        foreach ($file in $fileDialog.FileNames) {
            if (-not $listBoxSources.Items.Contains($file)) {
                $listBoxSources.Items.Add($file)
            }
        }
        Update-TotalSize
    }
})

# 移除按钮事件
$buttonRemove.Add_Click({
    $selectedItems = @($listBoxSources.SelectedItems)
    foreach ($item in $selectedItems) {
        $listBoxSources.Items.Remove($item)
    }
    if ($selectedItems.Count -gt 0) {
        Update-TotalSize
    }
})

# 频道自购复选框（独立控件）
$checkboxSelfPurchase = New-Object System.Windows.Forms.CheckBox
$checkboxSelfPurchase.Location = New-Object System.Drawing.Point(15, 198)
$checkboxSelfPurchase.Size = New-Object System.Drawing.Size(200, 20)
$checkboxSelfPurchase.Text = "频道自购（使用自购版资源说明）"
$checkboxSelfPurchase.Checked = $false
$form.Controls.Add($checkboxSelfPurchase)

# 7z-zstd 设置
$groupBox7z = New-Object System.Windows.Forms.GroupBox
$groupBox7z.Location = New-Object System.Drawing.Point(10, 223)
$groupBox7z.Size = New-Object System.Drawing.Size(660, 110)
$groupBox7z.Text = "7z-zstd 压缩（仅压缩文件夹，不含单独文件）"
$form.Controls.Add($groupBox7z)

$checkbox7z = New-Object System.Windows.Forms.CheckBox
$checkbox7z.Location = New-Object System.Drawing.Point(15, 25)
$checkbox7z.Size = New-Object System.Drawing.Size(100, 20)
$checkbox7z.Text = "启用压缩"
$checkbox7z.Checked = $true
$groupBox7z.Controls.Add($checkbox7z)

$label7zName = New-Object System.Windows.Forms.Label
$label7zName.Location = New-Object System.Drawing.Point(15, 50)
$label7zName.Size = New-Object System.Drawing.Size(100, 20)
$label7zName.Text = "输出名称:"
$groupBox7z.Controls.Add($label7zName)

$textBox7zName = New-Object System.Windows.Forms.TextBox
$textBox7zName.Location = New-Object System.Drawing.Point(115, 48)
$textBox7zName.Size = New-Object System.Drawing.Size(400, 20)
$textBox7zName.ForeColor = [System.Drawing.Color]::Gray
$textBox7zName.Text = "默认使用文件夹名称"
$groupBox7z.Controls.Add($textBox7zName)

$labelVolumeSize = New-Object System.Windows.Forms.Label
$labelVolumeSize.Location = New-Object System.Drawing.Point(15, 75)
$labelVolumeSize.Size = New-Object System.Drawing.Size(100, 20)
$labelVolumeSize.Text = "分卷大小(MB):"
$groupBox7z.Controls.Add($labelVolumeSize)

$textBoxVolumeSize = New-Object System.Windows.Forms.TextBox
$textBoxVolumeSize.Location = New-Object System.Drawing.Point(115, 73)
$textBoxVolumeSize.Size = New-Object System.Drawing.Size(100, 20)
$config = Load-Config
$textBoxVolumeSize.Text = $config.VolumeSizeMB.ToString()
$groupBox7z.Controls.Add($textBoxVolumeSize)

$labelVolumeTip = New-Object System.Windows.Forms.Label
$labelVolumeTip.Location = New-Object System.Drawing.Point(220, 75)
$labelVolumeTip.Size = New-Object System.Drawing.Size(295, 20)
$labelVolumeTip.Text = "(文件超过此大小将自动分卷，建议1900MB)"
$labelVolumeTip.ForeColor = [System.Drawing.Color]::Gray
$groupBox7z.Controls.Add($labelVolumeTip)

# 7z 名称框焦点事件
$textBox7zName.Add_Enter({
    if ($textBox7zName.Text -eq "默认使用文件夹名称" -and $textBox7zName.ForeColor -eq [System.Drawing.Color]::Gray) {
        $textBox7zName.Text = ""
        $textBox7zName.ForeColor = [System.Drawing.Color]::Black
    }
})

$textBox7zName.Add_Leave({
    if ([string]::IsNullOrWhiteSpace($textBox7zName.Text)) {
        $textBox7zName.Text = "默认使用文件夹名称"
        $textBox7zName.ForeColor = [System.Drawing.Color]::Gray
    }
})

$label7zExt = New-Object System.Windows.Forms.Label
$label7zExt.Location = New-Object System.Drawing.Point(520, 50)
$label7zExt.Size = New-Object System.Drawing.Size(50, 20)
$label7zExt.Text = ".7z"
$groupBox7z.Controls.Add($label7zExt)

# tar.zst 设置
$groupBoxTar = New-Object System.Windows.Forms.GroupBox
$groupBoxTar.Location = New-Object System.Drawing.Point(10, 343)
$groupBoxTar.Size = New-Object System.Drawing.Size(660, 80)
$groupBoxTar.Text = "tar.zst 压缩（分离压缩PC和安卓到文件夹）"
$form.Controls.Add($groupBoxTar)

$checkboxTar = New-Object System.Windows.Forms.CheckBox
$checkboxTar.Location = New-Object System.Drawing.Point(15, 25)
$checkboxTar.Size = New-Object System.Drawing.Size(100, 20)
$checkboxTar.Text = "启用压缩"
$checkboxTar.Checked = $true
$groupBoxTar.Controls.Add($checkboxTar)

$labelTarName = New-Object System.Windows.Forms.Label
$labelTarName.Location = New-Object System.Drawing.Point(15, 50)
$labelTarName.Size = New-Object System.Drawing.Size(100, 20)
$labelTarName.Text = "输出名称:"
$groupBoxTar.Controls.Add($labelTarName)

$textBoxTarName = New-Object System.Windows.Forms.TextBox
$textBoxTarName.Location = New-Object System.Drawing.Point(115, 48)
$textBoxTarName.Size = New-Object System.Drawing.Size(400, 20)
$textBoxTarName.Text = (Get-Date).ToString("yyMMdd_01")
$groupBoxTar.Controls.Add($textBoxTarName)

$labelTarExt = New-Object System.Windows.Forms.Label
$labelTarExt.Location = New-Object System.Drawing.Point(520, 50)
$labelTarExt.Size = New-Object System.Drawing.Size(80, 20)
$labelTarExt.Text = ".tar.zst"
$groupBoxTar.Controls.Add($labelTarExt)

# 日志输出
$labelLog = New-Object System.Windows.Forms.Label
$labelLog.Location = New-Object System.Drawing.Point(10, 433)
$labelLog.Size = New-Object System.Drawing.Size(60, 20)
$labelLog.Text = "进度:"
$form.Controls.Add($labelLog)

# 进度条
$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(70, 433)
$progressBar.Size = New-Object System.Drawing.Size(540, 18)
$progressBar.Style = "Continuous"
$progressBar.Minimum = 0
$progressBar.Maximum = 100
$progressBar.Value = 0
$form.Controls.Add($progressBar)

# 进度百分比标签
$script:labelPercent = New-Object System.Windows.Forms.Label
$script:labelPercent.Location = New-Object System.Drawing.Point(615, 433)
$script:labelPercent.Size = New-Object System.Drawing.Size(55, 18)
$script:labelPercent.Text = "0%"
$script:labelPercent.TextAlign = [System.Drawing.ContentAlignment]::MiddleRight
$script:labelPercent.Font = New-Object System.Drawing.Font("Microsoft YaHei", 9, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($script:labelPercent)

$textBoxLog = New-Object System.Windows.Forms.RichTextBox
$textBoxLog.Location = New-Object System.Drawing.Point(10, 455)
$textBoxLog.Size = New-Object System.Drawing.Size(660, 105)
$textBoxLog.ScrollBars = "Vertical"
$textBoxLog.ReadOnly = $true
$textBoxLog.Font = New-Object System.Drawing.Font("Consolas", 9)
$form.Controls.Add($textBoxLog)

function Log-Message {
    param(
        [Parameter(Position=0)]
        [string]$Message,
        [Parameter(Position=1)]
        [string]$ColorName = "Black"
    )
    $text = $Message
    if (-not $text.EndsWith("`r`n")) { $text += "`r`n" }
    $textBoxLog.SelectionStart = $textBoxLog.TextLength
    $textBoxLog.SelectionLength = 0
    try {
        $color = [System.Drawing.Color]::FromName($ColorName)
        $textBoxLog.SelectionColor = $color
    } catch {
        $textBoxLog.SelectionColor = [System.Drawing.Color]::Black
    }
    $textBoxLog.AppendText($text)
    $textBoxLog.SelectionColor = $textBoxLog.ForeColor
    $textBoxLog.ScrollToCaret()
    [System.Windows.Forms.Application]::DoEvents()
}

function Update-PipelineStatusUI {
    if (-not (Get-Command Get-PipelineTaskSummary -ErrorAction SilentlyContinue)) { return }
    $summary = Get-PipelineTaskSummary
    if ($textBoxTaskStatus) {
        $textBoxTaskStatus.Text = if ($summary) { $summary } else { '暂无任务' }
    }
}

function Add-SourcePaths {
    param([string[]]$Paths)
    if ($script:hasCompressed) {
        $listBoxSources.Items.Clear()
        $script:hasCompressed = $false
        Log-Message "[清空] 已清空上次的待压缩列表" "Blue"
    }
    foreach ($file in $Paths) {
        if (-not $listBoxSources.Items.Contains($file)) {
            $listBoxSources.Items.Add($file) | Out-Null
            if (Test-Path -LiteralPath $file -PathType Container) {
                New-PipelineTask -Path $file
                $taskObj = [PSCustomObject]@{ Path = $file; ScriptDir = $scriptDir }
                $script:jobQueue.Enqueue($taskObj)
                Log-Message "[队列] 预览扫描: $file" "Blue"
            } else {
                Log-Message "[已添加] 文件: $file"
            }
        }
    }
    Update-TotalSize
    Update-PipelineStatusUI
}

$comboRecent.Add_SelectedIndexChanged({
    if ($comboRecent.SelectedItem) {
        $p = [string]$comboRecent.SelectedItem
        if ($p -and (Test-Path -LiteralPath $p)) {
            Add-SourcePaths -Paths @($p)
        }
    }
})

$buttonSettings.Add_Click({
    $cfg = Load-Config
    Show-PlcPakSettingsDialog -Config $cfg -ConfigPath $configPath -OnSave {
        param($newCfg)
        $script:appConfig = $newCfg
        Log-Message "[设置] 配置已保存" "Blue"
    }
})

$form.Add_DragEnter({
    param($sender, $e)
    if ($e.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
        $e.Effect = [System.Windows.Forms.DragDropEffects]::Copy
    } else {
        $e.Effect = [System.Windows.Forms.DragDropEffects]::None
    }
})

$form.Add_DragDrop({
    param($sender, $e)
    try {
        $files = $e.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
        Add-SourcePaths -Paths @($files)
    } catch {
        Log-Message "[错误] 窗体拖放失败: $_" "Red"
    }
})

# 开始压缩按钮
$buttonStart = New-Object System.Windows.Forms.Button
$buttonStart.Location = New-Object System.Drawing.Point(520, 570)
$buttonStart.Text = "一键执行"
$buttonStart.Size = New-Object System.Drawing.Size(150, 35)
$buttonStart.Font = New-Object System.Drawing.Font("Microsoft YaHei", 10, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($buttonStart)

$buttonPreview = New-Object System.Windows.Forms.Button
$buttonPreview.Location = New-Object System.Drawing.Point(360, 570)
$buttonPreview.Size = New-Object System.Drawing.Size(90, 35)
$buttonPreview.Text = '清理预览'
$form.Controls.Add($buttonPreview)
$buttonPreview.Add_Click({
    if ($listBoxSources.Items.Count -eq 0) { return }
    $cfg = Load-Config
    $lines = New-Object System.Collections.ArrayList
    foreach ($item in $listBoxSources.Items) {
        if (Test-Path -LiteralPath $item -PathType Container) {
            $pv = Invoke-AdCleanup -TargetPath $item -ScriptDir $scriptDir -Config $cfg -PreviewOnly
            [void]$lines.Add("=== $item ===")
            [void]$lines.Add("扫描文件: $($pv.TotalScanned), 匹配: $($pv.TotalMatched)")
            foreach ($m in @($pv.MatchedFiles)) { [void]$lines.Add("  [将删] $m") }
        }
    }
    $text = if ($lines.Count -gt 0) { ($lines -join "`r`n") } else { '无匹配广告' }
    [System.Windows.Forms.MessageBox]::Show($text, '清理预览', 'OK', 'Information') | Out-Null
})

$buttonExportLog = New-Object System.Windows.Forms.Button
$buttonExportLog.Location = New-Object System.Drawing.Point(460, 570)
$buttonExportLog.Size = New-Object System.Drawing.Size(50, 35)
$buttonExportLog.Text = '日志'
$form.Controls.Add($buttonExportLog)
$buttonExportLog.Add_Click({ Export-PlcPakLog -LogText $textBoxLog.Text })

# 分卷大小输入验证
$textBoxVolumeSize.Add_TextChanged({
    $text = $textBoxVolumeSize.Text
    if ($text -match '^\d+$') {
        $value = [int]$text
        if ($value -ge 100 -and $value -le 10000) {
            $textBoxVolumeSize.BackColor = [System.Drawing.Color]::White
        } else {
            $textBoxVolumeSize.BackColor = [System.Drawing.Color]::LightYellow
        }
    } else {
        $textBoxVolumeSize.BackColor = [System.Drawing.Color]::LightPink
    }
})

# 开始压缩按钮事件
$buttonStart.Add_Click({
    try {
    # 检查是否有选中的项目
    if ($listBoxSources.Items.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("请添加要压缩的文件或文件夹!", "错误", 
            [System.Windows.Forms.MessageBoxButtons]::OK, 
            [System.Windows.Forms.MessageBoxIcon]::Error)
        return
    }
    
    # 验证所有项目是否存在
    foreach ($item in $listBoxSources.Items) {
        if (-not (Test-Path -LiteralPath $item)) {
            [System.Windows.Forms.MessageBox]::Show("项目不存在: $item", "错误", 
                [System.Windows.Forms.MessageBoxButtons]::OK, 
                [System.Windows.Forms.MessageBoxIcon]::Error)
            return
        }
    }
    
    if (-not $checkbox7z.Checked -and -not $checkboxTar.Checked) {
        [System.Windows.Forms.MessageBox]::Show("请至少选择一种压缩格式!", "错误", 
            [System.Windows.Forms.MessageBoxButtons]::OK, 
            [System.Windows.Forms.MessageBoxIcon]::Error)
        return
    }
    
    # 验证并保存分卷大小设置
    $volumeSizeText = $textBoxVolumeSize.Text.Trim()
    if ($volumeSizeText -notmatch '^\d+$') {
        [System.Windows.Forms.MessageBox]::Show("分卷大小必须是纯数字!", "错误", 
            [System.Windows.Forms.MessageBoxButtons]::OK, 
            [System.Windows.Forms.MessageBoxIcon]::Error)
        return
    }
    
    $volumeSizeMB = [int]$volumeSizeText
    if ($volumeSizeMB -lt 100 -or $volumeSizeMB -gt 10000) {
        [System.Windows.Forms.MessageBox]::Show("分卷大小必须在 100-10000 MB 之间!", "错误", 
            [System.Windows.Forms.MessageBoxButtons]::OK, 
            [System.Windows.Forms.MessageBoxIcon]::Error)
        return
    }
    
    $config = Load-Config
    $config.VolumeSizeMB = $volumeSizeMB
    $script:appConfig = $config
    Save-Config -Config $config

    Wait-ForAdJobs -TimeoutSec $config.AdScanTimeoutSec

    foreach ($item in $listBoxSources.Items) {
        if (Test-Path -LiteralPath $item -PathType Container) {
            if (-not $script:PipelineTasks.ContainsKey($item)) {
                New-PipelineTask -Path $item
                $pv = Invoke-AdCleanup -TargetPath $item -ScriptDir $scriptDir -Config $config -PreviewOnly
                Set-PipelineTaskFromScan -Path $item -Result $pv
            }
        }
    }
    Update-PipelineStatusUI
    if (-not (Confirm-PipelineCleanup -Config $config)) { return }

    $textBoxLog.AppendText("`r`n--- 广告清理 ---`r`n")
    Invoke-PipelineCleanAll -ScriptDir $scriptDir -Config $config
    Update-PipelineStatusUI
    $textBoxLog.AppendText("广告清理完成`r`n")
    
    # 禁用按钮防止重复点击
    $buttonStart.Enabled = $false
    $textBoxLog.Clear()
    
    $textBoxLog.AppendText("===============================================`r`n")
    $textBoxLog.AppendText("       一键执行：清理 + 压缩`r`n")
    $textBoxLog.AppendText("===============================================`r`n")
    
    # 分离文件夹和文件
    $folders = @()
    $files = @()
    foreach ($item in $listBoxSources.Items) {
        if (Test-Path -LiteralPath $item -PathType Container) {
            $folders += $item
        } else {
            $files += $item
        }
    }
    
    $textBoxLog.AppendText("文件夹数量: $($folders.Count)`r`n")
    $textBoxLog.AppendText("文件数量: $($files.Count)`r`n`r`n")
    
    # 确定输出目录（使用第一个项目的父目录）
    $firstItem = $listBoxSources.Items[0]
    if (Test-Path -LiteralPath $firstItem -PathType Container) {
        $outputDir = Split-Path $firstItem
    } else {
        $outputDir = Split-Path $firstItem
    }
    
    # 7z-zstd 压缩（仅压缩文件夹）
    if ($checkbox7z.Checked -and $7zPath -and $folders.Count -gt 0) {
        $textBoxLog.AppendText("`r`n--- 7z-zstd 压缩（仅文件夹）---`r`n")
        
        # 获取脚本目录
        if ($PSScriptRoot) {
            $scriptDir = $PSScriptRoot
        } elseif ($MyInvocation.MyCommand.Path) {
            $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
        } else {
            $scriptDir = [System.IO.Path]::GetDirectoryName([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
        }
        
        # 根据频道自购复选框状态选择资源说明文件
        $readmeFileName = if ($checkboxSelfPurchase.Checked) {
            "资源说明(必读-自购).txt"
        } else {
            "资源说明(必读).txt"
        }
        $readmeSource = Join-Path $scriptDir $readmeFileName
        
        foreach ($folder in $folders) {
            $folderName = Split-Path $folder -Leaf
            
            # 复制资源说明文件到目标文件夹
            $readmeTarget = Join-Path $folder "资源说明(必读).txt"
            
            if (Test-Path -LiteralPath $readmeSource) {
                if (Test-Path -LiteralPath $readmeTarget) {
                    $textBoxLog.AppendText("目标文件夹已存在资源说明文件,跳过复制`r`n")
                } else {
                    try {
                        Copy-Item -LiteralPath $readmeSource -Destination $readmeTarget -Force
                        $textBoxLog.AppendText("已复制 $readmeFileName 到目标文件夹`r`n")
                    } catch {
                        $textBoxLog.AppendText("警告: 无法复制资源说明文件: $_`r`n")
                    }
                }
            } else {
                $textBoxLog.AppendText("未找到 $readmeFileName,跳过复制`r`n")
            }
            
            $textBoxLog.AppendText("`r`n[提示] 广告已在本次一键执行中清理`r`n")
            
            $7zName = $textBox7zName.Text.Trim()
            if ([string]::IsNullOrWhiteSpace($7zName) -or $7zName -eq "默认使用文件夹名称") {
                $7zName = $folderName
            }
            
            if (-not $7zName.EndsWith(".7z")) {
                $7zName = "$7zName.7z"
            }
            
            $7zOutputPath = Join-Path $outputDir $7zName
            Compress-With7zZstd -SourcePath $folder -OutputName $7zOutputPath -SevenZipPath $7zPath -LogBox $textBoxLog -ProgressBar $progressBar -VolumeSizeMB $volumeSizeMB
        }
        
        if ($files.Count -gt 0) {
            $textBoxLog.AppendText("`r`n注意: 7z-zstd 模式跳过了 $($files.Count) 个单独文件`r`n")
        }
    }
    
    # tar.zst 压缩（分别压缩PC和安卓）
    if ($checkboxTar.Checked -and $tarZstdAvailable -and $7zPath) {
        $textBoxLog.AppendText("`r`n--- tar.zst 压缩（分离PC和安卓）---`r`n")
        
        # 获取基础名称
        $tarBaseName = $textBoxTarName.Text.Trim()
        if ([string]::IsNullOrWhiteSpace($tarBaseName)) {
            $tarBaseName = (Get-Date).ToString("yyMMdd_01")
        }
        
        # 移除可能的 .tar.zst 后缀
        $tarBaseName = $tarBaseName -replace '\.tar\.zst$', ''
        
        # 创建输出文件夹
        $tarOutputFolder = Join-Path $outputDir $tarBaseName
        if (Test-Path -LiteralPath $tarOutputFolder) {
            $textBoxLog.AppendText("警告: 输出文件夹 $tarBaseName 已存在，将被覆盖`r`n")
            Remove-Item -LiteralPath $tarOutputFolder -Recurse -Force -ErrorAction SilentlyContinue
        }
        
        try {
            New-Item -Path $tarOutputFolder -ItemType Directory -Force | Out-Null
            $textBoxLog.AppendText("创建输出文件夹: $tarBaseName`r`n")
            
            # 分离文件夹和APK
            $pcItems = @()
            $apkItems = @()
            
            foreach ($item in $listBoxSources.Items) {
                if (Test-Path -LiteralPath $item -PathType Container) {
                    $pcItems += $item
                } elseif ($item -match '\.apk$') {
                    $apkItems += $item
                } else {
                    $pcItems += $item
                }
            }
            
            $pcSuccess = $false
            $azSuccess = $false
            $tempRoot = Get-PlcPakTempRoot -Config $config
            
            # 压缩PC部分（文件夹和非APK文件）
            if ($pcItems.Count -gt 0) {
                $textBoxLog.AppendText("`r`n压缩PC部分（$($pcItems.Count) 个项目）...`r`n")
                
                # 创建临时PC文件夹
                $tempPC = Join-Path $tempRoot "plcpak_pc_$([guid]::NewGuid().ToString('N'))"
                if (Test-Path -LiteralPath $tempPC) {
                    Remove-Item -LiteralPath $tempPC -Recurse -Force -ErrorAction SilentlyContinue
                }
                New-Item -Path $tempPC -ItemType Directory -Force | Out-Null
                
                # 获取脚本目录
                if ($PSScriptRoot) {
                    $scriptDir = $PSScriptRoot
                } elseif ($MyInvocation.MyCommand.Path) {
                    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
                } else {
                    $scriptDir = [System.IO.Path]::GetDirectoryName([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
                }
                
                # 根据频道自购复选框状态选择资源说明文件
                $readmeFileName = if ($checkboxSelfPurchase.Checked) {
                    "资源说明(必读-自购).txt"
                } else {
                    "资源说明(必读).txt"
                }
                $readmeSource = Join-Path $scriptDir $readmeFileName
                
                # 复制PC项目
                foreach ($item in $pcItems) {
                    $itemName = Split-Path $item -Leaf
                    $destPath = Join-Path $tempPC $itemName
                    if (Test-Path -LiteralPath $item -PathType Container) {
                        Copy-Item -LiteralPath $item -Destination $destPath -Recurse -Force
                        
                        # 如果是文件夹，复制资源说明文件到该文件夹
                        $readmeTarget = Join-Path $destPath "资源说明(必读).txt"
                        if (Test-Path -LiteralPath $readmeSource) {
                            if (-not (Test-Path -LiteralPath $readmeTarget)) {
                                try {
                                    Copy-Item -LiteralPath $readmeSource -Destination $readmeTarget -Force
                                    $textBoxLog.AppendText("  已添加 $readmeFileName 到 $itemName`r`n")
                                } catch {
                                    $textBoxLog.AppendText("  警告: 无法复制资源说明文件到 $itemName : $_`r`n")
                                }
                            }
                        }
                    } else {
                        Copy-Item -LiteralPath $item -Destination $destPath -Force
                    }
                    $textBoxLog.AppendText("  添加: $itemName`r`n")
                }
                
                # 压缩PC
                $pcTarName = "$tarBaseName(PC).tar.zst"
                $pcTarPath = Join-Path $tarOutputFolder $pcTarName
                $pcSuccess = Compress-WithTarZst -SourcePath $tempPC -OutputName $pcTarPath -SevenZipPath $7zPath -LogBox $textBoxLog -ProgressBar $progressBar
                
                # 清理临时文件夹
                if (Test-Path -LiteralPath $tempPC) {
                    Remove-Item -LiteralPath $tempPC -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            
            # 压缩安卓部分（APK文件）
            if ($apkItems.Count -gt 0) {
                $textBoxLog.AppendText("`r`n压缩安卓部分（$($apkItems.Count) 个APK）...`r`n")
                
                # 创建临时安卓文件夹
                $tempAZ = Join-Path $tempRoot "plcpak_az_$([guid]::NewGuid().ToString('N'))"
                if (Test-Path -LiteralPath $tempAZ) {
                    Remove-Item -LiteralPath $tempAZ -Recurse -Force -ErrorAction SilentlyContinue
                }
                New-Item -Path $tempAZ -ItemType Directory -Force | Out-Null
                
                # 复制APK文件
                foreach ($item in $apkItems) {
                    $itemName = Split-Path $item -Leaf
                    $destPath = Join-Path $tempAZ $itemName
                    Copy-Item -LiteralPath $item -Destination $destPath -Force
                    $textBoxLog.AppendText("  添加: $itemName`r`n")
                }
                
                # 压缩安卓
                $azTarName = "$tarBaseName(AZ).tar.zst"
                $azTarPath = Join-Path $tarOutputFolder $azTarName
                $azSuccess = Compress-WithTarZst -SourcePath $tempAZ -OutputName $azTarPath -SevenZipPath $7zPath -LogBox $textBoxLog -ProgressBar $progressBar
                
                # 清理临时文件夹
                if (Test-Path -LiteralPath $tempAZ) {
                    Remove-Item -LiteralPath $tempAZ -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            
            # 压缩成功后自动递增编号
            if ($pcSuccess -or $azSuccess) {
                # 复制转存防炸.txt到输出文件夹
                if ($PSScriptRoot) {
                    $scriptDir = $PSScriptRoot
                } elseif ($MyInvocation.MyCommand.Path) {
                    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
                } else {
                    $scriptDir = Get-Location
                }
                $zhuancunFile = Join-Path $scriptDir "转存防炸.txt"
                if (Test-Path -LiteralPath $zhuancunFile) {
                    $destZhuancun = Join-Path $tarOutputFolder "转存防炸.txt"
                    Copy-Item -LiteralPath $zhuancunFile -Destination $destZhuancun -Force
                    $textBoxLog.AppendText("已添加转存防炸.txt到输出文件夹`r`n")
                }
                
                $currentName = $textBoxTarName.Text.Trim()
                if ($currentName -match '^(.+)_(\d+)$') {
                    $prefix = $matches[1]
                    $number = [int]$matches[2]
                    $nextNumber = $number + 1
                    $textBoxTarName.Text = "{0}_{1:D2}" -f $prefix, $nextNumber
                }
            }
            
            $textBoxLog.AppendText("`r`n所有tar.zst文件已保存到: $tarBaseName\`r`n")
        } catch {
            $textBoxLog.AppendText("`r`n[X] tar.zst 打包失败: $_`r`n")
        }
    }
    
    $textBoxLog.AppendText("`r`n===============================================`r`n")
    $textBoxLog.AppendText("       压缩任务完成!`r`n")
    $textBoxLog.AppendText("===============================================`r`n")

    Save-RecentProject -Projects @($listBoxSources.Items)
    
    # 标记已执行压缩（下次拖入新文件时会清空列表）
    $script:hasCompressed = $true
    
    # 重新启用按钮
    $buttonStart.Enabled = $true
    
    [System.Windows.Forms.MessageBox]::Show("压缩任务完成!", "完成", 
        [System.Windows.Forms.MessageBoxButtons]::OK, 
        [System.Windows.Forms.MessageBoxIcon]::Information)
    } catch {
        $textBoxLog.AppendText("`r`n[X] 发生严重错误: $_`r`n")
        $textBoxLog.AppendText("错误详情: $($_.Exception.Message)`r`n")
        $buttonStart.Enabled = $true
        
        [System.Windows.Forms.MessageBox]::Show("压缩过程发生错误:`r`n`r`n$_", "错误", 
            [System.Windows.Forms.MessageBoxButtons]::OK, 
            [System.Windows.Forms.MessageBoxIcon]::Error)
    }
})

# 初始化日志
$textBoxLog.AppendText("===============================================`r`n")
$textBoxLog.AppendText("    PLCPak GUI $script:PlcPakVersion`r`n")
$textBoxLog.AppendText("===============================================`r`n`r`n")

# 显示系统信息
try {
    $os = Get-WmiObject -Class Win32_OperatingSystem
    $cpu = Get-WmiObject -Class Win32_Processor | Select-Object -First 1
    $totalMemoryGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
    $freeMemoryGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
    
    $textBoxLog.AppendText("系统信息:`r`n")
    $textBoxLog.AppendText("  操作系统: $($os.Caption)`r`n")
    $textBoxLog.AppendText("  处理器: $($cpu.Name)`r`n")
    $textBoxLog.AppendText("  CPU核心: $($cpu.NumberOfCores) 核 $($cpu.NumberOfLogicalProcessors) 线程`r`n")
    $textBoxLog.AppendText("  内存: $totalMemoryGB GB 总计, $freeMemoryGB GB 可用`r`n")
    $textBoxLog.AppendText("`r`n")
} catch {
    $textBoxLog.AppendText("无法获取系统信息`r`n`r`n")
}

$textBoxLog.AppendText("检查依赖工具...`r`n")

if ($7zPath) {
    $textBoxLog.AppendText("[OK] 找到 7-Zip: $7zPath`r`n")
    $checkbox7z.Enabled = $true
} else {
    $textBoxLog.AppendText("[X] 未找到 7-Zip,7z-zstd 压缩不可用`r`n")
    $checkbox7z.Enabled = $false
    $checkbox7z.Checked = $false
}

if ($tarZstdAvailable) {
    $textBoxLog.AppendText("[OK] 7-Zip 支持 tar.zst 格式`r`n")
    $checkboxTar.Enabled = $true
} else {
    $textBoxLog.AppendText("[X] 7-Zip 不支持 tar.zst 格式,tar.zst 压缩不可用`r`n")
    $checkboxTar.Enabled = $false
    $checkboxTar.Checked = $false
}

if (Test-ManifestStale -ScriptDir $scriptDir -StaleDays $script:appConfig.ManifestStaleDays) {
    $textBoxLog.AppendText("[提示] 广告清单可能已过期，可运行「更新广告清单.bat」`r`n")
}

$textBoxLog.AppendText("`r`n准备就绪,拖入文件夹将自动预览扫描...`r`n")
if ($script:appConfig.RecentProjects -and $script:appConfig.RecentProjects.Count -gt 0) {
    $textBoxLog.AppendText("最近项目: $($script:appConfig.RecentProjects[0])`r`n")
}

if (Get-Command Test-PlcPakUpdate -ErrorAction SilentlyContinue) {
    $upd = Test-PlcPakUpdate -ScriptDir $scriptDir -CurrentVersion $script:PlcPakVersion
    if ($upd.HasUpdate) {
        $textBoxLog.AppendText("[更新] 发现新版本 $($upd.Latest)`r`n")
    }
}

# CLI 模式
if ($Clean -or $Compress -or $Preview -or $NoGui) {
    $targets = if ($Path) { @($Path) } else { @() }
    if ($targets.Count -eq 0) {
        Write-Error 'CLI 模式需要 -Path 参数'
        exit 1
    }
    $cfg = Load-Config
    Invoke-PlcPakCliPipeline -ScriptDir $scriptDir -Paths $targets -Config $cfg `
        -Preview:($Preview -and -not $Clean) -Clean:$Clean -Compress:$Compress `
        -Force:$Force -Format $Format -VolumeSizeMB $cfg.VolumeSizeMB
    exit $LASTEXITCODE
}

# 窗体关闭时清理资源
$form.Add_FormClosing({
    # 停止定时器
    if ($jobMonitorTimer) {
        $jobMonitorTimer.Stop()
        $jobMonitorTimer.Dispose()
    }
    
    # 清理后台Job
    if ($script:backgroundJobs.Count -gt 0) {
        foreach ($jobInfo in $script:backgroundJobs) {
            try {
                Stop-Job -Job $jobInfo.Job -ErrorAction SilentlyContinue
                Remove-Job -Job $jobInfo.Job -Force -ErrorAction SilentlyContinue
            } catch {}
        }
        $script:backgroundJobs.Clear()
    }
})

Init-AdGui    
[void]$form.ShowDialog()
