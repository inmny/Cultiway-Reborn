# pack.ps1 — 打包 Cultiway 发布包
# 用法: pwsh -NoProfile -File pack.ps1 [-OutDir <目录>]
# 输出: artifacts/Cultiway-<version>.zip（zip 内 mod.json 位于根层级，可直接作为 WorldBox mod 安装）
[CmdletBinding()]
param(
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
$modsRoot = $PSScriptRoot
Set-Location -LiteralPath $modsRoot

# 1. 从 mod.json 读取版本号
$modJsonPath = Join-Path $modsRoot 'mod.json'
$modJson = Get-Content -Raw -LiteralPath $modJsonPath | ConvertFrom-Json
$version = $modJson.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "无法从 mod.json 读取 version 字段"
}

# 2. 待打包条目（目录与文件）
$entries = @(
    'Assemblies',
    'Content',
    'GameResources',
    'Licenses',
    'Locales',
    'Source',
    'mod.json',
    'icon.png',
    'default_config.json',
    'LICENSE'
)

# 3. 校验条目存在
$missing = foreach ($e in $entries) {
    if (-not (Test-Path -LiteralPath (Join-Path $modsRoot $e))) { $e }
}
if ($missing) {
    throw "缺少以下条目，无法打包: $($missing -join ', ')"
}

# 4. 确定输出路径
if (-not $OutDir) { $OutDir = Join-Path $modsRoot 'artifacts' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$zipPath = Join-Path $OutDir "Cultiway-$version.zip"

# 5. 删除旧包（Compress-Archive 对已存在的 zip 会报错或追加，需先清理）
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# 6. 打包（直接列出条目，确保它们位于 zip 根层级）
$paths = $entries | ForEach-Object { Join-Path $modsRoot $_ }
Compress-Archive -Path $paths -DestinationPath $zipPath -CompressionLevel Optimal

# 7. 汇报结果
$sizeMb = '{0:N2}' -f ((Get-Item -LiteralPath $zipPath).Length / 1MB)
Write-Host "已生成: $zipPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "版本号 : $version"
Write-Host "条目数 : $($entries.Count)"
