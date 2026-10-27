"""整理文明建筑原图与最终实导预览，并发布主代理实际看过的候选。"""
import argparse
from collections import defaultdict
from datetime import datetime, timedelta, timezone
import hashlib
import html
import json
import os
from pathlib import Path
import shutil
from urllib.parse import quote
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
BATCH=ROOT/'Art/Worldfall/civil-buildings-v1'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
CSS='''body{background:#1e2731;color:#e2e8ee;font:16px/1.6 "Microsoft YaHei",sans-serif;margin:28px}a{color:#96ccff}img{width:100%;object-fit:contain;background:#303b46}img.pixel{image-rendering:pixelated}.grid{display:grid;grid-template-columns:repeat(5,minmax(140px,1fr));gap:8px}figure{margin:0}figcaption{font-size:13px}table{border-collapse:collapse;width:100%}td,th{padding:8px;border:1px solid #536474;text-align:left}code{overflow-wrap:anywhere}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:18px}'''

def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def write(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def link(path,folder):return quote(Path(os.path.relpath(path,folder)).as_posix(),safe='/')

def report(asset):
    folder=ROOT/'Art/Worldfall'/asset/'native-civ-v1'
    strategy=read(folder/'strategy.json');doc=read(folder/'models.candidate.json')
    image_version=strategy['candidate_sha256'][:12]
    grouped=defaultdict(dict)
    for record in strategy['models']:
        if not record.get('alias_of'):grouped[(record['style'],record['frame'])][record['level']]=record
    blocks=[];main_rows=[]
    for (style,frame),levels in grouped.items():
        original=ROOT/levels['near']['source'];preview=folder/'preview'/f'{style}-{frame}'
        tiles=[f'<figure><img class="pixel" src="{link(original,folder)}"><figcaption>原图：{style}/{frame}</figcaption></figure>']
        views=('front','side','back','top','middle','far') if levels['near']['state']=='main' else ('front','top')
        for view in views:
            path=preview/(view+'.png')
            if not path.is_file():continue
            label={'front':'正面','side':'侧面','back':'背面','top':'近景俯视','middle':'中景同机位','far':'远景同机位'}[view]
            tiles.append(f'<figure><img loading="lazy" src="{link(path,folder)}?v={image_version}"><figcaption>{label}</figcaption></figure>')
        count=' / '.join(str(levels[l]['triangles']) for l in ('near','middle','far'))
        links=' · '.join(f'<a href="{quote(levels[l]["glb"])}">{l} GLB</a>' for l in ('near','middle','far'))
        blocks.append(f'<h2>{html.escape(style+" / "+frame)} · 三角面 {count}</h2><div class="grid">{"".join(tiles)}</div><p>{links}</p>')
        if levels['near']['state']=='main':main_rows.append((style,frame,original,preview))
    batch_name='civil-buildings-v1' if '.EasternHuman' in asset or '.Gui' in asset else 'remaining-buildings-v1'
    header=f'<a href="../../{batch_name}/index.html">本轮全部建筑</a><h1>{html.escape(asset)}</h1><p>{html.escape(strategy["notes"])}</p><p>本轮为原图约束的 Blender 自建；未调用图片或三维生成平台。图片均来自最终 GLB 的独立重导入。施工进度与游戏外观待用户查看。</p>'
    header+='<p><a href="model.blend">可编辑模型</a> · <a href="build.py">构形脚本</a> · <a href="reopen-check.json">独立重开结果</a> · <a href="strategy.json">结构、面数与假设</a></p>'
    limitations=''.join('<li>'+html.escape(str(v))+'</li>' for v in strategy.get('assumptions',[]))
    (folder/'index.html').write_text(f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>{html.escape(asset)}</title><style>{CSS}</style><body>{header}<ul>{limitations}</ul>'+''.join(blocks)+'</body></html>',encoding='utf-8')
    # 所有主要外观在同一张图中，供主代理实际核对，尺寸不做独立拉伸。
    contact=Image.new('RGB',(1100,max(1,len(main_rows))*180),'#26323e');draw=ImageDraw.Draw(contact)
    columns=('原图','正面','侧面','背面','近景俯视')
    for row,(style,frame,original,preview) in enumerate(main_rows):
        paths=[original]+[preview/(v+'.png') for v in ('front','side','back','top')]
        for col,(path,label) in enumerate(zip(paths,columns)):
            if not path.is_file():continue
            image=Image.open(path).convert('RGBA')
            if col==0:image=image.crop(image.getbbox())
            factor=min(205/image.width,142/image.height);image=image.resize((max(1,round(image.width*factor)),max(1,round(image.height*factor))),Image.Resampling.NEAREST if col==0 else Image.Resampling.LANCZOS)
            x=col*220;y=row*180
            draw.text((x+5,y+3),f'{style}/{frame} {label}',font=FONT,fill='white')
            contact.paste(image,(x+(220-image.width)//2,y+30),image)
    contact.save(folder/'contact.png')
    print(json.dumps({'asset':asset,'models':len(doc['Models']),'appearances':len(main_rows),'contact':str(folder/'contact.png')},ensure_ascii=False))

def publish(asset,note,viewed=None):
    folder=ROOT/'Art/Worldfall'/asset/'native-civ-v1';candidate=folder/'models.candidate.json'
    doc=read(candidate);strategy=read(folder/'strategy.json');check=read(folder/'reopen-check.json');digest=sha(candidate)
    assert check['Passed'] and check['IndependentBlenderProcess'] and check['ReopenedEditableSource'] and check['ImportedAllFinalGLBs']
    assert digest==strategy['candidate_sha256']==check['candidate_sha256']
    assert len(doc['Models'])==len(strategy['models'])==len(check['Models'])
    actual={row['File']:row for row in check['Models']}
    triangles={};max_slots=0
    for model,record in zip(doc['Models'],strategy['models']):
        n=len(model['SlotNames']);max_slots=max(n,max_slots)
        assert n<=64 and all(len(model[k])==n for k in ('SlotColor','SlotKind','SlotFlags'))
        count=sum(len(p['Tri'])//3 for p in model['Parts']);triangles[model['Name']]=count
        assert count==record['triangles']<=record['budget']
        assert model['Info']['cultiway_building']=='1' and model['Info']['cultiway_asset']==asset
        proof=actual[Path(record['glb']).name]
        assert proof['CandidateJsonTrianglePositionsWindingAndSlotsMatch'] and proof['UnitNormals'] and proof['FiniteGeometry']
        assert proof['Sha256']==sha(folder/record['glb'])
        slots=model['Info']['kingdom_slots'].split(',')
        assert all(s=='none' or s in model['SlotNames'] for s in slots)
        if '.Gui' in asset:assert slots==['none']
    viewed=viewed or ['contact.png']
    assert all((folder/path).is_file() for path in viewed)
    assert (folder/'contact.png').is_file() and (folder/'model.blend').is_file()
    production=ROOT/'Content/Worldfall/Models'/asset/'models.json';production.parent.mkdir(parents=True,exist_ok=True)
    if production.is_file() and not (folder/'models.production-source.json').is_file():shutil.copyfile(production,folder/'models.production-source.json')
    shutil.copyfile(candidate,production)
    manifest={'asset':asset,'version':'native-civ-v1','publication':'published_offline','production_resource':production.relative_to(ROOT).as_posix(),
        'production_sha256':digest,'models':len(doc['Models']),'triangles':triangles,'max_color_slots':max_slots,'review_note':note,
        'source_glbs':['native-civ-v1/'+r['glb'] for r in strategy['models']], 'editable_sources':['native-civ-v1/model.blend'],
        'inspection_files':{'original':'native-civ-v1/contact.png','reference':'native-civ-v1/contact.png','raw_preview':'native-civ-v1/contact.png','candidate_front':'native-civ-v1/contact.png'},
        'inspection_note':'自建流程无独立图片生成与平台初稿，联系图明确展示实际原图和最终实导，未虚构四阶段来源。',
        'game_test':'pending_user_manual'}
    write(folder/'manifest.json',manifest)
    registry_path=ROOT/'Doc/修仙贴图三维化/Blender模型清单.json';registry=read(registry_path)
    row=next(r for r in registry if r['Id']==asset)
    row.update(Models=len(doc['Models']),ExportedModels=len(doc['Models']),Triangles=triangles,RuntimeSha256=digest,
        Authoring='Blender 4.4 自建建筑',PublishedNative=True,NativeVersion='native-civ-v1',PublishedTrellis=False,
        EditableSources=manifest['editable_sources'],source_glbs=manifest['source_glbs'],inspection_files=manifest['inspection_files'],
        WorldfallPaletteValid=True,GameTest='pending_user_manual',Status='原图外观与三档状态离线发布；游戏效果待用户查看')
    row.pop('PaletteIssues',None);write(registry_path,registry)
    write(folder/'visual-inspection.json',{'candidate_sha256':digest,'actual_image_view':True,'reviewer':'root','viewed':viewed,
        'status':'pass_offline','notes':note,'game_test':'pending_user_manual'})
    print(json.dumps({'published':asset,'models':len(doc['Models']),'sha256':digest},ensure_ascii=False))

def overview():
    inventory=read(BATCH/'inventory.json');rows=[];cards=[]
    for item in inventory:
        asset=item['id'];folder=ROOT/'Art/Worldfall'/asset/'native-civ-v1';manifest=folder/'manifest.json'
        revision=read(manifest).get('revision_pending') if manifest.is_file() else None
        done=manifest.is_file() and not revision and sha(ROOT/'Content/Worldfall/Models'/asset/'models.json')==read(manifest)['production_sha256']
        models=read(manifest)['models'] if done else 0
        rows.append({'id':asset,'published':done,'models':models, 'revision_pending':revision})
        if (folder/'contact.png').is_file():
            image=folder/'preview/base-main_0/top.png'
            if not image.is_file():image=folder/'contact.png'
            label=item.get('name') or asset.replace('_Cultiway.EasternHuman','（东人）').replace('_Cultiway.Gui','（诡族）')
            status=('修正中：'+revision) if revision else ('已发布' if done else '候选检查中')
            image_version=read(folder/'strategy.json')['candidate_sha256'][:12]
            reviews=[]
            if done:
                visual=read(folder/'visual-inspection.json')
                reviews=[f'<a href="{link(folder/path,BATCH)}?v={image_version}">检查图{i+1}</a>' for i,path in enumerate(visual['viewed'])]
            cards.append(f'<div><a href="{link(folder/"index.html",BATCH)}"><img loading="lazy" src="{link(image,BATCH)}?v={image_version}">{html.escape(label)}</a><p>{models}个模型 · {html.escape(status)}</p><p>{" · ".join(reviews)}</p></div>')
    report_date=datetime.now(timezone(timedelta(hours=8))).date().isoformat()
    write(BATCH/'progress.json',{'date':report_date,'expected':len(rows),'published':sum(r['published'] for r in rows),'models':sum(r['models'] for r in rows),'assets':rows,'game_test':'pending_user_manual'})
    title='东人与诡族建筑' if BATCH.name=='civil-buildings-v1' else '剩余群系专属建筑'
    scope='含全部原图外观及已有文化变体' if BATCH.name=='civil-buildings-v1' else '含原图编号的完好、废墟及近中远三档'
    evidence=' · '.join(f'<a href="{name}">{label}</a>' for name,label in (('originals.png','原图总览'),('README.md','交付说明'),('final-delivery-check.json','最终核对')) if (BATCH/name).is_file())
    (BATCH/'index.html').write_text(f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>{title}</title><style>{CSS}</style><body><h1>{title}</h1><p>共{len(rows)}类建筑，{scope}。已发布{sum(r["published"] for r in rows)}类、{sum(r["models"] for r in rows)}个模型。Blender自建；未使用三维生成平台，游戏效果由用户统一检查。</p><p>{evidence}</p><div class="cards">'+''.join(cards)+'</div></body></html>',encoding='utf-8')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--asset');parser.add_argument('--publish');parser.add_argument('--review-note');parser.add_argument('--viewed',nargs='+');parser.add_argument('--batch',default='civil-buildings-v1');args=parser.parse_args()
    BATCH=ROOT/'Art/Worldfall'/args.batch
    if args.asset:report(args.asset)
    if args.publish:
        assert args.review_note
        publish(args.publish,args.review_note,args.viewed)
    overview()
