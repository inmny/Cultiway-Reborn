# 调用实际编译的模型材质处理，核对所有正式植被的生效结果；不连接游戏。
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
foreach ($taskFile in @(
    'worldbox_Data/Managed/UnityEngine.CoreModule.dll',
    'worldbox_Data/Managed/Newtonsoft.Json.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll'
)) { [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $taskFile)) | Out-Null }
$taskWorldfall = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll'))
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskModelType = $taskWorldfall.GetType('FirstPerson.Core.MeshModel', $true)
$taskParseInfo = $taskModelType.GetMethod('ParseInfo', $taskFlags)
$taskApply = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallNatureMaterials', $true).GetMethod('Apply', $taskFlags)
$taskProgress = Get-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/biome-native-v1/progress.json') -Raw | ConvertFrom-Json
$taskAssets = @($taskProgress.assets.asset) + @('Dark_tree','Dark')
$taskResults = foreach ($taskAsset in $taskAssets) {
    $taskPath = Join-Path $taskRoot ('Content/Worldfall/Models/' + $taskAsset + '/models.json')
    $taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskPath))
    $taskModels = foreach ($taskExported in $taskDocument['Models']) {
        $taskModel = $taskExported.ToObject($taskModelType)
        $taskInfo = ($taskExported['Info'].Properties() | ForEach-Object { $_.Name + '=' + $_.Value.ToString() }) -join ';'
        $null = $taskParseInfo.Invoke($taskModel, [object[]]@($taskInfo))
        $taskBefore = [Newtonsoft.Json.Linq.JObject]::Parse([Newtonsoft.Json.JsonConvert]::SerializeObject($taskModel))
        $null = $taskApply.Invoke($null, [object[]]@($taskAsset, $taskModel))
        if ($taskModel.Info('cultiway_asset') -ne $taskAsset) { throw ('缺少实际群系编号：' + $taskModel.Name) }
        $taskAfter = [Newtonsoft.Json.Linq.JObject]::Parse([Newtonsoft.Json.JsonConvert]::SerializeObject($taskModel))
        $null = $taskBefore.Remove('SlotKind'); $null = $taskAfter.Remove('SlotKind')
        if (-not [Newtonsoft.Json.Linq.JToken]::DeepEquals($taskBefore, $taskAfter)) { throw ('材质之外的模型数据改变：' + $taskModel.Name) }
        if (@($taskModel.SlotKind | Where-Object { $_ -notin @(0,1,2) }).Count -ne 0) { throw ('存在不允许的表面纹理：' + $taskModel.Name) }
        if ($taskModel.SlotNames.Length -gt 64) { throw ('颜色槽超限：' + $taskModel.Name) }
        $taskCounts = @{ '0'=0; '1'=0; '2'=0 }
        foreach ($taskPart in $taskModel.Parts) {
            foreach ($taskSlot in $taskPart.Slot) { $taskCounts[[string][int]$taskModel.SlotKind[$taskSlot]]++ }
        }
        [ordered]@{ name=$taskModel.Name; frame=$taskModel.Info('cultiway_frame');
            plain_triangles=$taskCounts['0']; leaf_triangles=$taskCounts['1']; bark_triangles=$taskCounts['2'];
            other_data_unchanged=$true; slots=$taskModel.SlotNames.Length; slot_names=$taskModel.SlotNames; slot_kinds=$taskModel.SlotKind }
    }
    [ordered]@{ asset=$taskAsset; models=@($taskModels) }
}
# 检查角色边界，防止修复普通木纹时误改花瓣、菌盖、骨骸、符文或蜡烛。
$taskExamples = @(
    @('Dark_tree','main_3','uv_wood_00',2), @('Dark_tree','main_3','uv_leaf_00',1),
    @('Dark_tree','main_3','stone',0), @('Dark_tree','ruin_0','timber',0),
    @('Oak_tree','main_2','bark',2), @('Oak_tree','main_2','leaf',1),
    @('Fern_tree','ruin_1','dry_leaf',1), @('Cemetery_tree','main_10','stone',0),
    @('Rice_tree','main_1','grain',0), @('Rice','main_0','mint_base',0),
    @('Rice','main_0','green_shoot',1), @('Rice','main_0','dark_stem',1), @('Rice','main_0','lime_leaf',1),
    @('Knowledge_tree','main_2','paper',0),
    @('Candle_tree','main_0','wax_cream',0), @('Dark','main_4','petal_red',0),
    @('Coral','main_2','pale_A',0), @('Nurgle','main_0','petal',0),
    @('Bamboo','main_0','shoot_green',1), @('Nurgle','main_1','leaf_light',1),
    @('Titans_tree','main_14','c_d7c493',0), @('Slaanesh_tree','main_9','c_cb3684',0)
)
foreach ($taskExample in $taskExamples) {
    $taskModel = [Activator]::CreateInstance($taskModelType)
    $taskIsTree = $taskExample[0].EndsWith('_tree')
    $taskMarker = $(if ($taskIsTree) { 'cultiway_tree=1' } else { 'cultiway_plant=1' }) + ';cultiway_frame=' + $taskExample[1]
    $null = $taskParseInfo.Invoke($taskModel, [object[]]@($taskMarker))
    $taskModel.SlotNames = [string[]]@($taskExample[2]); $taskModel.SlotKind = [byte[]]@(4)
    $taskModel.SlotColor = [uint32[]]@(4294967295)
    $null = $taskApply.Invoke($null, [object[]]@($taskExample[0], $taskModel))
    if ($taskModel.SlotKind[0] -ne $taskExample[3]) { throw ('部件材质边界不符：' + ($taskExample -join '/')) }
}
$taskTotal = 0
foreach ($taskRow in $taskResults) { $taskTotal += $taskRow['models'].Count }
if ($taskResults.Count -ne 30 -or $taskTotal -ne 909) { throw '植被模型范围不完整' }
[ordered]@{
    passed=$true; assets=$taskResults.Count; models=$taskTotal; examples=$taskExamples.Count;
    application='WorldfallModelFiles.Load 在正式模型读入后分配 SlotKind，并根据文件编号补齐群系标记；正式文件、网格及作者原色不改';
    geometry_colors_flags_unchanged=$true; forbidden_kinds=@(3,4,7); game_test='pending_user_manual';
    scope='实际编译材质分配代码与实际Worldfall模型类型；不代表已看到游戏中的纹理'; results=@($taskResults)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/biome-sizing-v1/material-check.json') -Encoding utf8
[ordered]@{ passed=$true; assets=30; models=$taskTotal; examples=$taskExamples.Count } | ConvertTo-Json -Compress
