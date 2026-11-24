"""为已通过的原版世界复验增加按世界时间编码的录像。"""
from pathlib import Path

from common import version_stamp, write_json
from record_body_connections import encode

SCRIPTS = Path(__file__).parent


def progress_helpers(enabled):
    return (f'\nbool captureProgress = {str(enabled).lower()};\n'
            + (SCRIPTS / 'world_progress_capture.script').read_text() + '\n')


def encode_progress(folder, cases, result):
    for key, title in cases.items():
        case = folder / key
        write_json(case / 'metadata.json', {'version': version_stamp()[0]})
        write_json(case / 'result.json', result)
        encode(key, case, folder, True, title, '16.07 实际进度')
