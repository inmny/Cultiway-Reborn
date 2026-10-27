"""发布已实际看图、独立重新导入的群系透明叶片候选。"""
import argparse
import copy
import json
from pathlib import Path
import re
import shutil

from PIL import Image
import biome_native_publish as common

ROOT = common.ROOT
SUFFIXES = {'Near':'', 'Middle':'_m', 'Far':'_f'}


def publish_asset(asset, version, review_note):
    """先核对候选和真实导出记录，再写实体、配方、运行图与来源清单。"""
    out = ROOT/'Art/Worldfall'/asset/version
    read, write, digest = common.read, common.write, common.digest
    source = read(out/'models.production-source.json')
    candidate = read(out/'models.candidate.json')
    all_recipes = read(out/'recipes.json')
    recipes = [recipe for recipe in all_recipes if any(recipe['Levels'].values())]
    empty_recipes = [recipe for recipe in all_recipes if not any(recipe['Levels'].values())]
    files = read(out/'model-files.json')
    validation = read(out/'validation.json')
    assert read(out/'no-solid-crown-check.json')['Passed']
    assert validation['IndependentBlenderProcess'] and validation['ImportedAllFinalGLBs']
    original = {m['Name']:m for m in source['Models']}
    models = {m['Name']:m for m in candidate['Models']}
    assert original.keys() == models.keys() == {m['Name'] for m in files}
    assert {k:v for k,v in source.items() if k!='Models'} == {k:v for k,v in candidate.items() if k!='Models'}
    checked = {}
    for row in validation['Files']:
        path = out/row['File']
        assert digest(path) == row['Sha256']
        assert row['FiniteGeometry'] and row['UnitNormals'] and row['ValidUVAndIndices']
        checked[Path(row['File']).as_posix()] = row
    triangles = {name:sum(len(p['Tri'])//3 for p in model['Parts']) for name,model in models.items()}
    for row in files:
        assert row['Triangles'] == triangles[row['Name']]
        for key in ('Body','Combined'):
            assert row[key] in checked, (asset,row[key])
            assert checked[row[key]]['CandidateJsonTrianglePositionsWindingAndSlotsMatch']
        assert checked[row['Body']]['Triangles'] == row['Triangles']
        assert checked[row['Combined']]['Triangles'] == row['Triangles']+row['CrossGroups']*4
    production = ROOT/'Content/Worldfall/Models'/asset/'models.json'
    # 候选绑定制作开始时的生产快照；不能盖掉期间对同一资产的其他修改。
    assert read(production) in (source,candidate), asset+' 的正式模型在候选制作后另有变化'
    receipt=read(out/'receipt.json')
    inspection=receipt['inspection_files']
    assert all((out.parent/path).is_file() for path in inspection.values())
    preview_frames={frame:version+'/previews/'+frame+'/front.png' for frame in common.sources(asset)}
    assert all((out.parent/path).is_file() for path in preview_frames.values())
    runtime_recipes=[]
    seen_models=set()
    for recipe in recipes:
        assert recipe['AssetId'] == asset
        runtime = {key:copy.deepcopy(recipe[key]) for key in ('AssetId','Frame','ModelName','Levels')}
        runtime['NativeAlphaZeroMeansOpaque']=True
        runtime['Textures']={}
        used={card['Image'] for cards in recipe['Levels'].values() for card in cards}
        for key in sorted(used):
            relative=Path(recipe['Textures'][key])
            image_path=out/relative
            image=Image.open(image_path).convert('RGBA')
            assert max(image.size)<=512 and set(image.getchannel('A').tobytes())=={0,255}
            relative=relative.relative_to('textures').with_suffix('')
            namespace=asset.removesuffix('_tree').lower() if asset.endswith('_tree') else 'flora/'+asset.lower()
            resource='cultiway/worldfall/leaf_cards/'+namespace+'/'+relative.as_posix()
            target=ROOT/'GameResources'/(resource+'.png')
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(image_path,target)
            runtime['Textures'][key]=resource
        for level,suffix in SUFFIXES.items():
            name=recipe['ModelName']+suffix
            assert name in models and name not in seen_models
            seen_models.add(name)
            record=next(row for row in files if row['Name']==name)
            assert record['CrossGroups']==len(recipe['Levels'][level])
        target=ROOT/'Content/Worldfall/LeafCards'/(asset+'.'+recipe['Frame']+'.json')
        write(target,runtime)
        runtime_recipes.append(target.relative_to(ROOT).as_posix())
    assert {r['Name'] for r in files if r['CrossGroups']} <= seen_models
    # 旧候选可能包含无叶状态的空配方；正式目录只保留确实要绘制的叶簇。
    for recipe in empty_recipes:
        assert recipe['AssetId'] == asset
        target = ROOT/'Content/Worldfall/LeafCards'/(asset+'.'+recipe['Frame']+'.json')
        if target.is_file():
            assert read(target)['AssetId'] == asset
            target.unlink()
    shutil.copyfile(out/'models.candidate.json',production)
    sha=digest(production)
    strategy={'asset':asset,'version':version,'candidate_sha256':sha,
              'method':'非叶实体配透明PNG叶簇与薄花瓣；保留与重绘范围见本物体检查',
              'notes':review_note,'models':[]}
    for row in files:
        strategy['models'].append({'name':row['Name'],'frame':row['Frame'],'level':row['Level'],
            'glb':row['Combined'],'body_glb':row['Body'],'triangles':row['Triangles']+row['CrossGroups']*4,
            'body_triangles':row['Triangles'],'additional_billboard_triangles':row['CrossGroups']*4})
    write(out/'native-strategy.json',strategy)
    representative=next((r['Frame'] for r in recipes if r['Frame'].startswith('main_')),files[0]['Frame'])
    glbs=sorted({version+'/'+row[key] for row in files for key in ('Body','Combined')})
    editable=[version+'/'+p.relative_to(out).as_posix() for p in sorted(out.rglob('*.blend'))]
    editable += [version+'/'+p.name for p in sorted(out.glob('*.py'))]
    manifest={'asset':asset,'family':candidate['Family'],'version':version,'models':len(models),
        'static_appearances':len(common.sources(asset)), 'method':strategy['method'],
        'production_resource':production.relative_to(ROOT).as_posix(),'production_sha256':sha,
        'publication':'published_offline','triangles':triangles,
        'composite_triangles':{r['Name']:r['Triangles']+r['CrossGroups']*4 for r in files},
        'source_glbs':glbs,'editable_sources':editable,'inspection_files':inspection,
        'representative_preview':preview_frames[representative],'preview_frames':preview_frames,
        'source_snapshot':version+'/models.production-source.json',
        'leaf_cards_resources':runtime_recipes,'offline_checks':{
            'reimport':version+'/validation.json','preservation':version+'/preservation-check.json',
            'no_solid_crown':version+'/no-solid-crown-check.json'},
        'root_review':{'status':'actual_contact_images_viewed','candidate_sha256':sha,'note':review_note},
        'game_test':'pending_user_manual'}
    assert editable and all((out.parent/path).is_file() for path in glbs+editable)
    write(out/'manifest.json',manifest)
    production_changed=sha != digest(out/'models.production-source.json')
    receipt.update(published=True,production_changed=production_changed,production_replaced=production_changed,production_unchanged=not production_changed,
                   status='published_offline',production_sha256=sha,publication='published_offline',
                   game_test='pending_user_manual',leaf_cards_resources=runtime_recipes)
    write(out/'receipt.json',receipt)
    rows=read(ROOT/'Doc/修仙贴图三维化/Blender模型清单.json')
    row=next(row for row in rows if row['Id']==asset)
    row.update(NativeVersion=version,RuntimeSha256=sha,Triangles=triangles,
        Authoring='实体部件与Blender自建透明叶片',Status='真叶与薄瓣改透明贴图，已离线实装；游戏待用户查看',
        EditableSources=editable,source_glbs=glbs,inspection_files=inspection,GameTest='pending_user_manual')
    write(ROOT/'Doc/修仙贴图三维化/Blender模型清单.json',rows)
    banner='<!-- 当前发布记录 -->\n2026-10-17 已离线实装本资产透明叶片；[检查页](index.html)。游戏效果待用户查看。\n<!-- 当前发布记录结束 -->\n\n'
    path=out/'README.md'
    text=path.read_text(encoding='utf-8')
    text=re.sub(r'^<!-- 当前发布记录 -->.*?<!-- 当前发布记录结束 -->\s*','',text,flags=re.S)
    path.write_text(banner+text,encoding='utf-8')
    page=out/'index.html'
    html=page.read_text(encoding='utf-8')
    marker='<p>当前版本已离线实装，游戏效果待用户查看。<a href="manifest.json">正式资源与来源</a></p>'
    if marker not in html:
        html=html.replace('<h1',marker+'<h1',1)
        page.write_text(html,encoding='utf-8')
    result={'asset':asset,'published':True,'models':len(models),'leaf_frames':sum(any(r['Levels'].values()) for r in recipes),
            'changed_models':sum(models[n]!=original[n] for n in models),'game_test':'pending_user_manual'}
    print(json.dumps(result,ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--asset',required=True)
    parser.add_argument('--version',default='leaf-cards-v1')
    parser.add_argument('--review-note',required=True)
    args=parser.parse_args()
    publish_asset(args.asset,args.version,args.review_note)
