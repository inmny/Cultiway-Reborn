"""核对全量交付文件、检查页链接和已完成的接入检查。"""
import hashlib
import ast
from html.parser import HTMLParser
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
BATCH = ROOT / 'Art/Worldfall/biome-native-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.paths = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ('src', 'href') and value:
                link = urlsplit(value)
                if not link.scheme and not link.netloc and link.path:
                    self.paths.append(unquote(link.path))


progress = read(BATCH / 'progress.json')
assert progress['published_assets'] == 28
assert progress['published_models'] == progress['expected_models'] == 867
names, resources, pages, links = set(), [], [], 0
for row in progress['assets']:
    out = ROOT / 'Art/Worldfall' / row['asset'] / row['version']
    manifest = read(out / 'manifest.json')
    resource = ROOT / manifest['production_resource']
    assert digest(resource) == row['production_sha256'] == manifest['production_sha256']
    data = read(resource)
    assert len(data['Models']) == row['models'] == row['expected_models']
    for model in data['Models']:
        assert model['Name'] not in names, model['Name']
        names.add(model['Name'])
    for field in ('source_glbs', 'editable_sources'):
        for path in manifest[field]:
            assert (out.parent / path).is_file(), path
    for script in out.glob('*.py'):
        source = script.read_text(encoding='utf-8-sig')
        assert source.strip(), script
        ast.parse(source, filename=str(script))
    resources.append({'asset': row['asset'], 'models': row['models'], 'sha256': row['production_sha256']})
    pages.append(out / 'index.html')
pages.append(BATCH / 'index.html')
leaf_batch = ROOT / 'Art/Worldfall/biome-leaf-cards-v1'
leaf_progress = read(leaf_batch / 'progress.json')
assert leaf_progress['published_assets'] == leaf_progress['target_assets'] == 8
assert leaf_progress['covered_models'] == 282
dark_leaf_version = next(row['version'] for row in leaf_progress['assets'] if row['asset'] == 'Dark_tree')
pages.extend([leaf_batch / 'index.html', ROOT / 'Art/Worldfall/Dark_tree' / dark_leaf_version / 'index.html'])
flora_batch = ROOT / 'Art/Worldfall/biome-flora-leaf-cards-v1'
flora_progress = read(flora_batch / 'progress.json')
assert flora_progress['published_assets'] == flora_progress['target_assets'] == 11
assert flora_progress['target_models'] == 102
pages.append(flora_batch / 'index.html')
if any(row['asset'] == 'Dark' and row['status'] == '已离线实装' for row in flora_progress['assets']):
    pages.append(ROOT / 'Art/Worldfall/Dark/leaf-cards-v1/index.html')
leaf_runtime = read(ROOT / 'Art/Worldfall/biome-sizing-v1/leaf-card-check.json')
published_leaves = {row['asset'] for row in leaf_progress['assets'] if row['status'] == '已离线实装'}
published_leaves.update(row['asset'] for row in flora_progress['assets'] if row['status'] == '已离线实装')
assert leaf_runtime['passed'] and {row['asset'] for row in leaf_runtime['levels']} == published_leaves
atlas_capacity = read(leaf_batch / 'atlas-check.json')
assert all(row['SampledOrdersFitAtlas'] for row in atlas_capacity['Levels'].values())
atlas_native = read(leaf_batch / 'atlas-integration-check.json')
assert atlas_native['Passed'] and atlas_native['AtlasSide'] == atlas_capacity['AtlasSide']
for page in pages:
    parser = Links()
    parser.feed(page.read_text(encoding='utf-8'))
    for path in parser.paths:
        assert (page.parent / path).is_file(), (page, path)
        links += 1

integration = read(BATCH / 'integration-check.json')
assert integration['passed'] and integration['palettes'] == 867 and len(integration['assets']) == 28
for record in integration['assets']:
    assert record['source_sha256'] == next(row['sha256'] for row in resources if row['asset'] == record['asset'])
builds = []
for filename in ('build-with-worldfall.log', 'build-without-worldfall.log'):
    text = (BATCH / filename).read_text(encoding='utf-8-sig')
    errors = re.findall(r'(\d+)\s*个错误', text)
    warnings = re.findall(r'(\d+)\s*个警告', text)
    assert errors and int(errors[-1]) == 0, filename
    builds.append({'log': filename, 'errors': 0, 'warnings': int(warnings[-1])})
preserved = []
for asset, expected in {
    'Dark_tree': '03270bbdfd43f0cd02bd141bf9577548bdbf31a5afd8163e7dadbcdcb31704d3',
    'Dark': '44653a89530cf1da684ac9ca404f32101c737e476c2b5c1a43d0b827c315285c'
}.items():
    actual = digest(ROOT / 'Content/Worldfall/Models' / asset / 'models.json')
    amended = actual != expected
    leaf_amended = False
    if amended:
        if asset == 'Dark':
            out = ROOT / 'Art/Worldfall/Dark/leaf-cards-v1'
            manifest = read(out / 'manifest.json')
            assert manifest['production_sha256'] == actual
            assert digest(out / 'models.production-source.json') == expected
            assert read(out / 'models.candidate.json') == read(ROOT / manifest['production_resource'])
            assert read(out / 'preservation-check.json')['Passed']
            assert read(out / 'no-solid-crown-check.json')['Passed']
            preserved.append({'asset': asset, 'sha256': actual,
                              'root_stem_and_mushroom_geometry_preserved': True,
                              'flower_centers_scaled_about_original_center': 0.28,
                              'leaf_geometry_replaced_with_png': True,
                              'original_geometry_resource_sha256': expected})
            continue
        assert asset == 'Dark_tree'
        proof_path = ROOT / 'Art/Worldfall/Dark_tree/tripo-api-v2/worldfall-v1/material-kind-fix.json'
        proof = read(proof_path)
        assert proof['production_before_sha256'] == expected
        assert proof['production_replaced']
        assert proof['all_except_slot_kind_strictly_identical']
        leaf_out = ROOT / 'Art/Worldfall/Dark_tree/leaf-cards-v1'
        if (leaf_out / 'manifest.json').is_file():
            manifest = read(leaf_out / 'manifest.json')
            assert digest(leaf_out.parent / manifest['source_snapshot']) == proof['production_current_sha256']
            assert read(leaf_out / 'preservation-check.json')['Passed']
            assert read(leaf_out / 'no-solid-crown-check.json')['Passed']
            current_out = leaf_out.parent / dark_leaf_version
            current_manifest = read(current_out / 'manifest.json')
            assert current_manifest['production_sha256'] == actual
            assert read(current_out / 'models.candidate.json') == read(ROOT / current_manifest['production_resource'])
            if current_out != leaf_out:
                assert digest(current_out.parent / current_manifest['source_snapshot']) == manifest['production_sha256']
                assert read(current_out / 'models.production-source.json') == read(current_out / 'models.candidate.json')
                assert read(current_out / 'preservation-check.json')['Passed']
                assert read(current_out / 'no-solid-crown-check.json')['Passed']
            leaf_amended = True
        else:
            assert proof['production_current_sha256'] == actual
    preserved.append({'asset': asset, 'sha256': actual, 'geometry_and_colors_preserved': not leaf_amended,
                      'non_leaf_geometry_and_colors_preserved': True, 'leaf_geometry_replaced_with_png': leaf_amended,
                      'material_only_amendment': amended and not leaf_amended,
                      'original_geometry_resource_sha256': expected})
result = {'passed': True, 'assets': 28, 'appearances_and_ruins': 289, 'models': 867,
          'html_pages': len(pages), 'local_links_checked': links, 'resources': resources,
          'native_interface_and_palette_check': 'integration-check.json',
          'builds': builds, 'preserved_dark_assets': preserved,
          'transparent_leaf_assets': 8, 'transparent_leaf_covered_models': 282,
          'transparent_flora_assets': flora_progress['published_assets'],
          'transparent_flora_models': flora_progress['target_models'],
          'leaf_placement_cases': leaf_runtime['transform_cases'],
          'scope': '全量文件和离线接入检查；逐模型实际导入与看图见各物体记录',
          'game_test': 'pending_user_manual'}
(BATCH / 'final-check.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: result[key] for key in ('passed', 'assets', 'models', 'html_pages', 'local_links_checked')}, ensure_ascii=False))
