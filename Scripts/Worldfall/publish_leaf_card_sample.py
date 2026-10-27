"""发布已独立导入并看图的橡树叶簇样板，保留未改外观的原始来源。"""
import copy
import json
from pathlib import Path
import shutil
from PIL import Image
import biome_native_publish as publish

ROOT = publish.ROOT
VERSION = 'leaf-cards-v2'
OUT = ROOT / 'Art/Worldfall/Oak_tree' / VERSION
OLD = OUT.parent / 'native-biome-v1'


def main():
    read, write, digest = publish.read, publish.write, publish.digest
    data = read(OUT / 'models.candidate.json')
    source = read(OUT / 'models.production-source.json')
    recipe = read(OUT / 'cross-data.json')
    stats = read(OUT / 'geometry-stats.json')
    validation = read(OUT / 'validation.json')
    no_crown = read(OUT / 'no-solid-crown-check.json')
    coverage = read(OUT / 'coverage-check.json')
    assert no_crown['Passed'] and coverage['Passed']
    assert validation['IndependentBlenderProcess'] and validation['ImportedAllFinalGLBs']
    for record in validation['Files']:
        assert digest(OUT / record['File']) == record['Sha256']
        assert record['FiniteGeometry'] and record['UnitNormals'] and record['ValidUVAndIndices']
        if record['File'].startswith('glbs/body-'):
            assert record['CandidateJsonTrianglePositionsWindingAndSlotsMatch']
    originals = {m['Name']: m for m in source['Models']}
    changed = {recipe['ModelName'] + suffix for suffix in ('', '_m', '_f')}
    assert len(data['Models']) == 42
    assert sum(model == originals[model['Name']] for model in data['Models']) == 39
    assert {m['Name'] for m in data['Models'] if m != originals[m['Name']]} == changed
    assert all(row['CoreTriangles'] == 0 for row in stats['Levels'])
    # 运行图使用原生入口允许的尺寸；2K来源只保留在可编辑资产目录。
    runtime = {key: recipe[key] for key in ('AssetId', 'Frame', 'ModelName', 'Levels', 'NativeAlphaZeroMeansOpaque')}
    runtime['Textures'] = {}
    for key, relative in recipe['Textures'].items():
        path = OUT / relative
        image = Image.open(path).convert('RGBA')
        assert max(image.size) <= 512 and set(image.getchannel('A').tobytes()) <= {0,255}
        name = 'cultiway/worldfall/leaf_cards/oak/' + path.stem
        target = ROOT / 'GameResources' / (name + '.png')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
        runtime['Textures'][key] = name
    runtime_path = ROOT / 'Content/Worldfall/LeafCards/Oak_tree.main_2.json'
    write(runtime_path, runtime)
    production = ROOT / 'Content/Worldfall/Models/Oak_tree/models.json'
    shutil.copyfile(OUT / 'models.candidate.json', production)
    sha = digest(production)
    strategy = read(OLD / 'native-strategy.json')
    slot_stats = {row['Level'].lower(): row for row in stats['Levels']}
    triangles = {m['Name']: sum(len(p['Tri'])//3 for p in m['Parts']) for m in data['Models']}
    glbs = []
    for record in strategy['models']:
        if record['name'] in changed:
            record['glb'] = 'glbs/body-' + record['level'] + '.glb'
            record['triangles'] = triangles[record['name']]
            record['additional_billboard_triangles'] = slot_stats[record['level']]['CrossTriangles']
        else:
            record['glb'] = '../native-biome-v1/' + record['glb']
        glbs.append(VERSION + '/' + record['glb'])
    strategy.update(candidate_sha256=sha, method='仅树干枝根实体，加扩大覆盖的透明交叉叶簇；main_2无实体叶冠，其余39模型不变')
    write(OUT / 'native-strategy.json', strategy)
    manifest = copy.deepcopy(read(OLD / 'manifest.json'))
    manifest.update(version=VERSION, production_sha256=sha,
        method='Blender树干枝根加透明PNG交叉叶簇，无实体叶冠；只替换main_2三档',
        publication='published_offline_experiment', triangles=triangles,
        source_glbs=glbs + [VERSION+'/glbs/cross-hybrid-'+level+'.glb' for level in ('near','middle','far')],
        editable_sources=manifest['editable_sources'] + [VERSION+'/'+name for name in
            ('build.py','oak-leaf-cards.blend','leaf-cluster-source.blend','hanging-leaf-source.blend')],
        inspection_files=read(OUT/'receipt.json')['inspection_files'],
        offline_checks={key:VERSION+'/'+name for key,name in (
            ('reimport','validation.json'),('preservation','preservation-check.json'),
            ('no_solid_crown','no-solid-crown-check.json'),('coverage','coverage-check.json'))},
        composite_triangles={row['Level']:row['CombinedHybridTriangles'] for row in stats['Levels']},
        representative_preview=VERSION+'/previews/cross-hybrid-near/front.png',
        preview_frames={frame:(VERSION+'/previews/cross-hybrid-near/front.png' if frame=='main_2' else
            'native-biome-v1/previews/'+frame+'/front.png') for frame in publish.sources('Oak_tree')},
        root_review={'status':'actual_contact_images_viewed','candidate_sha256':sha,
            'note':'实际看正侧背、三档、35/82度仰视与俯视；无实体叶冠，原生竖片极端上下视角仍收窄'},
        leaf_cards_resource=runtime_path.relative_to(ROOT).as_posix(), game_test='pending_user_manual')
    write(OUT/'manifest.json', manifest)
    receipt = read(OUT/'receipt.json')
    receipt.update(production_replaced=True, production_sha256=sha, publication='published_offline_experiment',
        native_render_status='已接入原生贴片收集入口；构建与纯数据调用另核对，实际游戏显示待用户查看',game_test='pending_user_manual')
    write(OUT/'receipt.json',receipt)
    readme_path=OUT/'README.md'
    readme=readme_path.read_text(encoding='utf-8')
    end='<!-- 当前发布记录结束 -->'
    if readme.startswith('<!-- 当前发布记录 -->'):
        readme=readme.split(end,1)[1].lstrip()
    readme_path.write_text('<!-- 当前发布记录 -->\n2026-10-16 已离线实装 main_2 三档；39个其他模型逐字段保留。'
        '正式树体与叶簇数据已替换，游戏效果待用户查看。[检查页](index.html)。\n'+end+'\n\n'+readme,encoding='utf-8')
    rows=read(ROOT/'Doc/修仙贴图三维化/Blender模型清单.json')
    row=next(row for row in rows if row['Id']=='Oak_tree')
    row.update(NativeVersion=VERSION, RuntimeSha256=sha, Triangles=triangles,
        Authoring='Blender自建树体与原生PNG透明叶簇',
        EditableSources=manifest['editable_sources'], source_glbs=manifest['source_glbs'],
        inspection_files=manifest['inspection_files'], GameTest='pending_user_manual',
        Status='main_2移除实体叶冠并加密扩大透明叶簇，三档已离线发布；其余39模型原样；游戏待用户查看')
    write(ROOT/'Doc/修仙贴图三维化/Blender模型清单.json',rows)
    html='<!doctype html><meta charset="utf-8"><title>橡树透明叶簇 v2</title><style>'+publish.CSS+'</style>'
    html+='<h1>橡树 main_2：无实体叶冠、扩大透明叶簇</h1><p>已接入模组，重启后查看。树干、枝条和根脚保留实体，全部叶冠改为PNG交叉叶簇。2K来源保留；运行图使用原生入口允许的512/256。游戏验收待用户查看。</p>'
    rows=stats['Levels']
    html+='<p>近/中/远叶簇组数 '+ '/'.join(str(r['CrossGroups']) for r in rows) + '；含树体的三角面数 '+ '/'.join(str(r['CombinedHybridTriangles']) for r in rows) +'。其余39模型逐字段不变。原生交叉片均为竖直面，接近正上、正下时叶片仍会收窄。</p>'
    html+='<p><a href="../leaf-cards-v1/index.html">上一版对照</a> · <a href="README.md">说明</a> · <a href="manifest.json">当前来源</a> · <a href="validation.json">独立导入核对</a> · <a href="no-solid-crown-check.json">无实体叶冠核对</a> · <a href="coverage-check.json">覆盖范围</a> · <a href="cross-data.json">叶簇数据</a></p>'
    for name in ('comparison-front.png','comparison-views.png','comparison-underside.png','comparison-lod.png'):
        assert (OUT/name).is_file()
        html+='<p><a href="'+name+'"><img src="'+name+'"></a></p>'
    (OUT/'index.html').write_text(html,encoding='utf-8')
    old_page=OUT.parent/'leaf-cards-v1/index.html'
    old_html=old_page.read_text(encoding='utf-8')
    latest='<p><a href="../leaf-cards-v2/index.html">查看最新v2：移除实体叶冠，扩大并加密透明叶簇</a>；本页保留上一版对照。</p>'
    if latest not in old_html:
        old_html=old_html.replace('<h1>',latest+'<h1>',1)
        old_page.write_text(old_html,encoding='utf-8')
    publish.refresh()
    publish.overview()
    print(json.dumps({'published':True,'changed_models':3,'unchanged_models':39,'textures':len(runtime['Textures']),'game_test':'pending_user_manual'}))


if __name__=='__main__':
    main()
