"""清点两种文明的原图、占地与现有模型，制作原图检查表。"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Art/Worldfall/civil-buildings-v1'
OUT.mkdir(parents=True, exist_ok=True)
font_path = Path('C:/Windows/Fonts/msyh.ttc')
font = ImageFont.truetype(str(font_path), 14)
rows = []
for species in ('EasternHuman', 'Gui'):
    folder = ROOT / f'GameResources/buildings/civ_main/Cultiway.{species}'
    for asset_dir in sorted(folder.iterdir()):
        if not asset_dir.is_dir():
            continue
        asset = asset_dir.name
        seed_path = ROOT / 'Art/Worldfall/Seeds' / (asset + '.json')
        seed = json.loads(seed_path.read_text(encoding='utf-8-sig'))
        runtime = ROOT / 'Content/Worldfall/Models' / asset / 'models.json'
        main = sorted(asset_dir.glob('main*.png'))
        other = sorted(p for p in asset_dir.glob('*.png') if p not in main)
        styles = []
        for style in (ROOT/'GameResources/cultiway/building_skins').glob('*'):
            if (style/asset).is_dir():
                styles.append({'style':style.name,'images':[str(p.relative_to(ROOT)).replace('\\','/') for p in sorted((style/asset).glob('*.png'))]})
        row = {'id':asset,'species':species,'width':seed['Width'],'depth':seed['Depth'],
               'family':seed['Family'],'seed_models':[m['Name'] for m in seed['Models']],
               'originals':[str(p.relative_to(ROOT)).replace('\\','/') for p in main],
               'other_originals':[str(p.relative_to(ROOT)).replace('\\','/') for p in other],
               'styles':styles,'runtime_exists':runtime.is_file(),
               'runtime_sha256':hashlib.sha256(runtime.read_bytes()).hexdigest() if runtime.is_file() else None}
        rows.append(row)
    selected = [r for r in rows if r['species']==species]
    sheet = Image.new('RGB',(1250,((len(selected)+4)//5)*230),(30,34,40))
    d = ImageDraw.Draw(sheet)
    for i,row in enumerate(selected):
        x=(i%5)*250;y=(i//5)*230
        image = Image.open(ROOT/row['originals'][0]).convert('RGBA')
        image=image.crop(image.getbbox())
        factor=min(224/image.width,160/image.height)
        image=image.resize((round(image.width*factor),round(image.height*factor)),Image.Resampling.NEAREST)
        sheet.paste(image,(x+(250-image.width)//2,y+8),image)
        label=row['id'].replace('_Cultiway.'+species,'')
        d.text((x+8,y+174),label,font=font,fill='white')
        d.text((x+8,y+194),f"{row['width']}×{row['depth']} | {len(row['originals'])}原图 | {len(row['styles'])}风格",font=font,fill=(174,195,211))
    sheet.save(OUT/f'originals-{species}.png')
(OUT/'inventory.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'assets':len(rows),'main_images':sum(len(r['originals']) for r in rows),'style_images':sum(len(s['images']) for r in rows for s in r['styles']),'existing_exports':sum(r['runtime_exists'] for r in rows)},ensure_ascii=False))
