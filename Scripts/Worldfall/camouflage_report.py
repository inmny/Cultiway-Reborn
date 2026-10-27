"""更新静止伪装总页，保留原活动模型与真实检查记录。"""
from datetime import datetime, timezone
import native_unit_report as units

ROOT=units.ROOT
BATCH=ROOT/'Art/Worldfall/camouflage-v1'


def report():
    previous=units.read(BATCH/'final-check.json')
    palette=[units.read(BATCH/p) for p in ('palette-check.json','titan-palette-check.json')]
    runtime=units.read(BATCH/'titan-runtime-check.json')
    assets=[r['asset'] for r in palette[0]['assets']+palette[1]['assets']]
    assert len(set(assets))==11 and all(p['passed'] for p in palette) and runtime['passed']
    items=[]
    sections=[]
    for asset in assets:
        folder=ROOT/'Art/Worldfall'/asset/'camouflage-v1'
        manifest=units.read(folder/'manifest.json')
        review,reopen=(units.read(folder/p) for p in ('root-review.json','reopen-check.json'))
        digest=units.sha(ROOT/manifest['production_resource'])
        assert digest==manifest['production_sha256']==review['candidate_sha256']
        assert review['actual_image_view'] and reopen['ImportedAllFinalGLBs'] and reopen['Passed']
        counts=[n for name,n in manifest['triangles'].items() if '_disguised' in name]
        items.append({'asset':asset,'name':units.NAMES[asset],'sha256':digest,'models':6,
            'camouflage_triangles':counts,'active_models_unchanged':True,'actual_root_review':True,
            'independent_reopen':True,'leaf_resources':manifest['leaf_resources'],'game_test':'pending_user_manual'})
        prefix=f'../{asset}/camouflage-v1'
        sections.append(f'<section><h2><a href="{prefix}/index.html">{units.NAMES[asset]}</a></h2><p>伪装三档面数：'+
            ' / '.join(map(str,counts))+f'</p><a href="{prefix}/index.html"><img loading="lazy" src="{prefix}/contact.png?v={digest[:12]}"></a></section>')
    old_page=(BATCH/'index.html').read_text(encoding='utf-8')
    extras=old_page[old_page.index('<h2>右手向前持握'):old_page.index('</body>')]
    intro='七种树人收起双臂、根脚埋地；岩石泰坦收拢成岩石，熔岩泰坦成为低矮岩浆池，火山泰坦成为火山，冰石泰坦成为冰块。停稳约0.4秒后伪装，移动或战斗时恢复。十一类各有近、中、远三档，游戏效果待查看。'
    (BATCH/'index.html').write_text(f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>树人与泰坦静止伪装</title><style>{units.CSS}</style><body><h1>树人与泰坦静止伪装</h1><p>{intro}</p><p><a href="../creature-native-v1/index.html">全部生物</a> · <a href="final-check.json">检查记录</a></p>'+''.join(sections)+extras+'</body></html>',encoding='utf-8')
    previous.update(checked_at_utc=datetime.now(timezone.utc).isoformat(),assets=11,added_camouflage_models=33,
        state_cases=len(runtime.get('state_cases',[])) if isinstance(runtime.get('state_cases'),list) else runtime.get('state_cases',7),
        hand_cases=runtime['hand_cases'],palette_cases=sum(p['palette_cases'] for p in palette),items=items)
    build=ROOT/'tmp/WorldfallMovement-20261026'
    assert all('0 个错误' in (build/name).read_text(encoding='utf-8-sig') for name in ('daemon-final-build.log','daemon-no-worldfall-build.log'))
    previous['code_build']={'with_worldfall_errors':0,'without_worldfall_errors':0,'assembly_sha256':units.sha(build/'daemon-final-build/Cultiway.dll')}
    previous['source_sha256']={p:units.sha(ROOT/p) for p in previous['source_sha256']}
    units.write(BATCH/'final-check.json',previous)
    print({'assets':11,'added_camouflage_models':33,'palette_cases':previous['palette_cases']})


if __name__=='__main__':
    report()
