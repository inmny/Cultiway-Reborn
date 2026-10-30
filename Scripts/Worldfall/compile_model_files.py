"""把编辑用的 models.json 转为游戏直接读取的紧凑模型包；默认只更新发生变化的文件。"""
import argparse
import array
import base64
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import zlib

ROOT = Path(__file__).resolve().parents[2]
HEADER = b'CWFM' + struct.pack('<i', 1)


def write_string(stream, value):
    """写入与 BinaryReader.ReadString 一致的 UTF-8 字符串。"""
    data = value.encode('utf-8')
    length = len(data)
    while length >= 128:
        stream.write(bytes([(length & 127) | 128]))
        length >>= 7
    stream.write(bytes([length]))
    stream.write(data)


def write_array(stream, values, kind):
    """按运行时元素宽度保存原数据，不减面、不量化颜色或坐标。"""
    if kind == 'B' and isinstance(values, str):
        values = base64.b64decode(values)
    data = array.array(kind, values)
    if sys.byteorder != 'little' and data.itemsize > 1:
        data.byteswap()
    stream.write(struct.pack('<i', len(data)))
    stream.write(data.tobytes())


def write_vector(stream, value):
    """写入原有单精度坐标。"""
    stream.write(struct.pack('<3f', *(value[key] for key in ('X', 'Y', 'Z'))))


def info_text(value):
    """保持原生附加标记的文本含义。"""
    if isinstance(value, bool):
        return str(value)
    if isinstance(value, float):
        return format(value, '.15g')
    return str(value)


def compile_file(source, check=False):
    """按源文件摘要更新一份物体的紧凑模型包。"""
    # Git 在不同电脑上可能转换换行或保留 BOM，统一文本形式后再检查模型是否需要更新。
    text = source.read_text(encoding='utf-8-sig')
    digest = hashlib.sha256(text.encode('utf-8')).digest()
    target = source.with_suffix('.bin')
    if target.is_file():
        with target.open('rb') as stream:
            if stream.read(40) == HEADER + digest:
                return False
    if check:
        raise ValueError(f'模型包需要更新：{source}；请运行 Scripts/Worldfall/compile_model_files.py')
    document = json.loads(text)
    output = io.BytesIO()
    write_string(output, document['Id'])
    write_string(output, document['Category'])
    output.write(struct.pack('<i', len(document['Models'])))
    for model in document['Models']:
        write_string(output, model['Name'])
        write_string(output, ';'.join(f'{key}={info_text(value)}' for key, value in model['Info'].items()))
        output.write(struct.pack('<i', len(model['SlotNames'])))
        for name in model['SlotNames']:
            write_string(output, name)
        for key, kind in (('SlotKind', 'B'), ('SlotFlags', 'B'), ('SlotColor', 'I')):
            write_array(output, model[key], kind)
        write_vector(output, model['Min'])
        write_vector(output, model['Max'])
        output.write(struct.pack('<i', len(model['Parts'])))
        for part in model['Parts']:
            write_string(output, part['Name'])
            output.write(struct.pack('<i', part['Parent']))
            write_vector(output, part['Pivot'])
            for key, kind in (('Pos', 'f'), ('Nrm', 'f'), ('Tri', 'i'), ('Slot', 'B'), ('Axis', 'B')):
                write_array(output, part[key], kind)
            output.write(struct.pack('<i', part.get('Version', 0)))
    compressor = zlib.compressobj(level=6, wbits=-15)
    temporary = target.with_suffix('.bin.tmp')
    temporary.write_bytes(HEADER + digest + compressor.compress(output.getvalue()) + compressor.flush())
    temporary.replace(target)
    return True


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='只检查模型包是否与编辑文件一致')
    parser.add_argument('--asset', nargs='*', help='只处理指定资产，默认处理全部正式模型')
    args = parser.parse_args()
    directory = ROOT / 'Content/Worldfall/Models'
    sources = [directory / asset / 'models.json' for asset in args.asset] if args.asset else sorted(directory.rglob('models.json'))
    updated = sum(compile_file(source, args.check) for source in sources)
    size = sum(source.with_suffix('.bin').stat().st_size for source in sources)
    print(json.dumps({'assets': len(sources), 'updated': updated, 'bytes': size, 'checked': args.check}, ensure_ascii=False))
