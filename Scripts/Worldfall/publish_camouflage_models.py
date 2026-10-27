"""发布已由主代理看图的静止伪装模型；保留活动三档，不复制历史备份。"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def publish(asset):
    folder = ROOT / 'Art/Worldfall' / asset / 'camouflage-v1'
    candidate = folder / 'models.candidate.json'
    production = ROOT / 'Content/Worldfall/Models' / asset / 'models.json'
    doc, previous = read(candidate), read(production)
    digest = sha(candidate)
    review, reopen = read(folder / 'root-review.json'), read(folder / 'reopen-check.json')
    assert review['reviewer'] == 'root' and review['actual_image_view'] and review['candidate_sha256'] == digest
    assert all((folder / p).is_file() for p in review['viewed'])
    assert all(reopen[k] for k in ('Passed', 'IndependentBlenderProcess', 'ReopenedEditableSource', 'ImportedAllFinalGLBs'))
    assert reopen.get('candidate_sha256', reopen.get('CandidateSha256')) == digest
    active = {m['Name']: m for m in previous['Models'] if m['Info'].get('cultiway_camouflage_state') != 'disguised'}
    assert len(active) == 3 and len(doc['Models']) == 6
    triangles = {}
    for model in doc['Models']:
        name = model['Name']
        triangles[name] = sum(len(p['Tri']) // 3 for p in model['Parts'])
        assert len(model['SlotColor']) <= 64
        if name in active:
            assert model == active[name], name
        else:
            assert model['Info']['cultiway_camouflage_state'] == 'disguised'
            suffix = name[len(doc['Family'] + '_disguised'):]
            original = active[doc['Family'] + suffix]
            assert all(model['Info'][k] == original['Info'][k] for k in ('height', 'length'))
            # 树根允许少量埋在地面以下，伪装树干或岩石必须实际接地。
            assert model['Min']['Z'] <= 0.01 < model['Max']['Z']
    recipe_resources = []
    recipes_path = folder / 'recipes.json'
    if recipes_path.is_file():
        recipes = read(recipes_path)
        if isinstance(recipes, dict):
            recipes = [recipes] if 'ModelName' in recipes else []
        for index, recipe in enumerate(recipes):
            assert recipe['ModelName'] == doc['Family'] + '_disguised'
            for level, suffix in (('Near', ''), ('Middle', '_m'), ('Far', '_f')):
                model = next(m for m in doc['Models'] if m['Name'] == recipe['ModelName'] + suffix)
                part_names = {p['Name'] for p in model['Parts']}
                assert all(c['Part'] in part_names for c in recipe['Levels'][level])
            for image in recipe['Textures'].values():
                assert (ROOT / 'GameResources' / (image + '.png')).is_file(), image
            resource = ROOT / 'Content/Worldfall/LeafCards' / (asset + '.disguised' + (str(index) if index else '') + '.json')
            write(resource, recipe)
            recipe_resources.append(resource.relative_to(ROOT).as_posix())
    shutil.copyfile(candidate, production)
    source_glbs = ['camouflage-v1/' + p.relative_to(folder).as_posix() for p in sorted((folder / 'glbs').glob('*.glb'))]
    editable = ['camouflage-v1/model.blend', 'camouflage-v1/build.py']
    assert all((folder.parent / p).is_file() for p in editable + source_glbs)
    manifest = {
        'asset': asset, 'version': 'camouflage-v1', 'publication': 'published_offline',
        'production_resource': production.relative_to(ROOT).as_posix(), 'production_sha256': digest,
        'models': 6, 'triangles': triangles, 'active_models_unchanged': True,
        'source_glbs': source_glbs, 'editable_sources': editable, 'review': review,
        'leaf_resources': recipe_resources, 'game_test': 'pending_user_manual'
    }
    write(folder / 'manifest.json', manifest)
    registry_path = ROOT / 'Doc/修仙贴图三维化/Blender模型清单.json'
    registry = read(registry_path)
    row = next(r for r in registry if r['Id'] == asset)
    row.update(Models=6, ExportedModels=6, RuntimeSha256=digest, Triangles=triangles,
               NativeVersion='camouflage-v1', EditableSources=editable, source_glbs=source_glbs,
               PublicationRecord=(folder / 'manifest.json').relative_to(ROOT).as_posix(),
               GameTest='pending_user_manual', Status='活动及静止伪装各三档已离线发布；游戏效果待查看')
    row['inspection_files']['camouflage_contact'] = 'camouflage-v1/contact.png'
    row['inspection_files']['camouflage_reopen'] = 'camouflage-v1/reopen-check.json'
    row['CamouflageLeafCardsResources'] = recipe_resources
    write(registry_path, registry)
    progress_path = ROOT / 'Art/Worldfall/creature-native-v1/progress.json'
    progress = read(progress_path)
    item = next(i for i in progress['assets'] if i['id'] == asset)
    item.update(production_sha256=digest, visual_version=digest[:12], camouflage_version='camouflage-v1',
                camouflage_models=3, state='活动与静止伪装均已发布，待游戏测试')
    write(progress_path, progress)
    print(json.dumps({'published': asset, 'models': 6, 'sha256': digest}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('assets', nargs='+')
    for asset_id in parser.parse_args().assets:
        publish(asset_id)
