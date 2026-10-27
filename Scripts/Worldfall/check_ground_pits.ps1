# 用真实 Worldfall 打包入口验证地表副本；不创建显卡缓冲、不运行游戏。
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
$taskOutput = Join-Path $taskRoot 'Art/Worldfall/remaining-buildings-v1'
foreach ($taskFile in @('worldbox_Data/Managed/UnityEngine.CoreModule.dll',
    'worldbox_Data/Managed/Unity.Mathematics.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assemblies/0Harmony.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $taskFile)) | Out-Null
}
$taskWorldfall = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll'))
[Reflection.Assembly]::LoadFrom('C:/Users/DELL/.nuget/packages/newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll') | Out-Null
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskHelper = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallGroundPits', $true)
$taskGpuType = $taskWorldfall.GetType('FirstPerson.Gpu.GpuTerrain', $true)
$taskPack = $taskGpuType.GetMethod('Pack', $taskFlags)
$taskPackedField = $taskGpuType.GetField('_packed', $taskFlags)
$taskUpdate = $taskHelper.GetMethod('UpdatePits', $taskFlags)
$taskModels = [Collections.Generic.Dictionary[string,FirstPerson.Core.MeshModel]]::new()
$taskParseInfo = [FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$taskFlags)
$taskCandidate = Join-Path $taskRoot 'Art/Worldfall/Cultiway.SkavenBlight/native-civ-v1/models.candidate.json'
$taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskCandidate))
$taskProfile = $taskDocument['Models'][0]['Info']
foreach ($taskEntry in $taskDocument['Models']) {
    $taskModel = $taskEntry.ToObject([FirstPerson.Core.MeshModel])
    $taskInfo = ($taskEntry['Info'].Properties() | ForEach-Object { $_.Name+'='+$_.Value.ToString() }) -join ';'
    $null = $taskParseInfo.Invoke($taskModel,[object[]]@($taskInfo))
    foreach ($taskKey in @('cultiway_ground_pit','pit_cut_inner','pit_cut_radius','pit_floor')) {
        if ($taskModel.Info($taskKey) -ne $taskProfile[$taskKey].ToString()) { throw ('深坑约定不一致：'+$taskModel.Name) }
    }
    $taskModels.Add($taskModel.Name,$taskModel)
}
# 游戏用的旧补丁库不能在 PowerShell 的新运行时安装补丁。
# 按真实字段准备上下文，顺序执行原生方法和本次后置函数，不宣称游戏补丁已验收。
$taskShapes = $taskHelper.GetField('Shapes',$taskFlags).GetValue($null)
foreach ($taskModel in $taskModels.Values) {
    $taskShapes.Add($taskModel,[ValueTuple[single,single,single]]::new(
        [single]::Parse($taskModel.Info('pit_cut_inner'),[Globalization.CultureInfo]::InvariantCulture),
        [single]::Parse($taskModel.Info('pit_cut_radius'),[Globalization.CultureInfo]::InvariantCulture),
        [single]::Parse($taskModel.Info('pit_floor'),[Globalization.CultureInfo]::InvariantCulture)))
}
$taskHelper.GetField('PackedTerrain',$taskFlags).SetValue($null,$taskPackedField)
$taskHelper.GetField('MarkChanged',$taskFlags).SetValue($null,[FirstPerson.Core.TerrainCache].GetMethod('MarkChanged',$taskFlags))
$taskExpose = $taskHelper.GetMethod('ExposePits',$taskFlags)
$taskCollector = $taskWorldfall.GetType('FirstPerson.SceneCollector',$true)
$taskSampler = $taskWorldfall.GetType('FirstPerson.WorldSampler',$true)
if ($taskCollector.GetMethod('Collect',$taskFlags).GetParameters().Name -notcontains 'sampler' -or
    $null -eq $taskCollector.GetField('Meshes',$taskFlags) -or $null -eq $taskSampler.GetField('Cache',$taskFlags)) { throw '场景入口与预期不符' }
if (($taskPack.GetParameters().Name -join ',') -ne 'land,x0,y0,x1,y1') { throw '打包参数与后置函数不一致' }
function Invoke-PitPack {
    param($land,[int]$left,[int]$front,[int]$right,[int]$back)
    $null = $taskPack.Invoke($taskGpu,[object[]]@($land,$left,$front,$right,$back))
    $null = $taskExpose.Invoke($null,[object[]]@($taskGpu,$land,$left,$front,$right,$back))
}
$taskChecks = [Collections.Generic.List[string]]::new()
$taskChecks.Add('真实 Collect 与 Pack 字段、参数及后置函数匹配；未安装运行时补丁')
$taskLand = [FirstPerson.Core.TerrainCache]::new()
$taskLand.Allocate(64,64)
for ($taskI=0; $taskI -lt 4096; $taskI++) {
    $taskLand.Ground[$taskI] = 2
    $taskLand.Heights[$taskI] = 2
    $taskLand.Colors[$taskI] = [uint32](50000+$taskI)
    $taskLand.Sun[$taskI] = [byte]($taskI%255)
    $taskLand.Open[$taskI] = 137
    $taskLand.Flags[$taskI] = 36
    $taskLand.Materials[$taskI] = 14
}
$taskGpu = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskGpuType)
$taskPackedField.SetValue($taskGpu,[uint32[]]::new(4096*4))
$taskMeshes = [Collections.Generic.List[FirstPerson.Core.MeshInstance]]::new()
$null = $taskUpdate.Invoke($null,[object[]]@($taskLand,$taskMeshes))
Invoke-PitPack $taskLand 0 0 64 64
$taskPacked = $taskPackedField.GetValue($taskGpu)
$taskOriginal = $taskPacked.Clone()
$taskX0=0; $taskY0=0; $taskX1=0; $taskY1=0
$null = $taskLand.TakeChanged([ref]$taskX0,[ref]$taskY0,[ref]$taskX1,[ref]$taskY1)
$taskMesh = [FirstPerson.Core.MeshInstance]::new()
$taskMesh.Place(32.5,32.5,1.95,(-[Math]::PI/2),12,12,12)
$taskMeshes.Add($taskMesh)
$taskRadius = [single]::Parse($taskProfile['pit_cut_radius'].ToString(),[Globalization.CultureInfo]::InvariantCulture)
$taskExpected = 1.95+[single]::Parse($taskProfile['pit_floor'].ToString(),[Globalization.CultureInfo]::InvariantCulture)*12-0.1
$taskFirst = $true
foreach ($taskModel in $taskModels.Values) {
    $taskMesh.Model = $taskModel
    $null = $taskUpdate.Invoke($null,[object[]]@($taskLand,$taskMeshes))
    $taskDirty = $taskLand.TakeChanged([ref]$taskX0,[ref]$taskY0,[ref]$taskX1,[ref]$taskY1)
    if ($taskDirty -ne $taskFirst) { throw '模型出现应刷新，等尺寸换状态或距离档不应反复刷新' }
    $taskFirst = $false
    Invoke-PitPack $taskLand 0 0 64 64
    $taskChanged = 0
    for ($taskI=0; $taskI -lt 4096; $taskI++) {
        $taskDistance = [Math]::Sqrt([Math]::Pow(($taskI%64)+0.5-32.5,2)+[Math]::Pow([Math]::Floor($taskI/64)+0.5-32.5,2))
        for ($taskField=0; $taskField -lt 4; $taskField++) {
            $taskIndex = $taskI*4+$taskField
            if ($taskField -ge 2 -or $taskDistance -ge $taskRadius*12) {
                if ($taskPacked[$taskIndex] -ne $taskOriginal[$taskIndex]) { throw '坑外或颜色、标记、材质字节被改变' }
            } elseif ($taskField -eq 0 -and $taskPacked[$taskIndex] -ne $taskOriginal[$taskIndex]) { $taskChanged++ }
        }
        if ($taskLand.Ground[$taskI] -ne 2 -or $taskLand.Heights[$taskI] -ne 2) { throw '原始地图高度被改变' }
    }
    $taskCenter = (32*64+32)*4
    $taskActual = [BitConverter]::ToSingle([BitConverter]::GetBytes([uint32]$taskPacked[$taskCenter]),0)
    if ([Math]::Abs($taskActual-$taskExpected) -gt 0.0001 -or $taskChanged -le 0) { throw '坑底没有低于模型底面' }
    $taskChecks.Add('真实打包：'+$taskModel.Name+'，范围外原样、颜色标记原样、原地图高度不变')
}
# 保存真实打包的格点数据，供 Blender 复现插值后的地表遮挡。
$taskHeights = for ($taskI=0; $taskI -lt 4096; $taskI++) {
    [BitConverter]::ToSingle([BitConverter]::GetBytes([uint32]$taskPacked[$taskI*4]),0)
}
[ordered]@{width=64;height=64;heights=@($taskHeights);node_offset=0.5;center=@(32.5,32.5,1.95);scale=12;
    baseline=2;source='真实 Worldfall Pack 经当前 Cultiway 后置补丁得到的上传副本；非游戏截图'} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'ground-pit-upload.json') -Encoding utf8
$taskMeshes.Clear()
$null = $taskUpdate.Invoke($null,[object[]]@($taskLand,$taskMeshes))
if (-not $taskLand.TakeChanged([ref]$taskX0,[ref]$taskY0,[ref]$taskX1,[ref]$taskY1)) { throw '移除后未刷新旧坑口' }
Invoke-PitPack $taskLand $taskX0 $taskY0 $taskX1 $taskY1
for ($taskI=0; $taskI -lt $taskPacked.Length; $taskI++) {
    if ($taskPacked[$taskI] -ne $taskOriginal[$taskI]) { throw '移除后地表未完全恢复' }
}
$taskChecks.Add('移除建筑：只重打包标记范围，所有上传字节恢复')
$taskMesh.Place(0.5,0.5,1.95,(-[Math]::PI/2),12,12,12)
$taskMeshes.Add($taskMesh)
$null = $taskUpdate.Invoke($null,[object[]]@($taskLand,$taskMeshes))
if (-not $taskLand.TakeChanged([ref]$taskX0,[ref]$taskY0,[ref]$taskX1,[ref]$taskY1) -or $taskX0 -ne 0 -or $taskY0 -ne 0) { throw '地图边界未夹紧' }
Invoke-PitPack $taskLand $taskX0 $taskY0 $taskX1 $taskY1
$taskChecks.Add('地图边缘：范围夹紧且打包不越界')
$taskOtherLand = [FirstPerson.Core.TerrainCache]::new()
$taskOtherLand.Allocate(64,64)
$taskMeshes.Clear()
$null = $taskUpdate.Invoke($null,[object[]]@($taskOtherLand,$taskMeshes))
if (-not $taskLand.TakeChanged([ref]$taskX0,[ref]$taskY0,[ref]$taskX1,[ref]$taskY1)) { throw '换世界未标记旧表面恢复' }
Invoke-PitPack $taskLand 0 0 64 64
for ($taskI=0; $taskI -lt $taskPacked.Length; $taskI++) {
    if ($taskPacked[$taskI] -ne $taskOriginal[$taskI]) { throw '旧世界残留坑口' }
}
$taskChecks.Add('切换地表缓存：旧世界坑口恢复，没有串用')
$taskResult = [ordered]@{passed=$true;date='2026-10-19';worldfall_version=$taskWorldfall.GetName().Version.ToString();
    candidate_sha256=(Get-FileHash -LiteralPath $taskCandidate -Algorithm SHA256).Hash.ToLowerInvariant();
    assembly_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll') -Algorithm SHA256).Hash.ToLowerInvariant();
    checks=@($taskChecks);gpu_buffer_created=$false;game_test='pending_user_manual';
    scope='真实运行时方法与后置函数顺序执行的离线纯数据检查；未安装补丁、未初始化游戏或显卡；碰撞与寻路高度沿用原地图'}
$taskResult | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'ground-pit-check.json') -Encoding utf8
[ordered]@{passed=$true;checks=$taskChecks.Count;models=$taskModels.Count;pit_floor=$taskExpected} | ConvertTo-Json -Compress
