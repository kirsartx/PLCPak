function Show-PlcPakSettingsDialog {
    param([hashtable]$Config, [string]$ConfigPath, [scriptblock]$OnSave)

    $dlg = New-Object System.Windows.Forms.Form
    $dlg.Text = 'PLCPak 设置'
    $dlg.Size = New-Object System.Drawing.Size(480, 520)
    $dlg.StartPosition = 'CenterParent'
    $dlg.FormBorderStyle = 'FixedDialog'
    $dlg.MaximizeBox = $false

    $y = 15
    function Add-Field($label, $value, $width = 320) {
        $lbl = New-Object System.Windows.Forms.Label
        $lbl.Location = New-Object System.Drawing.Point(15, $y)
        $lbl.Size = New-Object System.Drawing.Size(120, 22)
        $lbl.Text = $label
        $dlg.Controls.Add($lbl)
        $tb = New-Object System.Windows.Forms.TextBox
        $tb.Location = New-Object System.Drawing.Point(140, $y - 2)
        $tb.Size = New-Object System.Drawing.Size($width, 22)
        $tb.Text = [string]$value
        $dlg.Controls.Add($tb)
        $script:y += 32
        return $tb
    }

    $tbVol = Add-Field '分卷大小(MB)' $Config.VolumeSizeMB 100
    $tbTimeout = Add-Field '扫描超时(秒)' $Config.AdScanTimeoutSec 100
    $tbThreshold = Add-Field '确认阈值' $Config.AdConfirmThreshold 100
    $tbThreads = Add-Field '压缩线程(0自动)' $Config.CompressThreads 100
    $tbTemp = Add-Field '临时目录' $Config.TempDir
    $tbStale = Add-Field '清单过期(天)' $Config.ManifestStaleDays 100

    $cbRecycle = New-Object System.Windows.Forms.CheckBox
    $cbRecycle.Location = New-Object System.Drawing.Point(140, $y)
    $cbRecycle.Text = '删除到回收站'
    $cbRecycle.Checked = [bool]$Config.UseRecycleBin
    $dlg.Controls.Add($cbRecycle)
    $y += 28

    $cbSkip = New-Object System.Windows.Forms.CheckBox
    $cbSkip.Location = New-Object System.Drawing.Point(140, $y)
    $cbSkip.Text = '已压缩文件存储模式(-ms=on)'
    $cbSkip.Checked = [bool]$Config.SkipStoreCompress
    $dlg.Controls.Add($cbSkip)
    $y += 28

    $cbPreview = New-Object System.Windows.Forms.CheckBox
    $cbPreview.Location = New-Object System.Drawing.Point(140, $y)
    $cbPreview.Text = '一键执行前预览确认'
    $cbPreview.Checked = [bool]$Config.PreviewBeforeClean
    $dlg.Controls.Add($cbPreview)
    $y += 36

    $lblW = New-Object System.Windows.Forms.Label
    $lblW.Location = New-Object System.Drawing.Point(15, $y)
    $lblW.Text = '白名单(每行一条)'
    $dlg.Controls.Add($lblW)
    $y += 22
    $tbWhite = New-Object System.Windows.Forms.TextBox
    $tbWhite.Location = New-Object System.Drawing.Point(15, $y)
    $tbWhite.Size = New-Object System.Drawing.Size(430, 80)
    $tbWhite.Multiline = $true
    $tbWhite.ScrollBars = 'Vertical'
    $tbWhite.Text = ($Config.Whitelist -join "`r`n")
    $dlg.Controls.Add($tbWhite)
    $y += 95

    $btnOk = New-Object System.Windows.Forms.Button
    $btnOk.Location = New-Object System.Drawing.Point(270, $y)
    $btnOk.Size = New-Object System.Drawing.Size(80, 30)
    $btnOk.Text = '保存'
    $dlg.Controls.Add($btnOk)

    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Location = New-Object System.Drawing.Point(360, $y)
    $btnCancel.Size = New-Object System.Drawing.Size(80, 30)
    $btnCancel.Text = '取消'
    $btnCancel.DialogResult = 'Cancel'
    $dlg.Controls.Add($btnCancel)

    $btnOk.Add_Click({
        $newCfg = $Config.Clone()
        $newCfg.VolumeSizeMB = [int]$tbVol.Text
        $newCfg.AdScanTimeoutSec = [int]$tbTimeout.Text
        $newCfg.AdConfirmThreshold = [int]$tbThreshold.Text
        $newCfg.CompressThreads = [int]$tbThreads.Text
        $newCfg.TempDir = $tbTemp.Text.Trim()
        $newCfg.ManifestStaleDays = [int]$tbStale.Text
        $newCfg.UseRecycleBin = $cbRecycle.Checked
        $newCfg.SkipStoreCompress = $cbSkip.Checked
        $newCfg.PreviewBeforeClean = $cbPreview.Checked
        $newCfg.Whitelist = @($tbWhite.Text -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $newCfg | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ConfigPath -Encoding UTF8 -Force
        if ($OnSave) { & $OnSave $newCfg }
        $dlg.DialogResult = 'OK'
        $dlg.Close()
    })

    [void]$dlg.ShowDialog()
}