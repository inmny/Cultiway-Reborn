# 使用实际程序集检查树人叶簇收集、活动部件、隐藏与显示状态，不连接游戏。
param([string]$Folder='Art/Worldfall/Cultiway.OakTreants/native-unit-v1')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'export_unit_poses.ps1') -Folder $Folder
$taskCardTable=$taskLeafType.GetField('LeafCardsByModel',$taskFlags).GetValue($null)
$taskImageTable=$taskLeafType.GetField('LeafCardImages',$taskFlags).GetValue($null)
$taskCollectLeaves=$taskLeafType.GetMethod('DrawCreatureLeafCards',$taskFlags)
$taskSprite=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([FirstPerson.Core.SpriteImage])
$taskProof=[Collections.Generic.List[object]]::new()
foreach ($taskEntry in $taskDocument['Models']) {
    $taskModel=$taskEntry.ToObject([FirstPerson.Core.MeshModel])
    $taskInfo=($taskEntry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'
    $null=$taskParseInfo.Invoke($taskModel,[object[]]@($taskInfo))
    $taskLevel=if ($taskModel.Name.EndsWith('_m')) {'Middle'} elseif ($taskModel.Name.EndsWith('_f')) {'Far'} else {'Near'}
    $taskCards=$taskLeafDocument['Levels'][$taskLevel].ToObject($taskLeafCardType.MakeArrayType())
    foreach ($taskCard in $taskCards) {
        if ($taskModel.PartIndex($taskCard.Part) -lt 0) {throw '叶簇部件不存在'}
        $taskImageTable[$taskCard.Image]=$taskSprite
    }
    $taskCardTable[$taskModel.Name]=$taskCards
    $taskRig=[FirstPerson.Core.CreatureRig]::new($taskModel)
    foreach ($taskMode in @('normal','moved_scaled','hidden_head','hidden_body','shadow_only','swimming','faded')) {
        $taskMesh=[FirstPerson.Core.MeshInstance]::new()
        $taskMesh.Model=$taskModel
        if ($taskMode -eq 'moved_scaled') {$taskMesh.Place(3,4,2,0.7,2,2,2)} else {$taskMesh.Place(0,0,0,-[Math]::PI/2,1,1,1)}
        $taskRig.Pose($taskMesh,1,1.5707963,0.5,$false,0,0)
        $taskMesh.Tag=317;$taskMesh.Flash=123;$taskMesh.Multiply=456;$taskMesh.Fade=1
        if ($taskMode -eq 'hidden_head') {$taskMesh.Hidden=[uint64]1 -shl $taskModel.PartIndex('head')}
        if ($taskMode -eq 'hidden_body') {$taskMesh.Hidden=[uint64]1 -shl $taskModel.PartIndex('body')}
        $taskMesh.ShadowOnly=$taskMode -eq 'shadow_only'
        $taskMesh.NoShadow=$taskMode -eq 'swimming'
        if ($taskMode -eq 'faded') {$taskMesh.Fade=0.5}
        $taskMeshes=[Collections.Generic.List[FirstPerson.Core.MeshInstance]]::new()
        $taskBoards=[Collections.Generic.List[FirstPerson.Core.Billboard]]::new()
        $taskMeshes.Add($taskMesh)
        $null=$taskCollectLeaves.Invoke($null,[object[]]@($taskMeshes,$taskBoards))
        $taskExpected=[Collections.Generic.List[object]]::new()
        foreach ($taskCard in $taskCards) {
            $taskPart=$taskModel.PartIndex($taskCard.Part)
            $taskHidden=$false
            for ($taskIndex=$taskPart;$taskIndex -ge 0;$taskIndex=$taskModel.Parts[$taskIndex].Parent) {
                if (($taskMesh.Hidden -band ([uint64]1 -shl $taskIndex)) -ne 0) {$taskHidden=$true;break}
            }
            if (-not $taskHidden -and -not $taskMesh.ShadowOnly) {$taskExpected.Add($taskCard)}
        }
        if ($taskBoards.Count -ne $taskExpected.Count) {throw ('叶簇数量/隐藏不符：'+$taskMode)}
        $taskMaximum=0.0
        for ($taskIndex=0;$taskIndex -lt $taskBoards.Count;$taskIndex++) {
            $taskCard=$taskExpected[$taskIndex];$taskBoard=$taskBoards[$taskIndex]
            $taskPosition=[FirstPerson.Core.PersonRig]::Transform($taskMesh,$taskModel.PartIndex($taskCard.Part),[FirstPerson.Core.Vec3]::new($taskCard.X,$taskCard.Y,$taskCard.Z))
            $taskError=[Math]::Abs($taskPosition.X-$taskBoard.X)+[Math]::Abs($taskPosition.Y-$taskBoard.Y)+[Math]::Abs($taskPosition.Z-$taskBoard.Z)
            $taskMaximum=[Math]::Max($taskMaximum,$taskError)
            if ($taskError -gt 0.000002 -or $taskBoard.Width -le 0 -or $taskBoard.Height -le 0) {throw '实际叶簇位置或尺寸无效'}
            if ($taskBoard.Tag -ne 317 -or $taskBoard.Flash -ne 123 -or $taskBoard.Multiply -ne 456) {throw '显示状态丢失'}
            if ($taskMesh.NoShadow -and $taskBoard.Shadow) {throw '浸水叶簇误投影'}
            if ($taskMode -eq 'faded' -and $taskBoard.Alpha -ne 127) {throw '透明度未随身体'}
            if ($taskMode -ne 'faded' -and $taskBoard.Alpha -ne 0) {throw '正常叶簇没有采用原生裁切'}
        }
        $taskProof.Add([ordered]@{model=$taskModel.Name;mode=$taskMode;cards=$taskBoards.Count;maximum_position_error=$taskMaximum})
    }
}
$taskResult=[ordered]@{passed=$true;method='实际DrawCreatureLeafCards与MakeCreatureLeafCard纯数据调用，比较原生PersonRig.Transform；没有安装补丁或连接游戏';
    candidate_sha256=(Get-FileHash -LiteralPath (Join-Path $taskFolder 'models.candidate.json') -Algorithm SHA256).Hash.ToLowerInvariant();
    leaf_card_sha256=(Get-FileHash -LiteralPath $taskLeafPath -Algorithm SHA256).Hash.ToLowerInvariant();
    leaf_source_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'Source/Content/Worldfall/WorldfallLeafCards.cs') -Algorithm SHA256).Hash.ToLowerInvariant();
    assembly_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'bin/Debug/net48/Cultiway.dll') -Algorithm SHA256).Hash.ToLowerInvariant();
    game_test='pending_user_manual';checks=@($taskProof)}
$taskResult | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $taskFolder 'leaf-runtime-check.json') -Encoding utf8
[ordered]@{passed=$true;models=$taskDocument['Models'].Count;checks=$taskProof.Count} | ConvertTo-Json -Compress
