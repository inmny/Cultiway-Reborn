<# 从 Windows 环境变量读取密钥，调用 Tripo；密钥不放入命令参数或输出文件。 #>
[CmdletBinding()]
param(
    [ValidateSet('Plan','Balance','Generate','Resume','Task')][string]$Mode = 'Plan',
    [string]$AssetId = 'Dark_tree',
    [string]$InputFile = 'Art/Worldfall/Dark_tree/reference/tripo-v1/tripo-input.png',
    [string]$OutputDirectory = 'Art/Worldfall/Dark_tree/tripo-api-v1',
    [ValidateSet('cn','global')][string]$Region = 'cn',
    [string]$TaskId
)
$ErrorActionPreference = 'Stop'
$previousTaskKey = $env:TRIPO_API_KEY
try {
    if ($Mode -ne 'Plan' -and -not $env:TRIPO_API_KEY) {
        $env:TRIPO_API_KEY = [Environment]::GetEnvironmentVariable('TRIPO_API_KEY', 'User')
        if (-not $env:TRIPO_API_KEY) {
            $env:TRIPO_API_KEY = [Environment]::GetEnvironmentVariable('TRIPO_API_KEY', 'Machine')
        }
    }
    $taskArguments = @((Join-Path $PSScriptRoot 'tripo-api.mjs'), '--mode', $Mode.ToLowerInvariant(),
        '--asset', $AssetId, '--input', $InputFile, '--output', $OutputDirectory, '--region', $Region)
    if ($TaskId) { $taskArguments += @('--task', $TaskId) }
    & node @taskArguments
    if ($LASTEXITCODE -ne 0) { throw "Tripo command failed ($LASTEXITCODE)." }
}
finally { $env:TRIPO_API_KEY = $previousTaskKey }
