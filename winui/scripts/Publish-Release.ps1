# 发布 PLCPak WinUI v4.0 到 dist 目录（共享 data/ + scripts/ + logs/ + workspace/）
param(
    [string]$Configuration = 'Release',
    [string]$DevAssets = 'F:\BaiduNetdiskDownload\PLCPak\dev'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$releasesDir = Join-Path (Split-Path -Parent $root) 'releases'
$sharedData = Join-Path $dist 'data'
$sharedScripts = Join-Path $dist 'scripts'
$sharedLogs = Join-Path $dist 'logs'
$sharedWorkspace = Join-Path $dist 'workspace'
$guiOut = Join-Path $dist 'PLCPak.WinUI'
$cliOut = Join-Path $dist 'PLCPak.Cli'
$guiApp = Join-Path $guiOut 'app'
$cliApp = Join-Path $cliOut 'app'

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $guiApp, $cliApp, $sharedData, $sharedScripts, $sharedLogs, $sharedWorkspace -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $sharedWorkspace 'inbox'), (Join-Path $sharedWorkspace 'extract'), (Join-Path $sharedWorkspace 'output'), (Join-Path $sharedWorkspace 'jobs'), (Join-Path $sharedWorkspace 'published') -Force | Out-Null

Push-Location $root
try {
    dotnet publish src\PLCPak.WinUI\PLCPak.WinUI.csproj -c $Configuration -p:Platform=x64 -r win-x64 --self-contained true -o $guiApp
    dotnet publish src\PLCPak.Cli\PLCPak.Cli.csproj -c $Configuration -r win-x64 --self-contained true -o $cliApp
} finally { Pop-Location }

$dataItems = @(
    '7-Zip-Zstandard',
    'AD-Samples',
    'Password-Samples',
    'compress-config.json',
    'version.json',
    'studio-config.json',
    'publish-templates.json',
    '资源说明(必读).txt',
    '资源说明(必读-自购).txt',
    '转存防炸.txt'
)

$scriptItems = @(
    'UpdateManifest.ps1',
    'UpdateManifest.bat',
    '更新广告清单.bat',
    'Remove-AdFiles.ps1'
)

foreach ($item in $dataItems) {
    $src = Join-Path $DevAssets $item
    if (-not (Test-Path -LiteralPath $src)) { continue }
    $dest = Join-Path $sharedData $item
    if (Test-Path -LiteralPath $src -PathType Container) {
        Copy-Item -LiteralPath $src -Destination $dest -Recurse -Force
    } else {
        Copy-Item -LiteralPath $src -Destination $dest -Force
    }
}

foreach ($item in $scriptItems) {
    $src = Join-Path $DevAssets $item
    if (-not (Test-Path -LiteralPath $src)) { continue }
    Copy-Item -LiteralPath $src -Destination (Join-Path $sharedScripts $item) -Force
}

$manifestScript = Join-Path $sharedScripts 'UpdateManifest.ps1'
if (Test-Path -LiteralPath $manifestScript) {
    $content = Get-Content -LiteralPath $manifestScript -Raw -Encoding UTF8
    $content = $content -replace '(?ms)\$ScriptDir = if \(\$PSScriptRoot\).*?\$scriptDir = \$ScriptDir', @'
$HelperDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ScriptDir = (Resolve-Path (Join-Path $HelperDir '..\data')).Path
$script:scriptDir = $ScriptDir
$scriptDir = $ScriptDir
'@
    $content = $content -replace "Join-Path \$ScriptDir 'Remove-AdFiles.ps1'", "Join-Path `$HelperDir 'Remove-AdFiles.ps1'"
    Set-Content -LiteralPath $manifestScript -Value $content -Encoding UTF8
}

$vf = Join-Path $sharedData 'version.json'
if (Test-Path -LiteralPath $vf) {
    $v = Get-Content -LiteralPath $vf -Raw -Encoding UTF8 | ConvertFrom-Json
    $v.version = '1.0.72'
    $v.latestVersion = '1.0.72'
    $v.releaseNotes = 'v1.0.72——修复暗色主题卡片/文字不可见、新手引导与命令面板'
    $v | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $vf -Encoding UTF8
}

@'
@echo off
chcp 65001 >nul
cd /d "%~dp0app"
start "" "%~dp0app\PLCPak.WinUI.exe"
'@ | Set-Content -LiteralPath (Join-Path $guiOut '启动 PLCPak.bat') -Encoding ASCII

@'
PLCPak v1.0.72 WinUI 目录说明
==========================

根目录（你看到的这一层）:
  启动 PLCPak.bat     推荐用这个启动
  启动说明.txt        本文件
  CHANGELOG.md        版本更新日志（Markdown，v10.8.0 起完整记录）
  app/                程序本体（exe 与运行库，请勿单独挪动 exe）

共享资源（与 CLI 共用，在上级 dist 目录）:
  ..\data/            配置、7-Zip、广告样本、资源说明
  ..\scripts/         维护脚本（更新广告清单等）
  ..\logs/            运行日志（自动生成）
  ..\workspace/       任务工作区（inbox/extract/output/jobs）

注意:
  1. 请保持整个 dist 文件夹完整（含 PLCPak.WinUI、PLCPak.Cli、data、scripts）
  2. 自包含发布，一般无需单独安装 .NET
  3. 若无法启动，查看 ..\logs\plcpak-startup.log

命令行版: ..\PLCPak.Cli\预览样本.bat
'@ | Set-Content -LiteralPath (Join-Path $guiOut '启动说明.txt') -Encoding UTF8

@'
@echo off
chcp 65001 >nul
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0UpdateManifest.ps1"
if errorlevel 1 pause
'@ | Set-Content -LiteralPath (Join-Path $sharedScripts '更新广告清单.bat') -Encoding ASCII

@'
@echo off
chcp 65001 >nul
cd /d "%~dp0app"
PLCPak.Cli.exe -Preview -Path "..\..\data\AD-Samples" -NoGui
'@ | Set-Content -LiteralPath (Join-Path $cliOut '预览样本.bat') -Encoding ASCII

@'
@echo off
chcp 65001 >nul
cd /d "%~dp0app"
PLCPak.Cli.exe %*
if errorlevel 1 pause
'@ | Set-Content -LiteralPath (Join-Path $cliOut 'PLCPak-CLI.bat') -Encoding ASCII

@'
PLCPak CLI v1.0.72
===============

  app/              命令行程序本体
  预览样本.bat      快速测试（使用共享 data\AD-Samples）
  PLCPak-CLI.bat    通用命令行入口

示例:
  PLCPak-CLI.bat -Preview -Path "D:\游戏" -NoGui
  PLCPak-CLI.bat -Preview -Path "D:\游戏" -Json

GUI 版: ..\PLCPak.WinUI\启动 PLCPak.bat
'@ | Set-Content -LiteralPath (Join-Path $cliOut '启动说明.txt') -Encoding UTF8

$changelogMd = Join-Path $releasesDir 'CHANGELOG.md'
if (-not (Test-Path -LiteralPath $changelogMd)) {
    Write-Warning "更新日志缺失，跳过: $changelogMd"
} else {
    Copy-Item -LiteralPath $changelogMd -Destination (Join-Path $dist 'CHANGELOG.md') -Force
}

if (-not (Test-Path -LiteralPath $releasesDir)) { New-Item -ItemType Directory -Path $releasesDir -Force | Out-Null }
$zip = Join-Path $releasesDir 'PLCPak_v1.0.72_WinUI.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -Force

Write-Host '发布完成:'
Write-Host "  GUI: $guiOut"
Write-Host "  CLI: $cliOut"
Write-Host "  共享 data: $sharedData"
Write-Host "  更新日志: $(Join-Path $dist 'CHANGELOG.md')"
Write-Host "  ZIP: $zip"
Write-Host ''
Write-Host 'dist 根目录:'
Get-ChildItem -LiteralPath $dist -Force | Select-Object Name, Mode | Format-Table -AutoSize