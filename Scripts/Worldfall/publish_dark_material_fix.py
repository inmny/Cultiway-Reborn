"""发布黑森林树木的纯材质修正；严格保持其他模型字段。"""
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Art/Worldfall/Dark_tree/tripo-api-v2/worldfall-v1'
PRODUCTION = ROOT / 'Content/Worldfall/Models/Dark_tree/models.json'
CANDIDATE = OUT / 'models.material-candidate.json'
PROOF = OUT / 'material-kind-fix.json'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


proof = read(PROOF)
before, after = read(PRODUCTION), read(CANDIDATE)
assert sha(PRODUCTION) == proof['production_before_sha256']
assert sha(CANDIDATE) == proof['candidate_sha256']
assert len(before['Models']) == len(after['Models']) == 24
assert {k: v for k, v in before.items() if k != 'Models'} == {k: v for k, v in after.items() if k != 'Models'}
for old, new in zip(before['Models'], after['Models']):
    assert {k: v for k, v in old.items() if k != 'SlotKind'} == {k: v for k, v in new.items() if k != 'SlotKind'}
    assert len(new['SlotKind']) == len(new['SlotNames']) and all(kind == 0 for kind in new['SlotKind'])
shutil.copyfile(CANDIDATE, PRODUCTION)
current_sha = sha(PRODUCTION)
assert current_sha == proof['candidate_sha256']
proof.update({'status': 'published_offline', 'production_replaced': True,
              'production_current_sha256': current_sha, 'game_test': 'pending_user_restart',
              'candidate_copy_retained': False})
write(PROOF, proof)

manifest_path = ROOT / 'Art/Worldfall/Dark_tree/tripo-api-v2/manifest.json'
manifest = read(manifest_path)
manifest.update({'production_sha256': current_sha, 'production_bytes': PRODUCTION.stat().st_size,
                 'material_kind_fix': 'worldfall-v1/material-kind-fix.json',
                 'material_game_test': 'pending_user_restart'})
write(manifest_path, manifest)
check_path = OUT / 'reopen-check.json'
check = read(check_path)
check.update({'original_geometry_checked_sha256': check['models_sha256'],
              'production_resource': 'Content/Worldfall/Models/Dark_tree/models.json',
              'production_sha256': current_sha,
              'metadata_only_amendment': '仅SlotKind改0；原实际导入和图片仍证明相同几何，不代表新游戏材质实测',
              'material_fix_record': 'material-kind-fix.json', 'material_game_test': 'pending_user_restart'})
write(check_path, check)
rows_path = ROOT / 'Doc/修仙贴图三维化/Blender模型清单.json'
rows = read(rows_path)
row = next(row for row in rows if row['Id'] == 'Dark_tree')
row.update({'RuntimeSHA256': current_sha, 'RuntimeSha256': current_sha,
            'MaterialKindFix': '树木所有颜色槽Plain=0，取消木材/布料程序格纹',
            'MaterialFixRecord': 'tripo-api-v2/worldfall-v1/material-kind-fix.json',
            'MaterialGameTest': 'pending_user_restart'})
write(rows_path, rows)
readme_path = OUT / 'README.md'
text = readme_path.read_text(encoding='utf-8-sig')
readme_path.write_text('黑森林树木材质修正已离线发布：全部槽改纯色0，几何、配色及原图未改。'
                      '[核验记录](material-kind-fix.json)，游戏效果待重启复测。\n\n' + text, encoding='utf-8')
# 仅删除逐字节相同的已发布候选副本。
assert sha(CANDIDATE) == sha(PRODUCTION)
CANDIDATE.unlink()
print(json.dumps({'published': True, 'models': 24, 'sha256': current_sha, 'only_slot_kind_changed': True}))
