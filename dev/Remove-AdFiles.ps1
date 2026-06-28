function Get-FileSHA256 {
    param([string]$FilePath)
    try {
        return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256 -ErrorAction Stop).Hash
    } catch {
        return $null
    }
}

function Get-ToolRoot {
    if ($script:scriptDir) { return $script:scriptDir }
    if ($scriptDir) { return $scriptDir }
    if ($PSScriptRoot) { return $PSScriptRoot }
    return [System.IO.Path]::GetDirectoryName([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
}

$script:AdSampleSkipNames = @(
    'README.txt', '示例说明.txt', 'ad-manifest.json', '.keep', '.DS_Store',
    'Remove-AdFiles.ps1', 'AdSampleExtension.ps1', '文件压缩工具.ps1', 'PLCPak.ps1', 'PLCPak_v1.7.4.ps1', '更新广告清单.ps1'
)
$script:AdSampleSkipExtensions = @('.ps1', '.bat', '.cmd', '.vbs')

function Get-AdSamplesRoot {
    param([string]$RootDir)
    if (-not $RootDir) { $RootDir = Get-ToolRoot }
    $dir = Join-Path $RootDir 'AD-Samples'
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -Path $dir -ItemType Directory -Force | Out-Null
    }
    return $dir
}

function Get-AdManifestPath {
    param([string]$RootDir)
    return Join-Path (Get-AdSamplesRoot -RootDir $RootDir) 'ad-manifest.json'
}

function ConvertTo-ManifestFileList {
    param($Files)
    $list = New-Object System.Collections.ArrayList
    if ($Files) {
        foreach ($item in @($Files)) { [void]$list.Add($item) }
    }
    return $list
}

function Read-AdManifest {
    param([string]$RootDir)
    $manifestPath = Get-AdManifestPath -RootDir $RootDir
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return [PSCustomObject]@{
            version     = 1
            description = '广告样本哈希清单'
            fileCount   = 0
            folders     = @()
            files       = (New-Object System.Collections.ArrayList)
        }
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $manifest.files = ConvertTo-ManifestFileList -Files $manifest.files
    if (-not $manifest.folders) { $manifest.folders = @() }
    return $manifest
}

function Save-AdManifest {
    param($Manifest, [string]$RootDir)
    $fileList = @($Manifest.files | Sort-Object { -$_.size }, { $_.name.ToLower() })
    $folderList = @($Manifest.folders | Sort-Object)
    $output = [PSCustomObject]@{
        version       = $Manifest.version
        description   = $Manifest.description
        generatedFrom = if ($Manifest.generatedFrom) { $Manifest.generatedFrom } else { '手动更新' }
        fileCount     = $fileList.Count
        folders       = $folderList
        files         = $fileList
        lastUpdated   = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    }
    $json = $output | ConvertTo-Json -Depth 6
    $path = Get-AdManifestPath -RootDir $RootDir
    $utf8 = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($path, $json, $utf8)
}

function Test-AdSkipFile {
    param([string]$Name, [string]$Extension)
    if ($script:AdSampleSkipNames -contains $Name) { return $true }
    if ($script:AdSampleSkipExtensions -contains $Extension.ToLower()) { return $true }
    return $false
}

function Add-AdFolderName {
    param($FolderSet, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Name)) { return }
    $key = $Name.ToLower()
    if (-not $FolderSet.ContainsKey($key)) { $FolderSet[$key] = $true }
}

function Register-AdSampleFile {
    param($File, $Manifest, $ExistingByKey, $FolderSet, [string]$RootDir)
    $sha = Get-FileSHA256 -FilePath $File.FullName
    if (-not $sha) { return @{ Added = 0; Updated = 0 } }
    $sha = $sha.ToLower()
    $key = ('{0}|{1}' -f $File.Name.ToLower(), $sha)
    $adRoot = Get-AdSamplesRoot -RootDir $RootDir
    $rel = $File.FullName.Substring($adRoot.Length).TrimStart('\', '/')
    $samplePath = ''
    if ($rel.Contains('\') -or $rel.Contains('/')) {
        $samplePath = (Split-Path $rel -Parent).Replace('\', '/')
    }

    $parentDir = Split-Path $File.FullName -Parent
    while ($parentDir -and $parentDir.Length -ge $adRoot.Length) {
        Add-AdFolderName -FolderSet $FolderSet -Name (Split-Path $parentDir -Leaf)
        if ($parentDir -eq $adRoot) { break }
        $parentDir = Split-Path $parentDir -Parent
    }

    if ($ExistingByKey.ContainsKey($key)) {
        $old = $ExistingByKey[$key]
        if ($old.samplePath -ne $samplePath -or [int]$old.size -ne [int]$File.Length) {
            $old.samplePath = $samplePath
            $old.size = [int]$File.Length
            return @{ Added = 0; Updated = 1; Duplicate = $false; File = $File }
        }
        return @{ Added = 0; Updated = 0; Duplicate = $true; File = $File }
    }

    $entry = [PSCustomObject]@{
        name       = $File.Name
        sha256     = $sha
        size       = [int]$File.Length
        samplePath = $samplePath
    }
    [void]$Manifest.files.Add($entry)
    $ExistingByKey[$key] = $entry
    return @{ Added = 1; Updated = 0; Duplicate = $false; File = $File }
}

function Import-AdSamplePaths {
    param([string[]]$Paths, [switch]$AutoPrune, [int]$ThresholdKB = 50, [scriptblock]$OnLog)
    $root = Get-ToolRoot
    $manifest = Read-AdManifest -RootDir $root
    $existingByKey = @{}
    foreach ($entry in @($manifest.files)) {
        $existingByKey[('{0}|{1}' -f $entry.name.ToLower(), $entry.sha256.ToLower())] = $entry
    }
    $folderSet = @{}
    foreach ($f in @($manifest.folders)) { Add-AdFolderName -FolderSet $folderSet -Name $f }

    $imported = New-Object System.Collections.ArrayList
    $stats = @{ Added = 0; Updated = 0; Skipped = 0; Pruned = 0; Copied = 0; Errors = @() }
    $threshold = $ThresholdKB * 1024
    $adRoot = Get-AdSamplesRoot -RootDir $root

    foreach ($inputPath in $Paths) {
        if (-not (Test-Path -LiteralPath $inputPath)) {
            $stats.Errors += "路径不存在: $inputPath"
            continue
        }
        if (Test-Path -LiteralPath $inputPath -PathType Container) {
            Add-AdFolderName -FolderSet $folderSet -Name (Split-Path $inputPath -Leaf)
            $destDir = Join-Path $adRoot (Split-Path $inputPath -Leaf)
            if (-not (Test-Path -LiteralPath $destDir)) { New-Item -Path $destDir -ItemType Directory -Force | Out-Null }
            foreach ($src in (Get-ChildItem -LiteralPath $inputPath -Recurse -File -Force -ErrorAction SilentlyContinue)) {
                if (Test-AdSkipFile -Name $src.Name -Extension $src.Extension) { continue }
                $rel = $src.FullName.Substring($inputPath.Length).TrimStart('\', '/')
                $dest = Join-Path $destDir $rel
                $parent = Split-Path $dest -Parent
                if (-not (Test-Path -LiteralPath $parent)) { New-Item -Path $parent -ItemType Directory -Force | Out-Null }
                Copy-Item -LiteralPath $src.FullName -Destination $dest -Force
                [void]$imported.Add($dest)
                $stats.Copied++
            }
        } else {
            $leaf = Split-Path $inputPath -Leaf
            if (Test-AdSkipFile -Name $leaf -Extension ([IO.Path]::GetExtension($leaf))) { continue }
            $dest = Join-Path $adRoot $leaf
            Copy-Item -LiteralPath $inputPath -Destination $dest -Force
            [void]$imported.Add($dest)
            $stats.Copied++
        }
    }

    foreach ($filePath in $imported) {
        $file = Get-Item -LiteralPath $filePath -Force
        $result = Register-AdSampleFile -File $file -Manifest $manifest -ExistingByKey $existingByKey -FolderSet $folderSet -RootDir $root
        if ($result.Duplicate) { $stats.Skipped++; continue }
        $stats.Added += $result.Added
        $stats.Updated += $result.Updated
        if ($result.Added -gt 0 -and $OnLog) { & $OnLog "[样本+] $($file.Name)" "Green" }
        if ($AutoPrune -and $file.Length -gt $threshold) {
            Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue
            $stats.Pruned++
            if ($OnLog) { & $OnLog "[样本-] 已精简: $($file.Name)" "Magenta" }
        }
    }

    $manifest.folders = @($folderSet.Keys)
    Save-AdManifest -Manifest $manifest -RootDir $root
    return $stats
}

function Sync-AdManifestFromFolder {
    param([switch]$AutoPrune, [int]$ThresholdKB = 50, [string]$RootDir, [scriptblock]$OnLog)
    if (-not $RootDir) { $RootDir = Get-ToolRoot }
    $adRoot = Get-AdSamplesRoot -RootDir $RootDir
    $manifest = Read-AdManifest -RootDir $RootDir
    $existingByKey = @{}
    foreach ($entry in @($manifest.files)) {
        $existingByKey[('{0}|{1}' -f $entry.name.ToLower(), $entry.sha256.ToLower())] = $entry
    }
    $folderSet = @{}
    foreach ($f in @($manifest.folders)) { Add-AdFolderName -FolderSet $folderSet -Name $f }
    foreach ($dir in (Get-ChildItem -LiteralPath $adRoot -Recurse -Directory -Force -ErrorAction SilentlyContinue)) {
        Add-AdFolderName -FolderSet $folderSet -Name $dir.Name
    }

    $stats = @{ Added = 0; Updated = 0; Skipped = 0; Pruned = 0 }
    $threshold = $ThresholdKB * 1024
    $pruneList = @()

    foreach ($file in (Get-ChildItem -LiteralPath $adRoot -Recurse -File -Force -ErrorAction SilentlyContinue)) {
        if (Test-AdSkipFile -Name $file.Name -Extension $file.Extension) { continue }
        $result = Register-AdSampleFile -File $file -Manifest $manifest -ExistingByKey $existingByKey -FolderSet $folderSet -RootDir $RootDir
        if ($result.Duplicate) { $stats.Skipped++; continue }
        $stats.Added += $result.Added
        $stats.Updated += $result.Updated
        if ($result.Added -gt 0 -and $OnLog) { & $OnLog "[样本+] $($file.Name)" "Green" }
        if ($file.Length -gt $threshold) { $pruneList += $file }
    }

    $manifest.folders = @($folderSet.Keys)
    Save-AdManifest -Manifest $manifest -RootDir $RootDir
    if ($AutoPrune) {
        foreach ($file in $pruneList) {
            Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue
            $stats.Pruned++
        }
    }
    return $stats
}

function Update-AdSampleCountLabel {
    if (-not $script:labelAdSampleCount) { return }
    try {
        $manifest = Read-AdManifest
        $count = if ($manifest.fileCount) { [int]$manifest.fileCount } else { @($manifest.files).Count }
        $script:labelAdSampleCount.Text = "清单: $count 个签名 / $(@($manifest.folders).Count) 个文件夹"
    } catch {
        $script:labelAdSampleCount.Text = '清单: 读取失败'
    }
}

function Set-UiButtonStyle {
    param($Button, $BackColorArgb, $ForeColorArgb, [int]$Height = 28, [switch]$Bold)
    if (-not $Button) { return }
    $back = [System.Drawing.Color]::FromArgb($BackColorArgb[0], $BackColorArgb[1], $BackColorArgb[2])
    $fore = [System.Drawing.Color]::FromArgb($ForeColorArgb[0], $ForeColorArgb[1], $ForeColorArgb[2])
    $Button.FlatStyle = 'Flat'
    $Button.FlatAppearance.BorderSize = 0
    $Button.BackColor = $back
    $Button.ForeColor = $fore
    $Button.Height = $Height
    $Button.Cursor = 'Hand'
    if ($Bold) {
        $Button.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5, [System.Drawing.FontStyle]::Bold)
    } else {
        $Button.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)
    }
}

function Set-UiTextBoxStyle {
    param($TextBox)
    if (-not $TextBox) { return }
    $TextBox.BorderStyle = 'FixedSingle'
    $TextBox.BackColor = [System.Drawing.Color]::White
    $TextBox.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)
}

function Move-ControlBy {
    param($Control, [int]$DeltaY = 0, [int]$DeltaX = 0)
    if (-not $Control) { return }
    $Control.Location = New-Object System.Drawing.Point(
        ($Control.Location.X + $DeltaX),
        ($Control.Location.Y + $DeltaY))
}

function Get-ScriptValue {
    param([string]$Name)
    $v = Get-Variable -Name $Name -ErrorAction SilentlyContinue
    if ($v) { return $v.Value }
    return $null
}

function Move-SourceFoldersFirst {
    if (-not (Get-Variable -Name listBoxSources -ErrorAction SilentlyContinue)) { return }
    if (-not $listBoxSources -or $listBoxSources.Items.Count -lt 2) { return }
    $folders = New-Object System.Collections.ArrayList
    $others = New-Object System.Collections.ArrayList
    foreach ($item in $listBoxSources.Items) {
        if (Test-Path -LiteralPath $item -PathType Container) {
            [void]$folders.Add($item)
        } else {
            [void]$others.Add($item)
        }
    }
    if ($folders.Count -eq 0) { return }
    if ($listBoxSources.Items[0] -eq $folders[0]) { return }
    $listBoxSources.BeginUpdate()
    try {
        $listBoxSources.Items.Clear()
        foreach ($item in $folders) { [void]$listBoxSources.Items.Add($item) }
        foreach ($item in $others) { [void]$listBoxSources.Items.Add($item) }
    } finally {
        $listBoxSources.EndUpdate()
    }
}

function Invoke-UiStep {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
    } catch {
        throw "步骤[$Name]: $_"
    }
}

function Clear-StartupLogNoise {
    if (-not (Get-Variable -Name textBoxLog -Scope Script -ErrorAction SilentlyContinue)) { return }
    $raw = $textBoxLog.Text
    if ([string]::IsNullOrWhiteSpace($raw)) { return }
    $keep = New-Object System.Collections.ArrayList
    $skipSys = $false
    foreach ($line in ($raw -split "`r?`n")) {
        if ($line -match '^=+$') { continue }
        if ($line -match '文件压缩打包工具\s*-\s*GUI') { continue }
        if ($line -match 'PLCPak\s*-\s*GUI') { continue }
        if ($line -match '^\[UI\]') { continue }
        if ($line -match '^系统信息') { $skipSys = $true; continue }
        if ($skipSys) {
            if ([string]::IsNullOrWhiteSpace($line)) { $skipSys = $false }
            continue
        }
        if ($line -match '^\s*(操作系统|处理器|CPU核心|内存):') { continue }
        [void]$keep.Add($line)
    }
    $text = (($keep | ForEach-Object { $_ }) -join "`r`n").Trim()
    if ($text) { $text += "`r`n" }
    $textBoxLog.Text = $text
}

function Init-AdGui {
    if ($script:AdSampleGuiInitialized) { return }
    if (-not (Get-Variable -Name form -Scope Script -ErrorAction SilentlyContinue)) { return }
    try {
        Init-AdGuiCore
        Clear-StartupLogNoise
        $script:AdSampleGuiInitialized = $true
    } catch {
        if (Get-Command Log-Message -ErrorAction SilentlyContinue) {
            Log-Message "[UI] 界面优化加载失败: $_" "Red"
        }
    }
}

$script:PlcPakVersion = 'v2.0.0'

function Init-AdGuiCore {
    $uiBg     = [System.Drawing.Color]::FromArgb(241, 245, 250)
    $uiCard   = [System.Drawing.Color]::White
    $uiHeader = [System.Drawing.Color]::FromArgb(22, 56, 96)
    $uiMuted  = [System.Drawing.Color]::FromArgb(100, 116, 139)
    $uiLogBg  = [System.Drawing.Color]::FromArgb(248, 250, 252)
    $uiFont   = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5)
    $uiFontSm = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)
    $pad      = 14
    $contentW = 772

    Invoke-UiStep '窗体' {
        $form.Text = "PLCPak $script:PlcPakVersion"
        $form.Size = New-Object System.Drawing.Size(800, 940)
        $form.BackColor = $uiBg
        $form.Font = $uiFont
    }

    Invoke-UiStep '标题栏' {
        $header = New-Object System.Windows.Forms.Panel
        $header.Dock = 'Top'
        $header.Height = 52
        $header.BackColor = $uiHeader
        $form.Controls.Add($header)

        $title = New-Object System.Windows.Forms.Label
        $title.AutoSize = $true
        $title.Location = New-Object System.Drawing.Point(18, 14)
        $title.Text = 'PLCPak'
        $title.ForeColor = [System.Drawing.Color]::White
        $title.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 14, [System.Drawing.FontStyle]::Bold)
        $header.Controls.Add($title)

        $versionLabel = New-Object System.Windows.Forms.Label
        $versionLabel.AutoSize = $true
        $versionLabel.Location = New-Object System.Drawing.Point(120, 20)
        $versionLabel.Text = $script:PlcPakVersion
        $versionLabel.ForeColor = [System.Drawing.Color]::FromArgb(186, 210, 235)
        $versionLabel.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5)
        $header.Controls.Add($versionLabel)
    }

    Invoke-UiStep '源项目区' {
        if (Get-Variable -Name labelRecent -ErrorAction SilentlyContinue) {
            $labelRecent.Location = New-Object System.Drawing.Point($pad, 66)
            $labelRecent.Font = $uiFontSm
            $labelRecent.ForeColor = $uiMuted
        }
        if (Get-Variable -Name comboRecent -ErrorAction SilentlyContinue) {
            $comboRecent.Location = New-Object System.Drawing.Point(88, 63)
            $comboRecent.Size = New-Object System.Drawing.Size(420, 24)
            $comboRecent.Font = $uiFontSm
        }
        if (Get-Variable -Name buttonSettings -ErrorAction SilentlyContinue) {
            $buttonSettings.Location = New-Object System.Drawing.Point(520, 61)
            $buttonSettings.Size = New-Object System.Drawing.Size(88, 28)
            Set-UiButtonStyle $buttonSettings @(100, 116, 139) @(255, 255, 255) 28
        }
        if (Get-Variable -Name labelTaskStatus -ErrorAction SilentlyContinue) {
            $labelTaskStatus.Location = New-Object System.Drawing.Point($pad, 92)
            $labelTaskStatus.Font = $uiFontSm
            $labelTaskStatus.ForeColor = $uiMuted
        }
        if (Get-Variable -Name textBoxTaskStatus -ErrorAction SilentlyContinue) {
            $textBoxTaskStatus.Location = New-Object System.Drawing.Point(88, 90)
            $textBoxTaskStatus.Size = New-Object System.Drawing.Size(520, 22)
            $textBoxTaskStatus.BackColor = $uiCard
            $textBoxTaskStatus.Font = $uiFontSm
            $textBoxTaskStatus.ForeColor = $uiHeader
        }

        $labelSource.Location = New-Object System.Drawing.Point($pad, 118)
        $labelSource.Size = New-Object System.Drawing.Size(560, 22)
        $labelSource.Text = '待压缩项目'
        $labelSource.ForeColor = $uiHeader
        $labelSource.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5, [System.Drawing.FontStyle]::Bold)

        $listBoxSources.Location = New-Object System.Drawing.Point($pad, 144)
        $listBoxSources.Size = New-Object System.Drawing.Size(548, 96)
        $listBoxSources.BackColor = $uiCard
        $listBoxSources.BorderStyle = 'FixedSingle'
        $listBoxSources.Font = $uiFont

        $buttonAddFolder.Location = New-Object System.Drawing.Point(580, 144)
        $buttonAddFolder.Size = New-Object System.Drawing.Size(196, 32)
        $buttonAddFolder.Text = '添加文件夹'
        Set-UiButtonStyle $buttonAddFolder @(37, 99, 235) @(255, 255, 255) 32

        $buttonAddFile.Location = New-Object System.Drawing.Point(580, 184)
        $buttonAddFile.Size = New-Object System.Drawing.Size(196, 32)
        $buttonAddFile.Text = '添加文件'
        Set-UiButtonStyle $buttonAddFile @(37, 99, 235) @(255, 255, 255) 32

        $buttonRemove.Location = New-Object System.Drawing.Point(580, 224)
        $buttonRemove.Size = New-Object System.Drawing.Size(196, 32)
        $buttonRemove.Text = '移除选中'
        Set-UiButtonStyle $buttonRemove @(239, 68, 68) @(255, 255, 255) 32

        $labelTotalSize.Location = New-Object System.Drawing.Point($pad, 248)
        $labelTotalSize.Size = New-Object System.Drawing.Size(760, 22)
        $labelTotalSize.Font = $uiFontSm
        $labelTotalSize.ForeColor = $uiMuted
        $labelTotalSize.BackColor = $uiBg

        $checkboxSelfPurchase.Location = New-Object System.Drawing.Point($pad, 276)
        $checkboxSelfPurchase.Size = New-Object System.Drawing.Size(360, 24)
        $checkboxSelfPurchase.Text = '频道自购（使用自购版资源说明）'
        $checkboxSelfPurchase.Font = $uiFontSm
        $checkboxSelfPurchase.ForeColor = $uiMuted

        $groupBox7z.Location = New-Object System.Drawing.Point($pad, 308)
        $groupBox7z.Size = New-Object System.Drawing.Size($contentW, 124)
        $groupBox7z.Text = '7z-zstd 压缩'
        $groupBox7z.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5, [System.Drawing.FontStyle]::Bold)
        $groupBox7z.ForeColor = $uiHeader
        $groupBox7z.BackColor = $uiCard

        $groupBoxTar.Location = New-Object System.Drawing.Point($pad, 444)
        $groupBoxTar.Size = New-Object System.Drawing.Size($contentW, 96)
        $groupBoxTar.Text = 'tar.zst 压缩'
        $groupBoxTar.Font = $groupBox7z.Font
        $groupBoxTar.ForeColor = $uiHeader
        $groupBoxTar.BackColor = $uiCard

        if ($checkbox7z) {
            $checkbox7z.Location = New-Object System.Drawing.Point(20, 26)
            $checkbox7z.Size = New-Object System.Drawing.Size(120, 24)
            $checkbox7z.Text = '启用 7z 压缩'
            $checkbox7z.Font = $uiFontSm
            $checkbox7z.ForeColor = $uiHeader
        }
        if ($checkboxTar) {
            $checkboxTar.Location = New-Object System.Drawing.Point(20, 26)
            $checkboxTar.Size = New-Object System.Drawing.Size(120, 24)
            $checkboxTar.Text = '启用 tar 压缩'
            $checkboxTar.Font = $uiFontSm
            $checkboxTar.ForeColor = $uiHeader
        }
        if ($label7zName) {
            $label7zName.Location = New-Object System.Drawing.Point(20, 56)
            $label7zName.Size = New-Object System.Drawing.Size(92, 22)
            $label7zName.ForeColor = $uiMuted
            $label7zName.Font = $uiFontSm
        }
        if ($labelTarName) {
            $labelTarName.Location = New-Object System.Drawing.Point(20, 56)
            $labelTarName.Size = New-Object System.Drawing.Size(92, 22)
            $labelTarName.ForeColor = $uiMuted
            $labelTarName.Font = $uiFontSm
        }
        if ($textBox7zName) {
            $textBox7zName.Location = New-Object System.Drawing.Point(118, 54)
            $textBox7zName.Size = New-Object System.Drawing.Size(500, 24)
            Set-UiTextBoxStyle $textBox7zName
            if ($textBox7zName.Text -eq '默认使用文件夹名称') { $textBox7zName.ForeColor = $uiMuted }
        }
        if ($textBoxTarName) {
            $textBoxTarName.Location = New-Object System.Drawing.Point(118, 54)
            $textBoxTarName.Size = New-Object System.Drawing.Size(500, 24)
            Set-UiTextBoxStyle $textBoxTarName
        }
        if ($label7zExt) { $label7zExt.Location = New-Object System.Drawing.Point(624, 58); $label7zExt.ForeColor = $uiMuted; $label7zExt.Font = $uiFontSm }
        if ($labelTarExt) { $labelTarExt.Location = New-Object System.Drawing.Point(624, 58); $labelTarExt.ForeColor = $uiMuted; $labelTarExt.Font = $uiFontSm }
        if ($labelVolumeSize) {
            $labelVolumeSize.Location = New-Object System.Drawing.Point(20, 88)
            $labelVolumeSize.Size = New-Object System.Drawing.Size(92, 22)
            $labelVolumeSize.ForeColor = $uiMuted
            $labelVolumeSize.Font = $uiFontSm
        }
        if ($textBoxVolumeSize) { $textBoxVolumeSize.Location = New-Object System.Drawing.Point(118, 86); $textBoxVolumeSize.Size = New-Object System.Drawing.Size(88, 24); Set-UiTextBoxStyle $textBoxVolumeSize }
        if ($labelVolumeTip) {
            $labelVolumeTip.Location = New-Object System.Drawing.Point(218, 90)
            $labelVolumeTip.Size = New-Object System.Drawing.Size(200, 20)
            $labelVolumeTip.Text = '超出自动分卷'
            $labelVolumeTip.Font = $uiFontSm
            $labelVolumeTip.ForeColor = $uiMuted
        }
    }

    Invoke-UiStep '广告样本库' {
        $adY = 550
        $groupBoxAdSamples = New-Object System.Windows.Forms.GroupBox
        $groupBoxAdSamples.Location = New-Object System.Drawing.Point($pad, $adY)
        $groupBoxAdSamples.Size = New-Object System.Drawing.Size($contentW, 108)
        $groupBoxAdSamples.Text = '广告样本库'
        $groupBoxAdSamples.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5, [System.Drawing.FontStyle]::Bold)
        $groupBoxAdSamples.ForeColor = $uiHeader
        $groupBoxAdSamples.BackColor = $uiCard
        $form.Controls.Add($groupBoxAdSamples)

        $panelAdDrop = New-Object System.Windows.Forms.Panel
        $panelAdDrop.Location = New-Object System.Drawing.Point(18, 30)
        $panelAdDrop.Size = New-Object System.Drawing.Size(736, 38)
        $panelAdDrop.AllowDrop = $true
        $panelAdDrop.BorderStyle = 'FixedSingle'
        $panelAdDrop.BackColor = $uiCard
        $groupBoxAdSamples.Controls.Add($panelAdDrop)

        $labelAdDrop = New-Object System.Windows.Forms.Label
        $labelAdDrop.Dock = 'Fill'
        $labelAdDrop.TextAlign = 'MiddleCenter'
        $labelAdDrop.Font = $uiFontSm
        $labelAdDrop.ForeColor = $uiMuted
        $labelAdDrop.Text = '拖入广告样本文件或文件夹'
        $panelAdDrop.Controls.Add($labelAdDrop)

        $buttonOpenAdFolder = New-Object System.Windows.Forms.Button
        $buttonOpenAdFolder.Location = New-Object System.Drawing.Point(18, 76)
        $buttonOpenAdFolder.Size = New-Object System.Drawing.Size(108, 28)
        $buttonOpenAdFolder.Text = '打开样本夹'
        Set-UiButtonStyle $buttonOpenAdFolder @(37, 99, 235) @(255, 255, 255) 28
        $groupBoxAdSamples.Controls.Add($buttonOpenAdFolder)

        $buttonSyncAdSamples = New-Object System.Windows.Forms.Button
        $buttonSyncAdSamples.Location = New-Object System.Drawing.Point(134, 76)
        $buttonSyncAdSamples.Size = New-Object System.Drawing.Size(108, 28)
        $buttonSyncAdSamples.Text = '扫描更新'
        Set-UiButtonStyle $buttonSyncAdSamples @(37, 99, 235) @(255, 255, 255) 28
        $groupBoxAdSamples.Controls.Add($buttonSyncAdSamples)

        $checkboxAutoPrune = New-Object System.Windows.Forms.CheckBox
        $checkboxAutoPrune.Location = New-Object System.Drawing.Point(260, 80)
        $checkboxAutoPrune.Size = New-Object System.Drawing.Size(200, 22)
        $checkboxAutoPrune.Text = '大文件仅保留签名'
        $checkboxAutoPrune.Checked = $true
        $checkboxAutoPrune.Font = $uiFontSm
        $checkboxAutoPrune.ForeColor = $uiMuted
        $groupBoxAdSamples.Controls.Add($checkboxAutoPrune)

        $script:labelAdSampleCount = New-Object System.Windows.Forms.Label
        $script:labelAdSampleCount.Location = New-Object System.Drawing.Point(480, 80)
        $script:labelAdSampleCount.Size = New-Object System.Drawing.Size(270, 22)
        $script:labelAdSampleCount.Font = $uiFontSm
        $script:labelAdSampleCount.ForeColor = $uiMuted
        $groupBoxAdSamples.Controls.Add($script:labelAdSampleCount)

        $labelLog.Visible = $false

        $progressBar.Location = New-Object System.Drawing.Point($pad, 632)
        $progressBar.Size = New-Object System.Drawing.Size(680, 22)

        if (Get-Variable -Name labelPercent -Scope Script -ErrorAction SilentlyContinue) {
            $script:labelPercent.Location = New-Object System.Drawing.Point(710, 632)
            $script:labelPercent.Size = New-Object System.Drawing.Size(60, 22)
            $script:labelPercent.ForeColor = $uiMuted
            $script:labelPercent.Font = $uiFontSm
        }

        $textBoxLog.Location = New-Object System.Drawing.Point($pad, 662)
        $textBoxLog.Size = New-Object System.Drawing.Size($contentW, 140)
        $textBoxLog.BackColor = $uiLogBg
        $textBoxLog.BorderStyle = 'FixedSingle'
        $textBoxLog.Font = New-Object System.Drawing.Font('Consolas', 10)

        $buttonStart.Location = New-Object System.Drawing.Point(624, 812)
        $buttonStart.Size = New-Object System.Drawing.Size(162, 44)
        $buttonStart.Text = '一键执行'
        Set-UiButtonStyle $buttonStart @(16, 185, 129) @(255, 255, 255) 44 -Bold

        $buttonStart.Add_MouseDown({
            Move-SourceFoldersFirst
        })

        $logBlock = {
            param([string]$Message, [string]$ColorName = 'Black')
            if (Get-Command Log-Message -ErrorAction SilentlyContinue) { Log-Message $Message $ColorName }
        }

        $panelAdDrop.Add_DragEnter({
            param($sender, $e)
            if ($e.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
                $e.Effect = [System.Windows.Forms.DragDropEffects]::Copy
                $labelAdDrop.Text = '松开鼠标以导入样本'
            } else { $e.Effect = [System.Windows.Forms.DragDropEffects]::None }
        })
        $panelAdDrop.Add_DragLeave({ $labelAdDrop.Text = '拖入广告样本文件或文件夹' })
        $panelAdDrop.Add_DragDrop({
            param($sender, $e)
            $labelAdDrop.Text = '拖入广告样本文件或文件夹'
            $paths = $e.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
            if ($paths) {
                try {
                    $stats = Import-AdSamplePaths -Paths $paths -AutoPrune:$checkboxAutoPrune.Checked -OnLog $logBlock
                    Update-AdSampleCountLabel
                    Log-Message "[样本] 导入完成: 新增 $($stats.Added), 更新 $($stats.Updated), 跳过 $($stats.Skipped), 精简 $($stats.Pruned)" 'Blue'
                } catch { Log-Message "[样本错误] $_" 'Red' }
            }
        })
        $buttonOpenAdFolder.Add_Click({
            $dir = Get-AdSamplesRoot
            Log-Message "[样本] 打开: $dir" 'Blue'
            Start-Process -FilePath 'explorer.exe' -ArgumentList $dir
        })
        $buttonSyncAdSamples.Add_Click({
            try {
                $stats = Sync-AdManifestFromFolder -AutoPrune:$checkboxAutoPrune.Checked -OnLog $logBlock
                Update-AdSampleCountLabel
                Log-Message "[样本] 扫描完成: 新增 $($stats.Added), 更新 $($stats.Updated), 精简 $($stats.Pruned)" 'Blue'
            } catch { Log-Message "[样本错误] $_" 'Red' }
        })
        Update-AdSampleCountLabel
    }
}