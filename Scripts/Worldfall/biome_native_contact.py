"""将原图与真正重导入的七种预览排成联系图，方便逐状态人工查看。"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
VIEWS = [('front','正面'),('side','侧面'),('back','背面'),('top','俯视'),
         ('near','近景'),('middle','中景'),('far','远景')]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--asset',required=True)
    parser.add_argument('--dir')
    args=parser.parse_args()
    out=ROOT/(args.dir or f'Art/Worldfall/{args.asset}/native-biome-v1')
    strategy=json.loads((out/'native-strategy.json').read_text(encoding='utf-8'))
    near=[r for r in strategy['models'] if r['level']=='near']
    records={(r['frame'],r['level']):r for r in strategy['models']}
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)
    pages=[]; folder=out/'contact-pages';folder.mkdir(exist_ok=True)
    for start in range(0,len(near),4):
        group=near[start:start+4]
        sheet=Image.new('RGB',(1920,60+290*len(group)),'#25313b')
        draw=ImageDraw.Draw(sheet)
        draw.text((10,12),f'{args.asset} · 原图、实际重导入四面与同相机三档',font=font,fill='#e7ecf1')
        for row,record in enumerate(group):
            frame=record['frame'];y=60+row*290
            original=Image.open(ROOT/record['source']).convert('RGBA')
            scale=min(210/original.width,220/original.height)
            original=original.resize((max(1,int(original.width*scale)),max(1,int(original.height*scale))),Image.Resampling.NEAREST)
            sheet.paste(original,((240-original.width)//2,y+33+(220-original.height)//2),original)
            draw.text((8,y),frame+' 原图',font=small,fill='#e7ecf1')
            for column,(view,label) in enumerate(VIEWS,1):
                path=out/'previews'/frame/(view+'.png')
                preview=Image.open(path).convert('RGB')
                assert preview.size==(240,240),(path,preview.size)
                sheet.paste(preview,(column*240,y+30))
                draw.text((column*240+8,y),label,font=small,fill='#e7ecf1')
                if view in ('near','middle','far'):
                    draw.text((column*240+8,y+270),str(records[(frame,view)]['triangles'])+' 三角面',font=small,fill='#c9d3dc')
        path=folder/f'{start//4:02d}.png';sheet.save(path)
        pages.append({'frames':[r['frame'] for r in group],'path':path.relative_to(out).as_posix()})
    (out/'contact-pages.json').write_text(json.dumps(pages,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('CONTACT_PAGES',args.asset,len(pages))


if __name__=='__main__':main()
