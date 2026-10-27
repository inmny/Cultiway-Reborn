"""整理同尺度离线图片、逐模型尺寸和原生纹理检查页。"""
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Art/Worldfall/biome-sizing-v1"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    data = read(OUT / "size-check.json")
    previews = read(OUT / "preview-check.json")
    assert len(previews["images"]) == 15
    preview_sizes = {row['asset']: row for row in previews['images']}
    width, height = 480, 505
    contact = Image.new("RGB", (width*5, height*3), (28, 37, 48))
    draw = ImageDraw.Draw(contact)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 20)
    cards = []
    table = []
    for index, group in enumerate(data["groups"]):
        row = group["representative"]
        pictured_ratio = preview_sizes[group['asset']]['tree_height'] / preview_sizes[group['asset']]['human_height']
        name = group["asset"] + ".png"
        image = Image.open(OUT / name)
        image.thumbnail((width-10, height-55))
        x, y = index%5*width, index//5*height
        contact.paste(image, (x+5, y+50))
        draw.text((x+10, y+5), f'{group["label"]} / 历史图约{pictured_ratio:.1f}倍', font=font, fill="white")
        plants = group["plant_examples"]
        plant_min = min(p["adult_ratio"] for p in plants)
        plant_max = max(p["adult_ratio"] for p in plants)
        table.append(f'<tr><td>{html.escape(group["label"])}</td><td>{row["frame"]}</td>'
                     f'<td>{row["previous_nominal_height"]:.2f}</td><td>{row["top_above_ground"]:.2f}</td>'
                     f'<td>{row["adult_ratio"]:.2f}倍</td><td>{plant_min:.2f}–{plant_max:.2f}倍</td></tr>')
        cards.append(f'<figure><a href="{name}"><img loading="lazy" src="{name}" alt="{html.escape(group["label"])}同尺度对照"></a>'
                     f'<figcaption>{html.escape(group["label"])}尺寸阶段实体参考：左为基础成年东人，中间为小植被，右为最大树类外观。'
                     f'图中约{pictured_ratio:.1f}倍身高，当前尺寸表约{row["adult_ratio"]:.1f}倍。</figcaption></figure>')
    contact.save(OUT / "overview.png")
    texture_link = '<a href="material-check.json">实际编译的原生纹理分配检查</a>' if (OUT / "material-check.json").exists() else '原生纹理分配检查正在整理'
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>群系植被：人物比例与原生纹理</title>
<style>body{margin:30px;background:#1c2530;color:#e9eef3;font:16px/1.7 "Microsoft YaHei",sans-serif}a{color:#8dc9ff}
table{border-collapse:collapse;width:100%}th,td{border:1px solid #53606e;padding:8px;text-align:left}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:18px}figure{margin:0;background:#2a3642;padding:10px}
img{display:block;width:100%}figcaption{padding-top:8px}p{max-width:1200px}</style><body>
<h1>群系植被：人物比例与原生纹理</h1>
<p>以基础成年东人0.9游戏单位为基准。2026-10-16橡树与黑暗森林的树木和花草分别按用户要求在当时尺寸上再乘2，包含残骸与近中远档。竹林、烛火、知识、稻田、橡树、黑暗森林累计为最初比例版的4倍，蕨类、血肉、巨灵为2倍。烛火树累计下移约树高6%，小型烛火略微下移；血肉树下移约自身高度3%。2026-10-17泰坦树类先下移3%，再追加6%，累计约自身高度9%，包含正常、残骸和三档，显示大小保持。幼树和人物仍保留各自高低差。</p>
<p>下方尺寸表为最新值；15幅图片是早期尺寸阶段的实体参考，共用相机和游戏单位，图片标题明确标出历史比例。橡树图仍为放大前7.2倍，当前14.4倍；黑暗森林图约7.1倍，当前埋根后约14.3倍。透明叶片实际外观请看最新叶簇检查页；归一模型图不代表游戏内大小。原生表面纹理未在这些参考图中模拟，游戏效果待用户查看。</p>
<p>树干用原生树皮2，明确的叶片用原生叶片1；花瓣、晶体、器官、骨骼、蜡和书册继续使用现有逐面原色。没有启用木板3、砖石4或布料7。尺寸和纹理在模型接入处生效，正式网格与原色保留。</p>
<p>''' + f'<a href="size-check.json">30类、909模型尺寸明细</a> · <a href="compiled-check.json">实际编译的尺寸与放置检查</a> · {texture_link} · <a href="../biome-native-v1/index.html">原图与全部外观检查页</a></p>'
    page += '<table><thead><tr><th>群系</th><th>代表外观</th><th>原生高度（未调整比例）</th><th>当前地面以上高</th><th>人物身高比</th><th>花草身高比</th></tr></thead><tbody>' + ''.join(table) + '</tbody></table>'
    page += '<p>表中为随机系数居中值；树木仍有约±10%、花草约±20%的个体差异。黑暗森林main_3与烛火树按0.12模型单位下沉，血肉树按0.06、泰坦树类按0.18模型单位下移，小型烛火额外下移0.01模型单位，地面以上高度已扣除此量。三档共用同一调整量。为了完整查看放大后的树木，15组图片统一采用更远的同一相机；人物实际身高没有改变。</p>'
    page += '<p><a href="oak-x2-2026-10-16/check.json">橡树乘2检查</a> · <a href="dark-x2-2026-10-16/check.json">黑暗森林乘2检查</a> · <a href="../biome-leaf-cards-v1/index.html">树类透明叶片</a> · <a href="../biome-flora-leaf-cards-v1/index.html">花草透明叶片</a> · <a href="../Oak_tree/leaf-cards-v4/index.html">橡树全外观与藤蔓（归一模型）</a></p>'
    page += '<div class="grid">' + ''.join(cards) + '</div></body></html>'
    (OUT / "index.html").write_text(page, encoding="utf-8")
    print(json.dumps(dict(images=15, page=str(OUT / "index.html")), ensure_ascii=False))


if __name__ == "__main__":
    main()
