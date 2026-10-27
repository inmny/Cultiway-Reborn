# 用实际程序集比较待机与移动尾姿，不启动游戏。
$ErrorActionPreference = 'Stop'
$taskTailRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $taskTailRoot
. (Join-Path $PSScriptRoot 'export_unit_poses.ps1') -Folder 'Art/Worldfall/Cultiway.Dreadnoughtus/native-unit-v1'
$taskTailResult = [Collections.Generic.List[object]]::new()
$taskTailIds = @('Diplodocus','Dreadnoughtus','Triceratops','TyrannosaurusRex','Velociraptor','Pterodactyl','FleshHoundKhorne')
foreach ($taskTailId in $taskTailIds) {
    $taskTailPath = 'Content/Worldfall/Models/Cultiway.'+$taskTailId+'/models.json'
    $taskTailDoc = [Newtonsoft.Json.Linq.JObject]::Parse([IO.File]::ReadAllText((Join-Path $taskTailRoot $taskTailPath)))
    foreach ($taskTailEntry in $taskTailDoc['Models']) {
        $taskTailModel = $taskTailEntry.ToObject([FirstPerson.Core.MeshModel])
        $taskTailInfo = ($taskTailEntry['Info'].Properties() | ForEach-Object { $_.Name+'='+$_.Value.ToString() }) -join ';'
        $null = $taskParseInfo.Invoke($taskTailModel,[object[]]@($taskTailInfo))
        $taskTailRig = [FirstPerson.Core.CreatureRig]::new($taskTailModel)
        $taskTailPart = $taskTailModel.PartIndex('tail')
        if ($taskTailPart -lt 0) { throw ('缺少实际尾部：'+$taskTailModel.Name) }
        $taskTailMesh = [FirstPerson.Core.MeshInstance]::new()
        $taskTailMesh.Model = $taskTailModel
        $taskTailStandingError=0.0; $taskTailLegError=0.0; $taskTailFrequencyError=0.0; $taskTailUnchangedError=0.0
        $taskTailSeries = [Collections.Generic.List[object]]::new()
        for ($taskTailSample=0; $taskTailSample -le 240; $taskTailSample++) {
            $taskTailTime=[single]($taskTailSample*0.05)
            $taskTailPhase=[single]($taskTailTime*6.0)
            $taskTailRig.Pose($taskTailMesh,[single]0,$taskTailPhase,$taskTailTime,$false,[single]0,[single]0.17)
            $taskTailOldIdle=$taskTailMesh.PartRotation[$taskTailPart]
            $null=$taskSupplement.Invoke($null,[object[]]@($taskTailMesh,[single]0,$taskTailPhase,$taskTailTime,$false,[single]0.17))
            $taskTailNewIdle=$taskTailMesh.PartRotation[$taskTailPart]
            $taskTailStandingError=[Math]::Max($taskTailStandingError,[Math]::Abs($taskTailOldIdle.C0.Y-$taskTailNewIdle.C0.Y))
            $taskTailIdleAngle=[Math]::Atan2($taskTailNewIdle.C0.Y,$taskTailNewIdle.C0.X)
            $taskTailRig.Pose($taskTailMesh,[single]1,$taskTailPhase,$taskTailTime,$false,[single]0,[single]0.17)
            $taskTailOldMove=$taskTailMesh.PartRotation[$taskTailPart]
            $taskTailOldParts=$taskTailMesh.PartRotation.Clone()
            $null=$taskSupplement.Invoke($null,[object[]]@($taskTailMesh,[single]1,$taskTailPhase,$taskTailTime,$false,[single]0.17))
            $taskTailNewMove=$taskTailMesh.PartRotation[$taskTailPart]
            $taskTailMoveAngle=[Math]::Atan2($taskTailNewMove.C0.Y,$taskTailNewMove.C0.X)
            $taskTailFrequencyError=[Math]::Max($taskTailFrequencyError,[Math]::Abs($taskTailMoveAngle/0.43-$taskTailIdleAngle/0.18))
            for($taskTailP=0;$taskTailP -lt $taskTailModel.Parts.Length;$taskTailP++) {
                if($taskTailModel.Parts[$taskTailP].Name.StartsWith('leg_')) {
                    foreach($taskTailC in @('C0','C1','C2')) { foreach($taskTailAxis in @('X','Y','Z')) {
                        $taskTailLegError=[Math]::Max($taskTailLegError,[Math]::Abs($taskTailOldParts[$taskTailP].$taskTailC.$taskTailAxis-$taskTailMesh.PartRotation[$taskTailP].$taskTailC.$taskTailAxis))
                    }}
                }
            }
            $taskTailUnchangedError=[Math]::Max($taskTailUnchangedError,[Math]::Abs($taskTailOldMove.C0.Y-$taskTailNewMove.C0.Y))
            $taskTailSeries.Add([ordered]@{time=$taskTailTime;idle=$taskTailIdleAngle;move=$taskTailMoveAngle;original_move=[Math]::Atan2($taskTailOldMove.C0.Y,$taskTailOldMove.C0.X)})
        }
        $taskTailControl=$taskTailId -eq 'FleshHoundKhorne'
        if($taskTailStandingError -gt 0.000001 -or $taskTailLegError -gt 0.000001) { throw ('待机或腿部被改变：'+$taskTailModel.Name) }
        if($taskTailControl) { if($taskTailUnchangedError -gt 0.000001) { throw '非恐龙尾部被改变' } }
        elseif($taskTailFrequencyError -gt 0.000002) { throw ('移动与待机频率不符：'+$taskTailModel.Name) }
        $taskTailResult.Add([ordered]@{asset='Cultiway.'+$taskTailId;model=$taskTailModel.Name;control=$taskTailControl;
            production_sha256=(Get-FileHash -LiteralPath $taskTailPath -Algorithm SHA256).Hash.ToLowerInvariant();
            idle_rotation_error=$taskTailStandingError;leg_rotation_error=$taskTailLegError;normalized_moving_idle_error=$taskTailFrequencyError;
            non_dinosaur_tail_error=if($taskTailControl){$taskTailUnchangedError}else{$null};samples=@($taskTailSeries)})
    }
}
$taskTailOutput=[ordered]@{passed=$true;method='实际CreatureRig.Pose和当前PoseCreature逐时刻比较；不使用游戏实例';
    source_sha256=(Get-FileHash -LiteralPath 'Source/Content/Worldfall/WorldfallModelIntegration.cs' -Algorithm SHA256).Hash.ToLowerInvariant();
    assembly_sha256=(Get-FileHash -LiteralPath 'bin/Debug/net48/Cultiway.dll' -Algorithm SHA256).Hash.ToLowerInvariant();
    expected='待机频率不变；六种恐龙移动频率与待机一致，幅度沿用原生；腿部和其他单位尾姿不变';
    game_test='pending_user_manual';models=@($taskTailResult)}
$taskTailOutput | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath 'Art/Worldfall/creature-native-v1/dinosaur-tail-check.json' -Encoding utf8
[ordered]@{passed=$true;models=$taskTailResult.Count;dinosaurs=6;control='FleshHoundKhorne';samples_per_model=241} | ConvertTo-Json -Compress
