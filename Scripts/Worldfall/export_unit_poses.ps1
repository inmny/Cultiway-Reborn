# 离线调用实际骨架与现有修仙动作补充；不安装补丁、不初始化游戏。
param([Parameter(Mandatory=$true)][string]$Folder,
      [string]$Assembly='bin/Debug/net48/Cultiway.dll')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskFolder = [IO.Path]::GetFullPath((Join-Path $taskRoot $Folder))
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
$taskParseInfo = [FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$taskFlags)
$taskSupplement = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration',$true).GetMethod('PoseCreature',$taskFlags)
$taskLeafPath = Join-Path $taskFolder 'leaf-cards.json'
$taskLeafDocument = $null
if (Test-Path -LiteralPath $taskLeafPath) {
    $taskLeafDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskLeafPath))
}
if ($null -ne $taskLeafDocument) {
    $taskLeafType = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallLeafCards',$true)
    $taskLeafCardType = $taskLeafType.GetNestedType('LeafCard',[Reflection.BindingFlags]::NonPublic)
    $taskMakeLeaf = $taskLeafType.GetMethod('MakeCreatureLeafCard',$taskFlags)
}
$taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText((Join-Path $taskFolder 'models.candidate.json')))
$taskPerson = $taskDocument['Humanoid'].ToObject([bool])
$taskModels = [Collections.Generic.List[object]]::new()
foreach ($taskEntry in $taskDocument['Models']) {
    $taskModel = $taskEntry.ToObject([FirstPerson.Core.MeshModel])
    $taskInfo = ($taskEntry['Info'].Properties() | ForEach-Object { $_.Name+'='+$_.Value.ToString() }) -join ';'
    $null = $taskParseInfo.Invoke($taskModel,[object[]]@($taskInfo))
    $taskRig = if ($taskPerson) { [FirstPerson.Core.PersonRig]::new($taskModel) } else { [FirstPerson.Core.CreatureRig]::new($taskModel) }
    $taskPoses = [Collections.Generic.List[object]]::new()
    $taskCases = @(
        @('idle',0,0,0.4,0,$false),@('step_left',1,1.5707963,0.5,0,$false),
        @('step_right',1,4.712389,0.9,0,$false),@('turn_right',0,0,0.4,0,$false),
        @('attack',0,0,0.5,0.5,$false))
    $taskInsectWings = $taskModel.Info('kind') -eq 'insect'
    if ($taskModel.Info('flap') -eq '1' -or $taskInsectWings) {
        $taskFlightUp = if ($taskInsectWings) { [Math]::PI / 64 } else { 0.3141593 }
        $taskFlightDown = if ($taskInsectWings) { [Math]::PI * 3 / 64 } else { 0.9424778 }
        $taskCases += ,@('flight_up',0,0,$taskFlightUp,0,$true)
        $taskCases += ,@('flight_down',0,0,$taskFlightDown,0,$true)
    }
    if ($taskPerson) {
        $taskCases[4][0]='arm_swing'
        $taskCases += ,@('run',1,1.5707963,0.5,0,$false)
        $taskCases += ,@('short_hair',0,0,0.4,0,$false)
    }
    foreach ($taskCase in $taskCases) {
        $taskMesh = [FirstPerson.Core.MeshInstance]::new()
        $taskMesh.Model = $taskModel
        $taskHeading = if ($taskCase[0] -eq 'turn_right') { 0 } else { -[Math]::PI/2 }
        $taskMesh.Place(0,0,0,[single]$taskHeading,1,1,1)
        if ($taskPerson) {
            $taskAppearance=[Activator]::CreateInstance($taskWorldfall.GetType('FirstPerson.Core.Humanoid+Palette',$true))
            $taskAppearance.LongHair=$taskCase[0] -ne 'short_hair'
            $taskAppearance.Beard=$taskCase[0] -eq 'short_hair'
            $taskFemale=$taskModel.Name -match '_fem(?:_[mf])?$'
            $taskRig.Dress($taskMesh,$taskAppearance,$true,$taskFemale)
            $taskHeadTurn=if ($taskCase[0] -eq 'turn_right') {0.4} else {0}
            $taskRun=if ($taskCase[0] -eq 'run') {1} else {0}
            $taskRig.Pose($taskMesh,[single]$taskCase[1],[single]$taskCase[2],[single]$taskCase[4],
                [single]([Math]::Sin($taskCase[3]*2.2)*0.01),[single]$taskHeadTurn,[single]0,[single]$taskRun,$null)
        } else {
            $taskRig.Pose($taskMesh,[single]$taskCase[1],[single]$taskCase[2],[single]$taskCase[3],[bool]$taskCase[5],[single]$taskCase[4],[single]0)
            $null = $taskSupplement.Invoke($null,[object[]]@($taskMesh,[single]$taskCase[1],[single]$taskCase[2],[single]$taskCase[3],[bool]$taskCase[5],[single]0))
        }
        $taskParts = for ($taskIndex=0; $taskIndex -lt $taskModel.Parts.Length; $taskIndex++) {
            $taskPart = $taskModel.Parts[$taskIndex]
            $taskR = $taskMesh.PartRotation[$taskIndex]
            $taskO = $taskMesh.PartOffset[$taskIndex]
            [ordered]@{name=$taskPart.Name;parent=$taskPart.Parent;pivot=@($taskPart.Pivot.X,$taskPart.Pivot.Y,$taskPart.Pivot.Z);
                columns=@(@($taskR.C0.X,$taskR.C0.Y,$taskR.C0.Z),@($taskR.C1.X,$taskR.C1.Y,$taskR.C1.Z),@($taskR.C2.X,$taskR.C2.Y,$taskR.C2.Z));
                offset=@($taskO.X,$taskO.Y,$taskO.Z);scale=$taskMesh.PartScale[$taskIndex]}
        }
        $taskB = $taskMesh.Basis
        $taskPose = [ordered]@{name=$taskCase[0];stride=$taskCase[1];phase=$taskCase[2];time=$taskCase[3];attack=$taskCase[4];flying=$taskCase[5];hidden=[uint64]$taskMesh.Hidden;
            basis=@(@($taskB.C0.X,$taskB.C0.Y,$taskB.C0.Z),@($taskB.C1.X,$taskB.C1.Y,$taskB.C1.Z),@($taskB.C2.X,$taskB.C2.Y,$taskB.C2.Z));parts=@($taskParts)}
        if ($null -ne $taskLeafDocument) {
            $taskLevel = if ($taskModel.Name.EndsWith('_m')) { 'Middle' } elseif ($taskModel.Name.EndsWith('_f')) { 'Far' } else { 'Near' }
            $taskLeafPoses = foreach ($taskCardEntry in $taskLeafDocument['Levels'][$taskLevel]) {
                $taskCard = $taskCardEntry.ToObject($taskLeafCardType)
                $taskPartIndex = $taskModel.PartIndex($taskCard.Part)
                if ($taskPartIndex -lt 0) { throw ('叶簇找不到活动部件：'+$taskCard.Part) }
                $taskBoard = $taskMakeLeaf.Invoke($null,[object[]]@($taskCard,$taskMesh,$null,$taskPartIndex))
                [ordered]@{Name=$taskCardEntry['Name'].ToString();Part=$taskCard.Part;
                    X=$taskBoard.X;Y=$taskBoard.Y;Z=$taskBoard.Z;Width=$taskBoard.Width;Height=$taskBoard.Height;
                    Angle=$taskBoard.Angle;Image=$taskCardEntry['Image'].ToString();Shadow=$taskBoard.Shadow}
            }
            $taskPose['leaf_cards'] = @($taskLeafPoses)
        }
        $taskPoses.Add($taskPose)
    }
    $taskKind=if ($taskPerson) {'humanoid'} else {$taskRig.Kind}
    $taskModels.Add([ordered]@{name=$taskModel.Name;kind=$taskKind;poses=@($taskPoses)})
}
$taskResult = [ordered]@{passed=$true;worldfall_version=$taskWorldfall.GetName().Version.ToString();
    candidate_sha256=(Get-FileHash -LiteralPath (Join-Path $taskFolder 'models.candidate.json') -Algorithm SHA256).Hash.ToLowerInvariant();
    assembly_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $Assembly) -Algorithm SHA256).Hash.ToLowerInvariant();
    integration_source_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'Source/Content/Worldfall/WorldfallModelIntegration.cs') -Algorithm SHA256).Hash.ToLowerInvariant();
    leaf_source_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'Source/Content/Worldfall/WorldfallLeafCards.cs') -Algorithm SHA256).Hash.ToLowerInvariant();
    leaf_card_sha256=if ($null -ne $taskLeafDocument) {(Get-FileHash -LiteralPath $taskLeafPath -Algorithm SHA256).Hash.ToLowerInvariant()} else {$null};
    rig=if ($taskPerson) {'PersonRig'} else {'CreatureRig'};
    person_dress_used=$taskPerson;
    method=if ($taskPerson) {'实际PersonRig.Dress与Pose纯数据调用，使用原生允许的空Walker；含真实短长发互斥显隐、肘膝，未模拟落脚锁定或装备攻击'} else {'实际CreatureRig.Pose与现有Cultiway.PoseCreature纯数据调用；转向为实例的真实朝向矩阵'};
    game_test='pending_user_manual';swimming='本接口没有游泳参数；未把走动回放称为游泳通过';models=@($taskModels)}
$taskResult | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $taskFolder 'poses.json') -Encoding utf8
[ordered]@{passed=$true;asset=$taskDocument['Id'].ToString();models=$taskModels.Count;poses=($taskModels | ForEach-Object {$_.poses.Count} | Measure-Object -Sum).Sum} | ConvertTo-Json -Compress
