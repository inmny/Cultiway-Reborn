"""清除已无正式配方引用、且在Art中有完全相同备份的叶簇运行PNG。"""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    directory = (ROOT / 'GameResources/cultiway/worldfall/leaf_cards').resolve()
    used = set()
    for file in (ROOT / 'Content/Worldfall/LeafCards').glob('*.json'):
        recipe = json.loads(file.read_text(encoding='utf-8-sig'))
        used.update((ROOT / 'GameResources' / (value + '.png')).resolve()
                    for value in recipe['Textures'].values())
    assert all(path.is_file() for path in used)
    copies = {}
    for path in (ROOT / 'Art/Worldfall').glob('*/leaf-cards-*/textures/**/*.png'):
        copies.setdefault(path.name, []).append(path)
    removed, retained = [], []
    for path in sorted(directory.rglob('*.png')):
        path = path.resolve()
        assert path.is_relative_to(directory)
        if path in used:
            continue
        sha = digest(path)
        backup = next((copy for copy in copies.get(path.name, []) if digest(copy) == sha), None)
        if backup is None:
            retained.append(path.relative_to(ROOT).as_posix())
            continue
        removed.append({'file': path.relative_to(ROOT).as_posix(), 'bytes': path.stat().st_size,
                        'sha256': sha, 'preserved_art_copy': backup.relative_to(ROOT).as_posix()})
        path.unlink()
    result = {'scope': '仅叶簇运行PNG；删除前确认不被任何正式配方引用且Art有同字节副本',
              'deleted': removed, 'retained_without_identical_copy': retained,
              'active_images_preserved': len(used), 'deleted_bytes': sum(row['bytes'] for row in removed)}
    output = ROOT / 'Art/Worldfall/biome-leaf-cards-v1/runtime-cleanup.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'deleted': len(removed), 'bytes': result['deleted_bytes'],
                      'active_images_preserved': len(used), 'retained': len(retained)}))


if __name__ == '__main__':
    main()
