# 实际程序集的模型读取、选模和逐帧配色纯数据检查；不连接或启动游戏。
param([string[]]$Assets,[string]$Output,[string]$Assembly = 'bin/Debug/net48/Cultiway.dll')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
foreach ($taskFile in @('worldbox_Data/Managed/UnityEngine.CoreModule.dll',
    'worldbox_Data/Managed/Unity.Mathematics.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assemblies/0Harmony.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $taskFile)) | Out-Null
}
$taskWorldfall = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot $Assembly))
[Reflection.Assembly]::LoadFrom('C:/Users/DELL/.nuget/packages/newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll') | Out-Null
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskIntegration = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration',$true)
$taskFamilies = $taskIntegration.GetField('ActorFamilies',$taskFlags).GetValue($null)
$taskModels = $taskIntegration.GetField('CustomModels',$taskFlags).GetValue($null)
$taskFamilies.Clear()
$taskModels.Clear()
$taskParse = [FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$taskFlags)
$taskSelect = $taskIntegration.GetMethod('SelectCreatureFamily',$taskFlags)
$taskSelectPerson = $taskIntegration.GetMethod('SelectPersonFamily',$taskFlags)
$taskProvide = $taskIntegration.GetMethod('ProvideCustomModel',$taskFlags)
$taskColors = $taskIntegration.GetMethod('UseCreatureColors',$taskFlags)
$taskBatch = Join-Path $taskRoot 'Art/Worldfall/creature-native-v1'
$taskProgress = Get-Content -LiteralPath (Join-Path $taskBatch 'progress.json') -Raw | ConvertFrom-Json
$taskResults = [Collections.Generic.List[object]]::new()
$taskPaletteCount = 0
foreach ($taskAsset in $taskProgress.assets | Where-Object {if ($Assets) {$_.id -in $Assets} else {$_.published}}) {
    $taskPath = Join-Path $taskRoot ('Content/Worldfall/Models/'+$taskAsset.id+'/models.json')
    $taskFolder = Join-Path $taskRoot ('Art/Worldfall/'+$taskAsset.id+'/native-unit-v1')
    $taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskPath))
    $taskFamily = $taskDocument['Family'].ToString()
    $taskFamilies.Add($taskAsset.id,$taskFamily)
    $taskActor = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([Actor])
    $taskActorAsset = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([ActorAsset])
    $taskActorAsset.id = $taskAsset.id
    [Actor].GetField('asset',$taskFlags).SetValue($taskActor,$taskActorAsset)
    if ($taskDocument['Humanoid'].ToObject([bool])) {
        [ActorAsset].GetField('is_humanoid',$taskFlags).SetValue($taskActorAsset,$true)
        $taskActorData=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([ActorData])
        [Actor].GetField('data',$taskFlags).SetValue($taskActor,$taskActorData)
        $taskOrdinaryProfession=[Enum]::GetValues([UnitProfession]) | Where-Object {$_ -ne [UnitProfession]::Warrior} | Select-Object -First 1
        foreach ($taskFemale in @($false,$true)) {
            foreach ($taskWarrior in @($false,$true)) {
                $taskSex=if ($taskFemale) {[ActorSex]::Female} else {[ActorSex]::Male}
                $taskProfession=if ($taskWarrior) {[UnitProfession]::Warrior} else {$taskOrdinaryProfession}
                [ActorData].GetProperty('sex',$taskFlags).SetValue($taskActorData,$taskSex)
                [Actor].GetField('_profession',$taskFlags).SetValue($taskActor,$taskProfession)
                $taskExpected=$taskFamily+$(if ($taskWarrior) {'_warrior'})+$(if ($taskFemale) {'_fem'})
                $taskArgs=[object[]]@($taskActor,'native_original')
                $null=$taskSelectPerson.Invoke($null,$taskArgs)
                if ($taskArgs[1] -ne $taskExpected) {throw ('人物性别职业选模不符：'+$taskAsset.id)}
            }
        }
        $taskSeed=[Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText((Join-Path $taskRoot ('Art/Worldfall/Seeds/'+$taskAsset.id+'.json'))))
        $taskPersonModels=foreach ($taskEntry in $taskDocument['Models']) {
            $taskModel=$taskEntry.ToObject([FirstPerson.Core.MeshModel])
            $taskText=($taskEntry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'
            $null=$taskParse.Invoke($taskModel,[object[]]@($taskText))
            $taskModels.Add($taskModel.Name,$taskModel)
            $taskArgs=[object[]]@($taskModel.Name,$null)
            $null=$taskProvide.Invoke($null,$taskArgs)
            if (-not [object]::ReferenceEquals($taskArgs[1],$taskModel)) {throw '人物模型读取入口不符'}
            $taskOriginal=$taskSeed['Models'] | Where-Object {$_.get_Item('Name').ToString() -eq $taskModel.Name} | Select-Object -First 1
            $taskOriginalSlots=$taskOriginal['SlotNames'].ToObject([string[]])
            $taskOriginalFlags=$taskOriginal['SlotFlags'].ToObject([byte[]])
            for ($taskIndex=0;$taskIndex -lt $taskOriginalSlots.Length;$taskIndex++) {
                if ($taskModel.SlotNames[$taskIndex] -ne $taskOriginalSlots[$taskIndex] -or $taskModel.SlotFlags[$taskIndex] -ne $taskOriginalFlags[$taskIndex]) {throw '人物肤色/衣色原槽或标记被改'}
            }
            if ($taskModel.SlotColor.Length -gt 64) {throw '人物颜色槽越界'}
            [ordered]@{name=$taskModel.Name;slots=$taskModel.SlotColor.Length;original_palette_slots_and_flags_preserved=$true}
        }
        $taskResults.Add([ordered]@{asset=$taskAsset.id;family=$taskFamily;sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToLowerInvariant();models=@($taskPersonModels);gender_profession_selections=4;person_palette='保留原人形配色入口和原槽标记；实际国家色、肤色及换装未作游戏实测'})
        continue
    }
    $taskCamouflage = @($taskDocument['Models'] | Where-Object {[string]$_.get_Item('Info').get_Item('cultiway_camouflage_state') -eq 'disguised'}).Count -gt 0
    if (-not $taskCamouflage) {
        $taskArgs = [object[]]@($taskActor,'native_original')
        $null = $taskSelect.Invoke($null,$taskArgs)
        if ($taskArgs[1] -ne $taskFamily) { throw ('生物选模不一致：'+$taskAsset.id) }
    }
    $taskSamples = Get-Content -LiteralPath (Join-Path $taskFolder 'palette-input.json') -Raw | ConvertFrom-Json
    $taskPoses = Get-Content -LiteralPath (Join-Path $taskFolder 'poses.json') -Raw | ConvertFrom-Json
    $taskModelResults = [Collections.Generic.List[object]]::new()
    foreach ($taskEntry in $taskDocument['Models']) {
        $taskModel = $taskEntry.ToObject([FirstPerson.Core.MeshModel])
        $taskText = ($taskEntry['Info'].Properties() | ForEach-Object { $_.Name+'='+$_.Value.ToString() }) -join ';'
        $null = $taskParse.Invoke($taskModel,[object[]]@($taskText))
        $taskModels.Add($taskModel.Name,$taskModel)
        $taskArgs = [object[]]@($taskModel.Name,$null)
        $null = $taskProvide.Invoke($null,$taskArgs)
        if (-not [object]::ReferenceEquals($taskArgs[1],$taskModel)) { throw ('实际模型入口不一致：'+$taskModel.Name) }
        if ($taskModel.SlotColor.Length -gt 64) { throw ('颜色槽越界：'+$taskModel.Name) }
        $taskBefore = $taskModel.SlotColor.Clone()
        $taskAuthoredColors = $taskModel.Info('cultiway_native_unit') -eq '1'
        $taskBackWings = $taskModel.Info('cultiway_back_wings') -eq '1'
        if ($taskBackWings) {
            $taskPoseModel = $taskPoses.models | Where-Object name -eq $taskModel.Name
            $taskFlightUp = $taskPoseModel.poses | Where-Object name -eq 'flight_up'
            $taskFlightDown = $taskPoseModel.poses | Where-Object name -eq 'flight_down'
            if (-not $taskFlightUp -or -not $taskFlightDown) { throw ('缺少背翼两相位：'+$taskModel.Name) }
            foreach ($taskPartName in @('wing_l','wing_r','wing_l2','wing_r2')) {
                if ($taskModel.PartIndex($taskPartName) -lt 0) { throw ('缺少双臂或背翼：'+$taskModel.Name+'/'+$taskPartName) }
                $taskUpRotation = ($taskFlightUp.parts | Where-Object name -eq $taskPartName).columns | ConvertTo-Json -Depth 6 -Compress
                $taskDownRotation = ($taskFlightDown.parts | Where-Object name -eq $taskPartName).columns | ConvertTo-Json -Depth 6 -Compress
                if (($taskPartName -in @('wing_l','wing_r')) -ne ($taskUpRotation -eq $taskDownRotation)) { throw ('背翼和持物臂动作混用：'+$taskModel.Name+'/'+$taskPartName) }
            }
        }
        $taskDynamicSlots = [Collections.Generic.HashSet[string]]::new()
        $taskReferencePalettes = [Collections.Generic.List[object]]::new()
        foreach ($taskSample in $taskSamples) {
            if ((Get-FileHash -LiteralPath (Join-Path $taskRoot $taskSample.source) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskSample.sha256) { throw ('原图已变化：'+$taskSample.source) }
            $taskImage = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([FirstPerson.Core.SpriteImage])
            $taskImage.Width = $taskSample.width
            $taskImage.Height = $taskSample.height
            $taskImage.Pixels = [uint32[]]$taskSample.pixels
            $taskArgs = [object[]]@($taskModel,$taskImage,$null)
            $null = $taskColors.Invoke($null,$taskArgs)
            $taskPalette = $taskArgs[2]
            if ($taskModel.Name -eq $taskFamily -and $taskSample.source -match '/main/(walk_0|swim_0)\.png$') {
                $taskReferencePalettes.Add([ordered]@{source=$taskSample.source;colors=@($taskPalette)})
            }
            if ($taskPalette.Length -ne $taskBefore.Length) { throw ('配色槽数不一致：'+$taskModel.Name) }
            for ($taskIndex=0;$taskIndex -lt $taskBefore.Length;$taskIndex++) {
                if ($taskModel.SlotColor[$taskIndex] -ne $taskBefore[$taskIndex]) { throw '原缺省颜色被取色函数改写' }
                $taskEffectiveColor = if ($taskPalette[$taskIndex] -eq 0) { $taskBefore[$taskIndex] } else { $taskPalette[$taskIndex] }
                if ($taskAuthoredColors -and $taskEffectiveColor -ne $taskBefore[$taskIndex]) { throw ('生物已绘制颜色被覆盖：'+$taskModel.Name+'/'+$taskModel.SlotNames[$taskIndex]) }
                if ($taskPalette[$taskIndex] -ne 0) { $null = $taskDynamicSlots.Add($taskModel.SlotNames[$taskIndex]) }
                if ($taskModel.SlotNames[$taskIndex] -notin @('fur','fur_light','dark','accent','horn','wing','spots','shell','nose') -and $taskPalette[$taskIndex] -ne 0) { throw ('固定色被覆盖：'+$taskModel.Name) }
            }
            $taskPaletteCount++
        }
        $taskModelResults.Add([ordered]@{name=$taskModel.Name;slots=$taskBefore.Length;sampled_frames=$taskSamples.Count;dynamic_slots=@($taskDynamicSlots);authored_colors_preserved=$taskAuthoredColors;independent_back_wings=$taskBackWings;fixed_slot_fallback_preserved=$true;reference_palettes=@($taskReferencePalettes)})
    }
    $taskResults.Add([ordered]@{asset=$taskAsset.id;family=$taskFamily;sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToLowerInvariant();models=@($taskModelResults);
        family_selection=if ($taskCamouflage) {'伪装族依赖Unity时钟与实际Actor；离线停稳记录与静态姿态另见check_hands_camouflage.ps1，完整Actor切换待游戏查看'} else {'实际选择入口通过'}})
}
$taskResult = [ordered]@{passed=$true;worldfall_version=$taskWorldfall.GetName().Version.ToString();assets=@($taskResults);palette_cases=$taskPaletteCount;
    method='实际程序集纯数据：普通族选模入口、全部MeshModel读取与逐帧CreatureColors配色；伪装族Actor选模依赖Unity时钟未调用；未初始化游戏、未安装补丁';game_test='pending_user_manual'}
$taskOutputPath=if ($Output) {Join-Path $taskRoot $Output} else {Join-Path $taskBatch 'integration-check.json'}
$taskResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskOutputPath -Encoding utf8
[ordered]@{passed=$true;assets=$taskResults.Count;palette_cases=$taskPaletteCount} | ConvertTo-Json -Compress
