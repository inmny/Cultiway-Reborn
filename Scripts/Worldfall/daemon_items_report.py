"""汇总四恶魔家族的专属手持物和实际髋腿姿态，不把离线回放当游戏实测。"""
import argparse
from datetime import datetime, timezone
import html
import re
from PIL import Image, ImageDraw
import native_unit_report as units

ROOT=units.ROOT
BATCH=ROOT/'Art/Worldfall/daemon-held-items-v1'
ASSETS=('GreatUncleanOneButcher','GreatUncleanOneBellRinger','GreatUncleanOneRainFather','KeeperSecrets',
    'KeeperSecretsNakari','ExaltedKeeperSecrets','LordChange','KairosFateweaver','ExaltedLordChange',
    'Bloodthirster','AnggrathUnbound','ExaltedBloodthirster')


def report(final=False,complete_views=False):
    legs=units.read(BATCH/'leg-render-check.json')
    sheets=[]
    for start in range(0,len(legs['groups']),4):
        rows=legs['groups'][start:start+4]
        sheet=Image.new('RGB',(1200,len(rows)*260),'#26323e')
        draw=ImageDraw.Draw(sheet)
        for row,item in enumerate(rows):
            label=units.NAMES[item['asset']]
            for col,(key,title) in enumerate((('native_front','原位置·正面'),('corrected_front','调整后·正面'),
                ('native_side','原位置·侧面'),('corrected_side','调整后·侧面'))):
                image=Image.open(BATCH/item['images'][key]).convert('RGBA')
                image.thumbnail((290,225),Image.Resampling.LANCZOS)
                sheet.paste(image,(col*300+(300-image.width)//2,row*260+30),image)
                draw.text((col*300+3,row*260+4),label+' '+title,font=units.FONT,fill='white')
        filename=f'contact-legs-{start//4+1:02}.png'
        sheet.save(BATCH/filename)
        sheets.append(filename)
    items=[]
    for short in ASSETS:
        asset='Cultiway.'+short
        folder=ROOT/'Art/Worldfall'/asset/'held-items-v1'
        manifest=units.read(folder/'manifest.json') if (folder/'manifest.json').is_file() else {}
        production=ROOT/'Content/Worldfall/Models'/asset/'models.json'
        published=bool(manifest.get('publication')=='published_offline' and manifest.get('production_sha256')==units.sha(production))
        items.append({'asset':asset,'name':units.NAMES[asset],'published':published,
            'sha256':manifest.get('production_sha256') if published else None})
    progress={'expected':len(items),'published':sum(x['published'] for x in items),'items':items,'game_test':'pending_user_manual'}
    units.write(BATCH/'progress.json',progress)
    full_sheets=[]
    if final or complete_views:
        loaded=units.read(BATCH/'loaded-reopen-check.json')
        assert loaded['passed'] and loaded['ImportedAllFinalGLBs'] and loaded['IndependentBlenderProcess']
        assert loaded['runtime_sha256']==units.sha(BATCH/'runtime-check.json') and len(loaded['files'])==60
        groups=loaded['groups']
        for start in range(0,len(items),4):
            sheet=Image.new('RGB',(1500,1040),'#26323e')
            draw=ImageDraw.Draw(sheet)
            for row,item in enumerate(items[start:start+4]):
                def picture(level,pose,view):
                    return next(g for g in groups if g['asset']==item['asset'] and g['level']==level and g['pose']==pose)['images'][view]
                tiles=((picture('near','idle','front'),'近景正面'),(picture('near','idle','side'),'近景侧面'),
                    (picture('near','idle','oblique'),'近景斜视'),(picture('middle','idle','oblique'),'中景'),
                    (picture('far','idle','oblique'),'远景'),(picture('near','attack','oblique'),'攻击姿态'))
                for col,(path,title) in enumerate(tiles):
                    image=Image.open(BATCH/path).convert('RGBA')
                    image.thumbnail((245,225),Image.Resampling.LANCZOS)
                    sheet.paste(image,(col*250+(250-image.width)//2,row*260+30),image)
                    draw.text((col*250+3,row*260+4),item['name']+' '+title,font=units.FONT,fill='white')
                folder=ROOT/'Art/Worldfall'/item['asset']/'held-items-v1'
                sheet.crop((0,row*260,1500,(row+1)*260)).save(folder/'runtime-contact.png')
                Image.open(BATCH/picture('near','idle','oblique')).save(folder/'runtime-thumbnail.png')
                page=(folder/'index.html').read_text(encoding='utf-8')
                page=re.sub(r'<!--runtime-final-->.*?<!--/runtime-final-->','',page,flags=re.S)
                current='<!--runtime-final--><section><h2>当前加载后的完整模型</h2><p>此图包含最新腿长、髋位和专属手持物；下方来源检查保留原始比例。</p><a href="../../daemon-held-items-v1/index.html"><img style="max-width:100%" src="runtime-contact.png"></a></section><!--/runtime-final-->'
                page=page.replace('<h1',current+'<h1',1)
                (folder/'index.html').write_text(page,encoding='utf-8')
            filename=f'contact-loaded-{start//4+1:02}.png'
            sheet.save(BATCH/filename)
            full_sheets.append(filename)
    cards=''.join(f'<section><h2>{html.escape(x["name"])}</h2><a href="../{x["asset"]}/held-items-v1/index.html"><img loading="lazy" src="../{x["asset"]}/held-items-v1/contact.png?v={x["sha256"][:12]}"></a></section>' for x in items if x['published'])
    pending='、'.join(x['name'] for x in items if not x['published'])
    leg_images=''.join(f'<img loading="lazy" src="{name}">' for name in sheets)
    page=f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>恶魔专属手持物与腿位</title><style>{units.CSS}</style><body><h1>恶魔专属手持物与腿位</h1><p>大不净者、守密者、万变魔君和嗜血狂魔各变体，按自己的像素贴图制作固定手持物，隐藏随机装备外观；已有的钟等原图物件完整复用。双腿回到躯干下方；除大不净者外，其余三类腿长增加约30%，上身和握物轴同步抬高，脚底仍落地。</p><p>专属手持物已发布 {progress["published"]}/{progress["expected"]}。'+(f'尚在制作：{html.escape(pending)}。' if pending else '全部三档已完成。')+'</p><p>以下均为离线检查，游戏中的最终显示待查看。各物体来源图保留原比例与真实持握图；腿位以这里最新的实际加载姿态对照为准。</p><p><a href="../creature-native-v1/index.html">全部生物</a> · <a href="runtime-check.json">实际姿态检查</a> · <a href="progress.json">逐项发布状态</a></p><h2>髋腿位置对照</h2><p>为清楚检查髋部，下面的图临时隐藏头、臂与背翼，只显示躯干与双腿；正式模型仍保留所有部件。脚底维持地面零高。</p>'+leg_images+'<h2>各变体的原图与专属持物</h2>'+cards+'</body></html>'
    if full_sheets:
        full='<h2>实际加载后的完整模型</h2><p>使用实际程序集拉长后的几何与姿态，导出后由新进程实导；下方包含当前腿长、持握与近中远三档。</p>'+''.join(f'<img loading="lazy" src="{name}">' for name in full_sheets)
        page=page.replace('<h2>髋腿位置对照</h2>',full+'<h2>髋腿位置对照</h2>')
    (BATCH/'index.html').write_text(page,encoding='utf-8')
    if final:
        runtime=units.read(BATCH/'runtime-check.json')
        palette=units.read(BATCH/'palette-check.json')
        review=units.read(BATCH/'root-review.json')
        assert progress['published']==12 and runtime['passed'] and runtime['required_fixed_items']
        assert runtime['models']==36 and runtime['pose_cases']==108
        assert palette['passed'] and len(palette['assets'])==12
        assert review['actual_image_view'] and review['reviewer']=='root' and set(review['viewed'])==set(sheets+full_sheets)
        units.write(BATCH/'final-check.json',{'passed':True,'checked_at_utc':datetime.now(timezone.utc).isoformat(),
            'published_assets':12,'models':36,'pose_cases':108,'palette_cases':palette['palette_cases'],
            'source_body_geometry_preserved':True,'runtime_hips_repositioned':True,
            'three_families_legs_lengthened':1.3,'great_unclean_one_leg_length_unchanged':True,
            'camera_cases':runtime['camera_cases'],'final_loaded_glbs_independently_reimported':len(loaded['files']),
            'game_test':'pending_user_manual','items':items})
    print({'published':progress['published'],'expected':12,'leg_sheets':sheets})


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--final',action='store_true')
    parser.add_argument('--prepare-final',action='store_true')
    args=parser.parse_args()
    report(args.final,args.prepare_final)
