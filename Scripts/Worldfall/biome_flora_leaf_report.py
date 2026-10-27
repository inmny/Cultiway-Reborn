"""汇总群系花草的真实材质分类、透明叶片进度和原图。"""
import html
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import biome_native_publish as common

ROOT = common.ROOT
OUT = ROOT / 'Art/Worldfall/biome-flora-leaf-cards-v1'
# 保留已确认的竹笋、火团、骨片与肉团，不因属于花草入口而新增叶片。
TARGETS = {
    'Bamboo': ('竹笋', False, '笋身与包裹的笋壳保持实体，不展开为草叶'),
    'Candle': ('烛火', False, '原图火团与暗尖保持实体'),
    'Cemetery': ('墓园', False, '灰色碎片与骨头保持实体'),
    'Coral': ('珊瑚', True, '海藻改透明叶簇；贝壳保留立体壳身与壳腔'),
    'Dark': ('黑暗森林', True, '草叶与小红瓣改透明图，菌帽与柄保持实体'),
    'Fern': ('蕨类', True, '卷曲蕨叶放大1.5倍，细叶柄连接原根茎，成对小羽片保持'),
    'FleshBlood': ('血肉', False, '肉团、灰色短根和器官保持实体'),
    'Knowledge': ('知识', True, '两种绿色芽丛改透明叶簇'),
    'Oak': ('橡树', True, '三种芽片改透明图，灰绿低根体保持实体'),
    'Rice': ('稻田', True, '细叶与基瓣改透明图，短杆保持实体'),
    'Titans': ('泰坦', True, '三种绿芽改透明小叶簇，短根鞘保持实体'),
    'Nurgle': ('纳垢', True, '薄花瓣与真叶改透明图，花心、短杆保持实体'),
    'Khorne': ('恐虐', True, '红瓣与叉叶改透明图，浅色骨壳保持实体'),
    'Tzeentch': ('奸奇', True, '蓝紫小花、双叉叶改透明图，短杆保持实体'),
    'Slaanesh': ('色孽', True, '粉色花瓣与叉叶改透明图，短柄保持实体'),
}


def original_contact():
    """全部主外观按最近邻放大，供分类和轮廓核对。"""
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 17)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 13)
    width, height = 400, 300
    canvas = Image.new('RGB', (width * 4, height * 4), '#25313b')
    draw = ImageDraw.Draw(canvas)
    for number, (asset, (label, _, _)) in enumerate(TARGETS.items()):
        x, y = number % 4 * width, number // 4 * height
        draw.text((x + 8, y + 5), label + ' / ' + asset, fill='#eef1f3', font=font)
        originals = list(common.sources(asset).items())
        columns = 3
        for i, (frame, path) in enumerate(originals):
            image = Image.open(path).convert('RGBA')
            scale = min(105 / image.width, 92 / image.height)
            image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.Resampling.NEAREST)
            tx, ty = x + i % columns * 130, y + 40 + i // columns * 125
            canvas.paste(image, (tx + (130 - image.width) // 2, ty), image)
            draw.text((tx + 5, ty + 96), frame, fill='#d7e0e5', font=small)
        draw.rectangle((x, y, x + width - 1, y + height - 1), outline='#53616c')
    canvas.save(OUT / 'original-contact.png')


def main():
    """根据实际发布清单生成进度，不把候选当成实装。"""
    OUT.mkdir(parents=True, exist_ok=True)
    original_contact()
    rows, cards = [], []
    for asset, (label, uses_leaves, note) in TARGETS.items():
        resource = ROOT / 'Content/Worldfall/Models' / asset / 'models.json'
        data = common.read(resource)
        out = ROOT / 'Art/Worldfall' / asset / 'leaf-cards-v1'
        row = dict(asset=asset, label=label, models=len(data['Models']), target=uses_leaves,
                   note=note, status='制作中' if uses_leaves else '按真实材质保留实体',
                   production_sha256=common.digest(resource),
                   game_test='pending_user_manual')
        if not uses_leaves:
            previous = common.read(common.asset_directory(asset) / 'manifest.json')
            assert previous['production_sha256'] == row['production_sha256']
            row['preserved_published_geometry'] = True
        if uses_leaves and (out / 'manifest.json').is_file():
            manifest = common.read(out / 'manifest.json')
            assert manifest['production_sha256'] == common.digest(resource)
            row.update(status='已离线实装', version='leaf-cards-v1',
                       production_sha256=manifest['production_sha256'],
                       inspection_page=common.link(out / 'index.html', OUT))
            preview = out.parent / manifest['representative_preview']
            cards.append('<div class="card"><a href="' + row['inspection_page'] + '"><img src="' + common.link(preview, OUT) + '"><b>' + label + '</b></a><p>' + note + '</p></div>')
        rows.append(row)
    result = dict(date='2026-10-17', assets=rows, reviewed_assets=len(rows),
                  target_assets=sum(r['target'] for r in rows),
                  target_models=sum(r['models'] for r in rows if r['target']),
                  published_assets=sum(r['status'] == '已离线实装' for r in rows),
                  preserved_assets=[r['asset'] for r in rows if not r['target']],
                  game_test='pending_user_manual')
    common.write(OUT / 'progress.json', result)
    page = '<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>群系花草透明叶片</title><style>' + common.CSS + '</style><body><h1>群系花草：透明叶片与叶簇</h1>'
    page += '<p>原图共15类；其中11类含可改为贴图的叶片或薄花瓣。已离线实装 ' + str(result['published_assets']) + ' / ' + str(result['target_assets']) + ' 类；这些资产共 ' + str(result['target_models']) + ' 个近中远模型，含原样保留的非叶外观。</p>'
    page += '<p>保留当前大小、原图主叶数量、色区和根脚。2K来源保留，运行图最多512，小花草优先256/128；共享原生图集、朝向和缩放。竖直双面交叉片接近正上、正下会收窄，游戏效果待用户查看。</p><p><a href="original-contact.png">全部像素原图</a> · <a href="progress.json">进度和分类</a> · <a href="../biome-leaf-cards-v1/index.html">树类透明叶片</a> · <a href="../biome-sizing-v1/index.html">游戏尺寸</a></p>'
    page += '<table><tr><th>群系</th><th>模型数</th><th>状态</th><th>部位</th></tr>'
    for row in rows:
        title = html.escape(row['label'])
        if row.get('inspection_page'):
            title = '<a href="' + row['inspection_page'] + '">' + title + '</a>'
        page += '<tr><td>' + title + '</td><td>' + str(row['models']) + '</td><td>' + row['status'] + '</td><td>' + row['note'] + '</td></tr>'
    page += '</table><div class="cards">' + ''.join(cards) + '</div></body></html>'
    (OUT / 'index.html').write_text(page, encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('reviewed_assets', 'target_assets', 'target_models', 'published_assets')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
