param([string]$OutDir = (Join-Path $PSScriptRoot 'manifest-update'))
$src = Join-Path $PSScriptRoot 'AD-Samples\ad-manifest.json'
if (-not (Test-Path -LiteralPath $src)) {
    Write-Error '找不到 ad-manifest.json'
    exit 1
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Copy-Item -LiteralPath $src -Destination (Join-Path $OutDir 'ad-manifest.json') -Force
@"
将 ad-manifest.json 覆盖到 PLCPak 目录下的 AD-Samples\ 即可更新广告清单，无需重下完整包。
"@ | Set-Content -LiteralPath (Join-Path $OutDir '使用说明.txt') -Encoding UTF8
$zip = Join-Path $PSScriptRoot 'ad-manifest-update.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $OutDir '*') -DestinationPath $zip -Force
Write-Host "已生成: $zip"