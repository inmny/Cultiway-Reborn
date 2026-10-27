# 实际骨架、变身选模与附身相机的离线检查，不启动游戏。
param([string]$Assembly='tmp/WorldfallMovement-20261026/build/Cultiway.dll',
      [string]$OutputFolder='Art/Worldfall/movement-v1')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame=[IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
foreach ($taskFile in @('worldbox_Data/Managed/UnityEngine.CoreModule.dll',
    'worldbox_Data/Managed/Unity.Mathematics.dll','worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assemblies/0Harmony.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $taskFile)) | Out-Null
}
$taskWorldfall=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$taskCultiway=[Reflection.Assembly]::LoadFrom((Join-Path $taskRoot $Assembly))
[Reflection.Assembly]::LoadFrom('C:/Users/DELL/.nuget/packages/newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll') | Out-Null
$taskFlags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskIntegration=$taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration',$true)
$taskParse=[FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$taskFlags)
$taskPose=$taskIntegration.GetMethod('PoseCreature',$taskFlags)
$taskEye=$taskIntegration.GetMethod('CalibrateCreatureEye',$taskFlags)
$taskFlight=$taskIntegration.GetMethod('HasFlightMovement',$taskFlags)
$taskHeight=$taskIntegration.GetMethod('CreatureFlightHeight',$taskFlags)
$taskSelect=$taskIntegration.GetMethod('SelectCreatureFamily',$taskFlags)
$taskCustom=$taskIntegration.GetField('CustomModels',$taskFlags).GetValue($null)
$taskFamilies=$taskIntegration.GetField('ActorFamilies',$taskFlags).GetValue($null)
$taskNativeShapes=[FirstPerson.Core.BeastEye].GetField('Shapes',$taskFlags).GetValue($null)
$taskCustom.Clear()
$taskFamilies.Clear()
$taskModels=[Collections.Generic.List[object]]::new()
$taskProgress=Get-Content -LiteralPath (Join-Path $taskRoot 'Art/Worldfall/creature-native-v1/progress.json') -Raw | ConvertFrom-Json
$taskNativePeople=0
$taskPersonCameras=[Collections.Generic.List[object]]::new()
foreach ($taskAsset in $taskProgress.assets) {
    $taskPath=Join-Path $taskRoot ('Content/Worldfall/Models/'+$taskAsset.id+'/models.json')
    $taskDocument=[Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskPath))
    $taskFamilies.Add($taskAsset.id,$taskDocument['Family'].ToString())
    if ($taskDocument['Humanoid'].ToObject([bool])) {
        $taskNativePeople++
        foreach ($taskEntry in $taskDocument['Models']) {
            $taskPerson=$taskEntry.ToObject([FirstPerson.Core.MeshModel])
            $taskPersonRig=[FirstPerson.Core.PersonRig]::new($taskPerson)
            $taskHead=$taskPerson.Parts[$taskPersonRig.Head]
            $taskHeadZ=for ($i=0;$i -lt $taskHead.VertexCount;$i++) {$taskHead.Pos[$i*3+2]/$taskPersonRig.Height}
            $taskRange=$taskHeadZ | Measure-Object -Minimum -Maximum
            if (0.9 -lt $taskRange.Minimum-0.08 -or 0.9 -gt $taskRange.Maximum+0.08) {throw ('原生人形相机与头部高度不符：'+$taskPerson.Name)}
            $taskPersonCameras.Add([ordered]@{name=$taskPerson.Name;native_eye_per_body=0.9;normalized_head_min=$taskRange.Minimum;normalized_head_max=$taskRange.Maximum;
                self_visibility='原生BeastView.ShowsBody对人形返回false，SceneCollector在第一人称不加入自身身体；此处只离线核对头部高度'})
        }
        continue
    }
    foreach ($taskEntry in $taskDocument['Models']) {
        $taskModel=$taskEntry.ToObject([FirstPerson.Core.MeshModel])
        $taskInfo=($taskEntry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'
        $null=$taskParse.Invoke($taskModel,[object[]]@($taskInfo))
        $taskCustom.Add($taskModel.Name,$taskModel)
        $taskModels.Add([ordered]@{asset=$taskAsset.id;model=$taskModel})
    }
}

function New-TestActor($id) {
    $actor=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([Actor])
    $asset=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([ActorAsset])
    $asset.id=$id
    [Actor].GetField('asset',$taskFlags).SetValue($actor,$asset)
    return $actor
}
function Vec-Array($v) {return ,@($v.X,$v.Y,$v.Z)}
function Matrix-Array($m) {return ,@((Vec-Array $m.C0),(Vec-Array $m.C1),(Vec-Array $m.C2))}
function Export-Pose($mesh,$name,$time,$phase,$lift) {
    $parts=for ($i=0;$i -lt $mesh.Model.Parts.Length;$i++) {
        $p=$mesh.Model.Parts[$i]
        [ordered]@{name=$p.Name;parent=$p.Parent;pivot=(Vec-Array $p.Pivot);columns=(Matrix-Array $mesh.PartRotation[$i]);
            offset=(Vec-Array $mesh.PartOffset[$i]);scale=$mesh.PartScale[$i]}
    }
    return [ordered]@{name=$name;time=$time;phase=$phase;lift=$lift;position=(Vec-Array $mesh.Position);basis=(Matrix-Array $mesh.Basis);hidden=$mesh.Hidden;parts=@($parts)}
}

$taskSelection=[Collections.Generic.List[object]]::new()
foreach ($taskId in @('Cultiway.Werewolf','Cultiway.Bloodsucker','Cultiway.Bloodthirsty','Cultiway.BloodBeast','Cultiway.JinWu')) {
    $taskActor=New-TestActor $taskId
    foreach ($taskMovementCase in @(@($false,$false),@($true,$false),@($false,$true))) {
        [Actor].GetField('_is_moving',$taskFlags).SetValue($taskActor,$taskMovementCase[0])
        [Actor].GetField('_possessed_movement',$taskFlags).SetValue($taskActor,$taskMovementCase[1])
        $taskMoving=$taskMovementCase[0] -or $taskMovementCase[1]
        $taskArgs=[object[]]@($taskActor,'native_original')
        $null=$taskSelect.Invoke($null,$taskArgs)
        $taskExpected=$taskFamilies[$taskId]+$(if ($taskMoving) {'_moving'})
        if ($taskArgs[1] -ne $taskExpected) {throw ('变身选模错误：'+$taskId)}
        $taskSelection.Add([ordered]@{asset=$taskId;moving=$taskMoving;possessed=$taskMovementCase[1];family=$taskArgs[1]})
    }
}

$taskCameras=[Collections.Generic.List[object]]::new()
$taskFlights=[Collections.Generic.List[object]]::new()
$taskWolves=[Collections.Generic.List[object]]::new()
$taskCameraCases=0
foreach ($taskRecord in $taskModels) {
    $taskModel=$taskRecord.model
    $taskOriginalEye=[FirstPerson.Core.BeastEye]::Of($taskModel)
    $taskEyeArgs=[object[]]@($taskModel,$taskOriginalEye)
    $null=$taskEye.Invoke($null,$taskEyeArgs)
    $taskCalibrated=$taskEyeArgs[1]
    # 原生Offset会再次读取Of；这里将实际后置补丁的结果写回其缓存以执行同一段相机数学。
    $taskNativeShapes[$taskModel]=$taskCalibrated
    $taskMask=[FirstPerson.Core.BeastEye]::HiddenParts($taskModel)
    if ($taskMask -ne [uint64]::MaxValue -or $null -ne [FirstPerson.Core.BeastEye]::Muzzle($taskModel)) {throw ('附身本体仍会遮挡：'+$taskModel.Name)}
    $taskMinUp=[double]::PositiveInfinity
    foreach ($taskScale in @(0.5,1,2)) {foreach ($taskYaw in @(0,1.5707963,3.1415927,4.712389)) {foreach ($taskPitch in @(-70,0,70)) {
        $taskOffset=[FirstPerson.Core.BeastEye]::Offset($taskModel,[single]$taskScale,[single]$taskYaw,[single]$taskYaw,[single]$taskPitch)
        foreach ($taskValue in @($taskOffset.X,$taskOffset.Y,$taskOffset.Z)) {
            if ([single]::IsNaN($taskValue) -or [single]::IsInfinity($taskValue)) {throw ('附身眼位无效：'+$taskModel.Name)}
        }
        if ($taskOffset.Z -le 0) {throw ('附身眼位落到地面以下：'+$taskModel.Name)}
        $taskMinUp=[Math]::Min($taskMinUp,$taskOffset.Z)
        $taskCameraCases++
    }}}
    $taskCanFly=$taskFlight.Invoke($null,[object[]]@($taskModel))
    $taskNearName=if ($taskModel.Name.EndsWith('_m') -or $taskModel.Name.EndsWith('_f')) {$taskModel.Name.Substring(0,$taskModel.Name.Length-2)} else {$taskModel.Name}
    $taskLift=if ($taskCanFly) {$taskHeight.Invoke($null,[object[]]@($taskCustom[$taskNearName],[single]1))} else {0}
    $taskCameras.Add([ordered]@{asset=$taskRecord.asset;name=$taskModel.Name;eye=(Vec-Array $taskCalibrated.Eye);pivot=(Vec-Array $taskCalibrated.Pivot);
        hidden_parts=$taskMask;visible_own_parts=0;minimum_eye_height=$taskMinUp;flight_lift=$taskLift;finite_cases=36})
    if ($taskCanFly -or $taskModel.Info('cultiway_move_form') -eq 'wolf') {
        $taskRig=[FirstPerson.Core.CreatureRig]::new($taskModel)
        $taskPoses=[Collections.Generic.List[object]]::new()
        $taskWingValues=[Collections.Generic.List[double]]::new()
        $taskWing=if ($taskModel.Info('cultiway_move_form') -eq 'bat_swarm') {'bat_0_wing_l'} elseif ($taskModel.Info('cultiway_back_wings') -eq '1') {'wing_l2'} else {'wing_l'}
        $taskLegPoses=[Collections.Generic.List[string]]::new()
        $taskCases=@(@('rest',0,0,0.4,0),@('move_a',1,0.2,0.1,$taskLift),@('move_b',1,1.7,0.5,$taskLift),
            @('move_c',1,3.2,1.1,$taskLift),@('move_d',1,4.7,1.5,$taskLift))
        foreach ($taskCase in $taskCases) {
            $taskMesh=[FirstPerson.Core.MeshInstance]::new()
            $taskMesh.Model=$taskModel
            $taskMesh.Place(0,0,[single]$taskCase[4],[single](-[Math]::PI/2),1,1,1)
            $taskRig.Pose($taskMesh,[single]$taskCase[1],[single]$taskCase[2],[single]$taskCase[3],$false,[single]0,[single]0)
            $null=$taskPose.Invoke($null,[object[]]@($taskMesh,[single]$taskCase[1],[single]$taskCase[2],[single]$taskCase[3],$false,[single]0))
            $taskPoses.Add((Export-Pose $taskMesh $taskCase[0] $taskCase[3] $taskCase[2] $taskCase[4]))
            if ($taskCanFly -and $taskCase[1] -gt 0) {
                $taskWingIndex=$taskModel.PartIndex($taskWing)
                if ($taskWingIndex -lt 0) {throw ('缺少实际飞行翅膀：'+$taskModel.Name)}
                $taskR=$taskMesh.PartRotation[$taskWingIndex]
                $taskWingValues.Add($taskR.C0.Z+$taskR.C1.Z)
                $taskLegAngles=foreach ($taskPart in $taskModel.Parts) {
                    if ($taskPart.Name.StartsWith('leg_') -and $taskPart.VertexCount -gt 0) {
                        Matrix-Array $taskMesh.PartRotation[$taskModel.PartIndex($taskPart.Name)]
                    }
                }
                $taskLegPoses.Add(($taskLegAngles | ConvertTo-Json -Depth 8 -Compress))
            }
        }
        if ($taskCanFly) {
            $taskRange=($taskWingValues | Measure-Object -Minimum -Maximum)
            if ($taskRange.Maximum-$taskRange.Minimum -lt 0.1) {throw ('移动时没有振翅：'+$taskModel.Name)}
            if (@($taskLegPoses | Select-Object -Unique).Count -gt 1) {throw ('空中仍在迈腿：'+$taskModel.Name)}
            if ($taskModel.Info('cultiway_sun_ring') -eq '1') {
                $taskSun=$taskModel.PartIndex('sun_ring')
                if ($taskSun -lt 0 -or $taskModel.Parts[$taskSun].VertexCount -eq 0) {throw '金乌缺少实际太阳火环'}
                $taskSunRotations=@($taskPoses | Select-Object -Skip 1 | ForEach-Object {$_.parts[$taskSun].columns | ConvertTo-Json -Depth 6 -Compress} | Select-Object -Unique)
                if ($taskSunRotations.Count -lt 2) {throw '移动太阳火环没有随动'}
            }
            $taskFlights.Add([ordered]@{asset=$taskRecord.asset;name=$taskModel.Name;lift=$taskLift;wing=$taskWing;wing_range=$taskRange.Maximum-$taskRange.Minimum;legs_tucked=$true;poses=@($taskPoses)})
        } else {$taskWolves.Add([ordered]@{asset=$taskRecord.asset;name=$taskModel.Name;poses=@($taskPoses)})}
    }
}

# 验证同一份离地距离确实传给相机，以及水面不再扣除半身。
$taskLiftMethod=$taskIntegration.GetMethod('ReportCreatureLift',$taskFlags)
$taskWaterEye=$taskIntegration.GetMethod('CorrectAirborneEye',$taskFlags)
$taskAirborne=$taskIntegration.GetMethod('ReportCreatureAirborne',$taskFlags)
$taskMovementType=$taskIntegration.GetNestedType('CreatureMovement',[Reflection.BindingFlags]::NonPublic)
$taskMovements=$taskIntegration.GetField('creatureMovements',$taskFlags).GetValue($null)
foreach ($taskRow in $taskFlights | Where-Object {$_.name -notmatch '_[mf]$'}) {
    $taskActor=New-TestActor $taskRow.asset
    $taskState=[Activator]::CreateInstance($taskMovementType,$true)
    $taskState.drawnLift=[single]$taskRow.lift
    $taskState.tall=[single]1
    $taskState.flying=$true
    $taskMovements.Add($taskActor,$taskState)
    $taskArgs=[object[]]@($taskActor,[single]0,$true)
    $null=$taskLiftMethod.Invoke($null,$taskArgs)
    if ([Math]::Abs($taskArgs[1]-$taskRow.lift) -gt 0.00001) {throw '相机与身体飞行高度不一致'}
    $taskArgs=[object[]]@($taskActor,$true,[single]($taskRow.lift+0.3),$true)
    $null=$taskWaterEye.Invoke($null,$taskArgs)
    if ([Math]::Abs($taskArgs[2]-($taskRow.lift+0.8)) -gt 0.00001) {throw '水面飞行相机高度仍被扣减'}
    $taskArgs=[object[]]@($taskActor,$false)
    $null=$taskAirborne.Invoke($null,$taskArgs)
    if (-not $taskArgs[1]) {throw '离地状态未传给原生显示'}
}

# 用零时间步的已离地记录调用实际前置入口，验证水面和面板上下文。
# 构造单位未设为存活，避免离线调用Unity时钟；本项不模拟游戏起飞过程。
$taskNativeModels=$taskWorldfall.GetType('FirstPerson.Models',$true)
$taskOpaque=$taskNativeModels.GetMethod('OpaqueWidth',$taskFlags).CreateDelegate([Func[FirstPerson.Core.SpriteImage,single]])
$taskIntegration.GetField('opaqueCreatureWidth',$taskFlags).SetValue($null,$taskOpaque)
$taskPrepare=$taskIntegration.GetMethod('PrepareCreatureFrame',$taskFlags)
$taskWaterActor=New-TestActor 'Cultiway.Mallard'
[Actor].GetField('_possessed_movement',$taskFlags).SetValue($taskWaterActor,$true)
$taskWaterState=[Activator]::CreateInstance($taskMovementType,$true)
$taskWaterState.lift=[single]1
$taskMovements.Add($taskWaterActor,$taskWaterState)
$taskImage=[FirstPerson.Core.SpriteImage]::new()
$taskImage.Width=1
$taskImage.Height=1
$taskImage.Pixels=[uint32[]]@([uint32]::MaxValue)
$taskImage.MeasureOpaqueRows()
$taskMeshList=[Collections.Generic.List[FirstPerson.Core.MeshInstance]]::new()
$taskArgs=[object[]]@($taskMeshList,$taskWaterActor,$taskImage,[single]1,[single]1,[single]1,[single]0,[single]-0.4,$true,0)
$null=$taskPrepare.Invoke($null,$taskArgs)
if ([Math]::Abs($taskArgs[7]-1) -gt 0.00001 -or $taskArgs[8]) {throw '水面起飞未恢复水面高度或仍在沉水'}
$taskEnter=$taskIntegration.GetMethod('EnterCreatureFigure',$taskFlags)
$taskLeave=$taskIntegration.GetMethod('LeaveCreatureFigure',$taskFlags)
$taskFigureState=[object[]]@($false)
$null=$taskEnter.Invoke($null,$taskFigureState)
$taskArgs=[object[]]@($taskMeshList,$taskWaterActor,$null,[single]1,[single]1,[single]1,[single]0,[single]100,$true,0)
$null=$taskPrepare.Invoke($null,$taskArgs)
$null=$taskLeave.Invoke($null,$taskFigureState)
if ($taskArgs[7] -ne 100 -or -not $taskArgs[8]) {throw '面板预览改写了实际起落状态'}
$taskOutput=Join-Path $taskRoot $OutputFolder
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskResult=[ordered]@{passed=$true;assembly_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $Assembly) -Algorithm SHA256).Hash.ToLowerInvariant();
    method='实际原生骨架与当前动作补充；构造Actor只检查移动选模；校准眼位写入原生形状缓存后执行实际Offset/HiddenParts/Muzzle；未安装Harmony、未启动游戏';
    morph_selection_cases=$taskSelection.Count;flight_models=$taskFlights.Count;wolf_models=$taskWolves.Count;camera_models=$taskCameras.Count;camera_cases=$taskCameraCases;
    native_humanoid_species=$taskNativePeople;native_humanoid_camera_models=$taskPersonCameras.Count;water_and_figure_prefix_cases=2;game_test='pending_user_manual';
    selections=@($taskSelection);cameras=@($taskCameras);native_person_cameras=@($taskPersonCameras);flight=@($taskFlights);wolves=@($taskWolves)}
$taskResult | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $taskOutput 'runtime-check.json') -Encoding utf8
[ordered]@{passed=$true;morph_selection_cases=$taskSelection.Count;flight_models=$taskFlights.Count;wolf_models=$taskWolves.Count;camera_models=$taskCameras.Count;camera_cases=$taskCameraCases} | ConvertTo-Json -Compress
