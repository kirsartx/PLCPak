$script:PipelineTasks = @{}

function Get-PlcPakVersionInfo {
    param([string]$ScriptDir)
    $vf = Join-Path $ScriptDir 'version.json'
    if (Test-Path -LiteralPath $vf) {
        try { return Get-Content -LiteralPath $vf -Raw -Encoding UTF8 | ConvertFrom-Json } catch {}
    }
    return [PSCustomObject]@{ version = '2.0.0'; channel = 'stable'; manifestUrl = ''; toolUrl = '' }
}

function Test-PlcPakUpdate {
    param([string]$ScriptDir, [string]$CurrentVersion)
    $info = Get-PlcPakVersionInfo -ScriptDir $ScriptDir
    if ($info.latestVersion -and $info.latestVersion -ne $CurrentVersion) {
        return [PSCustomObject]@{ HasUpdate = $true; Latest = $info.latestVersion; Notes = $info.releaseNotes; Url = $info.toolUrl }
    }
    return [PSCustomObject]@{ HasUpdate = $false }
}

function Get-PlcPakTempRoot {
    param([hashtable]$Config)
    if ($Config.TempDir -and (Test-Path -LiteralPath $Config.TempDir)) { return $Config.TempDir }
    return $env:TEMP
}

function New-PipelineTask {
    param([string]$Path)
    $script:PipelineTasks[$Path] = [PSCustomObject]@{
        Path       = $Path
        State      = '待扫描'
        Matched    = 0
        Scanned    = 0
        ScanResult = $null
        Cleaned    = $false
        Compressed = $false
    }
}

function Set-PipelineTaskFromScan {
    param([string]$Path, $Result)
    if (-not $script:PipelineTasks.ContainsKey($Path)) { New-PipelineTask -Path $Path }
    $t = $script:PipelineTasks[$Path]
    $t.ScanResult = $Result
    $t.Scanned = $Result.TotalScanned
    $t.Matched = $Result.TotalMatched
    if ($Result.TotalMatched -gt 0) {
        $t.State = '待确认'
    } else {
        $t.State = '无广告'
    }
}

function Get-PipelineTaskSummary {
    $lines = New-Object System.Collections.ArrayList
    foreach ($k in $script:PipelineTasks.Keys) {
        $t = $script:PipelineTasks[$k]
        [void]$lines.Add("$($t.State) | 匹配$($t.Matched) | $(Split-Path $k -Leaf)")
    }
    return ($lines -join "`r`n")
}

function Confirm-PipelineCleanup {
    param([hashtable]$Config, [switch]$Force)
    $need = @($script:PipelineTasks.Values | Where-Object { $_.Matched -gt 0 -and -not $_.Cleaned })
    if ($need.Count -eq 0) { return $true }
    $total = ($need | Measure-Object -Property Matched -Sum).Sum
    if ($Force) { return $true }
    if ($Config.PreviewBeforeClean -or $total -ge $Config.AdConfirmThreshold) {
        $msg = "共 $($need.Count) 个文件夹、$total 项广告待删除。`r`n`r`n" + (Get-PipelineTaskSummary) + "`r`n`r`n确认删除？"
        return ([System.Windows.Forms.MessageBox]::Show($msg, 'PLCPak 清理确认', 'YesNo', 'Warning') -eq 'Yes')
    }
    return $true
}

function Invoke-PipelineCleanAll {
    param([string]$ScriptDir, [hashtable]$Config)
    foreach ($t in @($script:PipelineTasks.Values)) {
        if ($t.Matched -le 0 -or $t.Cleaned) { continue }
        if (-not (Test-Path -LiteralPath $t.Path -PathType Container)) { continue }
        $r = Invoke-AdCleanup -TargetPath $t.Path -ScriptDir $ScriptDir -Config $Config
        Write-AdSessionLog -ScriptDir $ScriptDir -TargetPath $t.Path -Result $r
        $t.Cleaned = $true
        $t.State = '已清理'
    }
}

function Test-ArchiveIntegrity {
    param([string]$SevenZipPath, [string]$ArchivePath)
    if (-not (Test-Path -LiteralPath $ArchivePath)) { return $false }
    $out = & $SevenZipPath t $ArchivePath 2>&1
    return ($LASTEXITCODE -eq 0)
}

function Invoke-PlcPakCliPipeline {
    param(
        [string]$ScriptDir,
        [string[]]$Paths,
        [hashtable]$Config,
        [switch]$Preview,
        [switch]$Clean,
        [switch]$Compress,
        [string]$Format = '7z',
        [switch]$Force,
        [int]$VolumeSizeMB = 1900
    )
    $7z = Join-Path $ScriptDir '7-Zip-Zstandard\7z.exe'
    foreach ($p in $Paths) {
        if (-not (Test-Path -LiteralPath $p)) { Write-Error "不存在: $p"; continue }
        New-PipelineTask -Path $p
        if ($Preview -or $Clean) {
            $pv = Invoke-AdCleanup -TargetPath $p -ScriptDir $ScriptDir -Config $Config -PreviewOnly:$Preview
            Set-PipelineTaskFromScan -Path $p -Result $pv
            Write-Host "[$p] 扫描=$($pv.TotalScanned) 匹配=$($pv.TotalMatched)"
            if ($Preview) {
                foreach ($m in @($pv.MatchedFiles)) { Write-Host "  [将删] $m" }
            }
        }
        if ($Clean -and -not $Preview) {
            if ($pv.TotalMatched -ge $Config.AdConfirmThreshold -and -not $Force) {
                $a = Read-Host "匹配 $($pv.TotalMatched) 项，输入 Y 确认删除"
                if ($a -notmatch '^[Yy]') { continue }
            }
            $r = Invoke-AdCleanup -TargetPath $p -ScriptDir $ScriptDir -Config $Config
            Write-AdSessionLog -ScriptDir $ScriptDir -TargetPath $p -Result $r
            Write-Host "[$p] 已删=$($r.TotalRemoved)"
        }
        if ($Compress -and (Test-Path -LiteralPath $p -PathType Container) -and (Test-Path -LiteralPath $7z)) {
            $name = Split-Path $p -Leaf
            $outDir = Split-Path $p -Parent
            if ($Format -match '7z') {
                $out = Join-Path $outDir "$name.7z"
                $args = @('a', '-t7z', '-m0=zstd', '-mx=11', '-mmt=4', $out, $p)
                if ($Config.SkipStoreCompress) { $args = @('a', '-t7z', '-m0=zstd', '-mx=11', '-mmt=4', '-ms=on', $out, $p) }
                & $7z @args | Out-Null
                $ok = Test-ArchiveIntegrity -SevenZipPath $7z -ArchivePath $out
                Write-Host "[$p] 7z => $out $(if($ok){'[校验OK]'}else{'[校验失败]'})"
            }
        }
    }
}