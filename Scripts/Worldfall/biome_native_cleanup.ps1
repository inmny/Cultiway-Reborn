# 仅清除本批正式资源的重复候选和Blender自动备份；不移除来源、预览或其他版本。
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskBatch = [System.IO.Path]::GetFullPath((Join-Path $taskRoot 'Art/Worldfall'))
$taskProgressPath = Join-Path $taskBatch 'biome-native-v1/progress.json'
$taskProgress = Get-Content -LiteralPath $taskProgressPath -Raw | ConvertFrom-Json
$taskDeleted = @()
foreach ($taskAsset in $taskProgress.assets | Where-Object { $_.status -eq '离线发布' }) {
    $taskDirectory = [System.IO.Path]::GetFullPath((Join-Path $taskBatch ($taskAsset.asset + '/native-biome-v1')))
    if (-not $taskDirectory.StartsWith($taskBatch + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw '清理路径超出本批Art目录。' }
    $taskProduction = Join-Path $taskRoot ('Content/Worldfall/Models/' + $taskAsset.asset + '/models.json')
    $taskCandidate = Join-Path $taskDirectory 'models.candidate.json'
    if (Test-Path -LiteralPath $taskCandidate) {
        $taskCandidateHash = (Get-FileHash -LiteralPath $taskCandidate -Algorithm SHA256).Hash
        $taskProductionHash = (Get-FileHash -LiteralPath $taskProduction -Algorithm SHA256).Hash
        if ($taskCandidateHash -ne $taskProductionHash) { throw ('候选并非正式资源副本，保留：' + $taskAsset.asset) }
        $taskDeleted += [ordered]@{path=$taskCandidate; bytes=(Get-Item -LiteralPath $taskCandidate).Length; reason='与正式资源相同的候选JSON副本'}
        Remove-Item -LiteralPath $taskCandidate
    }
    foreach ($taskBackup in Get-ChildItem -LiteralPath $taskDirectory -Filter '*.blend1' -File) {
        $taskDeleted += [ordered]@{path=$taskBackup.FullName; bytes=$taskBackup.Length; reason='Blender自动备份；最终来源model.blend保留'}
        Remove-Item -LiteralPath $taskBackup.FullName
    }
    foreach ($taskReceiptName in @('receipt.json', 'delivery.json', 'handoff.json')) {
        $taskReceiptPath = Join-Path $taskDirectory $taskReceiptName
        if (Test-Path -LiteralPath $taskReceiptPath) {
            $taskReceipt = Get-Content -LiteralPath $taskReceiptPath -Raw | ConvertFrom-Json -AsHashtable
            $taskReceipt['resource_current'] = 'Content/Worldfall/Models/' + $taskAsset.asset + '/models.json'
            $taskReceipt['candidate_copy_retained'] = Test-Path -LiteralPath $taskCandidate
            if ($taskReceipt.ContainsKey('candidate_resource') -and -not $taskReceipt['candidate_copy_retained']) {
                $taskReceipt['candidate_resource_before_cleanup'] = $taskReceipt['candidate_resource']
                $taskReceipt.Remove('candidate_resource')
            }
            if ($taskReceipt['status'] -eq 'ready_for_parent_publish') { $taskReceipt['status'] = 'published_offline' }
            $taskReceipt | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $taskReceiptPath -Encoding utf8
        }
    }
    $taskReadmePath = Join-Path $taskDirectory 'README.md'
    if (Test-Path -LiteralPath $taskReadmePath) {
        $taskReadme = [System.IO.File]::ReadAllText($taskReadmePath)
        if (-not $taskReadme.StartsWith('<!-- 当前发布记录 -->')) {
            $taskBanner = '<!-- 当前发布记录 -->' + "`n" + '2026-10-14 已离线发布 ' + $taskAsset.models + ' 个模型；[原图与最终模型检查页](index.html)。正式资源为 `Content/Worldfall/Models/' + $taskAsset.asset + '/models.json`。游戏效果待用户查看。以下保留对象制作说明。' + "`n<!-- 当前发布记录结束 -->`n`n"
            [System.IO.File]::WriteAllText($taskReadmePath, $taskBanner + $taskReadme, [System.Text.UTF8Encoding]::new($false))
        }
    }
}
$taskResult = [ordered]@{scope='仅本批native-biome-v1重复候选和自动备份'; files=$taskDeleted; retained='model.blend、build.py、GLB、真实图片、原图判断、预算和检查记录'; game_test='pending_user_manual'}
$taskResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskBatch 'biome-native-v1/cleanup.json') -Encoding utf8
$taskDeletedBytes = 0
foreach ($taskDeletedFile in $taskDeleted) { $taskDeletedBytes += $taskDeletedFile['bytes'] }
[ordered]@{deleted=$taskDeleted.Count; bytes=$taskDeletedBytes} | ConvertTo-Json -Compress
