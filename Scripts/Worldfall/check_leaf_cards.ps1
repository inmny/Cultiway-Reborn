# 直接执行实际编译的叶簇放置与场景收集，不启动或连接游戏。
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
foreach ($taskFile in @('worldbox_Data/Managed/UnityEngine.CoreModule.dll',
    'worldbox_Data/Managed/Unity.Mathematics.dll','worldbox_Data/Managed/Newtonsoft.Json.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $taskFile)) | Out-Null
}
$taskWorldfall = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll'))
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskIntegration = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallLeafCards', $true)
$taskCardType = $taskIntegration.GetNestedType('LeafCard', $taskFlags)
$taskCollectorType = $taskWorldfall.GetType('FirstPerson.SceneCollector',$true)
$taskRecordType = $taskCollectorType.GetMethod('DrawAsModel',$taskFlags).GetParameters()[0].ParameterType
$taskPlantEntry = $taskCollectorType.GetMethod('DrawPlantModel',$taskFlags)
if ($taskPlantEntry.GetParameters()[0].ParameterType -ne $taskRecordType -or $taskPlantEntry.ReturnType -ne [bool]) {
    throw '原生花草入口不符合共用植被叶簇的记录结构'
}
$taskInstanceType = $taskWorldfall.GetType('FirstPerson.Core.MeshInstance',$true)
$taskModelType = $taskWorldfall.GetType('FirstPerson.Core.MeshModel',$true)
$taskImageType = $taskWorldfall.GetType('FirstPerson.Core.SpriteImage',$true)
$taskBillboardField = $taskCollectorType.GetField('Billboards',$taskFlags)
$taskCollector = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskCollectorType)
$taskBillboards = [Activator]::CreateInstance($taskBillboardField.FieldType)
$taskBillboardField.SetValue($taskCollector,$taskBillboards)
$taskRecord = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskRecordType)
$taskTree = [Activator]::CreateInstance($taskInstanceType)
$taskTree.Model = [Activator]::CreateInstance($taskModelType)
$taskTree.Tag = 37
$taskRecordType.GetField('Mesh',$taskFlags).SetValue($taskRecord,$taskTree)
$taskIntegration.GetField('LeafCardModelMesh',$taskFlags).SetValue($null,$taskRecordType.GetField('Mesh',$taskFlags))
$taskIntegration.GetField('LeafCardBillboards',$taskFlags).SetValue($null,$taskBillboardField)
$taskModels = $taskIntegration.GetField('LeafCardsByModel',$taskFlags).GetValue($null)
$taskImages = $taskIntegration.GetField('LeafCardImages',$taskFlags).GetValue($null)
$taskRecipePaths = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Content/Worldfall/LeafCards') -Filter '*.json' | Sort-Object Name)
$taskPlacements=0; $taskCollectionCases=0; $taskFadeCases=0
$taskLevels = foreach ($taskRecipePath in $taskRecipePaths) {
    $taskRecipe = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskRecipePath.FullName))
    foreach ($taskProperty in $taskRecipe['Textures'].Properties()) {
        $taskPath=$taskProperty.Value.ToString()
        if ($taskImages.ContainsKey($taskPath)) { continue }
        $taskPng=[IO.File]::ReadAllBytes((Join-Path $taskRoot ('GameResources/'+$taskPath+'.png')))
        $taskImage = [Activator]::CreateInstance($taskImageType)
        $taskImage.Width=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($taskPng,16))
        $taskImage.Height=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($taskPng,20))
        $taskImage.PivotX=0.5; $taskImage.PivotY=0
        $taskImages.Add($taskPath,$taskImage)
    }
    foreach ($taskPair in @(@('Near',''),@('Middle','_m'),@('Far','_f'))) {
    $taskCards = $taskRecipe['Levels'][$taskPair[0]].ToObject($taskCardType.MakeArrayType())
    foreach ($taskCard in $taskCards) { $taskCard.Image=$taskRecipe['Textures'][$taskCard.Image].ToString() }
    $taskName=$taskRecipe['ModelName'].ToString()+$taskPair[1]
    $taskModels.Add($taskName,$taskCards)
    $taskTree.Model.Name=$taskName
    $taskIsPlant = -not $taskRecipe['AssetId'].ToString().EndsWith('_tree')
    foreach ($taskScale in @([single]0.5,[single]1,[single]2)) {
        $taskAcrossScale = $taskScale * $(if ($taskIsPlant) { 1 } else { .8 })
        foreach ($taskAngle in @([single]0,[single]0.6,[single]1.8)) {
            $taskTree.Place(10,20,3,$taskAngle,$taskAcrossScale,$taskAcrossScale,$taskScale)
            foreach ($taskCard in $taskCards) {
                $taskImage=$taskImages[$taskCard.Image]
                $taskBillboard=$taskIntegration.GetMethod('MakeLeafCard',$taskFlags).Invoke($null,[object[]]@($taskCard,$taskTree,$taskImage))
                $taskTheta=$taskAngle+[Math]::PI/2
                $taskX=10+([Math]::Cos($taskTheta)*$taskCard.X-[Math]::Sin($taskTheta)*$taskCard.Y)*$taskAcrossScale
                $taskY=20+([Math]::Sin($taskTheta)*$taskCard.X+[Math]::Cos($taskTheta)*$taskCard.Y)*$taskAcrossScale
                if ([Math]::Abs($taskBillboard.X-$taskX) -gt 1e-5 -or [Math]::Abs($taskBillboard.Y-$taskY) -gt 1e-5 -or
                    [Math]::Abs($taskBillboard.Z-(3+$taskCard.Z*$taskScale)) -gt 1e-5 -or
                    [Math]::Abs($taskBillboard.Width-$taskCard.Width*$taskAcrossScale) -gt 1e-6 -or
                    [Math]::Abs($taskBillboard.Height-$taskCard.Height*$taskScale) -gt 1e-6 -or
                    $taskBillboard.Tag -ne 37 -or $taskBillboard.Alpha -ne 0 -or $taskBillboard.Kind.ToString() -ne 'Cross' -or
                    -not [object]::ReferenceEquals($taskBillboard.Image,$taskImage)) { throw '叶簇放置、图片或原生裁切参数不符' }
                $taskPlacements++
            }
        }
    }
    foreach ($taskFade in @([single]1,[single]0.18)) {
        $taskTree.Fade=$taskFade
        $taskCard=$taskCards[0]
        $taskBillboard=$taskIntegration.GetMethod('MakeLeafCard',$taskFlags).Invoke($null,[object[]]@($taskCard,$taskTree,$taskImages[$taskCard.Image]))
        $taskExpectedAlpha=$(if ($taskFade -ge 1) { 0 } else { [int][Math]::Truncate($taskFade*255) })
        if ($taskBillboard.Alpha -ne $taskExpectedAlpha) { throw ('叶簇淡出不符：Fade='+$taskTree.Fade+' Alpha='+$taskBillboard.Alpha+' expected='+$taskExpectedAlpha) }
        $taskFadeCases++
    }
    $taskTree.Fade=1
    $taskBillboards.Clear()
    $taskIntegration.GetMethod('DrawLeafCards',$taskFlags).Invoke($null,[object[]]@($taskCollector,$taskRecord,$true)) | Out-Null
    if ($taskBillboards.Count -ne $taskCards.Length) { throw '实际场景收集的叶簇数量不符' }
    $taskBillboards.Clear()
    $taskIntegration.GetMethod('DrawLeafCards',$taskFlags).Invoke($null,[object[]]@($taskCollector,$taskRecord,$false)) | Out-Null
    $taskTree.ShadowOnly=$true
    $taskIntegration.GetMethod('DrawLeafCards',$taskFlags).Invoke($null,[object[]]@($taskCollector,$taskRecord,$true)) | Out-Null
    $taskTree.ShadowOnly=$false; $taskTree.Model.Name='__leaf_check_unregistered__'+$taskName
    $taskIntegration.GetMethod('DrawLeafCards',$taskFlags).Invoke($null,[object[]]@($taskCollector,$taskRecord,$true)) | Out-Null
    if ($taskBillboards.Count -ne 0) { throw '失败、阴影专用或未登记状态入口误加叶簇' }
    $taskCollectionCases+=4
    [ordered]@{asset=$taskRecipe['AssetId'].ToString(); frame=$taskRecipe['Frame'].ToString();
        model=$taskName; level=$taskPair[0]; cards=$taskCards.Length; solid_alpha=0; collection_passed=$true}
    }
}
$taskResult=[ordered]@{passed=$true; transform_cases=$taskPlacements; collection_cases=$taskCollectionCases; fade_cases=$taskFadeCases;
    native_entries=@('DrawAsModel','DrawPlantModel');
    recipes=$taskRecipePaths.Length; textures=$taskImages.Count; levels=@($taskLevels);
    source='实际编译的放置与收集方法、真实Worldfall类型、全部实际发布叶簇配方；图片大小读取真实PNG头，缓存为纯数据替身，不实例化Unity';
    scope='验证坐标、随机缩放继承、朝向、三档、登记状态与未登记状态边界、贴图引用、正常Alpha=0及近树冠共同淡出；不代表已实际绘制PNG或GPU';game_test='pending_user_manual'}
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/biome-sizing-v1/leaf-card-check.json') -Encoding utf8
[ordered]@{passed=$true; placements=$taskPlacements; collection_cases=$taskCollectionCases} | ConvertTo-Json -Compress
