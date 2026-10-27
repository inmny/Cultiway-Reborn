"""核对本批13类建筑的最终文件与已有实导证据；不启动游戏。"""
from collections import defaultdict
from datetime import datetime, timezone
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[2]
BATCH = ROOT / 'Art/Worldfall/remaining-buildings-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class PageLinks(HTMLParser):
    """收集检查页直接使用的本地文件。"""

    def __init__(self):
        super().__init__()
        self.paths = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ('href', 'src') and value:
                url = urlsplit(value)
                if not url.scheme and not url.netloc and url.path:
                    self.paths.append(unquote(url.path))


def check():
    inventory = read(BATCH / 'inventory.json')
    progress = read(BATCH / 'progress.json')
    integration = read(BATCH / 'integration-check.json')
    registry = {r['Id']: r for r in read(ROOT / 'Doc/修仙贴图三维化/Blender模型清单.json')}
    native = {r['asset']: r for r in integration['assets']}
    assert len(inventory) == progress['published'] == progress['expected'] == 13
    assert all(r['published'] and not r['revision_pending'] for r in progress['assets'])
    items = []
    all_names = set()
    link_count = 0
    for item in inventory:
        asset = item['id']
        folder = ROOT / 'Art/Worldfall' / asset / registry[asset]['NativeVersion']
        candidate = folder / 'models.candidate.json'
        production = ROOT / 'Content/Worldfall/Models' / asset / 'models.json'
        digest = sha(candidate)
        strategy = read(folder / 'strategy.json')
        reopen = read(folder / 'reopen-check.json')
        manifest = read(folder / 'manifest.json')
        visual = read(folder / 'visual-inspection.json')
        doc = read(candidate)
        assert digest == sha(production) == strategy['candidate_sha256'] == reopen['candidate_sha256'] == manifest['production_sha256'] == visual['candidate_sha256'] == registry[asset]['RuntimeSha256'] == native[asset]['sha256'], asset
        assert not manifest.get('revision_pending') and manifest['publication'] == 'published_offline', asset
        assert all(reopen[k] for k in ('Passed', 'IndependentBlenderProcess', 'ReopenedEditableSource', 'ImportedAllFinalGLBs')), asset
        assert visual['reviewer'] == 'root' and visual['actual_image_view'] and visual['status'] == 'pass_offline', asset
        for path in visual['viewed']:
            assert (folder / path).is_file(), (asset, path)
        for path in ('model.blend', 'build.py', manifest.get('contact', 'contact.png'), 'index.html', 'README.md', manifest.get('original_inspection', 'original-inspection.json')):
            assert (folder / path).is_file(), (asset, path)
        for path, expected in item['source_sha256'].items():
            assert sha(ROOT / path) == expected, (asset, path)
        records = strategy['models']
        proofs = {r['File']: r for r in reopen['Models']}
        assert len(doc['Models']) == len(records) == len(proofs) == manifest['models'] == native[asset]['models'] == len(item['main_indices']) * 6, asset
        source_paths = defaultdict(set)
        levels = defaultdict(set)
        triangles = {}
        for model, record in zip(doc['Models'], records):
            name = model['Name']
            assert name == record['name'] and name not in all_names, (asset, name)
            all_names.add(name)
            assert model['Info']['cultiway_asset'] == asset and model['Info']['cultiway_building'] == '1' and model['Info']['kingdom_slots'] == 'none', name
            assert len(model['SlotNames']) <= 64, name
            count = sum(len(p['Tri']) // 3 for p in model['Parts'])
            assert count == record['triangles'] <= record['budget'], name
            triangles[name] = count
            proof = proofs[Path(record['glb']).name]
            assert all(proof[k] for k in ('FiniteGeometry', 'UnitNormals', 'ValidUVAndIndices', 'CandidateJsonTrianglePositionsWindingAndSlotsMatch')), name
            assert proof['Triangles'] == count and proof['Sha256'] == sha(folder / record['glb']), name
            source_paths[record['state']].update(record['animation_sources'])
            levels[(record['state'], record['frame'])].add(record['level'])
        assert triangles == manifest['triangles'] == registry[asset]['Triangles'], asset
        assert source_paths['main'] == set(item['originals']), asset
        assert source_paths['ruin'] == {p for p in item['other_originals'] if Path(p).name.startswith('ruin_')}, asset
        assert {frame for state, frame in levels if state == 'main'} == {f'main_{i}' for i in item['main_indices']}, asset
        assert all(v == {'near', 'middle', 'far'} for v in levels.values()), asset
        parser = PageLinks()
        parser.feed((folder / 'index.html').read_text(encoding='utf-8'))
        for path in parser.paths:
            assert (folder / path).is_file(), (asset, '检查页缺失链接', path)
        link_count += len(parser.paths)
        items.append({'asset': asset, 'appearances': len(item['main_indices']), 'models': len(records), 'sha256': digest, 'main_triangles': [r['triangles'] for r in records if r['state'] == 'main' and r['frame'] == 'main_0'], 'ruin_triangles': [r['triangles'] for r in records if r['state'] == 'ruin' and r['frame'] == 'ruin_0']})
    parser = PageLinks()
    parser.feed((BATCH / 'index.html').read_text(encoding='utf-8'))
    for path in parser.paths:
        assert (BATCH / path).is_file(), ('总览缺失链接', path)
    link_count += len(parser.paths)
    preserved = read(BATCH / 'preserved-before.json')['assets']
    for row in preserved:
        assert sha(ROOT / row['production_resource']) == row['sha256'], row['id']
    civil = read(ROOT / 'Art/Worldfall/civil-buildings-v1/final-delivery-check.json')['items']
    assert len(civil) == 39
    for row in civil:
        assert sha(ROOT / 'Content/Worldfall/Models' / row['asset'] / 'models.json') == row['sha256'], row['asset']
    revised = {'Cultiway.NurgleTower', 'Cultiway.SlaaneshTower', 'Cultiway.KhorneTower', 'Cultiway.SkavenBlight', 'Cultiway.TzeentchTower'}
    # 以上次交付清单比较未重制的资产，不依赖已清理的历史备份。
    previous = read(BATCH / 'final-delivery-check.json')
    for row in previous['items']:
        if row['asset'] not in revised:
            assert sha(ROOT / 'Content/Worldfall/Models' / row['asset'] / 'models.json') == row['sha256'], row['asset']
    builds = read(BATCH / 'build-check.json')
    assert all(builds[k]['exit_code'] == builds[k]['errors'] == 0 for k in ('worldfall_enabled', 'worldfall_disabled'))
    assert integration['passed'] and integration['authored_state_palettes_preserved']
    assert set(native) == {i['id'] for i in inventory}
    assert integration['family_cases'] == 69 and integration['palette_cases'] == 552 and integration['stage_cases'] == 0
    assert read(BATCH / 'stage-failures.json') == []
    beauty = read(ROOT / 'Art/Worldfall/Cultiway.SlaaneshTower/native-civ-v1/runtime-normal-reopen-check.json')
    assert all(beauty[k] for k in ('Passed', 'IndependentBlenderProcess', 'ReopenedBlendNormalsMatchSerializedNrm', 'AllFinalGlbNormalsMatchSerializedNrm'))
    assert len(beauty['GLBs']) == len(beauty['EditableSource']) == 6
    assert all(r['MatchedSerializedNrm'] and r['UnitNormals'] for r in beauty['GLBs'] + beauty['EditableSource'])
    assert beauty['candidate_sha256'] == native['Cultiway.SlaaneshTower']['sha256']
    for row in beauty['GLBs']:
        assert row['Sha256'] == sha(ROOT / 'Art/Worldfall/Cultiway.SlaaneshTower/native-civ-v1/glbs' / row['File'])
    pit_check = read(BATCH / 'ground-pit-check.json')
    assert pit_check['passed'] and not pit_check['gpu_buffer_created']
    assert pit_check['candidate_sha256'] == native['Cultiway.SkavenBlight']['sha256']
    assert pit_check['assembly_sha256'] == builds['worldfall_enabled']['assembly_sha256']
    assert sha(ROOT / 'Source/Content/Worldfall/WorldfallGroundPits.cs') == builds['source_sha256']
    pit_models = read(ROOT / 'Content/Worldfall/Models/Cultiway.SkavenBlight/models.json')['Models']
    assert len(pit_models) == 6 and all(m['Info']['cultiway_ground_pit'] == '1' and
        m['Info']['pit_cut_inner'] == '0.375' and m['Info']['pit_cut_radius'] == '0.410' and
        m['Info']['pit_floor'] == '-0.780' and m['Min']['Z'] < 0 for m in pit_models)
    assert sum(i['appearances'] for i in items) == 23 and len(all_names) == progress['models'] == 138
    result = {'passed': True, 'checked_at_utc': datetime.now(timezone.utc).isoformat(), 'assets': 13, 'appearances': 23, 'models': 138,
              'registered_nonvegetation_building_file_coverage': len(items) + len(civil) + len(preserved),
              'all_source_images_unchanged_and_animation_paths_covered': True, 'all_appearances_have_three_distances_and_main_ruin': True,
              'all_candidate_production_review_registry_native_hashes_match': True, 'all_final_glbs_independently_reimported': True,
              'all_root_visual_reviews_current': True, 'checked_page_links': link_count, 'preserved_special_buildings': len(preserved),
              'preserved_civil_buildings': len(civil), 'family_cases': 69, 'palette_cases': 552, 'stage_cases': 0,
              'other_eight_batch_buildings_unchanged': True,
              'beauty_final_glb_and_editable_normals_match_runtime': True,
              'builds_zero_errors': True, 'build_evidence_date': builds['date'], 'game_test': 'pending_user_manual',
              'ground_pit_upload_data_cases': len(pit_check['checks']), 'ground_pit_runtime_patch_test': 'pending_user_manual',
              'limitations': ['本批以原图主要特点解构为静态三维，未宣称逐像素或细纹完全复刻', '原图动画保留来源，本批未制作连续动画', '不可见侧背和未确认用途按物体逐项记录', '未启动或操作游戏；最终外观、交互、性能及无Worldfall启动待用户实测'], 'items': items}
    (BATCH / 'final-delivery-check.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('passed', 'assets', 'appearances', 'models', 'registered_nonvegetation_building_file_coverage', 'checked_page_links')}, ensure_ascii=False))


if __name__ == '__main__':
    check()
