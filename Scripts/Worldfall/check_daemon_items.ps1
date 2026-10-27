# 四恶魔家族的髋腿位置和专属手持物：调用实际程序集，不进入游戏。
param([string]$Assembly='tmp/WorldfallMovement-20261026/daemon-final-build/Cultiway.dll',
      [switch]$RequireFixedItems,
      [string]$OutputFolder='Art/Worldfall/daemon-held-items-v1')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame=[IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
foreach ($file in @('worldbox_Data/Managed/UnityEngine.CoreModule.dll','worldbox_Data/Managed/Unity.Mathematics.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll','worldbox_Data/StreamingAssets/mods/NML/Assemblies/0Harmony.dll')) {
    $null=[Reflection.Assembly]::LoadFrom((Join-Path $taskGame $file))
}
$null=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$mod=[Reflection.Assembly]::LoadFrom((Join-Path $taskRoot $Assembly))
$null=[Reflection.Assembly]::LoadFrom('C:/Users/DELL/.nuget/packages/newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll')
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$integration=$mod.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration',$true)
$pose=$integration.GetMethod('PoseCreature',$flags)
$prepare=$integration.GetMethod('PrepareDaemonProportions',$flags)
$eye=$integration.GetMethod('CalibrateCreatureEye',$flags)
$nativeShapes=[FirstPerson.Core.BeastEye].GetField('Shapes',$flags).GetValue($null)
$parse=[FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$flags)
$assets=@('GreatUncleanOneButcher','GreatUncleanOneBellRinger','GreatUncleanOneRainFather','KeeperSecrets','KeeperSecretsNakari',
    'ExaltedKeeperSecrets','LordChange','KairosFateweaver','ExaltedLordChange','Bloodthirster','AnggrathUnbound','ExaltedBloodthirster')
$records=[Collections.Generic.List[object]]::new()
$loadedModels=[Collections.Generic.List[object]]::new()
$proportions=[Collections.Generic.List[object]]::new()
$cameraCases=0
$caseCount=0
function Vec-Array($v) {return ,@($v.X,$v.Y,$v.Z)}
function Matrix-Columns($m) {return ,@((Vec-Array $m.C0),(Vec-Array $m.C1),(Vec-Array $m.C2))}
function Export-BodyPose($mesh,$name) {
    $parts=for ($i=0;$i -lt $mesh.Model.Parts.Length;$i++) {
        $p=$mesh.Model.Parts[$i]
        [ordered]@{name=$p.Name;parent=$p.Parent;pivot=(Vec-Array $p.Pivot);columns=(Matrix-Columns $mesh.PartRotation[$i]);
            offset=(Vec-Array $mesh.PartOffset[$i]);scale=$mesh.PartScale[$i]}
    }
    return [ordered]@{name=$name;hidden=$mesh.Hidden;position=(Vec-Array $mesh.Position);basis=(Matrix-Columns $mesh.Basis);parts=@($parts)}
}
foreach ($short in $assets) {
    $id='Cultiway.'+$short
    $document=[Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText((Join-Path $taskRoot ('Content/Worldfall/Models/'+$id+'/models.json'))))
    foreach ($entry in $document['Models']) {
        $model=$entry.ToObject([FirstPerson.Core.MeshModel])
        $null=$parse.Invoke($model,[object[]]@(($entry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'))
        if ($RequireFixedItems -and $model.Info('cultiway_fixed_hand_items') -ne '1') {throw ('固定手持物尚未发布：'+$id)}
        $nativeModel=$entry.ToObject([FirstPerson.Core.MeshModel])
        $null=$parse.Invoke($nativeModel,[object[]]@(($entry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'))
        $nativeRig=[FirstPerson.Core.CreatureRig]::new($nativeModel)
        $nativeMesh=[FirstPerson.Core.MeshInstance]::new(); $nativeMesh.Model=$nativeModel
        $null=$prepare.Invoke($null,[object[]]@($model))
        $stretch=if($short.StartsWith('GreatUncleanOne')) {1.0} else {1.3}
        $hipIndex=$model.PartIndex('leg_l')
        $rise=$nativeModel.Parts[$hipIndex].Pivot.Z*($stretch-1)
        foreach($partIndex in 0..($model.Parts.Length-1)) {
            $part=$model.Parts[$partIndex]; $old=$nativeModel.Parts[$partIndex]
            $leg=$part.Name -in @('leg_l','leg_r')
            if([Math]::Abs($part.Pivot.Z-$old.Pivot.Z-$rise) -gt 0.00002) {throw '关节未随新髋高同步'}
            for($vertex=0;$vertex -lt $part.Pos.Length;$vertex+=3) {
                $expectedZ=if($leg) {$old.Pos[$vertex+2]*$stretch} else {$old.Pos[$vertex+2]+$rise}
                if($part.Pos[$vertex] -ne $old.Pos[$vertex] -or $part.Pos[$vertex+1] -ne $old.Pos[$vertex+1] -or
                    [Math]::Abs($part.Pos[$vertex+2]-$expectedZ) -gt 0.00002) {throw '加载后的腿长或上身位置不符'}
                $normalLength=[Math]::Sqrt([Math]::Pow($part.Nrm[$vertex],2)+[Math]::Pow($part.Nrm[$vertex+1],2)+[Math]::Pow($part.Nrm[$vertex+2],2))
                if([Math]::Abs($normalLength-1) -gt 0.002) {throw '拉长后法线异常'}
            }
        }
        $loaded=$entry.DeepClone()
        foreach($partIndex in 0..($model.Parts.Length-1)) {
            $loaded['Parts'][$partIndex]['Pos']=[Newtonsoft.Json.Linq.JArray]::FromObject($model.Parts[$partIndex].Pos)
            $loaded['Parts'][$partIndex]['Nrm']=[Newtonsoft.Json.Linq.JArray]::FromObject($model.Parts[$partIndex].Nrm)
            $loaded['Parts'][$partIndex]['Pivot']=[Newtonsoft.Json.Linq.JObject]::FromObject($model.Parts[$partIndex].Pivot)
        }
        $loaded['Max']=[Newtonsoft.Json.Linq.JObject]::FromObject($model.Max)
        $loaded['Info']['cultiway_leg_stretch']=[string]$stretch
        $loadedModels.Add($loaded)
        $proportions.Add([ordered]@{asset=$id;name=$model.Name;leg_stretch=$stretch;upper_body_rise=$rise;feet_plane_unchanged=$true})
        $eyeArgs=[object[]]@($model,[FirstPerson.Core.BeastEye]::Of($model))
        $null=$eye.Invoke($null,$eyeArgs)
        $oldEyeArgs=[object[]]@($nativeModel,[FirstPerson.Core.BeastEye]::Of($nativeModel))
        $null=$eye.Invoke($null,$oldEyeArgs)
        if([Math]::Abs($eyeArgs[1].Eye.Z-$oldEyeArgs[1].Eye.Z-$rise) -gt 0.00002) {throw '相机未随上身同步抬高'}
        $nativeShapes[$model]=$eyeArgs[1]
        if([FirstPerson.Core.BeastEye]::HiddenParts($model) -ne [uint64]::MaxValue -or
            $null -ne [FirstPerson.Core.BeastEye]::Muzzle($model)) {throw '新比例附身时本体仍遮挡'}
        foreach($yaw in @(0,1.5707963,3.1415927,4.712389)) {foreach($pitch in @(-70,0,70)) {
            $offset=[FirstPerson.Core.BeastEye]::Offset($model,[single]1,[single]$yaw,[single]$yaw,[single]$pitch)
            if($offset.Z -le 0 -or [single]::IsNaN($offset.Z) -or [single]::IsInfinity($offset.Z)) {throw '新比例相机高度异常'}
            $cameraCases++
        }}
        $rig=[FirstPerson.Core.CreatureRig]::new($model)
        $mesh=[FirstPerson.Core.MeshInstance]::new(); $mesh.Model=$model
        $frames=foreach ($sample in @(@('idle',0,0,0),@('move',1,1.2,0),@('attack',0,0,0.5))) {
            $mesh.Place(0,0,0,[single](-[Math]::PI/2),1,1,1)
            $nativeMesh.Place(0,0,0,[single](-[Math]::PI/2),1,1,1)
            $nativeRig.Pose($nativeMesh,[single]$sample[1],[single]$sample[2],[single]0.4,$false,[single]$sample[3],[single]0)
            $rig.Pose($mesh,[single]$sample[1],[single]$sample[2],[single]0.4,$false,[single]$sample[3],[single]0)
            $native=Export-BodyPose $nativeMesh 'native'
            $null=$pose.Invoke($null,[object[]]@($mesh,[single]$sample[1],[single]$sample[2],[single]0.4,$false,[single]0))
            foreach ($side in @('l','r')) {
                $index=$model.PartIndex('leg_'+$side); $part=$model.Parts[$index]
                $hip=[FirstPerson.Core.Vec3]::op_Addition($part.Pivot,$mesh.PartOffset[$index])
                if ([Math]::Abs($hip.Y) -gt 0.00002 -or ($hip.X -lt 0) -ne ($side -eq 'l')) {throw ('腿未回到躯干下方：'+$model.Name)}
                if ([Math]::Abs($mesh.PartOffset[$index].Z) -gt 0.00002) {throw '腿部调整改变了脚底高度'}
            }
            $grips=foreach ($name in @('held_item_r','held_item_l')) {
                $item=$model.PartIndex($name)
                if ($item -lt 0) {continue}
                $part=$model.Parts[$item]
                $itemAt=[FirstPerson.Core.PersonRig]::Transform($mesh,$item,$part.Pivot)
                $handAt=[FirstPerson.Core.PersonRig]::Transform($mesh,$part.Parent,$part.Pivot)
                $delta=[FirstPerson.Core.Vec3]::op_Subtraction($itemAt,$handAt)
                if ([FirstPerson.Core.Vec3]::Dot($delta,$delta) -gt 0.00000001) {throw '手持物脱离父手掌'}
                [ordered]@{part=$name;parent=$part.Parent;grip_error=0}
            }
            $caseCount++
            [ordered]@{name=$sample[0];native=$native;corrected=(Export-BodyPose $mesh $sample[0]);grips=@($grips)}
        }
        $records.Add([ordered]@{asset=$id;name=$model.Name;fixed_items=$model.Info('cultiway_fixed_hand_items') -eq '1';poses=@($frames)})
    }
}
$out=Join-Path $taskRoot $OutputFolder
$null=[IO.Directory]::CreateDirectory($out)
[ordered]@{Models=@($loadedModels);proportions=@($proportions);method='实际调用与游戏文件加载相同的PrepareDaemonProportions；大不净者保持原长，其余九类腿长1.3倍'} |
    ConvertTo-Json -Depth 24 -Compress | Set-Content -LiteralPath (Join-Path $out 'loaded-models.json') -Encoding utf8
[ordered]@{passed=$true;required_fixed_items=[bool]$RequireFixedItems;assets=$assets.Count;models=$records.Count;pose_cases=$caseCount;
    camera_cases=$cameraCases;possessed_body_and_muzzle_hidden=$true;
    assembly_sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $Assembly) -Algorithm SHA256).Hash.ToLowerInvariant();
    method='实际加载比例、CreatureRig与PoseCreature回放：三族腿长增加30%，大不净者不变；髋部归中、脚底保持，固定道具随父臂一致';game_test='pending_user_manual';records=@($records)} |
    ConvertTo-Json -Depth 24 | Set-Content -LiteralPath (Join-Path $out 'runtime-check.json') -Encoding utf8
[ordered]@{passed=$true;assets=$assets.Count;models=$records.Count;pose_cases=$caseCount} | ConvertTo-Json -Compress
