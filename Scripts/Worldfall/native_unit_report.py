"""生物清点、检查页和已看过候选的发布；只写本轮生物记录。"""
import argparse
import csv
from datetime import datetime
import hashlib
import html
import json
import os
from pathlib import Path
import shutil
from urllib.parse import quote

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
BATCH = ROOT / 'Art/Worldfall/creature-native-v1'
REGISTRY = ROOT / 'Doc/修仙贴图三维化/Blender模型清单.json'
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 14)
NAMES = {r['key'].replace('Cultiway. ', 'Cultiway.'):r['cz'].strip() for r in csv.DictReader((ROOT/'Locales/actors.csv').open(encoding='utf-8-sig'))}
CSS = 'body{background:#222e39;color:#e6edf5;font:16px/1.65 Microsoft YaHei,sans-serif;margin:28px}a{color:#99ceff}img{width:100%;object-fit:contain;background:#293a49}.pixel{image-rendering:pixelated}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:10px}figure{margin:0}figcaption{font-size:13px}td,th{padding:7px;border:1px solid #536474}table{border-collapse:collapse;width:100%}code{overflow-wrap:anywhere}'


def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def write(path, data):
    path=Path(path)
    temporary=path.with_name(path.name+'.writing')
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    temporary.replace(path)
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def link(path, folder): return quote(Path(os.path.relpath(path, folder)).as_posix(), safe='/')
def folder_of(asset): return ROOT / 'Art/Worldfall' / asset / 'native-unit-v1'


def initialize():
    """保存本轮起点及全部原图位置，不能把Seed基础几何算作已建模。"""
    if (BATCH/'inventory.json').is_file():
        return
    BATCH.mkdir(parents=True, exist_ok=True)
    priority = read(ROOT/'Doc/修仙贴图三维化/群系与文明优先清单.json')
    order = ['Cultiway.Werewolf', 'Cultiway.OakTreants', 'Cultiway.KnowledgeGenie']
    for group in ('biome_units', 'tower_units'):
        for ids in priority[group].values():
            order.extend(i for i in ids if i not in order)
    rows = [r for r in read(REGISTRY) if r['Category'] == 'unit' and r['Id'] != 'Cultiway.Train']
    order.extend(r['Id'] for r in rows if r['Id'] not in order)
    rows.sort(key=lambda row: order.index(row['Id']))
    inventory = []
    for row in rows:
        asset = row['Id']
        runtime = ROOT/'Content/Worldfall/Models'/asset/'models.json'
        name = asset.removeprefix('Cultiway.')
        source = ROOT/'GameResources/actors/species/other'/('Skaven' if name.startswith('Skaven_') else 'Cultiway')/name
        if name == 'Plant': source = ROOT/'GameResources/actors/species/other/plant'
        original = sorted(source.glob('**/*.png'))
        existing = bool(row.get('ExportedModels') and runtime.is_file())
        inventory.append({'id': asset, 'shape': row['Shape'], 'family': row['Family'],
            'existing_completed': existing, 'initial_sha256': sha(runtime) if runtime.is_file() else None,
            'source_folder': source.relative_to(ROOT).as_posix(), 'source_pngs': len(original),
            'original_hashes': {p.relative_to(ROOT).as_posix(): sha(p) for p in original},
            'seed': 'Art/Worldfall/Seeds/'+asset+'.json'})
    write(BATCH/'inventory.json', {'created': str(datetime.now()), 'scope': '全部非船单位，排除非生物火车',
        'units': inventory, 'excluded': ['Cultiway.Train', '4类船只'],
        'initial_completed': sum(r['existing_completed'] for r in inventory),
        'new_required': sum(not r['existing_completed'] for r in inventory)})
    write(BATCH/'production-baseline.json', {p.relative_to(ROOT).as_posix(): sha(p)
        for p in (ROOT/'Content/Worldfall/Models').glob('*/models.json')})


def report(asset):
    """只使用最终实导图片，原图和动作各有明确标记。"""
    folder = folder_of(asset)
    strategy = read(folder/'strategy.json')
    description = strategy['notes']
    if isinstance(description, list): description = '；'.join(description)
    sources = [ROOT/p for p in strategy['sources']]
    palette_inputs = []
    for path in sources:
        if path.stem.startswith(('walk_', 'swim_')) and not path.stem.endswith(('_head','_item')):
            image = Image.open(path).convert('RGBA')
            palette_inputs.append({'source':path.relative_to(ROOT).as_posix(),'sha256':sha(path),
                'width':image.width,'height':image.height,
                'pixels':[r | g<<8 | b<<16 | a<<24 for r,g,b,a in image.get_flattened_data()]})
    write(folder/'palette-input.json',palette_inputs)
    document = read(folder/'models.candidate.json')
    first_walk = next((p for p in sources if p.stem.startswith('walk_0') and not p.stem.endswith(('_head','_item'))),sources[0])
    original = next((p for p in sources if document.get('Humanoid') and p.parent.name=='male_1' and p.name=='walk_0.png'),
        next((p for p in sources if p.name == 'walk_0.png'),first_walk))
    version = strategy['candidate_sha256'][:8] + sha(folder/'reopen-check.json')[:8]
    tiles = [('原图', original)] + [(label, folder/'preview/near'/(view+'.png'))
        for label, view in [('正面','front'),('侧面','side'),('背面','back'),('斜俯视','top')]]
    tiles += [('近景同机位',folder/'preview/near/top.png'),('中景同机位',folder/'preview/middle/top.png'),
        ('远景同机位',folder/'preview/far/top.png')]
    tiles += [(label+'仰视（背面剔除）',folder/'preview'/level/'underside_culled.png')
        for label,level in [('近景','near'),('中景','middle'),('远景','far')]
        if (folder/'preview'/level/'underside_culled.png').is_file()]
    kind = document['Models'][0]['Info'].get('kind')
    legless_spirit = document['Models'][0]['Info'].get('cultiway_legless_spirit')=='1'
    phase_label = '强制关节检查' if asset == 'Cultiway.Plant' else '无足移动相位' if legless_spirit else '悬浮相位' if kind=='ghost' else '离线移动相位' if document['Shape'] == 'Machine' or kind in ('fish', 'blob') else '离线迈步'
    labels = {'idle':'离线待机','step_left':phase_label+'A',
        'step_right':phase_label+'B','turn_right':'离线转向','attack':'离线进攻'}
    if document.get('Humanoid'):
        labels.pop('attack')
        labels.update(arm_swing='原生摆臂',run='无足疾行相位' if legless_spirit else '原生跑步',short_hair='原生短发与胡须显隐')
    if (folder/'poses/flight_up.png').is_file():
        labels.update({'flight_up':'离线振翅A','flight_down':'离线振翅B'})
    tiles += [(labels[name],folder/'poses'/(name+'.png')) for name in labels]
    if document.get('Humanoid'):
        for variant,title in [('fem','普通女'),('warrior','男战士'),('warrior_fem','女战士')]:
            tiles += [(title+' '+label,folder/'preview'/variant/'near'/(view+'.png'))
                for label,view in [('正面','front'),('侧面','side'),('背面','back'),('斜俯','top')]]
            tiles += [(title+' '+label,folder/'preview'/variant/level/'top.png')
                for label,level in [('中景','middle'),('远景','far')]]
            tiles += [(title+' '+label+'仰视（背面剔除）',folder/'preview'/variant/level/'underside_culled.png')
                for label,level in [('近景','near'),('中景','middle'),('远景','far')]]
            tiles += [(title+' '+labels[name],folder/'poses'/variant/(name+'.png')) for name in labels]
    assert all(p.is_file() for _, p in tiles)
    contact = Image.new('RGB', (1100, ((len(tiles)+4)//5)*210), '#26323e')
    draw = ImageDraw.Draw(contact)
    for number, (label, path) in enumerate(tiles):
        image = Image.open(path).convert('RGBA')
        if number == 0: image = image.crop(image.getbbox())
        scale = min(210/image.width, 175/image.height)
        image = image.resize((max(1,round(image.width*scale)),max(1,round(image.height*scale))), Image.Resampling.NEAREST if number == 0 else Image.Resampling.LANCZOS)
        x, y = (number%5)*220, (number//5)*210
        draw.text((x+5,y+3),label,font=FONT,fill='white')
        contact.paste(image,(x+(220-image.width)//2,y+28),image)
    contact.save(folder/'contact.png')
    blocks = ''.join(f'<figure><img src="{link(p,folder)}?v={version}"><figcaption>{html.escape(label)}</figcaption></figure>' for label,p in tiles)
    originals = ''.join(f'<figure><img class="pixel" loading="lazy" src="{link(p,folder)}"><figcaption>{html.escape(p.name)}</figcaption></figure>' for p in sources)
    notes = ''.join('<li>'+html.escape(str(n))+'</li>' for n in strategy.get('assumptions', []))
    files = ['model.blend','build.py','strategy.json','original-inspection.json','reopen-check.json','poses.json','receipt.json',
        'review/details.png','review/joins.png']
    file_titles={'review/details.png':'面部与发型放大','review/joins.png':'四肢衔接放大'}
    links = ' · '.join(f'<a href="{quote(n)}">{html.escape(file_titles.get(n,n))}</a>' for n in files if (folder/n).is_file())
    variant_titles={'base':'普通男','fem':'普通女','warrior':'男战士','warrior_fem':'女战士'}
    glbs = ' · '.join(f'<a href="{quote(row["glb"])}">{variant_titles[row["variant"]]+" " if document.get("Humanoid") else ""}{row["level"]} 实体GLB（{row["triangles"]}三角面）</a>' +
        (f' / <a href="{quote(row["combined_glb"])}">含透明叶簇GLB</a>' if row.get('combined_glb') else '') for row in strategy['models'])
    (folder/'index.html').write_text(f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>{NAMES.get(asset,asset)}</title><style>{CSS}</style><body><a href="../../creature-native-v1/index.html">全部生物进度</a><h1>{html.escape(NAMES.get(asset,asset))}</h1><p><code>{asset}</code></p><p>{html.escape(description)}</p><p>原图约束的Blender自建。三档与方向图均来自实际GLB重新导入。动作使用实际骨架纯数据回放；游泳、装备、运行配色、游戏效果待用户测试。</p><p>{links}</p><p>{glbs}</p><ul>{notes}</ul><div class="grid">{blocks}</div><h2>本物体原图</h2><div class="grid">{originals}</div></body></html>', encoding='utf-8')
    print(json.dumps({'asset':asset,'contact':str(folder/'contact.png')},ensure_ascii=False))


def withdraw(asset, note):
    """用户纠正主体结构时留档撤回该项，其他清单行和模型不变。"""
    folder=folder_of(asset)
    manifest=read(folder/'manifest.json')
    production=ROOT/manifest['production_resource']
    assert production.resolve().is_relative_to((ROOT/'Content/Worldfall/Models'/asset).resolve())
    assert sha(production)==manifest['production_sha256']
    original=next(row for row in read(BATCH/'inventory.json')['units'] if row['id']==asset)
    restore_source=folder/'models.production-source.json' if original['existing_completed'] else None
    if restore_source is not None:
        assert sha(restore_source)==original['initial_sha256']
    registry=read(REGISTRY)
    assert isinstance(registry,list) and len(registry)==228
    history=BATCH/'corrections'/asset/manifest['production_sha256'][:12]
    history.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(REGISTRY,history/'registry.before.json')
    write(history/'manifest.before.json',manifest)
    assert not (history/'models.withdrawn.json').exists()
    production.replace(history/'models.withdrawn.json')
    if restore_source is not None:
        shutil.copyfile(restore_source,production)
        manifest['withdrawal_restored_sha256']=sha(production)
    if manifest.get('leaf_publication'):
        leaf=ROOT/manifest['leaf_publication']['resource']
        assert leaf.resolve() == (ROOT/'Content/Worldfall/LeafCards'/(asset+'.json')).resolve()
        assert sha(leaf) == manifest['leaf_publication']['runtime_sha256']
        leaf.replace(history/'leaf-cards.withdrawn.json')
    manifest.update(publication='withdrawn_for_user_correction',user_correction=note)
    write(folder/'manifest.json',manifest)
    row=next(r for r in registry if r['Id']==asset)
    row.update(PublishedNative=False,ExportedModels=0,Status=note)
    temporary=REGISTRY.with_suffix('.withdrawing.json')
    write(temporary,registry);assert len(read(temporary))==228
    temporary.replace(REGISTRY)
    page=folder/'index.html'
    if page.is_file():
        text=page.read_text(encoding='utf-8-sig')
        page.write_text(text.replace('<body>','<body><p style="border:2px solid #f4b46a;padding:16px">'+html.escape(note)+'。当前图已撤回，正在重制，不能作为最终模型。</p>'),encoding='utf-8')
    print(json.dumps({'withdrawn':asset,'reason':note},ensure_ascii=False))


def publish(asset, note):
    """发布根代理已实际看过的模型，严格绑定来源、GLB与生产字节。"""
    folder = folder_of(asset)
    candidate = folder/'models.candidate.json'
    doc, strategy, check, poses = [read(folder/n) for n in ('models.candidate.json','strategy.json','reopen-check.json','poses.json')]
    digest = sha(candidate)
    assert digest == strategy['candidate_sha256'] == check['candidate_sha256'] == poses['candidate_sha256']
    assert check['Passed'] and check['IndependentBlenderProcess'] and check['ReopenedEditableSource'] and check['ImportedAllFinalGLBs']
    seed=read(ROOT/'Art/Worldfall/Seeds'/(asset+'.json'))
    model_count=len(seed['Models'])
    assert model_count == (12 if doc.get('Humanoid') else 3)
    assert len(doc['Models']) == len(strategy['models']) == len(check['Models']) == model_count
    assert {m['Name'] for m in doc['Models']} == {m['Name'] for m in seed['Models']}
    if doc.get('Humanoid'):
        assert check['ActualPersonRigUsed'] and check['ActualPersonDressUsed'] and poses['rig']=='PersonRig'
    assert doc['Id'] == asset and (folder/'contact.png').is_file() and (folder/'receipt.json').is_file()
    receipt = read(folder/'receipt.json')
    assert receipt['candidate_sha256'] == digest
    leaf_publication = None
    leaf_path = folder/'leaf-cards.json'
    if leaf_path.is_file():
        recipe = read(leaf_path)
        runtime_check=read(folder/'leaf-runtime-check.json')
        assert runtime_check['passed'] and runtime_check['candidate_sha256'] == digest
        assert strategy['leaf_card_sha256'] == check['leaf_card_sha256'] == poses['leaf_card_sha256'] == sha(leaf_path)
        assert runtime_check['leaf_card_sha256'] == sha(leaf_path)
        assert check['LeafPosesFromActualBillboardMethod']
        assert all(proof['LeafCards']['Sha256'] == sha(folder/record['combined_glb'])
            and proof['LeafCards']['CardObjects'] == record['leaf_cards']
            and proof['LeafCards']['CandidateJsonTrianglePositionsWindingAndSlotsMatch']
            for record,proof in zip(strategy['models'],check['Models']))
        assert recipe['AssetId'] == asset and recipe['ModelName'] == doc['Family']
        for model,level in zip(doc['Models'],('Near','Middle','Far')):
            names={p['Name'] for p in model['Parts']}
            assert all(card['Part'] in names for card in recipe['Levels'][level])
        texture_dir=ROOT/'GameResources/cultiway/worldfall/leaf_cards/creatures'/asset.removeprefix('Cultiway.').lower()
        texture_dir.mkdir(parents=True,exist_ok=True)
        runtime_textures={}
        texture_hashes={}
        for key,path in recipe['Textures'].items():
            source=folder/path
            assert sha(source) == strategy['leaf_texture_hashes'][key] == check['leaf_texture_hashes'][key]
            png=Image.open(source).convert('RGBA')
            assert max(png.size)<=512 and set(png.getchannel('A').get_flattened_data())=={0,255}
            destination=texture_dir/source.name
            shutil.copyfile(source,destination)
            runtime_textures[key]=destination.relative_to(ROOT/'GameResources').with_suffix('').as_posix()
            texture_hashes[destination.relative_to(ROOT).as_posix()]=sha(destination)
        runtime_recipe={key:recipe[key] for key in ('AssetId','ModelName','Levels')}
        runtime_recipe['Textures']=runtime_textures
        runtime_leaf=ROOT/'Content/Worldfall/LeafCards'/(asset+'.json')
        write(runtime_leaf,runtime_recipe)
        leaf_publication={'recipe_sha256':sha(leaf_path),'resource':runtime_leaf.relative_to(ROOT).as_posix(),
            'runtime_sha256':sha(runtime_leaf),'texture_hashes':texture_hashes,
            'combined_glbs':['native-unit-v1/'+row['combined_glb'] for row in strategy['models']],
            'actual_collector_check':'native-unit-v1/leaf-runtime-check.json'}
    originals = read(BATCH/'inventory.json')['units']
    original = next(r for r in originals if r['id'] == asset)
    assert not original['existing_completed'] or read(BATCH/'inventory.json').get('redraw_existing_requested')
    assert all(sha(ROOT/path) == value for path,value in original['original_hashes'].items())
    assert strategy['sources'] and all(sha(ROOT/path)==value for path,value in strategy['sources'].items())
    approved_parents = {'leg_fl':'seg_2','leg_fr':'seg_2','leg_bl':'seg_7','leg_br':'seg_7'} if asset=='Cultiway.QingLong' else {}
    assert strategy.get('parent_adjustments', {}) == approved_parents
    triangles = {}
    for model, record, proof in zip(doc['Models'], strategy['models'], check['Models']):
        assert proof['Sha256'] == sha(folder/record['glb']) and proof['CandidateJsonTrianglePositionsWindingAndSlotsMatch']
        assert proof['GlbFloatNormalsMatchRuntimeNrm'] and proof['EditableNormalsMatchRuntimeNrm'] and proof['UnitNormals']
        assert proof['OriginalPartsAndParentsPreserved'] or (approved_parents
            and proof.get('OriginalPartsAndDeclaredParentsVerified')
            and proof.get('DeclaredParentAdjustments')==approved_parents)
        assert len(model['SlotNames']) <= 64
        count = sum(len(part['Tri'])//3 for part in model['Parts'])
        assert count == record['triangles'] <= record['budget']
        triangles[model['Name']] = count
    production = ROOT/'Content/Worldfall/Models'/asset/'models.json'
    production.parent.mkdir(parents=True, exist_ok=True)
    if production.is_file() and not (folder/'models.production-source.json').is_file():
        shutil.copyfile(production, folder/'models.production-source.json')
    shutil.copyfile(candidate, production)
    backup = folder/'model.blend1'
    if backup.is_file(): backup.unlink()
    manifest = {'asset':asset, 'shape':doc['Shape'], 'publication':'published_offline', 'production_resource':production.relative_to(ROOT).as_posix(),
        'production_sha256':digest, 'models':model_count, 'triangles':triangles, 'source_glbs':['native-unit-v1/'+r['glb'] for r in strategy['models']],
        'editable_sources':['native-unit-v1/model.blend','native-unit-v1/build.py'],
        'inspection_files':{'original':'native-unit-v1/original-inspection.json','candidate_front':'native-unit-v1/preview/near/front.png',
            'directions_and_poses':'native-unit-v1/contact.png','actual_reopen':'native-unit-v1/reopen-check.json'},
        'root_visual_review':note, 'reviewed_sha256':digest, 'game_test':'pending_user_manual'}
    if leaf_publication is not None:
        manifest['leaf_publication']=leaf_publication
        manifest['source_glbs'].extend(leaf_publication['combined_glbs'])
    write(folder/'manifest.json',manifest)
    registry = read(REGISTRY)
    row = next(r for r in registry if r['Id'] == asset)
    row.update({'ExportedModels':model_count, 'Models':model_count, 'Shape':doc['Shape'], 'RuntimeSha256':digest, 'Triangles':triangles,
        'Authoring':'Blender 4.4 原图约束独立自建', 'PublishedNative':True, 'NativeVersion':'native-unit-v1',
        'PublishedTrellis':False, 'WorldfallPaletteValid':True, 'EditableSources':manifest['editable_sources'],
        'source_glbs':manifest['source_glbs'], 'inspection_files':manifest['inspection_files'],
        'GameTest':'pending_user_manual', 'Status':'三档独立生物已离线发布；游戏验收待用户查看'})
    if leaf_publication is not None: row['LeafCardsResource']=leaf_publication['resource']
    write(REGISTRY,registry)
    print(json.dumps({'published':asset,'triangles':triangles,'sha256':digest},ensure_ascii=False))


def progress():
    """完整清单始终显示未完成项，避免首批发布冒充全量完成。"""
    inventory = read(BATCH/'inventory.json')
    redraw_existing=inventory.get('redraw_existing_requested',False)
    required=len(inventory['units']) if redraw_existing else inventory['new_required']
    preserved=0 if redraw_existing else inventory['initial_completed']
    registry = {r['Id']: r for r in read(REGISTRY)}
    current_folders = {item['id']: ROOT/'Art/Worldfall'/item['id']/registry[item['id']].get('NativeVersion', 'native-unit-v1') for item in inventory['units']}
    rows = []
    for item in inventory['units']:
        folder = current_folders[item['id']]
        manifest = folder/'manifest.json'
        publication = read(manifest) if manifest.is_file() else {}
        runtime = ROOT/'Content/Worldfall/Models'/item['id']/'models.json'
        published = publication.get('publication') == 'published_offline' and runtime.is_file() and publication.get('production_sha256') == sha(runtime)
        leaf=publication.get('leaf_publication')
        if published and leaf:
            published=(ROOT/leaf['resource']).is_file() and sha(ROOT/leaf['resource'])==leaf['runtime_sha256']
            published=published and all((ROOT/p).is_file() and sha(ROOT/p)==value for p,value in leaf['texture_hashes'].items())
        if published:
            published = all((ROOT/p).is_file() for p in publication.get('leaf_resources', []))
        correcting = publication.get('publication') == 'withdrawn_for_user_correction'
        state = '已发布，待游戏测试' if published else '用户纠正结构，重制中' if correcting else '已出候选，检查中' if (folder/'models.candidate.json').is_file() else '已有旧模型，按新风格重绘排队中' if item['existing_completed'] and redraw_existing else '已有正式模型，保留' if item['existing_completed'] else '排队中'
        shape = publication.get('shape',item['shape']) if published or correcting else item['shape']
        visual_version=(publication.get('production_sha256','')[:12]+(leaf['runtime_sha256'][:12] if leaf else '')) if published else None
        rows.append({'id':item['id'],'name':NAMES.get(item['id'],item['id']),'shape':shape,'existing':item['existing_completed'],'published':published,'state':state,'production_sha256':publication.get('production_sha256') if published else None,'visual_version':visual_version})
    count = sum(r['published'] for r in rows)
    redrawn = sum(r['published'] and r['existing'] for r in rows) if redraw_existing else 0
    changed = []
    allowed = {f'Content/Worldfall/Models/{r["id"]}/models.json' for r in rows if r['published']}
    for path,value in read(BATCH/'production-baseline.json').items():
        if path not in allowed and sha(ROOT/path) != value: changed.append(path)
    result = {'total_units':len(rows),'preserved_existing':preserved,'redraw_existing':inventory['initial_completed'] if redraw_existing else 0,'redraw_existing_published':redrawn,'new_required':required,
        'new_published':count,'new_remaining':required-count,'other_production_changes':changed,
        'game_test':'pending_user_manual','assets':rows}
    write(BATCH/'progress.json',result)
    content = ''.join(f'<tr><td><a href="{link(current_folders[r["id"]]/"index.html",BATCH)}">{r["id"]}</a></td><td>{r["shape"]}</td><td>{r["state"]}</td></tr>' if (current_folders[r['id']]/'index.html').is_file() else f'<tr><td>{r["id"]}</td><td>{r["shape"]}</td><td>{r["state"]}</td></tr>' for r in rows)
    def thumbnail(folder):
        if (folder/'runtime-thumbnail.png').is_file(): return 'runtime-thumbnail.png'
        return 'contact.png' if folder.name in ('camouflage-v1','movement-v1','held-items-v1') else 'preview/near/top.png'
    cards = ''.join(f'<figure><a href="{link(current_folders[r["id"]]/"index.html",BATCH)}"><img loading="lazy" src="{link(current_folders[r["id"]]/thumbnail(current_folders[r["id"]]),BATCH)}?v={r["visual_version"]}"><figcaption>{html.escape(r["name"])}</figcaption></a></figure>' for r in rows if r['published'])
    for r in rows: content = content.replace('>'+r['id']+'<','>'+html.escape(r['name'])+' · '+r['id']+'<')
    scope_note=f'包含原有{inventory["initial_completed"]}类正式生物的统一风格重绘，其中{redrawn}类已完成重绘' if redraw_existing else f'保留原有{preserved}类'
    followups=(('movement-v1','移动变身、飞行动作与附身视角'),('camouflage-v1','树人与泰坦的静止伪装'),
        ('water-float-v1','水面喷气背包与月亮悬浮'),('daemon-held-items-v1','恶魔专属手持物与腿部比例'))
    motion_link='<p>'+' · '.join(f'<a href="../{folder}/index.html">{label}</a>' for folder,label in followups if (BATCH.parent/folder/'index.html').is_file())+'</p>'
    (BATCH/'index.html').write_text(f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>修仙生物模型</title><style>{CSS}</style><body><h1>修仙生物独立建模</h1>{motion_link}<p>范围{len(rows)}类：{scope_note}；本轮{count}/{required}类已发布，尚余{required-count}类。火车与船只单列。每物体独立代理，每批最多3个。</p><p>本轮按像素原图自行构形，直接用Blender制作，没有平台生成初稿或独立高清参考。近中远分别制作，最终GLB重新导入核对，动作使用原生骨架离线回放。全部新模型游戏验收待用户统一查看。</p><p><a href="progress.json">逐项进度</a> · <a href="inventory.json">来源与初始清点</a></p><div class="grid">{cards}</div><table><tr><th>生物</th><th>形体</th><th>当前状态</th></tr>{content}</table></body></html>', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='assets'},ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('mode',choices=('init','report','publish','withdraw','progress'))
    parser.add_argument('--asset')
    parser.add_argument('--note')
    args = parser.parse_args()
    if args.mode == 'init': initialize(); progress()
    elif args.mode == 'report': report(args.asset)
    elif args.mode == 'publish': publish(args.asset,args.note); progress()
    elif args.mode == 'withdraw': withdraw(args.asset,args.note); progress()
    else: progress()
