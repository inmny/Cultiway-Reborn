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
# 编辑文本留在仓库，发行包只带直接读取的网格；摘要检查防止发布过期的转换结果。
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $modsRoot 'Content/Worldfall/Models') -Filter 'models.json' -Recurse -File) {
    $binaryPath = [IO.Path]::ChangeExtension($source.FullName, '.bin')
    if (-not (Test-Path -LiteralPath $binaryPath)) {
        throw "缺少模型包，请先运行 python Scripts/Worldfall/compile_model_files.py：$($source.FullName)"
    }
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($binaryPath))
    try {
        if ($reader.ReadUInt32() -ne 0x4D465743 -or $reader.ReadInt32() -ne 1) { throw "模型包格式不符：$binaryPath" }
        $savedDigest = [BitConverter]::ToString($reader.ReadBytes(32)).Replace('-', '')
    }
    finally { $reader.Dispose() }
    # 与转换脚本一致，忽略 Git 检出时的换行差异和文本 BOM。
    $sourceText = [IO.File]::ReadAllText($source.FullName).Replace("`r`n", "`n").Replace("`r", "`n")
    $digestAlgorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $sourceDigest = [BitConverter]::ToString($digestAlgorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($sourceText))).Replace('-', '')
    }
    finally { $digestAlgorithm.Dispose() }
    if ($savedDigest -ne $sourceDigest) {
        throw "模型包需要更新，请先运行 python Scripts/Worldfall/compile_model_files.py：$($source.FullName)"
    }
}

if (-not $OutDir) { $OutDir = Join-Path $modsRoot 'artifacts' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$zipPath = Join-Path $OutDir "Cultiway-$version.zip"

# 5. 替换同版本输出包，不修改工作区中的源码或资源。
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# 6. 保留运行目录结构，排除符号、编辑来源和备份；Source/Debug 中有启动必需的源码，不能整目录排除。
Add-Type -AssemblyName System.IO.Compression.FileSystem
$excluded = [Collections.Generic.List[string]]::new()
$fileCount = 0
$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $entries) {
        $path = Join-Path $modsRoot $entry
        if (Test-Path -LiteralPath $path -PathType Container) {
            $null = $archive.CreateEntry($entry + '/')
            foreach ($directory in Get-ChildItem -LiteralPath $path -Directory -Recurse) {
                $name = $directory.FullName.Substring($modsRoot.Length + 1).Replace('\', '/')
                $null = $archive.CreateEntry($name + '/')
            }
        }
        foreach ($file in Get-ChildItem -LiteralPath $path -File -Recurse) {
            $name = $file.FullName.Substring($modsRoot.Length + 1).Replace('\', '/')
            if (($entry -eq 'Assemblies' -and $file.Extension -ne '.dll') -or
                $file.Name -match '(?i)\.(pdb|mdb|meta|bak|old|tmp|temp|log|xlsx|pyc|psd|blend\d*)$' -or
                $file.Name -match '(?i)^(README(?:\..*)?|placeholder(?:_readme\.txt)?)$' -or
                $name.StartsWith('Content/PreparedLLMResults/', [StringComparison]::OrdinalIgnoreCase) -or
                ($name.StartsWith('Content/Worldfall/Models/', [StringComparison]::OrdinalIgnoreCase) -and $file.Name -eq 'models.json')) {
                $excluded.Add($name)
                continue
            }
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
            $fileCount++
        }
    }
    $instructions = $archive.CreateEntry('安装说明.txt')
    $writer = [IO.StreamWriter]::new($instructions.Open(), [Text.UTF8Encoding]::new($false))
    try {
        $writer.Write(@"
修仙[重制] $version 安装说明

1. 先在 WorldBox 中正确安装并启用 NeoModLoader。
2. 退出游戏，将本压缩包解压到 WorldBox/Mods/Cultiway-Reborn/。
3. 确认该文件夹中直接包含 mod.json、Source、Content、GameResources 等，避免多嵌套一层文件夹。
4. 更新安装时先移走旧版文件夹，再解压新包；不要同时保留两份修仙模组。
5. 重新启动游戏，等待加载器编译并加载模组。包内源码是运行所需文件。

三维外观需要另行安装 Worldfall；本包包含修仙自有三维模型，不包含 Worldfall 本体。
本包沿用 mod.json 的预览版说明，存档功能仍按预览版状态处理。
"@)
    }
    finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

# 7. 汇报结果
$sizeMb = '{0:N2}' -f ((Get-Item -LiteralPath $zipPath).Length / 1MB)
Write-Host "已生成: $zipPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "版本号 : $version"
Write-Host "运行文件: $fileCount；另附安装说明"
Write-Host "已排除  : $($excluded.Count) 个调试或编辑文件"
if ($excluded.Count) { $excluded | ForEach-Object { Write-Host "  $_" } }
