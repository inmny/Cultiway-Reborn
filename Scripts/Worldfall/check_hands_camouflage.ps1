# 离线调用实际骨架、握物方向和伪装停稳记录；不启动游戏。
param(
    [string]$Assembly = 'tmp/WorldfallHandsBodies-20261026/build/Cultiway.dll',
    [switch]$IncludeCamouflage,
    [string]$Output = 'Art/Worldfall/camouflage-v1/runtime-check.json'
)
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
$taskParse = [FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$taskFlags)
$taskBasisMethod = $taskIntegration.GetMethod('ForwardHeldItemBasis',$taskFlags)
$taskPalmMethod = $taskIntegration.GetMethod('FindCreaturePalm',$taskFlags)
$taskFacePerson = $taskIntegration.GetMethod('FacePersonHeldItemForward',$taskFlags)
$taskPose = $taskIntegration.GetMethod('PoseCreature',$taskFlags)

function Read-Model($entry) {
    $model = $entry.ToObject([FirstPerson.Core.MeshModel])
    $info = ($entry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'
    $null = $taskParse.Invoke($model,[object[]]@($info))
    return $model
}
function Assert-Close($actual,$expected,$message) {
    if ([Math]::Abs($actual-$expected) -gt 0.00002) {throw $message}
}
function Vec-Array($vector) {return ,@($vector.X,$vector.Y,$vector.Z)}

$taskStateType = $taskIntegration.GetNestedType('CamouflageState',[Reflection.BindingFlags]::NonPublic)
$taskState = [Activator]::CreateInstance($taskStateType,$true)
$taskUse = $taskStateType.GetMethod('UseDisguise',$taskFlags)
$taskStateCases = @(
    # 未出手的新单位，包括世界时间为零、世界暂停的情况。
    @($false,10,0,$false),@($false,10.2,0,$false),@($false,10.5,0,$true),
    # 开始移动立即恢复，停下后重新等待。
    @($true,10.6,0,$false),@($false,10.8,0,$false),@($false,11.1,0,$true),
    # 真正出手立即恢复；暂停后相同的攻击记录不能永远阻止伪装。
    @($false,11.2,100,$false),@($false,11.4,100,$false),@($false,11.7,100,$true),
    @($false,11.8,101,$false),@($false,12.3,101,$true),
    # 入水、飞行、磁铁拖动、死亡等活动状态仍持续阻止伪装。
    @($true,12.4,101,$false),@($true,13,101,$false),
    @($false,13.2,101,$false),@($false,13.5,101,$true)
)
foreach ($case in $taskStateCases) {
    $actual = $taskUse.Invoke($taskState,[object[]]@([bool]$case[0],[single]$case[1],[double]$case[2]))
    if ($actual -ne $case[3]) {throw '伪装停稳、出手或暂停后的切换时机不符'}
}

$taskHandCases = [Collections.Generic.List[object]]::new()
$taskHandPreview = $null
foreach ($taskId in @('Cultiway.FishPeopleShaman','Cultiway.EasternHuman','Cultiway.HalfDeerMan','Cultiway.Centaur','Cultiway.Mermaid')) {
    $taskPath = Join-Path $taskRoot ('Content/Worldfall/Models/'+$taskId+'/models.json')
    $taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskPath))
    foreach ($taskEntry in $taskDocument['Models']) {
        $taskModel = Read-Model $taskEntry
        $taskPerson = $taskDocument['Humanoid'].ToObject([bool])
        if (-not $taskPerson -and -not $taskIntegration.GetMethod('HasCreatureItemHand',$taskFlags).Invoke($null,[object[]]@($taskModel))) {
            throw ('真实掌臂仍被持物入口排除：'+$taskModel.Name)
        }
        $taskRig = if ($taskPerson) {[FirstPerson.Core.PersonRig]::new($taskModel)} else {[FirstPerson.Core.CreatureRig]::new($taskModel)}
        foreach ($taskHeading in @(-1.5707963,0,1.5707963,3.1415927)) {
            $taskMesh = [FirstPerson.Core.MeshInstance]::new()
            $taskMesh.Model = $taskModel
            $taskMesh.Place(0,0,0,[single]$taskHeading,$(if ($taskPerson) {-1} else {1}),1,1)
            if ($taskPerson) {$taskRig.Pose($taskMesh,[single]0,[single]0,[single]0,[single]0,[single]0,[single]0,[single]0,$null)}
            else {$taskRig.Pose($taskMesh,[single]0,[single]0,[single]0.4,$false,[single]0,[single]0)}
            $taskForward = $taskMesh.Basis.Mul([FirstPerson.Core.Vec3]::new(0,-1,0)).Normalized()
            $taskUp = $taskMesh.Basis.C2.Normalized()
            $taskRight = [FirstPerson.Core.Vec3]::Cross($taskForward,$taskUp).Normalized()
            if ($taskPerson) {$taskPalm=$taskRig.HandWorld($taskMesh)}
            else {
                $taskArm = $taskModel.PartIndex('wing_l')
                $taskLocalPalm = $taskPalmMethod.Invoke($null,[object[]]@($taskModel.Parts[$taskArm]))
                $taskMesh.PartRotation[$taskArm] = [FirstPerson.Core.Mat3]::op_Multiply([FirstPerson.Core.Mat3]::RotX(-0.4),$taskMesh.PartRotation[$taskArm])
                $taskPalm = [FirstPerson.Core.PersonRig]::Transform($taskMesh,$taskArm,$taskLocalPalm)
            }
            if ([FirstPerson.Core.Vec3]::Dot($taskPalm,$taskRight) -le 0 -or $taskPalm.Z -le 0) {throw ('不是实际右手或手心低于地面：'+$taskModel.Name)}
            foreach ($taskKindName in @('Sword','Axe','Spear','Hammer','Bow','Staff','Gun','Dagger','Shotgun')) {
                $taskKind = [Enum]::Parse([FirstPerson.Core.WeaponKind],$taskKindName)
                $taskBasis = $taskBasisMethod.Invoke($null,[object[]]@($taskMesh,$taskKind,[single]1))
                Assert-Close ([FirstPerson.Core.Vec3]::Dot($taskBasis.C0,[FirstPerson.Core.Vec3]::Cross($taskBasis.C1,$taskBasis.C2))) 1 '武器方向镜像或不正交'
                foreach ($taskAxis in @($taskBasis.C0,$taskBasis.C1,$taskBasis.C2)) {Assert-Close ([FirstPerson.Core.Vec3]::Dot($taskAxis,$taskAxis)) 1 '武器轴长度错误'}
                $taskAim = if ($taskKindName -in @('Gun','Shotgun','Bow')) {$taskBasis.C1} else {$taskBasis.C2}
                if ([FirstPerson.Core.Vec3]::Dot($taskAim,$taskForward) -lt 0.2) {throw ('武器仍朝后：'+$taskModel.Name+'/'+$taskKindName)}
                if ($taskPerson) {
                    $taskArgs = [object[]]@($taskRig,$taskMesh,$taskKind,[single]0,[single]1,[FirstPerson.Core.Vec3]::new(99,99,99),[FirstPerson.Core.Mat3]::Identity,$true)
                    $null = $taskFacePerson.Invoke($null,$taskArgs)
                    Assert-Close $taskArgs[5].X $taskPalm.X '人物右手挂点错误'
                    Assert-Close $taskArgs[5].Y $taskPalm.Y '人物右手挂点错误'
                    $taskArgs[3]=[single]0.5
                    $taskArgs[5]=[FirstPerson.Core.Vec3]::new(99,99,99)
                    $null = $taskFacePerson.Invoke($null,$taskArgs)
                    Assert-Close $taskArgs[5].X 99 '人物原生攻击位置被覆盖'
                }
                $taskLook = [FirstPerson.Core.WeaponLook]::new()
                $taskLook.Kind=$taskKind
                $taskLook.Head=[uint32]4289449455
                $taskLook.Handle=[uint32]4287271714
                $taskLook.Accent=[uint32]4294956083
                $taskWeapon = [FirstPerson.Core.ViewMeshes]::Item($taskLook)
                $taskGripZ = if ($taskKindName -in @('Staff','Spear')) {$taskWeapon.Min.Z*0.5} else {0}
                $taskScale = $taskModel.InfoFloat('height',1)
                $taskBottom = [FirstPerson.Core.Vec3]::new(
                    $(if ($taskBasis.C0.Z -lt 0) {$taskWeapon.Max.X} else {$taskWeapon.Min.X}),
                    $(if ($taskBasis.C1.Z -lt 0) {$taskWeapon.Max.Y} else {$taskWeapon.Min.Y}),
                    $(if ($taskBasis.C2.Z -lt 0) {$taskWeapon.Max.Z-$taskGripZ} else {$taskWeapon.Min.Z-$taskGripZ}))
                $taskBottomZ=$taskBasis.Mul($taskBottom).Z
                if ($taskBottomZ -lt 0) {
                    $taskScale = [Math]::Min($taskScale,$taskPalm.Z*0.95/-$taskBottomZ)
                }
                $taskLowest = [double]::PositiveInfinity
                foreach ($taskPart in $taskWeapon.Parts) {
                    for ($taskV=0;$taskV -lt $taskPart.VertexCount;$taskV++) {
                        $taskLocal=[FirstPerson.Core.Vec3]::new($taskPart.Pos[$taskV*3],$taskPart.Pos[$taskV*3+1],$taskPart.Pos[$taskV*3+2]-$taskGripZ)
                        $taskWorld=$taskBasis.Mul($taskLocal)
                        $taskLowest=[Math]::Min($taskLowest,$taskPalm.Z+$taskWorld.Z*$taskScale)
                    }
                }
                if (-not $taskPerson -and $taskLowest -lt -0.00002) {throw ('生物长柄穿地：'+$taskModel.Name+'/'+$taskKindName)}
                $taskHandCases.Add([ordered]@{model=$taskModel.Name;heading=$taskHeading;weapon=$taskKindName;right_hand=$true;forward=[FirstPerson.Core.Vec3]::Dot($taskAim,$taskForward);lowest_z=$taskLowest})
                if ($taskModel.Name -eq 'creature_cultiway_Cultiway.FishPeopleShaman' -and $taskHeading -eq -1.5707963 -and $taskKindName -eq 'Staff') {
                    $taskPartStates = for ($taskIndex=0;$taskIndex -lt $taskModel.Parts.Length;$taskIndex++) {
                        $taskPart=$taskModel.Parts[$taskIndex]
                        $taskRotation=$taskMesh.PartRotation[$taskIndex]
                        [ordered]@{parent=$taskPart.Parent;pivot=(Vec-Array $taskPart.Pivot);
                            columns=@((Vec-Array $taskRotation.C0),(Vec-Array $taskRotation.C1),(Vec-Array $taskRotation.C2));
                            offset=(Vec-Array $taskMesh.PartOffset[$taskIndex]);scale=$taskMesh.PartScale[$taskIndex]}
                    }
                    $taskWeaponPosition=[FirstPerson.Core.Vec3]::op_Subtraction($taskPalm,[FirstPerson.Core.Vec3]::op_Multiply($taskBasis.Mul([FirstPerson.Core.Vec3]::new(0,0,$taskGripZ)),[single]$taskScale))
                    $taskHandPreview=[ordered]@{Body=$taskEntry;Pose=@{hidden=$taskMesh.Hidden;basis=@((Vec-Array $taskMesh.Basis.C0),(Vec-Array $taskMesh.Basis.C1),(Vec-Array $taskMesh.Basis.C2));parts=@($taskPartStates)};
                        Weapon=[Newtonsoft.Json.Linq.JObject]::FromObject($taskWeapon);WeaponPosition=(Vec-Array $taskWeaponPosition);
                        WeaponScale=$taskScale;WeaponBasis=@((Vec-Array $taskBasis.C0),(Vec-Array $taskBasis.C1),(Vec-Array $taskBasis.C2))}
                }
            }
        }
    }
}

$taskDisguises = [Collections.Generic.List[object]]::new()
$taskLeafCases = 0
if ($IncludeCamouflage) {
    $taskColors = $taskIntegration.GetMethod('UseCreatureColors',$taskFlags)
    $taskLeafType = $taskCultiway.GetType('Cultiway.Content.Worldfall.WorldfallLeafCards',$true)
    $taskCardType = $taskLeafType.GetNestedType('LeafCard',[Reflection.BindingFlags]::NonPublic)
    $taskMakeLeaf = $taskLeafType.GetMethod('MakeCreatureLeafCard',$taskFlags)
    foreach ($taskId in @('AcaciaTreants','BanyanTreants','CoconutTreants','OakTreants','SycamoreTreants','DeathTreants','FireTreants','RockGiant','LavaGiant','IceGiant','VolcanicGiant')) {
        if (-not $taskIntegration.GetField('CamouflageActors',$taskFlags).GetValue($null).Contains('Cultiway.'+$taskId)) {
            throw ('伪装单位未接入：'+$taskId)
        }
        $taskPath = Join-Path $taskRoot ('Content/Worldfall/Models/Cultiway.'+$taskId+'/models.json')
        $taskDocument = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskPath))
        $taskFamily = $taskDocument['Family'].ToString()
        foreach ($taskSuffix in @('','_m','_f')) {
            $taskEntry = $taskDocument['Models'] | Where-Object {$_.get_Item('Name').ToString() -eq $taskFamily+'_disguised'+$taskSuffix} | Select-Object -First 1
            if (-not $taskEntry) {throw ('缺少伪装距离档：'+$taskId+$taskSuffix)}
            $taskModel = Read-Model $taskEntry
            $taskMesh = [FirstPerson.Core.MeshInstance]::new()
            $taskMesh.Model = $taskModel
            $taskMesh.Place(0,0,0,[single](-[Math]::PI/2),1,1,1)
            $taskRig = [FirstPerson.Core.CreatureRig]::new($taskModel)
            $taskRig.Pose($taskMesh,[single]1,[single]1.2,[single]0.8,$false,[single]0.5,[single]0.2)
            $null=$taskPose.Invoke($null,[object[]]@($taskMesh,[single]1,[single]1.2,[single]0.8,$false,[single]0.2))
            foreach ($taskRotation in $taskMesh.PartRotation) {
                Assert-Close $taskRotation.C0.X 1 '伪装仍叠加活动姿态'
                Assert-Close $taskRotation.C1.Y 1 '伪装仍叠加活动姿态'
                Assert-Close $taskRotation.C2.Z 1 '伪装仍叠加活动姿态'
            }
            $taskColorArgs=[object[]]@($taskModel,$null,$null)
            $null=$taskColors.Invoke($null,$taskColorArgs)
            if ($taskColorArgs[2].Length -ne $taskModel.SlotColor.Length -or @($taskColorArgs[2] | Where-Object {$_ -ne 0}).Count) {throw '伪装原配色被覆盖'}
            $taskLeafPath=Join-Path $taskRoot ('Content/Worldfall/LeafCards/Cultiway.'+$taskId+'.disguised.json')
            $taskCardsChecked=0
            if (Test-Path -LiteralPath $taskLeafPath) {
                $taskRecipe=[Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText($taskLeafPath))
                $taskLevel=if ($taskSuffix -eq '_m') {'Middle'} elseif ($taskSuffix -eq '_f') {'Far'} else {'Near'}
                foreach ($taskCardEntry in $taskRecipe['Levels'][$taskLevel]) {
                    $taskCard=$taskCardEntry.ToObject($taskCardType)
                    $taskPart=$taskModel.PartIndex($taskCard.Part)
                    if ($taskPart -lt 0) {throw '伪装叶簇缺少枝干'}
                    $taskBoard=$taskMakeLeaf.Invoke($null,[object[]]@($taskCard,$taskMesh,$null,$taskPart))
                    Assert-Close $taskBoard.X $taskCard.X '伪装叶片位置错误'
                    Assert-Close $taskBoard.Y $taskCard.Y '伪装叶片位置错误'
                    Assert-Close $taskBoard.Z $taskCard.Z '伪装叶片位置错误'
                    Assert-Close $taskBoard.Width $taskCard.Width '伪装叶片比例错误'
                    Assert-Close $taskBoard.Height $taskCard.Height '伪装叶片比例错误'
                    $taskCardsChecked++
                    $taskLeafCases++
                }
            }
            $taskDisguises.Add([ordered]@{name=$taskModel.Name;static_pose=$true;authored_colors_preserved=$true;leaf_cards_checked=$taskCardsChecked;height=$taskModel.Max.Z-$taskModel.Min.Z;scale_height=$taskModel.InfoFloat('height',0)})
        }
    }
}
$taskResult=[ordered]@{passed=$true;method='实际Worldfall骨架、武器网格和当前修仙程序集纯数据调用；无游戏、无GPU';
    assembly_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $Assembly) -Algorithm SHA256).Hash.ToLowerInvariant();
    state_cases=$taskStateCases.Count;hand_cases=$taskHandCases.Count;camouflage_models=$taskDisguises.Count;leaf_card_cases=$taskLeafCases;
    game_test='pending_user_manual';hands=@($taskHandCases);disguises=@($taskDisguises)}
$taskOutput=Join-Path $taskRoot $Output
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskOutput)) | Out-Null
$taskResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskOutput -Encoding utf8
if ($null -ne $taskHandPreview) {
    $taskHandPreview | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($taskOutput)) 'hand-preview.json') -Encoding utf8
}
[ordered]@{passed=$true;state_cases=$taskStateCases.Count;hand_cases=$taskHandCases.Count;camouflage_models=$taskDisguises.Count} | ConvertTo-Json -Compress
