# 读取实际程序集并执行尺寸表、指令改写和花草放置；不启动、连接或控制游戏。
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame = [IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
foreach ($taskFile in @(
    'worldbox_Data/Managed/UnityEngine.CoreModule.dll',
    'worldbox_Data/Managed/Unity.Mathematics.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assemblies/0Harmony.dll'
)) { [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $taskFile)) | Out-Null }
$taskWorldfall = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll'))
$taskSizes = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallBiomeSizes', $true)
$taskCollector = $taskWorldfall.GetType('FirstPerson.SceneCollector', $true)
$taskScales = $taskSizes.GetField('TreeScales', $taskFlags).GetValue($null)
$taskLoaded = [Collections.Generic.HashSet[string]]::new([string[]]@($taskScales.Keys), [StringComparer]::Ordinal)
$taskSizes.GetField('LoadedTrees', $taskFlags).SetValue($null, $taskLoaded)
foreach ($taskName in @('TreeGrow','TreeWiden')) {
    $taskSizes.GetField('Native' + $taskName, $taskFlags).SetValue($null, $taskCollector.GetMethod($taskName, $taskFlags))
}
$taskUpdate = $taskCollector.GetMethod('UpdateBuildingSprite', $taskFlags)
$taskRecord = $taskUpdate.GetParameters()[0].ParameterType
$taskSizes.GetField('SizedBiomeBuilding', $taskFlags).SetValue($null, $taskRecord.GetField('Building', $taskFlags))
$taskSpriteGet = $taskUpdate.GetParameters()[1].ParameterType.GetMethod('Get', $taskFlags)
$taskSizes.GetField('NativeSpriteGet', $taskFlags).SetValue($null, $taskSpriteGet)

# 只解码本次会改写的两个方法，保留每条原指令对象；不生成或安装动态补丁。
$taskOpCodes = @{}
foreach ($taskField in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
    $taskOp = $taskField.GetValue($null)
    $taskOpCodes[([int]$taskOp.Value -band 65535)] = $taskOp
}
function Read-SizeInstructions([Reflection.MethodInfo]$method) {
    $bytes = $method.GetMethodBody().GetILAsByteArray()
    $result = [Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    $offset = 0
    while ($offset -lt $bytes.Length) {
        $code = [int]$bytes[$offset++]
        if ($code -eq 254) { $code = 65024 + $bytes[$offset++] }
        $op = $taskOpCodes[$code]
        if ($null -eq $op) { throw ('指令解码错误：' + $method.Name + ' offset=' + $offset + ' code=' + $code + ' opcodes=' + $taskOpCodes.Count) }
        $operand = $null
        $length = 0
        switch ($op.OperandType.ToString()) {
            'InlineNone' { }
            'ShortInlineVar' { $operand = $bytes[$offset]; $length = 1 }
            'InlineVar' { $operand = [BitConverter]::ToUInt16($bytes, $offset); $length = 2 }
            'ShortInlineI' { $operand = [int]$bytes[$offset]; $length = 1 }
            'ShortInlineBrTarget' { $operand = [int]$bytes[$offset]; $length = 1 }
            'InlineI' { $operand = [BitConverter]::ToInt32($bytes, $offset); $length = 4 }
            'InlineBrTarget' { $operand = [BitConverter]::ToInt32($bytes, $offset); $length = 4 }
            'InlineI8' { $operand = [BitConverter]::ToInt64($bytes, $offset); $length = 8 }
            'ShortInlineR' { $operand = [BitConverter]::ToSingle($bytes, $offset); $length = 4 }
            'InlineR' { $operand = [BitConverter]::ToDouble($bytes, $offset); $length = 8 }
            'InlineSwitch' {
                $count = [BitConverter]::ToInt32($bytes, $offset)
                $operand = $bytes[$offset..($offset + 3 + 4 * $count)]
                $length = 4 + 4 * $count
            }
            'InlineMethod' { $operand = $method.Module.ResolveMethod([BitConverter]::ToInt32($bytes, $offset)); $length = 4 }
            'InlineField' { $operand = $method.Module.ResolveField([BitConverter]::ToInt32($bytes, $offset)); $length = 4 }
            'InlineType' { $operand = $method.Module.ResolveType([BitConverter]::ToInt32($bytes, $offset)); $length = 4 }
            'InlineTok' { $operand = $method.Module.ResolveMember([BitConverter]::ToInt32($bytes, $offset)); $length = 4 }
            'InlineString' { $operand = $method.Module.ResolveString([BitConverter]::ToInt32($bytes, $offset)); $length = 4 }
            'InlineSig' { $operand = $method.Module.ResolveSignature([BitConverter]::ToInt32($bytes, $offset)); $length = 4 }
            default { throw ('未识别指令类型：' + $op.OperandType) }
        }
        $offset += $length
        $result.Add([HarmonyLib.CodeInstruction]::new($op, $operand))
    }
    return ,$result
}
$taskHooks = foreach ($taskPair in @(
    @('UpdateBuildingSprite','ScaleNearbyBiomeTrees',5,3),
    @('FarSpriteOf','ScaleDistantBiomeTrees',3,0)
)) {
    $taskNativeMethod = $taskCollector.GetMethod($taskPair[0], $taskFlags)
    $taskOriginal = Read-SizeInstructions $taskNativeMethod
    $taskArgs = [object[]]::new(1); $taskArgs[0] = $taskOriginal
    $taskPatched = @($taskSizes.GetMethod($taskPair[1], $taskFlags).Invoke($null, $taskArgs))
    $taskBefore = 0; $taskAdded = 0; $taskCursor = 0
    for ($taskPatchedIndex = 0; $taskPatchedIndex -lt $taskPatched.Count; $taskPatchedIndex++) {
        $taskInstruction = $taskPatched[$taskPatchedIndex]
        if ($taskCursor -lt $taskOriginal.Count -and [object]::ReferenceEquals($taskInstruction, $taskOriginal[$taskCursor])) { $taskCursor++ }
        if ($taskInstruction.operand -is [Reflection.MethodInfo]) {
            if ($taskInstruction.operand.Name -in @('TreeGrow','TreeWiden')) { $taskBefore++ }
            if ($taskInstruction.operand.Name -eq 'BiomeTreeDisplayScale') {
                $taskAdded++
                if ($taskPair[0] -eq 'FarSpriteOf') {
                    $taskExpectedArgument = $(if ($taskNativeMethod.IsStatic) { [Reflection.Emit.OpCodes]::Ldarg_0 } else { [Reflection.Emit.OpCodes]::Ldarg_1 })
                    if ($taskPatched[$taskPatchedIndex - 1].opcode -ne $taskExpectedArgument -or
                        $taskNativeMethod.GetParameters()[0].ParameterType.Name -ne 'BuildingAsset') { throw '远景传入的资产参数位置不符' }
                } else {
                    if ($taskPatched[$taskPatchedIndex - 3].opcode -ne [Reflection.Emit.OpCodes]::Ldarg_1 -or
                        $taskPatched[$taskPatchedIndex - 2].operand.Name -ne 'Building' -or
                        $taskPatched[$taskPatchedIndex - 1].operand.FieldType.Name -ne 'BuildingAsset') { throw '近景记录到资产的参数传递不符' }
                }
            }
        }
    }
    if ($taskBefore -ne 2 -or $taskAdded -ne 2 -or $taskCursor -ne $taskOriginal.Count -or
        $taskPatched.Count -ne $taskOriginal.Count + 2 * $taskPair[2] + $taskPair[3]) { throw ('尺寸入口改写不符：' + $taskPair[0]) }
    [ordered]@{ method=$taskPair[0]; original_instructions_preserved=$true; argument_types_checked=$true; scaled_growth_calls=$taskAdded }
}
# 蜡烛近远景都在读入贴图前选同一帧；原指令、读图和控制流仍保留。
$taskCandleHooks = foreach ($taskPair in @(
    @('UpdateBuildingSprite','ScaleNearbyBiomeTrees',3), @('FarImage','StableFarCandleSprite',2)
)) {
    $taskOriginal = Read-SizeInstructions ($taskCollector.GetMethod($taskPair[0], $taskFlags))
    $taskArgs = [object[]]::new(1); $taskArgs[0] = $taskOriginal
    $taskPatched = @($taskSizes.GetMethod($taskPair[1], $taskFlags).Invoke($null, $taskArgs))
    $taskFixedCalls = 0
    for ($taskIndex=0; $taskIndex -lt $taskPatched.Count; $taskIndex++) {
        $taskInstruction = $taskPatched[$taskIndex]
        if ($taskInstruction.operand -is [Reflection.MethodInfo] -and $taskInstruction.operand.Name -eq 'StableCandleSprite') {
            $taskFixedCalls++
            if ($taskPatched[$taskIndex+1].operand -ne $taskSpriteGet) { throw '固定蜡烛帧未放在原生读图之前' }
            if ($taskPair[0] -eq 'UpdateBuildingSprite') {
                if ($taskPatched[$taskIndex-2].opcode -ne [Reflection.Emit.OpCodes]::Ldarg_1 -or
                    $taskPatched[$taskIndex-1].operand.Name -ne 'Building') { throw '蜡烛近景建筑参数不符' }
            } elseif ($taskPatched[$taskIndex-1].opcode -ne [Reflection.Emit.OpCodes]::Ldarg_1) { throw '蜡烛远景建筑参数不符' }
        }
    }
    if ($taskFixedCalls -ne 1) { throw ('蜡烛固定帧入口数量不符：' + $taskPair[0]) }
    [ordered]@{method=$taskPair[0]; fixed_sprite_calls=1; building_argument_checked=$true}
}
$taskAssetType = $taskRecord.GetField('Building', $taskFlags).FieldType.GetField('asset', $taskFlags).FieldType
$taskBoundary = foreach ($taskId in @($taskScales.Keys) + @('tree_green_1','Cultiway.SectHall')) {
    $taskAsset = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskAssetType)
    $taskAsset.id = $taskId
    $taskFactor = $taskSizes.GetMethod('BiomeTreeDisplayScale', $taskFlags).Invoke($null, [object[]]@($taskAsset))
    $taskExpected = $taskSizes.GetMethod('TreeScale', $taskFlags).Invoke($null, [object[]]@($taskId))
    if ($taskFactor -ne $taskExpected) { throw ('资产比例不符：' + $taskId) }
    [ordered]@{ asset=$taskId; scale=$taskFactor }
}
# 用未连接Unity的引用对象核对四帧选择，且残骸、其他资产不受影响。
$taskBuildingType = $taskRecord.GetField('Building', $taskFlags).FieldType
$taskSpriteType = $taskSpriteGet.GetParameters()[0].ParameterType
$taskBuilding = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskBuildingType)
$taskCandleAsset = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskAssetType)
$taskCandleAsset.id = 'Candle_tree'; $taskBuilding.asset = $taskCandleAsset
$taskDataType = $taskBuildingType.GetField('data', $taskFlags).FieldType
$taskBuilding.data = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskDataType)
$taskStateType = $taskDataType.GetField('state', $taskFlags).FieldType
$taskAnimationType = $taskBuildingType.GetField('animData', $taskFlags).FieldType
$taskBuilding.animData = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskAnimationType)
$taskSprites = [Array]::CreateInstance($taskSpriteType, 4)
for ($taskIndex=0; $taskIndex -lt 4; $taskIndex++) {
    $taskSprites.SetValue([Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskSpriteType),$taskIndex)
}
$taskBuilding.animData.main = $taskSprites
$taskCandleFrameCases = 0
foreach ($taskCase in @(@('Candle_tree','Normal',$true), @('Candle_tree','Ruins',$false),
    @('Oak_tree','Normal',$false))) {
    $taskBuilding.asset.id = $taskCase[0]
    $taskBuilding.data.state = [Enum]::Parse($taskStateType,$taskCase[1])
    foreach ($taskSprite in $taskSprites) {
        $taskSelected = $taskSizes.GetMethod('StableCandleSprite', $taskFlags).Invoke($null,[object[]]@($taskSprite,$taskBuilding))
        $taskExpected = $(if ($taskCase[2]) { $taskSprites.GetValue(1) } else { $taskSprite })
        if (-not [object]::ReferenceEquals($taskSelected,$taskExpected)) { throw '蜡烛固定帧或残骸边界不符' }
        $taskCandleFrameCases++
    }
}
if ($null -ne $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration').GetMethod('MoveBiomeFlame',$taskFlags)) {
    throw '仍存在烛火缩放动作'
}
$taskModelType = $taskWorldfall.GetType('FirstPerson.Core.MeshModel', $true)
$taskInstanceType = $taskWorldfall.GetType('FirstPerson.Core.MeshInstance', $true)
$taskParseInfo = $taskModelType.GetMethod('ParseInfo', $taskFlags)
$taskFlowerCases = 0
foreach ($taskCase in @(
    @('cultiway_plant=1',1.2), @('cultiway_tree=1',1.0), @('person=1',1.0),
    @('cultiway_plant=1;cultiway_asset=Knowledge',4.8), @('cultiway_plant=1;cultiway_asset=Rice',4.8),
    @('cultiway_plant=1;cultiway_asset=Bamboo',4.8), @('cultiway_plant=1;cultiway_asset=Candle',4.8),
    @('cultiway_plant=1;cultiway_asset=Dark',4.8), @('cultiway_plant=1;cultiway_asset=Fern',2.4),
    @('cultiway_plant=1;cultiway_asset=Oak',4.8),
    @('cultiway_plant=1;cultiway_asset=FleshBlood',2.4), @('cultiway_plant=1;cultiway_asset=Titans',2.4)
)) {
    $taskMarker = $taskCase[0]
    $taskModel = [Activator]::CreateInstance($taskModelType)
    $null = $taskParseInfo.Invoke($taskModel, [object[]]@($taskMarker))
    $taskInstance = [Activator]::CreateInstance($taskInstanceType); $taskInstance.Model = $taskModel
    foreach ($taskInput in @([single]0.5,[single]0.625,[single]0.75)) {
        for ($taskRepeat=0; $taskRepeat -lt 3; $taskRepeat++) {
            $taskArgs = [object[]]@($taskInstance, $taskInput, $taskInput, $taskInput)
            $null = $taskSizes.GetMethod('ScaleBiomePlants', $taskFlags).Invoke($null, $taskArgs)
            $taskInstance.Place(10, 20, 1, 0, $taskArgs[1], $taskArgs[2], $taskArgs[3])
            $taskExpected = $taskInput * $taskCase[1]
            if ([Math]::Abs($taskInstance.Basis.C2.Z - $taskExpected) -gt 1e-6 -or
                $taskInstance.Position.Z -ne 1 -or [Math]::Abs($taskArgs[1] - $taskArgs[3]) -gt 1e-6) { throw '花草放置比例或重复调用不符' }
            $taskFlowerCases++
        }
    }
}
$taskSinkCases = 0
foreach ($taskCase in @(
    @('cultiway_tree=1;cultiway_asset=Candle_tree',.12), @('cultiway_plant=1;cultiway_asset=Candle',.01),
    @('cultiway_tree=1;cultiway_asset=FleshBlood_tree',.06), @('cultiway_plant=1;cultiway_asset=FleshBlood',0),
    @('cultiway_tree=1;cultiway_asset=Titans_tree',.18), @('cultiway_plant=1;cultiway_asset=Titans',0),
    @('cultiway_tree=1;cultiway_asset=Dark_tree',0), @('person=1;cultiway_asset=Candle_tree',0)
)) {
    $taskMarker = $taskCase[0]
    $taskModel = [Activator]::CreateInstance($taskModelType)
    $null = $taskParseInfo.Invoke($taskModel, [object[]]@($taskMarker))
    $taskInstance = [Activator]::CreateInstance($taskInstanceType); $taskInstance.Model = $taskModel
    foreach ($taskInput in @([single]0.5,[single]1,[single]2)) {
        for ($taskRepeat=0; $taskRepeat -lt 3; $taskRepeat++) {
            $taskInstance.Place(10, 20, 1, 0, $taskInput, $taskInput, $taskInput)
            $null = $taskSizes.GetMethod('LowerBiomeModels', $taskFlags).Invoke($null, [object[]]@($taskInstance, $taskInput))
            $taskExpected = 1 - $taskCase[1] * $taskInput
            if ([Math]::Abs($taskInstance.Position.Z - $taskExpected) -gt 1e-6) { throw '群系模型下移范围或重复调用不符' }
            $taskSinkCases++
        }
    }
}
$taskPlantScales = foreach ($taskId in @('Knowledge','Rice','Bamboo','Candle','Dark','Fern','Oak','FleshBlood','Titans')) {
    [ordered]@{asset=$taskId; scale=$taskSizes.GetMethod('PlantScaleFor', $taskFlags).Invoke($null, [object[]]@($taskId))}
}
$taskGrowth = foreach ($taskPixels in 2..60) {
    $taskMapHeight = [single]($taskPixels * 0.25)
    [ordered]@{ pixels=$taskPixels; scale_base=0.25; grow=$taskCollector.GetMethod('TreeGrow', $taskFlags).Invoke($null, [object[]]@($taskMapHeight));
        widen=$taskCollector.GetMethod('TreeWiden', $taskFlags).Invoke($null, [object[]]@($taskMapHeight)) }
}
$taskOutput = Join-Path $taskRoot 'Art/Worldfall/biome-sizing-v1'
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
[ordered]@{
    passed=$true; worldfall_version=$taskWorldfall.GetName().Version.ToString();
    adult_height=$taskSizes.GetField('AdultHeight', $taskFlags).GetRawConstantValue();
    plant_scale=$taskSizes.GetField('PlantScale', $taskFlags).GetRawConstantValue();
    plant_scales=@($taskPlantScales); candle_root_sink=$taskSizes.GetField('CandleRootSink', $taskFlags).GetRawConstantValue();
    fleshblood_root_sink=$taskSizes.GetField('FleshBloodRootSink', $taskFlags).GetRawConstantValue(); candle_plant_sink=$taskSizes.GetField('CandlePlantSink', $taskFlags).GetRawConstantValue();
    titans_root_sink=$taskSizes.GetField('TitansRootSink', $taskFlags).GetRawConstantValue();
    tree_scales=@($taskBoundary); native_growth=@($taskGrowth); hooks=@($taskHooks); plant_placement_cases=$taskFlowerCases; candle_placement_cases=$taskSinkCases;
    candle_sprite_hooks=@($taskCandleHooks); candle_fixed_frame_cases=$taskCandleFrameCases; flame_scale_animation_removed=$true;
    scope='实际编译尺寸表、实际原生生长方法、两处指令改写及花草重复放置；未安装补丁或调用游戏场景';
    game_test='pending_user_manual'
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'compiled-check.json') -Encoding utf8
[ordered]@{passed=$true; tree_assets=$taskScales.Count; native_sites=4; plant_cases=$taskFlowerCases; candle_cases=$taskSinkCases} | ConvertTo-Json -Compress
