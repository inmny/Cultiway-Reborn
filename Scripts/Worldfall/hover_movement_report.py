"""整理水面背包与月亮悬浮的实际姿态回放。"""
import html
from PIL import Image, ImageDraw
import native_unit_report as units

BATCH=units.ROOT/'Art/Worldfall/water-float-v1'
runtime=units.read(BATCH/'runtime-check.json')
render=units.read(BATCH/'render-check.json')
assert runtime['passed'] and render['passed']
sheet=Image.new('RGB',(1500,1250),'#26323e')
draw=ImageDraw.Draw(sheet)
sections=[]
for row,record in enumerate(render['groups']):
    label=units.NAMES[record['asset']]
    tiles=[]
    for col,(view,caption) in enumerate((('front','斜前'),('back','斜后'),('side','侧面'),('middle','中景同尺度'),('far','远景同尺度'))):
        path=record['images'][view]
        image=Image.open(BATCH/path).convert('RGBA')
        image.thumbnail((290,215),Image.Resampling.LANCZOS)
        sheet.paste(image,(col*300+(300-image.width)//2,row*250+30),image)
        draw.text((col*300+4,row*250+4),label+' · '+caption,font=units.FONT,fill='white')
        tiles.append(f'<figure><img loading="lazy" src="{path}"><figcaption>{caption}</figcaption></figure>')
    sections.append(f'<h2>{html.escape(label)}</h2><div class="grid">'+''.join(tiles)+'</div>')
sheet.save(BATCH/'contact.png')
page=f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>水面喷气与月亮悬浮</title><style>{units.CSS}</style><body><h1>水面喷气与月亮悬浮</h1><p>阿斯塔特、帝皇禁卫和静默修女在水面使用背包双喷口悬空，岸上保持步行；玉蟾与月兔移动时平稳漂浮，不再迈腿。悬浮高度参与附身相机，隐藏自身身体时一并隐藏自己的尾焰。</p><p>以下为正式模型和实际程序集姿态的离线回放，水面或地面均为零高度。20种水陆停走状态、15个距离模型两相检查通过；实际游戏效果待查看。</p><p><a href="../creature-native-v1/index.html">全部生物</a> · <a href="runtime-check.json">状态检查</a> · <a href="render-check.json">离地与渲染检查</a></p>'+''.join(sections)+'</body></html>'
(BATCH/'index.html').write_text(page,encoding='utf-8')
print({'passed':True,'groups':len(render['groups'])})
