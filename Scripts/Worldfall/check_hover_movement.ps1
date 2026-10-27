# 水面喷气、月亮单位漂浮与附身隐藏的实际程序集离线回放，不操作游戏。
param([string]$Assembly='tmp/WorldfallMovement-20261026/water-float-held-build/Cultiway.dll',
      [string]$OutputFolder='Art/Worldfall/water-float-v1')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskGame=[IO.Path]::GetFullPath((Join-Path $taskRoot '../..'))
foreach ($file in @('worldbox_Data/Managed/UnityEngine.CoreModule.dll','worldbox_Data/Managed/Unity.Mathematics.dll',
    'worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll','worldbox_Data/StreamingAssets/mods/NML/Assemblies/0Harmony.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $taskGame $file)) | Out-Null
}
$null=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $taskRoot '../Worldfall/Assemblies/Worldfall.dll')))
$mod=[Reflection.Assembly]::LoadFrom((Join-Path $taskRoot $Assembly))
$null=[Reflection.Assembly]::LoadFrom('C:/Users/DELL/.nuget/packages/newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll')
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$integration=$mod.GetType('Cultiway.Content.Worldfall.WorldfallModelIntegration',$true)
$prepare=$integration.GetMethod('PrepareCreatureFrame',$flags)
$pose=$integration.GetMethod('PoseCreature',$flags)
$drawJet=$integration.GetMethod('DrawCreatureJetpack',$flags)
$models=$integration.GetField('CustomModels',$flags).GetValue($null)
$families=$integration.GetField('ActorFamilies',$flags).GetValue($null)
$movements=$integration.GetField('creatureMovements',$flags).GetValue($null)
$meshesState=$integration.GetField('movementMeshes',$flags).GetValue($null)
$nativeModels=[FirstPerson.WorldBoxMod].Assembly.GetType('FirstPerson.Models')
$integration.GetField('opaqueCreatureWidth',$flags).SetValue($null,$nativeModels.GetMethod('OpaqueWidth',$flags).CreateDelegate([Func[FirstPerson.Core.SpriteImage,single]]))
$image=[FirstPerson.Core.SpriteImage]::new()
$image.Width=1; $image.Height=1; $image.Pixels=[uint32[]]@([uint32]::MaxValue)
$image.MeasureOpaqueRows()
$parse=[FirstPerson.Core.MeshModel].GetMethod('ParseInfo',$flags)
$records=[Collections.Generic.List[object]]::new()
$cases=[Collections.Generic.List[object]]::new()
function V($value) {return ,@($value.X,$value.Y,$value.Z)}
function Matrix-Columns($value) {return ,@((V $value.C0),(V $value.C1),(V $value.C2))}
function Pose-Record($mesh,$label) {
    $parts=for ($i=0;$i -lt $mesh.Model.Parts.Length;$i++) {
        $p=$mesh.Model.Parts[$i]
        [ordered]@{name=$p.Name;parent=$p.Parent;pivot=(V $p.Pivot);columns=(Matrix-Columns $mesh.PartRotation[$i]);offset=(V $mesh.PartOffset[$i]);scale=$mesh.PartScale[$i]}
    }
    return [ordered]@{name=$label;hidden=$mesh.Hidden;position=(V $mesh.Position);basis=(Matrix-Columns $mesh.Basis);parts=@($parts)}
}
foreach ($short in @('Astartes','AdeptusCustodes','SistersSilence','YuChan','YueTu')) {
    $id='Cultiway.'+$short
    $jet=$short -notin @('YuChan','YueTu')
    $doc=[Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText((Join-Path $taskRoot ('Content/Worldfall/Models/'+$id+'/models.json'))))
    $family=$doc['Family'].ToString()
    $families.Add($id,$family)
    foreach ($entry in $doc['Models']) {
        $model=$entry.ToObject([FirstPerson.Core.MeshModel])
        $null=$parse.Invoke($model,[object[]]@(($entry['Info'].Properties() | ForEach-Object {$_.Name+'='+$_.Value.ToString()}) -join ';'))
        $models.Add($model.Name,$model)
    }
    foreach ($mode in @('land_idle','land_move','water_idle','water_move')) {
        $water=$mode.StartsWith('water'); $moving=$mode.EndsWith('move')
        $actor=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([Actor])
        $asset=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([ActorAsset]); $asset.id=$id
        [Actor].GetField('asset',$flags).SetValue($actor,$asset)
        [NanoObject].GetField('_alive',$flags).SetValue($actor,$true)
        [Actor].GetField('_is_moving',$flags).SetValue($actor,$moving)
        [Actor].GetField('_is_in_liquid',$flags).SetValue($actor,$water)
        $list=[Collections.Generic.List[FirstPerson.Core.MeshInstance]]::new()
        for ($step=0;$step -lt 60;$step++) {
            $arguments=[object[]]@($list,$actor,$image,[single]1,[single]1,[single]$(if ($moving) {1} else {0}),[single]0.05,[single]$(if ($water) {-0.4} else {0}),$water,0)
            $null=$prepare.Invoke($null,$arguments)
        }
        $state=$movements.GetOrCreateValue($actor)
        $airborne=if ($jet) {$water} else {$moving}
        if ($state.flying -ne $airborne -or ($airborne -and ($arguments[7] -le 0.01 -or $arguments[8]))) {throw ('悬空或水面恢复错误：'+$id+'/'+$mode)}
        if ($state.jetpack -ne ($jet -and $water)) {throw ('喷气背包使用场景错误：'+$id+'/'+$mode)}
        $cases.Add([ordered]@{asset=$id;mode=$mode;airborne=$state.flying;jetpack=$state.jetpack;height=$arguments[7];swimming=$arguments[8]})
        if (-not $airborne -or $mode -ne $(if ($jet) {'water_idle'} else {'land_move'})) {continue}
        foreach ($suffix in @('','_m','_f')) {
            $model=$models[$family+$suffix]
            $rig=[FirstPerson.Core.CreatureRig]::new($model)
            $body=[FirstPerson.Core.MeshInstance]::new(); $body.Model=$model
            $meshesState.Add($body,$state)
            $frames=foreach ($sample in @(0.2,0.8)) {
                $scale=1/[Math]::Max($model.InfoFloat('height',1),$model.InfoFloat('length',1))
                $body.Place(0,0,[single]$arguments[7],[single](-[Math]::PI/2),[single]$scale,[single]$scale,[single]$scale)
                $rig.Pose($body,[single]1,[single]$sample,[single]$sample,$false,[single]0,[single]0)
                $null=$pose.Invoke($null,[object[]]@($body,[single]1,[single]$sample,[single]$sample,$false,[single]0))
                $extra=$null; $extraPose=$null
                if ($jet) {
                    $list.Clear()
                    $null=$drawJet.Invoke($null,[object[]]@($list,$id,$body,$state.exhaust,[single]$sample))
                    if ($list.Count -ne 1) {throw '水面未画双喷口尾焰'}
                    $extra=[Newtonsoft.Json.Linq.JObject]::FromObject($state.exhaust.Model).ToObject([object])
                    $extraPose=Pose-Record $state.exhaust 'jets'
                    $body.Hidden=[uint64]::MaxValue; $list.Clear()
                    $null=$drawJet.Invoke($null,[object[]]@($list,$id,$body,$state.exhaust,[single]$sample))
                    if ($list.Count) {throw '附身仍画自己的尾焰'}
                    $body.Hidden=0
                }
                [ordered]@{body=(Pose-Record $body ('sample'+$sample));extra_model=$extra;extra_pose=$extraPose}
            }
            $legStates=foreach ($frame in $frames) {($frame.body.parts | Where-Object name -Like 'leg_*' | Select-Object columns,offset | ConvertTo-Json -Depth 8 -Compress)}
            if ($legStates[0] -ne $legStates[1]) {throw ('悬空时仍在迈腿：'+$model.Name)}
            $records.Add([ordered]@{asset=$id;name=$model.Name;mode=$mode;poses=@($frames)})
        }
    }
}
$output=Join-Path $taskRoot $OutputFolder
$null=[IO.Directory]::CreateDirectory($output)
[ordered]@{passed=$true;method='实际程序集执行水陆停走20种状态与15个距离档姿态；实际双喷口几何与附身隐藏回放；无游戏无补丁安装';game_test='pending_user_manual';state_cases=$cases.Count;posed_models=$records.Count;cases=@($cases);records=@($records)} |
    ConvertTo-Json -Depth 35 | Set-Content -LiteralPath (Join-Path $output 'runtime-check.json') -Encoding utf8
[ordered]@{passed=$true;state_cases=$cases.Count;posed_models=$records.Count} | ConvertTo-Json -Compress
