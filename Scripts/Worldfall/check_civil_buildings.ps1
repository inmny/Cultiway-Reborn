# 在独立进程核对实际程序集的模型、选模、国家色和施工阶段；不连接或启动游戏。
param([string]$Batch = 'civil-buildings-v1')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskBatchPath = Join-Path $taskRoot ('Art/Worldfall/'+$Batch)
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
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
$taskIntegration = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration', $true)
$taskNativeModel = $taskWorldfall.GetType('FirstPerson.Core.MeshModel', $true)
$taskInfo = $taskNativeModel.GetMethod('ParseInfo', $taskFlags)
$taskStageType = $taskWorldfall.GetType('FirstPerson.Core.BuildSite', $true)
$taskAt = $taskStageType.GetMethod('At', $taskFlags)
$taskShows = $taskStageType.GetMethod('Shows', $taskFlags)
$taskClutter = $taskStageType.GetMethod('IsClutter', $taskFlags)
$taskWholePalette = $taskIntegration.GetMethod('UseBuildingPalette', $taskFlags)
$taskRuinPalette = $taskIntegration.GetMethod('UseRuinPalette', $taskFlags)
$taskFamily = $taskIntegration.GetMethod('CivilBuildingFamily', $taskFlags)
$taskVariants = $taskIntegration.GetField('CivilBuildingVariants', $taskFlags).GetValue($null)
$taskVariants.Clear()
$taskModels = $taskIntegration.GetField('CustomModels', $taskFlags).GetValue($null)
$taskBuildings = $taskIntegration.GetField('BuildingFamilies', $taskFlags).GetValue($null)
$taskModels.Clear()
$taskBuildings.Clear()
$taskAuthoredPalettes = @{}
$taskProgress = Get-Content -LiteralPath (Join-Path $taskBatchPath 'progress.json') -Raw | ConvertFrom-Json
$taskResults = [Collections.Generic.List[object]]::new()
$taskFamilies = [Collections.Generic.List[object]]::new()
$taskPaletteCases = 0
$taskStageCases = 0
$taskStageFailures = [Collections.Generic.List[object]]::new()
foreach ($taskAsset in $taskProgress.assets | Where-Object published) {
    $taskPath = Join-Path $taskRoot ('Content/Worldfall/Models/'+$taskAsset.id+'/models.json')
    $taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskPath))
    $taskCount = 0
    foreach ($taskExported in $taskDocument['Models']) {
        $taskModel = $taskExported.ToObject($taskNativeModel)
        $taskText = ($taskExported['Info'].Properties() | ForEach-Object { $_.Name+'='+$_.Value.ToString() }) -join ';'
        $null = $taskInfo.Invoke($taskModel, [object[]]@($taskText))
        $taskModels.Add($taskModel.Name,$taskModel)
        $taskAuthoredPalettes[$taskModel.Name] = $taskModel.SlotColor.Clone()
        if ($taskModel.Name -eq $taskDocument['Family'].ToString()) { $taskBuildings.Add($taskAsset.id,$taskModel.Name) }
        foreach ($taskColor in @([uint32]4281558681,[uint32]4291541862)) {
            foreach ($taskMethod in @($taskWholePalette,$taskRuinPalette)) {
                $taskArgs = [object[]]@($taskModel,$taskColor,$null)
                $null = $taskMethod.Invoke($null,$taskArgs)
                $taskPalette = $taskArgs[2]
                if ($taskPalette.Length -ne $taskModel.SlotColor.Length) { throw ('配色槽数不一致：'+$taskModel.Name) }
                $taskCountrySlots = $taskExported['Info']['kingdom_slots'].ToString().Split(',')
                if ($taskMethod -eq $taskWholePalette) {
                    for ($taskIndex=0;$taskIndex -lt $taskPalette.Length;$taskIndex++) {
                        if ($taskModel.SlotNames[$taskIndex] -notin $taskCountrySlots -and $taskPalette[$taskIndex] -ne $taskModel.SlotColor[$taskIndex]) {
                            throw ('固定材质被国家色覆盖：'+$taskModel.Name+'/'+$taskModel.SlotNames[$taskIndex])
                        }
                    }
                }
                $taskPaletteCases++
            }
        }
        if ($taskExported['Info']['cultiway_frame'].ToString().StartsWith('main_') -and
            -not $taskModel.Name.EndsWith('_m') -and -not $taskModel.Name.EndsWith('_f')) {
            $taskStyle = $taskExported['Info']['cultiway_style'].ToString()
            $taskIndex = [int]$taskExported['Info']['cultiway_frame'].ToString().Substring(5)
            if (-not $taskVariants.ContainsKey($taskAsset.id)) {
                $taskVariants.Add($taskAsset.id,[Collections.Generic.Dictionary[string,Collections.Generic.Dictionary[int,string]]]::new())
            }
            $taskStyles = $taskVariants[$taskAsset.id]
            if (-not $taskStyles.ContainsKey($taskStyle)) { $taskStyles.Add($taskStyle,[Collections.Generic.Dictionary[int,string]]::new()) }
            $taskStyles[$taskStyle].Add($taskIndex,$taskModel.Name)
            $taskFamilies.Add([pscustomobject]@{asset=$taskAsset.id;style=$taskStyle;index=$taskIndex;name=$taskModel.Name})
        }
        if ($taskModel.Name.StartsWith('site_')) {
            foreach ($taskStage in 0..3) {
                foreach ($taskFar in @($false,$true)) {
                    $taskDerived = $taskAt.Invoke($null,[object[]]@($taskModel,[int]$taskStage,[bool]$taskFar))
                    $taskExpected = @($taskModel.Parts | Where-Object {
                        $taskShows.Invoke($null,[object[]]@($_.Name,[int]$taskStage)) -and
                        (-not $taskFar -or -not $taskClutter.Invoke($null,[object[]]@($_.Name)))
                    })
                    if ($taskDerived.Parts.Length -ne $taskExpected.Length) { throw ('施工显隐不一致：'+$taskModel.Name) }
                    $taskTriangles = 0
                    foreach ($taskPart in $taskDerived.Parts) { $taskTriangles += $taskPart.Tri.Length / 3 }
                    if ($taskTriangles -le 0) { $taskStageFailures.Add([ordered]@{asset=$taskAsset.id;model=$taskModel.Name;stage=$taskStage;far=$taskFar}) }
                    $taskStageCases++
                }
            }
        }
        $taskCount++
    }
    $taskResults.Add([ordered]@{asset=$taskAsset.id;models=$taskCount;sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToLowerInvariant()})
}
ConvertTo-Json -InputObject ($taskStageFailures.ToArray()) -Depth 6 | Set-Content -LiteralPath (Join-Path $taskBatchPath 'stage-failures.json') -Encoding utf8
if ($taskStageFailures.Count -gt 0) {
    throw ('施工空显示：'+$taskStageFailures.Count+'项，见stage-failures.json')
}
$null = $taskIntegration.GetMethod('PrepareBuildingPalettes',$taskFlags).Invoke($null,@())
foreach ($taskModel in $taskModels.Values) {
    $taskBefore = $taskAuthoredPalettes[$taskModel.Name]
    for ($taskIndex=0;$taskIndex -lt $taskBefore.Length;$taskIndex++) {
        if ($taskBefore[$taskIndex] -ne $taskModel.SlotColor[$taskIndex]) { throw ('已绘制的状态配色被覆盖：'+$taskModel.Name) }
    }
}
$taskFamilyCases = 0
foreach ($taskEntry in $taskFamilies) {
    foreach ($taskStyle in @($taskEntry.style,'missing_style')) {
        $taskSelected = $taskFamily.Invoke($null,[object[]]@($taskEntry.asset,$taskStyle,[int]$taskEntry.index))
        $taskExpectedStyles = $taskVariants[$taskEntry.asset]
        $taskExpectedVariants = if ($taskStyle -eq 'missing_style') { $taskExpectedStyles['base'] } else { $taskExpectedStyles[$taskStyle] }
        $taskExpectedName = if ($taskExpectedVariants.ContainsKey($taskEntry.index)) { $taskExpectedVariants[$taskEntry.index] } else { @($taskExpectedVariants.GetEnumerator() | Sort-Object Key)[0].Value }
        if ($taskSelected -ne $taskExpectedName) { throw ('原图或文化选模不一致：'+$taskEntry.asset) }
        $taskFamilyCases++
    }
    $taskSelected = $taskFamily.Invoke($null,[object[]]@($taskEntry.asset,$taskEntry.style,[int]999))
    $taskFirst = @($taskVariants[$taskEntry.asset][$taskEntry.style].GetEnumerator() | Sort-Object Key)[0].Value
    if ($taskSelected -ne $taskFirst) { throw ('文化缺图未沿用本风格：'+$taskEntry.asset) }
    $taskFamilyCases++
}
$taskCollector = $taskWorldfall.GetType('FirstPerson.SceneCollector',$true)
$taskRecord = $taskCollector.GetMethod('DrawAsModel',$taskFlags).GetParameters()[0].ParameterType
foreach ($taskField in @('MeshFamily','FamilyChecked','FamilyAsset','FamilyGen','Route')) {
    if ($null -eq $taskRecord.GetField($taskField,$taskFlags)) { throw ('缺少原生缓存字段：'+$taskField) }
}
if ('Building' -notin [Enum]::GetNames($taskRecord.GetField('Route',$taskFlags).FieldType)) { throw '原生建筑路线不存在' }
$taskResult = [ordered]@{passed=$true;worldfall_version=$taskWorldfall.GetName().Version.ToString();assets=@($taskResults);family_cases=$taskFamilyCases;palette_cases=$taskPaletteCases;authored_state_palettes_preserved=$true;stage_cases=$taskStageCases;game_test='pending_user_manual';scope='实际程序集纯数据选模、配色和施工阶段；未初始化游戏，未安装运行时补丁'}
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskBatchPath 'integration-check.json') -Encoding utf8
[ordered]@{passed=$true;assets=$taskResults.Count;family_cases=$taskFamilyCases;palette_cases=$taskPaletteCases;stage_cases=$taskStageCases} | ConvertTo-Json -Compress
