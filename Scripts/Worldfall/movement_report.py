"""整理移动形态、飞行三档与附身视角检查，不把离线回放写成游戏实测。"""
import argparse
from datetime import datetime, timezone
import hashlib
import html
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import native_unit_report as units

ROOT=units.ROOT
BATCH=ROOT/'Art/Worldfall/movement-v1'
ASSETS=('Cultiway.Werewolf','Cultiway.Bloodsucker','Cultiway.Bloodthirsty','Cultiway.BloodBeast','Cultiway.JinWu')
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)


def pose_sha(runtime):
    return hashlib.sha256(json.dumps([runtime['flight'],runtime['wolves']],sort_keys=True,separators=(',',':')).encode()).hexdigest()


def contacts():
    render=units.read(BATCH/'render-check.json')
    pages=[]
    for start in range(0,len(render['groups']),4):
        group=render['groups'][start:start+4]
        sheet=Image.new('RGB',(1200,len(group)*270),'#26323e')
        draw=ImageDraw.Draw(sheet)
        for row,record in enumerate(group):
            label=units.NAMES.get(record['asset'],record['asset'])
            for col,(key,caption) in enumerate((('move_a','移动 A'),('move_b','移动 B'),('middle','中景同尺度'),('far','远景同尺度'))):
                image=Image.open(BATCH/record['images'][key]).convert('RGBA')
                image.thumbnail((290,238),Image.Resampling.LANCZOS)
                sheet.paste(image,(col*300+(300-image.width)//2,row*270+29),image)
                draw.text((col*300+5,row*270+5),label+' · '+caption,font=FONT,fill='white')
        filename=f'contact-motion-{start//4+1:02}.png'
        sheet.save(BATCH/filename)
        pages.append(filename)
    units.write(BATCH/'contact-files.json',pages)
    print(json.dumps({'contacts':pages},ensure_ascii=False))


def finish():
    runtime=units.read(BATCH/'runtime-check.json')
    render=units.read(BATCH/'render-check.json')
    geometry=units.read(BATCH/'geometry-check.json')
    visual=units.read(BATCH/'root-runtime-review.json')
    palette=units.read(BATCH/'palette-check.json')
    assert runtime['passed'] and runtime['flight_models']==72 and runtime['wolf_models']==3
    assert runtime['morph_selection_cases']==15 and runtime['water_and_figure_prefix_cases']==2
    assert runtime['native_humanoid_camera_models']==24 and runtime['camera_models']==435
    assert geometry['passed'] and render['passed'] and render['backface_culled']
    assert pose_sha(runtime)==render['pose_data_sha256']==geometry['pose_data_sha256']==visual['pose_data_sha256']
    assert visual['actual_image_view'] and visual['reviewer']=='root'
    assert len(visual['viewed'])==len(units.read(BATCH/'contact-files.json'))
    published=[]
    for asset in ASSETS:
        folder=ROOT/'Art/Worldfall'/asset/'movement-v1'
        manifest=units.read(folder/'manifest.json')
        production=ROOT/'Content/Worldfall/Models'/asset/'models.json'
        assert manifest['production_sha256']==units.sha(production)==units.sha(folder/'models.candidate.json')
        assert manifest['review']['actual_image_view']
        published.append({'asset':asset,'models':manifest['models'],'sha256':manifest['production_sha256']})
    # 人物、群体原色均按本次实际读取与配色检查更新，其他资产保留各自原检查。
    path=ROOT/'Art/Worldfall/creature-native-v1/integration-check.json'
    integration=units.read(path)
    revised={r['asset']:r for r in palette['assets']}
    integration['assets']=[revised.get(r['asset'],r) for r in integration['assets']]
    integration['palette_cases']=sum(m.get('sampled_frames',0) for r in integration['assets'] for m in r['models'])
    integration['movement_revision']={'runtime_check':'Art/Worldfall/movement-v1/runtime-check.json','palette_check':'Art/Worldfall/movement-v1/palette-check.json',
        'changed_model_assets':list(ASSETS),'game_test':'pending_user_manual'}
    units.write(path,integration)
    registry=units.read(units.REGISTRY)
    for row in registry:
        if row['Id']=='Cultiway.BloodBeast': row['Shape']='WingedBiped'
    units.write(units.REGISTRY,registry)
    giant_manifest=ROOT/'Art/Worldfall/Cultiway.BloodBeast/movement-v1/manifest.json'
    manifest=units.read(giant_manifest)
    manifest['shape']='WingedBiped'
    units.write(giant_manifest,manifest)
    build=ROOT/'tmp/WorldfallMovement-20261026'
    assert '0 个错误' in (build/'build.log').read_text(encoding='utf-8-sig')
    assert '0 个错误' in (build/'build-no-worldfall.log').read_text(encoding='utf-8-sig')
    assert units.sha(build/'build/Cultiway.dll')==runtime['assembly_sha256']
    sources=['Source/Content/Worldfall/WorldfallModelIntegration'+suffix+'.cs' for suffix in ('','.Movement','.Possession','.Equipment')]
    final={'passed':True,'checked_at_utc':datetime.now(timezone.utc).isoformat(),'published':published,
        'flying_species':len({r['asset'] for r in runtime['flight']}),'flight_models':72,'wolf_models':3,
        'morph_selection_cases':15,'water_and_figure_prefix_cases':2,'camera_models':435,'camera_cases':15660,'native_person_camera_models':24,
        'pose_data_sha256':pose_sha(runtime),'all_flying_sample_vertices_above_ground':True,
        'minimum_normalized_flight_clearance':min(r['minimum_moving_z'] for r in geometry['models'] if r['flying']),
        'own_creature_visible_parts':0,'with_worldfall_build_errors':0,'without_worldfall_build_errors':0,
        'source_sha256':{p:units.sha(ROOT/p) for p in sources},'assembly_sha256':runtime['assembly_sha256'],
        'game_test':'pending_user_manual','limitations':['离线使用实际程序集回放，未安装补丁到运行游戏；地形、遮挡、进出水面和实际附身切换待游戏查看',
            '原生人形沿用自身隐藏路径，本次另外核对24个人形模型的默认眼高与头部范围']}
    units.write(BATCH/'final-check.json',final)
    cards=''.join(f'<section><h2>{html.escape(units.NAMES.get(asset,asset))}</h2><a href="../{asset}/movement-v1/index.html"><img loading="lazy" src="../{asset}/movement-v1/contact.png"></a></section>' for asset in ASSETS)
    rows=[]
    for record in render['groups']:
        tiles=''.join(f'<figure><img loading="lazy" src="{record["images"][key]}"><figcaption>{label}</figcaption></figure>' for key,label in (('move_a','移动姿态 A'),('move_b','移动姿态 B'),('middle','中景同尺度'),('far','远景同尺度')))
        rows.append(f'<h3>{html.escape(units.NAMES.get(record["asset"],record["asset"]))}</h3><div class="motion">{tiles}</div>')
    page=f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>移动变身、飞行与附身视角</title><style>{units.CSS}.motion{{display:grid;grid-template-columns:repeat(4,minmax(160px,1fr));gap:8px}}section{{margin:28px 0}}</style><body><h1>移动变身、飞行与附身视角</h1><p>狼人移动时变为四足灰狼；鲜血贵族变为一群黑蝙蝠；渴血者变为一群白蝙蝠。嗜血野兽站立时为人形，移动时变为深色身体、红色薄膜翅膀的巨型蝙蝠。金乌移动时，身躯外围出现太阳般的火环。</p><p>飞行单位移动时平滑离地，振翅并收腿；停下后落回原有静止姿态。附身视角跟随当前眼位与飞行高度，自体网格和口鼻从第一人称画面中隐藏。</p><p>以下为原生骨架的离线姿态回放，灰平面代表地面；游戏中的实际切换、地形和视角仍待查看。</p><p><a href="../creature-native-v1/index.html">全部生物</a> · <a href="final-check.json">检查结果</a> · <a href="runtime-check.json">相机与动作记录</a></p>'+cards+'<h2>飞行与奔跑三档回放</h2>'+''.join(rows)+'</body></html>'
    (BATCH/'index.html').write_text(page,encoding='utf-8')
    units.progress()
    print(json.dumps({k:final[k] for k in ('passed','flying_species','flight_models','camera_models','native_person_camera_models')},ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=('contacts','finish'))
    mode=parser.parse_args().mode
    contacts() if mode=='contacts' else finish()
