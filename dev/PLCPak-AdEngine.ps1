function Get-AdEngineDefaultConfig {
    return @{
        VolumeSizeMB          = 1900
        AdScanTimeoutSec      = 300
        AdConfirmThreshold    = 20
        UseRecycleBin         = $true
        PreviewBeforeClean    = $true
        ParallelHashWorkers   = 4
        UseScanCache          = $true
        HashSkipExtensions    = @('.png', '.jpg', '.jpeg', '.gif', '.bmp', '.webp', '.apk', '.dll', '.exe', '.mp4', '.avi', '.mkv', '.mp3', '.wav', '.zip', '.7z', '.rar', '.zst', '.tar', '.iso', '.unity3d', '.assets', '.bundle')
        HashMaxFileMB         = 50
        CompressThreads       = 0
        TempDir               = ''
        Whitelist             = @()
        RecentProjects        = @()
        SkipStoreCompress     = $true
        ManifestStaleDays     = 7
        AdLikeExtensions      = @('.txt', '.url', '.bat', '.cmd', '.lnk', '.ini', '.html', '.htm', '.pdf', '.doc', '.docx', '.zip', '.apk')
    }
}

function Merge-AdEngineConfig {
    param($Json)
    $defaults = Get-AdEngineDefaultConfig
    if (-not $Json) { return $defaults }
    $out = $defaults.Clone()
    foreach ($key in $defaults.Keys) {
        if ($null -ne $Json.PSObject.Properties[$key]) {
            $val = $Json.$key
            if ($val -is [System.Array] -or $val -is [object[]]) {
                $out[$key] = @($val)
            } elseif ($val -is [bool]) {
                $out[$key] = [bool]$val
            } elseif ($key -match 'MB|Sec|Threshold|Threads|Days|Workers') {
                $out[$key] = [int]$val
            } else {
                $out[$key] = [string]$val
            }
        }
    }
    return $out
}

function Get-FileSHA256Fast {
    param([string]$FilePath)
    try {
        return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256 -ErrorAction Stop).Hash.ToLower()
    } catch {
        return $null
    }
}

function Test-AdWhitelist {
    param([string]$RelativePath, [string[]]$Whitelist)
    if (-not $Whitelist -or $Whitelist.Count -eq 0) { return $false }
    $rel = $RelativePath.Replace('/', '\').ToLower()
    foreach ($w in $Whitelist) {
        if ([string]::IsNullOrWhiteSpace($w)) { continue }
        $pat = $w.Replace('/', '\').ToLower().Trim()
        if ($rel -like $pat -or $rel -eq $pat) { return $true }
    }
    return $false
}

function Get-AdSamplesEnhanced {
    param([string]$ScriptDir)
    $d = Join-Path $ScriptDir 'AD-Samples'
    if (-not (Test-Path -LiteralPath $d)) {
        $d = Join-Path $ScriptDir 'AD-simple'
        if (-not (Test-Path -LiteralPath $d)) { return $null }
    }

    $samples = @{
        FileNames   = @{}
        FileHashes  = @{}
        FolderNames = @{}
        HashOnly    = @{}
    }

    $sf = Get-ChildItem -LiteralPath $d -Force -Recurse -ErrorAction SilentlyContinue |
        Where-Object { -not $_.PSIsContainer -and $_.Name -notin @('README.txt', '示例说明.txt', 'ad-manifest.json', '.keep', '.DS_Store') -and $_.Extension -notin @('.ps1', '.bat', '.cmd', '.vbs') }

    $sd = Get-ChildItem -LiteralPath $d -Force -Recurse -Directory -ErrorAction SilentlyContinue
    if ($sd) {
        foreach ($x in $sd) {
            $k = $x.Name.ToLower()
            if (-not $samples.FolderNames.ContainsKey($k)) { $samples.FolderNames[$k] = $true }
        }
    }

    if ($sf) {
        foreach ($f in $sf) {
            $n = $f.Name.ToLower()
            if (-not $samples.FileNames.ContainsKey($n)) { $samples.FileNames[$n] = @() }
            $samples.FileNames[$n] += $f.FullName
            $h = Get-FileSHA256Fast -FilePath $f.FullName
            if ($h) {
                if (-not $samples.FileHashes.ContainsKey($h)) { $samples.FileHashes[$h] = @() }
                $samples.FileHashes[$h] += $f.FullName
            }
        }
    }

    $mp = Join-Path $d 'ad-manifest.json'
    if (Test-Path -LiteralPath $mp) {
        try {
            $m = Get-Content -LiteralPath $mp -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($x in @($m.folders)) {
                $k = $x.ToLower()
                if (-not $samples.FolderNames.ContainsKey($k)) { $samples.FolderNames[$k] = $true }
            }
            foreach ($e in @($m.files)) {
                $k = $e.name.ToLower()
                if (-not $samples.FileNames.ContainsKey($k)) { $samples.FileNames[$k] = @('manifest') }
                $sha = $e.sha256.ToLower()
                if (-not $samples.FileHashes.ContainsKey($sha)) { $samples.FileHashes[$sha] = @('manifest') }
                $samples.HashOnly[$sha] = $k
            }
        } catch {}
    }

    if ($samples.FileNames.Count -eq 0 -and $samples.FolderNames.Count -eq 0) { return $null }
    return $samples
}

function Get-ScanCachePath {
    param([string]$ScriptDir)
    return Join-Path $ScriptDir 'scan-cache.json'
}

function Test-ScanCacheValid {
    param([string]$TargetPath, [string]$ScriptDir, [hashtable]$Config)
    if (-not $Config.UseScanCache) { return $false }
    $cp = Get-ScanCachePath -ScriptDir $ScriptDir
    if (-not (Test-Path -LiteralPath $cp)) { return $false }
    try {
        $cache = Get-Content -LiteralPath $cp -Raw -Encoding UTF8 | ConvertFrom-Json
        $item = $cache.entries | Where-Object { $_.path -eq $TargetPath } | Select-Object -First 1
        if (-not $item) { return $false }
        $dir = Get-Item -LiteralPath $TargetPath -Force
        return ($item.mtime -eq $dir.LastWriteTimeUtc.Ticks)
    } catch { return $false }
}

function Update-ScanCache {
    param([string]$TargetPath, [string]$ScriptDir, $Stats)
    $cp = Get-ScanCachePath -ScriptDir $ScriptDir
    $entries = @()
    if (Test-Path -LiteralPath $cp) {
        try {
            $old = Get-Content -LiteralPath $cp -Raw -Encoding UTF8 | ConvertFrom-Json
            $entries = @($old.entries | Where-Object { $_.path -ne $TargetPath })
        } catch {}
    }
    $dir = Get-Item -LiteralPath $TargetPath -Force
    $entries += [PSCustomObject]@{
        path   = $TargetPath
        mtime  = $dir.LastWriteTimeUtc.Ticks
        scanned = $Stats.TotalScanned
        matched = $Stats.TotalMatched
        time   = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    }
    @{ version = 1; entries = $entries } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $cp -Encoding UTF8 -Force
}

function Get-FileSHA256Parallel {
    param([System.IO.FileInfo[]]$Files, [int]$Workers = 4)
    $map = @{}
    if (-not $Files -or $Files.Count -eq 0) { return $map }
    if ($Files.Count -le 2 -or $Workers -le 1) {
        foreach ($f in $Files) { $map[$f.FullName] = Get-FileSHA256Fast -FilePath $f.FullName }
        return $map
    }
    $pool = [runspacefactory]::CreateRunspacePool(1, [Math]::Min($Workers, 8))
    $pool.Open()
    $handles = @()
    foreach ($f in $Files) {
        $ps = [powershell]::Create().AddScript({
            param($p)
            (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower()
        }).AddArgument($f.FullName)
        $ps.RunspacePool = $pool
        $handles += [PSCustomObject]@{ File = $f.FullName; Handle = $ps.BeginInvoke() ; PS = $ps }
    }
    foreach ($h in $handles) {
        $map[$h.File] = $h.PS.EndInvoke($h.Handle)
        $h.PS.Dispose()
    }
    $pool.Close()
    $pool.Dispose()
    return $map
}

function Write-AdSessionLog {
    param([string]$ScriptDir, [string]$TargetPath, $Result)
    $lp = Join-Path $ScriptDir '.plcpak-session.json'
    $entry = [PSCustomObject]@{
        time    = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
        target  = $TargetPath
        removed = @($Result.RemovedFiles)
        count   = $Result.TotalRemoved
    }
    $list = @($entry)
    if (Test-Path -LiteralPath $lp) {
        try {
            $old = Get-Content -LiteralPath $lp -Raw -Encoding UTF8 | ConvertFrom-Json
            $list = @($old) + $list | Select-Object -Last 20
        } catch {}
    }
    $list | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $lp -Encoding UTF8 -Force
}

function Test-IsAdFileOptimized {
    param($File, $AdSamples, $Config, $HashMap)
    $name = $File.Name.ToLower()
    if (-not $AdSamples.FileNames.ContainsKey($name)) { return $false }
    $ext = $File.Extension.ToLower()
    if ($Config.HashSkipExtensions -contains $ext) {
        return $true
    }
    $hash = if ($HashMap -and $HashMap.ContainsKey($File.FullName)) { $HashMap[$File.FullName] } else { Get-FileSHA256Fast -FilePath $File.FullName }
    if ($hash -and $AdSamples.FileHashes.ContainsKey($hash)) { return $true }
    return $false
}

function Remove-AdItemSafe {
    param([string]$Path, [bool]$IsDirectory, [bool]$UseRecycleBin)
    if ($IsDirectory) {
        if ($UseRecycleBin) {
            try {
                [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($Path,
                    [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
                    [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
                return 'recycled'
            } catch {}
        }
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        return 'deleted'
    }
    if ($UseRecycleBin) {
        try {
            [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($Path,
                [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
                [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
            return 'recycled'
        } catch {}
    }
    Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
    return 'deleted'
}

function Invoke-AdCleanup {
    param(
        [string]$TargetPath,
        [string]$ScriptDir,
        [hashtable]$Config,
        [switch]$PreviewOnly
    )

    Add-Type -AssemblyName Microsoft.VisualBasic -ErrorAction SilentlyContinue | Out-Null

    $adSamples = Get-AdSamplesEnhanced -ScriptDir $ScriptDir
    $stats = @{
        TotalScanned = 0
        TotalMatched = 0
        TotalRemoved = 0
        RemovedFiles = [System.Collections.ArrayList]::new()
        MatchedFiles = [System.Collections.ArrayList]::new()
        Errors       = [System.Collections.ArrayList]::new()
    }

    if (-not $adSamples) { return $stats }

    if ($Config.UseScanCache -and $PreviewOnly -and (Test-ScanCacheValid -TargetPath $TargetPath -ScriptDir $ScriptDir -Config $Config)) {
        $cp = Get-ScanCachePath -ScriptDir $ScriptDir
        $cache = Get-Content -LiteralPath $cp -Raw -Encoding UTF8 | ConvertFrom-Json
        $item = $cache.entries | Where-Object { $_.path -eq $TargetPath } | Select-Object -First 1
        $stats.TotalScanned = [int]$item.scanned
        $stats.TotalMatched = [int]$item.matched
        if ($item.matched -gt 0) { [void]$stats.MatchedFiles.Add('(缓存) 上次扫描匹配项') }
        return $stats
    }

    if ($adSamples.FolderNames.Count -gt 0) {
        try {
            $allDirs = Get-ChildItem -LiteralPath $TargetPath -Force -Recurse -Directory -ErrorAction SilentlyContinue |
                Where-Object { -not ($_.Attributes -match 'ReparsePoint') } |
                Sort-Object { $_.FullName.Length } -Descending
            foreach ($dir in $allDirs) {
                $rel = $dir.FullName.Substring($TargetPath.Length).TrimStart('\', '/')
                if (Test-AdWhitelist -RelativePath $rel -Whitelist $Config.Whitelist) { continue }
                if ($adSamples.FolderNames.ContainsKey($dir.Name.ToLower())) {
                    [void]$stats.MatchedFiles.Add("[文件夹] $rel")
                    if (-not $PreviewOnly) {
                        try {
                            $mode = Remove-AdItemSafe -Path $dir.FullName -IsDirectory $true -UseRecycleBin $Config.UseRecycleBin
                            [void]$stats.RemovedFiles.Add("[文件夹] $rel ($mode)")
                            $stats.TotalRemoved++
                        } catch {
                            [void]$stats.Errors.Add("无法删除文件夹: $($dir.FullName) - $_")
                        }
                    }
                    $stats.TotalMatched++
                }
            }
        } catch {
            [void]$stats.Errors.Add("扫描文件夹失败: $_")
        }
    }

    try {
        $allFiles = Get-ChildItem -LiteralPath $TargetPath -Force -Recurse -ErrorAction SilentlyContinue |
            Where-Object { -not $_.PSIsContainer -and -not ($_.Attributes -match 'ReparsePoint') }
        if ($allFiles) {
            $stats.TotalScanned = $allFiles.Count
            $candidates = @($allFiles | Where-Object { $adSamples.FileNames.ContainsKey($_.Name.ToLower()) })
            $workers = if ($Config.ParallelHashWorkers) { [int]$Config.ParallelHashWorkers } else { 4 }
            $hashMap = Get-FileSHA256Parallel -Files $candidates -Workers $workers
            foreach ($file in $candidates) {
                $rel = $file.FullName.Substring($TargetPath.Length).TrimStart('\', '/')
                if (Test-AdWhitelist -RelativePath $rel -Whitelist $Config.Whitelist) { continue }
                if (Test-IsAdFileOptimized -File $file -AdSamples $adSamples -Config $Config -HashMap $hashMap) {
                    [void]$stats.MatchedFiles.Add($rel)
                    $stats.TotalMatched++
                    if (-not $PreviewOnly) {
                        try {
                            $mode = Remove-AdItemSafe -Path $file.FullName -IsDirectory $false -UseRecycleBin $Config.UseRecycleBin
                            [void]$stats.RemovedFiles.Add("$rel ($mode)")
                            $stats.TotalRemoved++
                        } catch {
                            [void]$stats.Errors.Add("无法删除: $($file.FullName) - $_")
                        }
                    }
                }
            }
        }
    } catch {
        [void]$stats.Errors.Add("扫描文件失败: $_")
    }

    if (-not $PreviewOnly -and $stats.TotalRemoved -gt 0) {
        $emptyDirs = Get-ChildItem -LiteralPath $TargetPath -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { -not ($_.Attributes -match 'ReparsePoint') } |
            Where-Object { (Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue).Count -eq 0 } |
            Sort-Object { $_.FullName.Length } -Descending
        foreach ($dir in $emptyDirs) {
            try { Remove-AdItemSafe -Path $dir.FullName -IsDirectory $true -UseRecycleBin $Config.UseRecycleBin | Out-Null } catch {}
        }
    }

    if ($PreviewOnly -and $Config.UseScanCache) {
        Update-ScanCache -TargetPath $TargetPath -ScriptDir $ScriptDir -Stats $stats
    }

    return $stats
}

function Test-ManifestStale {
    param([string]$ScriptDir, [int]$StaleDays)
    $mp = Join-Path $ScriptDir 'AD-Samples\ad-manifest.json'
    if (-not (Test-Path -LiteralPath $mp)) { return $false }
    try {
        $m = Get-Content -LiteralPath $mp -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($m.lastUpdated) {
            $dt = [datetime]::Parse($m.lastUpdated)
            return ((Get-Date) - $dt).TotalDays -gt $StaleDays
        }
    } catch {}
    return $false
}