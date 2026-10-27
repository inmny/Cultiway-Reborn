"""发布人工查看后的群系自建模型，整理可追溯的来源和检查页。"""
import argparse
from collections import Counter, defaultdict
import hashlib
import html
import json
from pathlib import Path
import re
import shutil
from datetime import date
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[2]
BATCH = ROOT / 'Art/Worldfall/biome-native-v1'
BIOMES = ['Bamboo', 'Candle', 'Cemetery', 'Coral', 'Fern', 'FleshBlood', 'Knowledge',
          'Oak', 'Rice', 'Titans', 'Nurgle', 'Khorne', 'Tzeentch', 'Slaanesh']
ASSETS = [asset for biome in BIOMES for asset in (biome + '_tree', biome)]
LABELS = {'Bamboo': '竹林', 'Candle': '烛火', 'Cemetery': '墓园', 'Coral': '珊瑚',
          'Fern': '蕨类', 'FleshBlood': '血肉', 'Knowledge': '知识', 'Oak': '橡树',
          'Rice': '稻米', 'Titans': '巨灵', 'Nurgle': '纳垢', 'Khorne': '恐虐',
          'Tzeentch': '奸奇', 'Slaanesh': '色孽'}
VIEWS = {'front': '正面', 'side': '侧面', 'back': '背面', 'top': '俯视',
         'near': '近景', 'middle': '中景', 'far': '远景'}
CSS = '''body{font:16px/1.6 "Microsoft YaHei",sans-serif;color:#e5e7eb;background:#18222d;margin:30px}
a{color:#8cc5ff}h1,h2{line-height:1.35}p{max-width:1100px}table{border-collapse:collapse;width:100%}
th,td{padding:8px;border:1px solid #52606c;text-align:left}figure{margin:0;background:#303c46;padding:8px}
img{display:block;width:100%;object-fit:contain}img.sprite{height:220px;image-rendering:pixelated}
.grid{display:grid;grid-template-columns:repeat(8,minmax(100px,1fr));gap:8px;margin:20px 0}
figcaption{text-align:center;font-size:14px}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:18px}
.card{background:#283744;padding:16px}.card img{height:220px}summary{cursor:pointer}code{overflow-wrap:anywhere}
@media(max-width:1000px){.grid{grid-template-columns:repeat(4,minmax(110px,1fr))}}'''


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(asset):
    groups = defaultdict(list)
    for path in sorted((ROOT / 'GameResources/buildings' / asset).glob('*.png')):
        match = re.fullmatch(r'(main|ruin)_\s*(\d+)(?:_(\d+))?', path.stem)
        if match:
            state, number, animation = match.groups()
            groups[f'{state}_{int(number)}'].append((int(animation or 0), animation is not None, path))
    return {frame: sorted(rows)[0][2] for frame, rows in groups.items()}


def link(path, relative_to):
    # 两者都在仓库；保留相对链接，使检查包随仓库一起移动。
    import os
    return quote(Path(os.path.relpath(path, relative_to)).as_posix(), safe='/')


def asset_page(asset, out, strategy, sha):
    original = sources(asset)
    by_frame = defaultdict(dict)
    for record in strategy['models']:
        by_frame[record['frame']][record['level']] = record
    blocks = []
    for frame in sorted(by_frame, key=lambda f: (f.startswith('ruin'), int(f.split('_')[1]))):
        levels = by_frame[frame]
        tiles = [f'<figure><img class="sprite" src="{link(original[frame], out)}"><figcaption>原始像素图</figcaption></figure>']
        for view, label in VIEWS.items():
            path = out / 'previews' / frame / (view + '.png')
            assert path.is_file(), path
            tiles.append(f'<figure><img loading="lazy" src="{link(path,out)}"><figcaption>{label}</figcaption></figure>')
        counts = ' / '.join(str(levels[level]['triangles']) for level in ('near', 'middle', 'far'))
        models = ' · '.join(f'<a href="{quote(levels[level]["glb"])}">{VIEWS[level]}GLB</a>' for level in ('near','middle','far'))
        blocks.append(f'<h2>{html.escape(frame)} · 三角面 {counts}</h2><div class="grid">{"".join(tiles)}</div><p>{models}</p>')
    notes = html.escape(str(strategy.get('notes', '')))
    assumptions = ''.join('<li>'+html.escape(str(note))+'</li>' for note in strategy.get('assumptions', []))
    doc = f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>{asset} 原图与模型</title><style>{CSS}</style><body>'
    doc += f'<a href="../../biome-native-v1/index.html">全部群系</a><h1>{asset} 原图与模型</h1><p>Blender 自建实体；全部图片来自实际重导入的导出模型。游戏验收待用户查看。</p><p>{notes}</p><ul>{assumptions}</ul>'
    doc += '<p><a href="model.blend">可编辑模型</a> · <a href="build.py">生成脚本</a> · <a href="manifest.json">资源与检查记录</a> · <a href="reopen-check.json">实际导入检查</a> · <a href="visual-inspection.json">看图记录</a></p>'
    doc += ''.join(blocks)+f'<p>正式资源 SHA-256：<code>{sha}</code></p></body></html>'
    (out / 'index.html').write_text(doc, encoding='utf-8')


def source_contact(asset, out):
    from PIL import Image, ImageDraw, ImageFont
    items = sorted(sources(asset).items(), key=lambda row: (row[0].startswith('ruin'),int(row[0].split('_')[1])))
    cols = min(6,len(items)); size = 180
    canvas = Image.new('RGB',(cols*size,((len(items)+cols-1)//cols)*size),'#25313b')
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
    for i,(frame,path) in enumerate(items):
        original=Image.open(path).convert('RGBA')
        scale=min(150/original.width,145/original.height)
        enlarged=original.resize((max(1,int(original.width*scale)),max(1,int(original.height*scale))),Image.Resampling.NEAREST)
        x=(i%cols)*size; y=(i//cols)*size
        draw.text((x+5,y+3),f'{frame}  {original.width}×{original.height}',font=font,fill='#e5e7eb')
        canvas.paste(enlarged,(x+(size-enlarged.width)//2,y+27+(145-enlarged.height)//2),enlarged)
    canvas.save(out/'source-contact.png')


def asset_directory(asset):
    """从正式清单取得当前发布版本，旧版本留作历史。"""
    rows = read(ROOT / 'Doc/修仙贴图三维化/Blender模型清单.json')
    row = next(row for row in rows if row['Id'] == asset)
    return ROOT / 'Art/Worldfall' / asset / row['NativeVersion']


def publish(asset, review_note, version='native-biome-v1'):
    assert asset in ASSETS, asset
    out = ROOT / 'Art/Worldfall' / asset / version
    candidate = out / 'models.candidate.json'
    production = ROOT / 'Content/Worldfall/Models' / asset / 'models.json'
    source = candidate if candidate.exists() else production
    data, strategy, check, visual = (read(path) for path in (
        source, out / 'native-strategy.json', out / 'reopen-check.json', out / 'visual-inspection.json'))
    sha = digest(source)
    assert check['candidate_sha256'] == visual['candidate_sha256'] == strategy['candidate_sha256'] == sha, asset
    assert check['fresh_blender_process'] and check['all_glbs_actually_imported'] and check['all_geometry_color_normals_pass']
    actual_names = {record['name'] for record in check['models'] if record['geometry_color_normals_pass']}
    assert len(actual_names) == len(data['Models'])
    expected = {(frame, level) for frame in sources(asset) for level in ('near', 'middle', 'far')}
    assert {(record['frame'], record['level']) for record in strategy['models']} == expected
    assert len(strategy['models']) == len(data['Models']) == len(expected)
    records = {record['name']: record for record in strategy['models']}
    triangles, palette = {}, 0
    tree = asset.endswith('_tree')
    marker = 'cultiway_tree' if tree else 'cultiway_plant'
    for model in data['Models']:
        name = model['Name']; info = model['Info']; record = records[name]
        assert name in actual_names and info[marker] == '1' and info['cultiway_asset'] == asset
        assert info['palette'] == 'fixed' and info['kingdom_slots'] == 'none'
        assert info['cultiway_frame'] == record['frame']
        wind = tree and asset in ('Bamboo_tree','Fern_tree','Oak_tree','Rice_tree') and record['frame'].startswith('main_')
        assert info['cultiway_wind'] == ('1' if wind else '0')
        slots = len(model['SlotNames']); palette = max(palette, slots)
        assert 0 < slots <= 64 and len(model['SlotColor']) == len(model['SlotKind']) == len(model['SlotFlags']) == slots
        assert 4 not in model['SlotKind'], '本批自然物件不能使用建筑砖石纹理：'+name
        triangles[name] = sum(len(part['Tri']) // 3 for part in model['Parts'])
        assert triangles[name] == record['triangles'] <= record['budget']
        assert abs(model['Min']['Z']) <= 1e-6
        if tree: assert abs(model['Max']['Z'] - 2) <= 1e-6
        for order, part in enumerate(model['Parts']):
            assert -1 <= part['Parent'] < order
            assert len(part['Slot']) == len(part['Tri']) // 3
            assert all(0 <= slot < slots for slot in part['Slot'])
        assert (out / record['glb']).is_file()
    assert (out / 'model.blend').is_file() and (out / 'build.py').is_file()
    production.parent.mkdir(parents=True, exist_ok=True)
    if source != production: shutil.copyfile(source, production)
    assert digest(production) == sha
    source_contact(asset,out)
    manifest = {'asset': asset, 'family': data['Family'], 'version': version,
                'method': 'Blender自行构形；未使用三维生成平台', 'models': len(data['Models']),
                'static_appearances': len(sources(asset)), 'production_resource': production.relative_to(ROOT).as_posix(),
                'production_sha256': sha, 'publication': 'published_offline', 'max_color_slots': palette,
                'triangles': triangles, 'source_glbs': [version+'/'+r['glb'] for r in strategy['models']],
                'editable_sources': [version+'/model.blend', version+'/build.py'],
                'inspection_files': {'original': 'source-contact.png',
                                     'reference': version+'/index.html',
                                     'raw_preview': version+'/previews/main_0/front.png',
                                     'candidate_front': version+'/previews/main_0/front.png'},
                'root_review': {'status': 'actual_contact_images_viewed', 'note': review_note, 'candidate_sha256': sha},
                'offline_checks': {'reopen': version+'/reopen-check.json', 'visual': version+'/visual-inspection.json'},
                'game_test': 'pending_user_manual'}
    original_rel = manifest['inspection_files']['original']
    manifest['inspection_files']['original'] = version+'/'+original_rel
    manifest['inspection_files']['reference'] = manifest['inspection_files']['original']
    receipt_path = out / 'receipt.json'
    if receipt_path.is_file():
        receipt = read(receipt_path)
        inspection = receipt.get('inspection_files', {})
        if all(isinstance(inspection.get(key), str) for key in manifest['inspection_files']):
            assert all((out.parent / inspection[key]).is_file() for key in manifest['inspection_files'])
            manifest['inspection_files'] = {key: inspection[key] for key in manifest['inspection_files']}
        for path in receipt.get('editable_sources', []):
            assert (out.parent / path).is_file(), path
            if path not in manifest['editable_sources']:
                manifest['editable_sources'].append(path)
    write(out / 'manifest.json', manifest)
    for receipt_name in ('receipt.json','delivery.json','handoff.json'):
        receipt_path=out/receipt_name
        if receipt_path.exists():
            receipt=read(receipt_path)
            receipt.update({'production_resource':manifest['production_resource'],'production_sha256':sha,
                            'status':'published_offline','publication':'published_offline','game_test':'pending_user_manual'})
            write(receipt_path,receipt)
    readme_path=out/'README.md'
    if readme_path.exists():
        text=readme_path.read_text(encoding='utf-8')
        end='<!-- 当前发布记录结束 -->'
        if text.startswith('<!-- 当前发布记录 -->'):text=text.split(end,1)[1].lstrip()
        banner=f'<!-- 当前发布记录 -->\n{date.today().isoformat()} 已离线发布 {len(data["Models"])} 个模型；[原图与最终模型检查页](index.html)。正式资源为 `{manifest["production_resource"]}`，SHA-256 `{sha}`。游戏效果待用户查看。以下保留对象制作说明。\n{end}\n\n'
        readme_path.write_text(banner+text,encoding='utf-8')
    asset_page(asset, out, strategy, sha)
    global_path = ROOT / 'Doc/修仙贴图三维化/Blender模型清单.json'
    global_rows = read(global_path)
    row = next(r for r in global_rows if r['Id'] == asset)
    row.update({'Family': data['Family'], 'Models': len(data['Models']), 'ExportedModels': len(data['Models']),
                'Authoring': 'Blender 4.4 自建实体', 'Status': '全部外观、残骸和三档离线发布；游戏验收待用户查看',
                'Triangles': triangles, 'RuntimeSha256': sha, 'WorldfallPaletteValid': True,
                'PublishedTrellis': False, 'PublishedNative': True, 'NativeVersion': version,
                'EditableSources': manifest['editable_sources'], 'source_glbs': manifest['source_glbs'],
                'inspection_files': manifest['inspection_files'], 'GameTest': 'pending_user_manual'})
    write(global_path, global_rows)
    print('PUBLISHED', asset, len(data['Models']), sha)


def refresh():
    progress, cards, table = [], [], []
    total = 0
    for asset in ASSETS:
        expected = len(sources(asset))*3
        out = asset_directory(asset)
        path = out / 'manifest.json'
        label = LABELS[asset.removesuffix('_tree')] + ('物件' if asset.endswith('_tree') else '花草')
        row = {'asset':asset, 'label':label, 'expected_models':expected, 'status':'制作中', 'game_test':'pending_user_manual'}
        counts = '—'
        if path.exists():
            manifest = read(path)
            assert digest(ROOT / manifest['production_resource']) == manifest['production_sha256']
            row.update({'status':'离线发布','models':manifest['models'],'version':manifest['version'],'production_sha256':manifest['production_sha256']})
            total += manifest['models']
            strategy = read(out / 'native-strategy.json')
            ranges = []
            for level in ('near','middle','far'):
                numbers = [r['triangles'] for r in strategy['models'] if r['level']==level]
                ranges.append(str(min(numbers)) if min(numbers)==max(numbers) else f'{min(numbers)}–{max(numbers)}')
            counts = ' / '.join(ranges)
            row['triangle_ranges'] = ranges
            url = link(out / 'index.html', BATCH)
            preview_path = out.parent / manifest['representative_preview'] if 'representative_preview' in manifest else out / 'previews/main_0/front.png'
            preview = link(preview_path, BATCH)
            cards.append(f'<div class="card"><a href="{url}"><img loading="lazy" src="{preview}"><b>{label} · {asset}</b></a><p>{manifest["models"]}模型；近/中/远面数 {counts}</p></div>')
        table.append(f'| {label} `{asset}` | {expected} | {row["status"]} | {counts} | 待用户查看 |')
        progress.append(row)
    write(BATCH/'progress.json', {'version':'native-biome-v1','assets':progress,'published_assets':sum(r['status']=='离线发布' for r in progress),
                                'published_models':total,'expected_models':sum(r['expected_models'] for r in progress),'game_test':'pending_user_manual'})
    title = f'全部群系自建植被：已发布 {len(cards)} / 28 类'
    (BATCH/'index.html').write_text(f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>{title}</title><style>{CSS}</style><body><h1>{title}</h1><p>本页28类按实际像素原图由 Blender 自行构形，没有调用三维生成平台。原图、四面与同相机近中远在每个物体的检查页中。有真叶的树类已改透明叶簇；黑暗森林保留原树干与无叶残骸，其叶部改造单独记录。最新人物比例与原生树皮纹理在接入时应用；游戏效果待用户自行查看。</p><p><a href="../biome-leaf-cards-v1/index.html">全部透明叶片与叶簇</a> · <a href="../biome-sizing-v1/index.html">人物比例与原生纹理检查</a> · <a href="../Dark_tree/tripo-api-v2/index.html">黑森林历史原网格检查页</a> · <a href="../Dark_tree/tripo-api-v2/worldfall-v1/material-kind-fix.json">黑森林材质字段核验</a></p><p>已发布{total}个模型，点击下方查看全部随机外观和残骸。<a href="overview.png">全部群系原图与代表模型总对照</a> · <a href="progress.json">逐项进度</a></p><div class="cards">'+''.join(cards)+'</div></body></html>', encoding='utf-8')
    (ROOT/'Doc/修仙贴图三维化/群系树木与花草自建/资产结果.md').write_text('# 资产结果\n\n当前文件数据自动汇总；发布代表离线检查后已写入游戏资源，游戏效果仍待用户查看。面数为实际三角面，包含主外观和残骸的范围。\n\n| 资产 | 预期模型数 | 状态 | 近/中/远三角面 | 游戏验收 |\n| --- | ---: | --- | --- | --- |\n'+'\n'.join(table)+'\n', encoding='utf-8')
    print('BATCH',len(cards),'assets',total,'models')


def overview():
    """用实际重导入预览拼全群系原图对照，不能代替逐物体四面检查。"""
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    width, header, row_height = 1280, 95, 250
    canvas = Image.new('RGB', (width, header+row_height*len(BIOMES)), '#25313b')
    draw = ImageDraw.Draw(canvas)
    draw.text((18,12), '群系自建模型 · 原图与实际导出预览', fill='#eef2f7', font=font)
    columns = [(160,'物件原图'),(410,'物件模型'),(750,'花草原图'),(1000,'花草模型')]
    for x,label in columns: draw.text((x,52),label,fill='#c9d4dd',font=small)
    for row,biome in enumerate(BIOMES):
        y = header+row*row_height
        draw.text((12,y+90),LABELS[biome],fill='#eef2f7',font=font)
        for column,asset in enumerate((biome+'_tree',biome)):
            out = asset_directory(asset)
            src = [(frame,path) for frame,path in sources(asset).items() if frame.startswith('main_')]
            frame,path = max(src,key=lambda r: Image.open(r[1]).height)
            x = 130+column*590
            original = Image.open(path).convert('RGBA')
            original.thumbnail((210,204),Image.Resampling.NEAREST)
            scale = min(210/original.width,204/original.height)
            original = original.resize((int(original.width*scale),int(original.height*scale)),Image.Resampling.NEAREST)
            canvas.paste(original,(x+(225-original.width)//2,y+(215-original.height)//2),original)
            manifest=read(out/'manifest.json')
            preview=out.parent/manifest['preview_frames'][frame] if 'preview_frames' in manifest else out/'previews'/frame/'front.png'
            if (out/'manifest.json').exists() and preview.exists():
                model=Image.open(preview).convert('RGB').resize((220,220),Image.Resampling.LANCZOS)
                canvas.paste(model,(x+245,y))
            else: draw.text((x+285,y+95),'制作中',fill='#aab7c2',font=small)
            draw.text((x+12,y+222),asset+' / '+frame,fill='#c9d4dd',font=small)
        draw.line((0,y+row_height-1,width,y+row_height-1),fill='#53616c')
    canvas.save(BATCH/'overview.png')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', nargs='*', default=[])
    parser.add_argument('--review-note')
    parser.add_argument('--version', default='native-biome-v1')
    args = parser.parse_args()
    if args.assets:
        assert args.review_note, '发布时须填写本次实际查看的具体结果'
        for asset in args.assets: publish(asset, args.review_note, args.version)
    refresh()
    overview()
