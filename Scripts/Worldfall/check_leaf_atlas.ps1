# 读取真实原生指令并执行图集尺寸改写，不安装补丁、不调用Unity或启动游戏。
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
$taskWorldfallPath = [IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll'))
$taskWorldfall = [Reflection.Assembly]::LoadFrom($taskWorldfallPath)
$taskCultiway = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll'))
$taskCards = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallLeafCards', $true)
$taskSprites = $taskWorldfall.GetType('FirstPerson.Gpu.GpuSprites', $true)
$taskPatch = $taskCards.GetMethod('ExpandLeafAtlas', $taskFlags)
$taskOpCodes = @{}
foreach ($taskField in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
    $taskOp = $taskField.GetValue($null)
    $taskOpCodes[([int]$taskOp.Value -band 65535)] = $taskOp
}
function Read-AtlasInstructions([Reflection.MethodInfo]$method) {
    $bytes = $method.GetMethodBody().GetILAsByteArray()
    $result = [Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    $offset = 0
    while ($offset -lt $bytes.Length) {
        $code = [int]$bytes[$offset++]
        if ($code -eq 254) { $code = 65024 + $bytes[$offset++] }
        $op = $taskOpCodes[$code]
        if ($null -eq $op) { throw ('无法解码 ' + $method.Name + ' 的指令') }
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
$taskChecks = foreach ($taskName in @('Place', 'Prepare')) {
    $taskMethod = $taskSprites.GetMethod($taskName, $taskFlags)
    $taskInstructions = Read-AtlasInstructions $taskMethod
    $taskBefore = @($taskInstructions | ForEach-Object { [pscustomobject]@{ Op = $_.opcode; Value = $_.operand } })
    $taskArgs = [object[]]::new(1)
    $taskArgs[0] = $taskInstructions
    $taskAfter = @($taskPatch.Invoke($null, $taskArgs))
    if ($taskAfter.Count -ne $taskBefore.Count) { throw '图集改写改变了指令数量' }
    $taskChanges = 0
    $taskCap = 0
    for ($taskI = 0; $taskI -lt $taskBefore.Count; $taskI++) {
        $taskOld = $taskBefore[$taskI]
        $taskNew = $taskAfter[$taskI]
        if ($taskOld.Op -ne $taskNew.opcode) { throw '图集改写改变了操作类型' }
        $taskExpected = $taskOld.Value
        if ($taskOld.Op -eq [Reflection.Emit.OpCodes]::Ldc_I4 -and $taskOld.Value -eq 2048) {
            $taskExpected = 6144; $taskChanges++
        } elseif ($taskOld.Op -eq [Reflection.Emit.OpCodes]::Ldc_R4 -and $taskOld.Value -eq [single]2048) {
            $taskExpected = [single]6144; $taskChanges++
        } elseif ($taskOld.Op -eq [Reflection.Emit.OpCodes]::Ldc_R4 -and $taskOld.Value -eq [single](1.0 / 2048)) {
            $taskExpected = [single](1.0 / 6144); $taskChanges++
        }
        if (-not [object]::Equals($taskExpected, $taskNew.operand)) { throw ('指令值不符：' + $taskName + ' #' + $taskI) }
        if ($taskOld.Op -eq [Reflection.Emit.OpCodes]::Ldc_I4 -and $taskOld.Value -eq 512) { $taskCap++ }
    }
    if ($taskChanges -ne 4) { throw ($taskName + ' 改写数量不符：' + $taskChanges) }
    if ($taskName -eq 'Place' -and $taskCap -ne 2) { throw '512单图限制不符' }
    [ordered]@{ Method = $taskName; Instructions = $taskAfter.Count; ChangedSizeConstants = $taskChanges; Preserved512Caps = $taskCap }
}
$taskReset = $taskCards.GetMethod('ResetSmallLeafAtlas', $taskFlags)
$taskFields = foreach ($taskParameter in $taskReset.GetParameters()) {
    $taskName = $taskParameter.Name.Substring(3)
    $taskField = $taskSprites.GetField($taskName, $taskFlags)
    $taskType = $taskParameter.ParameterType
    if ($taskType.IsByRef) { $taskType = $taskType.GetElementType() }
    if ($taskField.FieldType -ne $taskType) { throw ('原生图集字段不符：' + $taskName) }
    $taskName
}
$taskResult = [ordered]@{
    Passed = $true; AtlasSide = 6144; SingleImageLimit = 512
    NativeAssemblySha256 = (Get-FileHash -LiteralPath $taskWorldfallPath -Algorithm SHA256).Hash.ToLowerInvariant()
    NativeVersion = $taskWorldfall.GetName().Version.ToString()
    Methods = @($taskChecks); ResetFieldsMatch = @($taskFields)
    Scope = '真实程序集指令改写及字段绑定检查；没有调用Unity或安装运行补丁'
    GameTest = 'pending_user_manual'
}
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/biome-leaf-cards-v1/atlas-integration-check.json') -Encoding utf8
$taskResult | ConvertTo-Json -Depth 8
