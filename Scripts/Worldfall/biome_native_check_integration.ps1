# 只核对原生接口与正式资源的固定配色，不连接或启动游戏。
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [System.IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
[System.Reflection.Assembly]::LoadFrom((Join-Path $taskGame 'worldbox_Data/Managed/UnityEngine.CoreModule.dll')) | Out-Null
[System.Reflection.Assembly]::LoadFrom((Join-Path $taskGame 'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll')) | Out-Null
$taskWorldfall = [System.Reflection.Assembly]::LoadFrom([System.IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskNewtonsoft = [System.Reflection.Assembly]::LoadFrom((Join-Path $taskGame 'worldbox_Data/Managed/Newtonsoft.Json.dll'))
$taskFlags = [System.Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskCollector = $taskWorldfall.GetType('FirstPerson.SceneCollector', $true)
$taskModelType = $taskWorldfall.GetType('FirstPerson.Core.MeshModel', $true)
$taskObjectType = $taskNewtonsoft.GetType('Newtonsoft.Json.Linq.JObject', $true)
$taskParse = $taskObjectType.GetMethod('Parse', [type[]]@([string]))
$taskParseInfo = $taskModelType.GetMethod('ParseInfo', $taskFlags)
$taskFlora = $taskWorldfall.GetType('FirstPerson.Core.Flora', $true)
$taskHandles = $taskFlora.GetMethod('Handles', $taskFlags)
$taskColors = $taskFlora.GetMethod('Colors', $taskFlags)
$taskInterfaces = foreach ($taskEntry in @(
    @{ name='DrawAsModel'; fields=@('MeshFamily','FamilyChecked','FamilyAsset','FamilyGen','Route') },
    @{ name='DrawPlantModel'; fields=@('MeshFamily','FamilyChecked','FamilyAsset') }
)) {
    $taskMethod = $taskCollector.GetMethod($taskEntry.name, $taskFlags)
    $taskRecord = $taskMethod.GetParameters()[0].ParameterType
    foreach ($taskFieldName in $taskEntry.fields) {
        if ($null -eq $taskRecord.GetField($taskFieldName, $taskFlags)) { throw ('缺少原生缓存字段：' + $taskFieldName) }
    }
    [ordered]@{ method=$taskEntry.name; fields=$taskEntry.fields; passed=$true }
}
$taskProgress = Get-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/biome-native-v1/progress.json') -Raw | ConvertFrom-Json
$taskResults = foreach ($taskAsset in $taskProgress.assets | Where-Object { $_.status -eq '离线发布' }) {
    $taskModelsPath = Join-Path $taskRoot ('Content/Worldfall/Models/' + $taskAsset.asset + '/models.json')
    $taskDocument = $taskParse.Invoke($null, [object[]]@([System.IO.File]::ReadAllText($taskModelsPath)))
    $taskCount = 0
    foreach ($taskExported in $taskDocument['Models']) {
        $taskModel = $taskExported.ToObject($taskModelType)
        $taskInfo = ($taskExported['Info'].Properties() | ForEach-Object { $_.Name + '=' + $_.Value.ToString() }) -join ';'
        $null = $taskParseInfo.Invoke($taskModel, [object[]]@($taskInfo))
        if (-not $taskHandles.Invoke($null, [object[]]@($taskModel))) { throw ('未走原生植被配色：' + $taskModel.Name) }
        $taskPalette = $taskColors.Invoke($null, [object[]]@($taskModel, $null, [uint32]0))
        if (@($taskPalette | Where-Object { $_ -ne 0 }).Count -ne 0) { throw ('原生配色覆盖了自建固定色：' + $taskModel.Name) }
        $taskCount++
    }
    $taskHash = (Get-FileHash -LiteralPath $taskModelsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($taskHash -ne $taskAsset.production_sha256) { throw ('正式资源校验值已变化：' + $taskAsset.asset) }
    [ordered]@{ asset=$taskAsset.asset; models=$taskCount; source_sha256=$taskHash; fixed_colors_preserved=$true }
}
$taskPaletteTotal = 0
foreach ($taskReadResult in $taskResults) { $taskPaletteTotal += $taskReadResult['models'] }
$taskResult = [ordered]@{
    scope='读取正式自建植被模型，并在独立进程调用实际Worldfall程序集的纯数据配色方法；不代替游戏绘制与选模验收'
    passed=$true
    worldfall_version=$taskWorldfall.GetName().Version.ToString()
    native_interfaces=@($taskInterfaces)
    assets=@($taskResults)
    palettes=$taskPaletteTotal
    game_test='pending_user_manual'
    selection_execution='接入选择代码已构建；游戏对象与选模结合待用户查看'
    check_runtime='新版pwsh；不初始化游戏旧补丁库'
}
$taskResult | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/biome-native-v1/integration-check.json') -Encoding utf8
[ordered]@{passed=$true; assets=$taskResults.Count; palettes=$taskResult.palettes} | ConvertTo-Json -Compress
