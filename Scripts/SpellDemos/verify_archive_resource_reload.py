#!/usr/bin/env python3
"""用历史构造验证新版实际资源及正式存读。"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import zipfile

from common import PROJECT, write_json
from verify_skill_archive_fixtures import FIXTURES, sections


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中使用 dev.py exec')
    import baseline_scenarios as baseline

    source = FIXTURES / 'resource-channels-v1-materials-v4.zip'
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    output = Path(tempfile.mkdtemp(prefix='archive-resource-reload-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    with zipfile.ZipFile(source) as archive:
        archive.extract('save/map.wbox', output)
        expected = json.loads(archive.read('restart-expected.json'))
    map_file = output / 'save/map.wbox'
    raw = map_file.read_bytes()
    write_json(output / 'expected.json', {key: expected[key] for key in ('owner', 'customId', 'leave')})

    content = output / 'resources/Content'
    content.mkdir(parents=True)
    for item in (PROJECT / 'Content').iterdir():
        if item.name == 'Forms':
            shutil.copytree(item, content / item.name)
        else:
            (content / item.name).symlink_to(item, target_is_directory=item.is_dir())

    script = output / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    script.write_text(body.replace('"$OUTPUT"', json.dumps(str(output))))
    write_json(output / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '100000')
        if not result.get('passed') or map_file.read_bytes() != raw or hashlib.sha256(source.read_bytes()).hexdigest() != source_hash:
            raise RuntimeError('复验失败或旧档发生改写')
        if (Path(result['backup']) / 'map.wbox').read_bytes() != raw:
            raise RuntimeError('原档备份与输入不同')
        old, current = sections(map_file), sections(output / 'updated/map.wbox')
        if any(current[key]['version'] <= old[key]['version'] for key in ('ability_constructions', 'world_materials')):
            raise RuntimeError('旧构造或材料没有写回当前格式')
        if any('ResourceVersion' in entry for entry in current['ability_constructions']['data']['Definitions']):
            raise RuntimeError('存档不应包含运行期资源版本')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    result['fixtureSha256'] = source_hash
    write_json(output / 'result.json', result)
    print(output)


if __name__ == '__main__':
    main()
