param(
    [int]$ThresholdKB = 50,
    [switch]$PruneLarge,
    [switch]$NoPrune
)

$ErrorActionPreference = 'Stop'
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$script:scriptDir = $ScriptDir
$scriptDir = $ScriptDir

$modulePath = Join-Path $ScriptDir 'Remove-AdFiles.ps1'
if (-not (Test-Path -LiteralPath $modulePath)) {
    Write-Host '[错误] 找不到 Remove-AdFiles.ps1' -ForegroundColor Red
    exit 1
}
. $modulePath

function Write-ColorLine {
    param([string]$Text, [string]$Color = 'White')
    Write-Host $Text -ForegroundColor $Color
}

Write-ColorLine '============================================' Cyan
Write-ColorLine '  广告样本清单更新工具' Cyan
Write-ColorLine '============================================' Cyan
Write-Host ''

try {
    $logBlock = {
        param([string]$Message, [string]$ColorName = 'White')
        Write-ColorLine $Message $ColorName
    }

    $stats = Sync-AdManifestFromFolder -ThresholdKB $ThresholdKB -RootDir $ScriptDir -OnLog $logBlock
    $manifest = Read-AdManifest -RootDir $ScriptDir

    Write-Host ''
    Write-ColorLine '--------------------------------------------' Cyan
    Write-ColorLine "完成: 新增 $($stats.Added) 条, 更新 $($stats.Updated) 条, 跳过 $($stats.Skipped) 条" Green
    Write-ColorLine "清单: $(Get-AdManifestPath -RootDir $ScriptDir)" White
    Write-ColorLine "合计: $($manifest.fileCount) 个文件签名, $(@($manifest.folders).Count) 个文件夹名" White

    $adRoot = Get-AdSamplesRoot -RootDir $ScriptDir
    $thresholdBytes = $ThresholdKB * 1024
    $large = @(Get-ChildItem -LiteralPath $adRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -gt $thresholdBytes -and -not (Test-AdSkipFile -Name $_.Name -Extension $_.Extension) })

    if ($large.Count -eq 0) {
        Write-ColorLine '没有需要精简的大文件。' DarkGray
        exit 0
    }

    Write-Host ''
    Write-ColorLine "发现 $($large.Count) 个大文件 (> ${ThresholdKB}KB):" Yellow
    foreach ($f in $large) {
        Write-Host ('  - {0} ({1:N1} MB)' -f $f.Name, ($f.Length / 1MB))
    }

    $doPrune = $false
    if ($PruneLarge) {
        $doPrune = $true
    } elseif (-not $NoPrune) {
        $answer = Read-Host '是否删除这些大文件以节省空间? [Y/N]'
        $doPrune = $answer -match '^[Yy]'
    }

    if ($doPrune) {
        $pruneStats = Sync-AdManifestFromFolder -AutoPrune -ThresholdKB $ThresholdKB -RootDir $ScriptDir
        Write-ColorLine "已精简 $($pruneStats.Pruned) 个大文件，检测签名已保留在清单中。" Green
    } else {
        Write-ColorLine '已保留所有实体文件。' DarkGray
    }
} catch {
    Write-ColorLine "[错误] $($_.Exception.Message)" Red
    exit 1
}