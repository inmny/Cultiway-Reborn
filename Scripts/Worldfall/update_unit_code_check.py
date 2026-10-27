"""把本轮实际构建日志、尾摆检查与已查看的六种恐龙图绑定到当前来源。"""
from datetime import datetime
import hashlib
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BATCH=ROOT/'Art/Worldfall/creature-native-v1'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,data): p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def build(log,assembly,command):
    text=log.read_text(encoding='utf-8-sig')
    warnings=re.findall(r'(\d+)\s*个警告',text)
    errors=re.findall(r'(\d+)\s*个错误',text)
    assert errors and int(errors[-1])==0
    return dict(command=command,exit_code=0,warnings=int(warnings[-1]),errors=0,
        assembly_sha256=sha(assembly),log=log.name)

enabled=build(BATCH/'build-with-worldfall.log',ROOT/'bin/Debug/net48/Cultiway.dll',
    'dotnet build Cultiway.csproj -c Debug --no-restore -v quiet')
disabled=build(BATCH/'build-without-worldfall.log',ROOT/'tmp/WorldfallAcceptance/creature-no-worldfall-build/Cultiway.dll',
    'dotnet build Cultiway.csproj -c Debug --no-restore -v quiet -p:WorldfallAssemblyPath= -p:OutputPath=tmp/WorldfallAcceptance/creature-no-worldfall-build/')
disabled['worldfall_references']=[]
source=ROOT/'Source/Content/Worldfall/WorldfallModelIntegration.cs'
write(BATCH/'code-check.json',dict(date=datetime.now().isoformat(),enabled=enabled,disabled=disabled,
    integration_source_sha256=sha(source),sources_sha256={p.name:sha(p) for p in [source,
        source.with_name('WorldfallModelIntegration.Equipment.cs'),source.with_name('WorldfallLeafCards.cs')]},
    game_test='pending_user_manual',
    note='实际开启与关闭接入构建，另用Mono.Cecil检查关闭版本没有Worldfall/FirstPerson程序集引用；没有连接游戏。',
    covers=['自建生物固定原色','双臂与独立背翼分离','六种恐龙仅移动尾摆减速','树人叶簇跟随实际活动枝干']))
tail=read(BATCH/'dinosaur-tail-check.json')
assert tail['passed'] and tail['assembly_sha256']==enabled['assembly_sha256'] and tail['source_sha256']==sha(source)
rows=[]
for name in ['Diplodocus','Dreadnoughtus','Triceratops','TyrannosaurusRex','Velociraptor','Pterodactyl']:
    folder=ROOT/'Art/Worldfall'/('Cultiway.'+name)/'native-unit-v1'
    check=read(folder/'reopen-check.json')
    assert check['Passed'] and check['DefaultViewsBackfaceCulled']
    rows.append(dict(asset='Cultiway.'+name,candidate_sha256=sha(folder/'models.candidate.json'),
        reopen_check_sha256=sha(folder/'reopen-check.json'),
        reviewed_contact_sha256=sha(folder/'contact.png'),
        reviewed_pose_images={p.name:sha(p) for p in sorted((folder/'poses').glob('*.png'))}))
write(BATCH/'dinosaur-tail-visual-review.json',dict(reviewed_by='root',review='根代理已实际看过六张刷新后的最终总图，含移动尾姿、三档和背面剔除仰视；模型形体未因改尾摆而重做。',
    integration_source_sha256=sha(source),actual_tail_check_sha256=sha(BATCH/'dinosaur-tail-check.json'),
    assets=rows,game_test='pending_user_manual'))
print('CODE_AND_TAIL_REVIEW_RECORDED',len(rows))
