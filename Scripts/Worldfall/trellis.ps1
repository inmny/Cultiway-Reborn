# Trellis 原生管线：安装、检查或对单个物体直接生成带贴图网格。
[CmdletBinding()]
param(
    [ValidateSet('Setup', 'Check', 'Generate')][string]$Mode = 'Check',
    [ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$AssetId = 'Cultiway.SectHall',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Variant = 'default',
    [ValidateRange(1, 99)][int]$Revision = 1,
    [string]$PythonExecutable
)
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'trellis-config.json') -Raw | ConvertFrom-Json
$runtime = Join-Path $taskRoot $config.runtimeDirectory
$models = Join-Path $taskRoot $config.modelDirectory
$python = Join-Path $taskRoot $config.python
$cli = Join-Path $runtime 'trellis-cli.exe'

if ($Mode -eq 'Setup') {
    $env:PYTHONIOENCODING = 'utf-8'
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    $archive = Join-Path $taskRoot 'Tools/Trellis/downloads/trellis-cuda-v0.8.1.zip'
    New-Item -ItemType Directory -Path (Split-Path -Parent $archive) -Force | Out-Null
    if (-not (Test-Path -LiteralPath $archive)) {
        Write-Output '下载 Trellis Windows CUDA 运行包'
        Invoke-WebRequest -Uri $config.runtimeUrl -OutFile ($archive + '.part')
        if ((Get-FileHash -LiteralPath ($archive + '.part') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $config.runtimeSha256) { throw '运行包校验不符' }
        Move-Item -LiteralPath ($archive + '.part') -Destination $archive
    }
    if (-not (Test-Path -LiteralPath $cli)) {
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $config.runtimeSha256) { throw '运行包校验不符' }
        Expand-Archive -LiteralPath $archive -DestinationPath $runtime
    }
    if (-not (Test-Path -LiteralPath $python)) {
        if (-not $PythonExecutable) { throw '首次安装请用 -PythonExecutable 指定 Python 3.12 或更新版本的可执行文件' }
        & $PythonExecutable -m venv (Join-Path $taskRoot 'Tools/Trellis/.venv')
        if ($LASTEXITCODE -ne 0) { throw '建立独立 Python 下载环境失败' }
    }
    & $python -m pip --isolated install --disable-pip-version-check --no-cache-dir --progress-bar off -r (Join-Path $PSScriptRoot 'trellis-requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw '安装权重下载依赖失败' }
    & $python (Join-Path $PSScriptRoot 'download_trellis.py')
    if ($LASTEXITCODE -ne 0) { throw 'Trellis 模型下载未完成' }
}
if ($Mode -ne 'Generate') {
    $missing = @($config.models | Where-Object { -not (Test-Path -LiteralPath (Join-Path $models $_.name)) })
    [ordered]@{ Version=$config.runtimeVersion; RuntimeReady=(Test-Path -LiteralPath $cli); Quantization=$config.quantization; Resolution=$config.resolution; MissingModels=@($missing | ForEach-Object { $_.name }) } | ConvertTo-Json
    exit 0
}
if (-not (Test-Path -LiteralPath $cli)) { throw '先运行 -Mode Setup' }
foreach ($model in $config.models) {
    if (-not (Test-Path -LiteralPath (Join-Path $models $model.name))) { throw ('缺少模型：' + $model.name) }
}
$assetRoot = Join-Path $taskRoot ('Art/Worldfall/' + $AssetId)
$version = if ($Variant -eq 'default') { 'v' + $Revision } else { $Variant + '-v' + $Revision }
$inputRelative = 'reference/trellis-input-' + $version + '.png'
$outputRelative = 'trellis/generated-' + $version + '.glb'
$inputImage = Join-Path $assetRoot $inputRelative
if (-not (Test-Path -LiteralPath $inputImage)) { throw '先使用 imagegen 生成此物体的单物体输入图' }
$outputDirectory = Join-Path $assetRoot 'trellis'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$output = Join-Path $assetRoot $outputRelative
if (Test-Path -LiteralPath $output) { throw ('已有生成结果：' + $output) }
$gpuLease = New-Object System.Threading.Mutex($false, 'CultiwayTrellisGeneration')
$acquired = $false
try {
    Write-Output '等待 Trellis 显卡任务'
    $acquired = $gpuLease.WaitOne()
    if (Get-Process -Name 'worldbox' -ErrorAction SilentlyContinue) { throw '请先关闭测试游戏，避免 Worldfall 与 Trellis 同时占用显存' }
    Write-Output ('开始生成：' + $AssetId)
    & $cli $inputImage $output --models $models --res $config.resolution --seed $config.seed --steps $config.steps --atlas $config.atlas --webp off --require-gpu --verbose
    if ($LASTEXITCODE -ne 0) { throw ('Trellis 生成失败：' + $LASTEXITCODE) }
    if (-not (Test-Path -LiteralPath $output)) { throw 'Trellis 未产生网格文件' }
    $record = [ordered]@{ Asset=$AssetId; Variant=$Variant; Revision=$Revision; Generator='pwilkin/trellis.cpp'; Version=$config.runtimeVersion; Quantization=$config.quantization; Resolution=$config.resolution; Seed=$config.seed; Steps=$config.steps; Atlas=$config.atlas; TextureEncoding='PNG'; Input=$inputRelative; Output=$outputRelative; InputSha256=(Get-FileHash -LiteralPath $inputImage).Hash; OutputSha256=(Get-FileHash -LiteralPath $output).Hash; Bytes=(Get-Item -LiteralPath $output).Length; Notes='原始直出网格；国家色、占地、动作部件与近远档由本物体的独立 Blender 子代理适配。' }
    [System.IO.File]::WriteAllText((Join-Path $outputDirectory ('generation-' + $version + '.json')), ($record | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
    Write-Output $output
}
finally {
    if ($acquired) { $gpuLease.ReleaseMutex() }
    $gpuLease.Dispose()
}
