"""汇总本轮真叶改透明贴片的进度与实际发布来源。"""
import json
import biome_native_publish as common

ROOT=common.ROOT
OUT=ROOT/'Art/Worldfall/biome-leaf-cards-v1'
TARGETS={
    'Bamboo_tree':('竹林','leaf-cards-v1','窄竹叶扇，保留全部竹竿和节'),
    'Fern_tree':('蕨类','leaf-cards-v3','叶簇适度加密，main_0补连续细茎；main_5保留多枝银杏梭形冠'),
    'Knowledge_tree':('知识','leaf-cards-v1','侧叶与绿芽；书册和藤杆为实体'),
    'Rice_tree':('稻田','leaf-cards-v1','只改稻叶；穗粒、秆和浅色根部保留'),
    'Nurgle_tree':('纳垢','leaf-cards-v1','垂冠及真叶片；器官和断干保留'),
    'Slaanesh_tree':('色孽','leaf-cards-v1','粉色冠簇；卷枝、立像、柱带保留'),
    'Oak_tree':('橡树','leaf-cards-v4','主干直径加粗25%，下垂部为细藤蔓；透明叶冠与当前游戏大小×2保持'),
    'Dark_tree':('黑暗森林','leaf-cards-v2','冷杉短针叶枝片；保留原树干与落地偏移'),
}


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    records=[]
    cards=[]
    for asset,(label,version,note) in TARGETS.items():
        out=ROOT/'Art/Worldfall'/asset/version
        path=out/'manifest.json'
        data=common.read(ROOT/'Content/Worldfall/Models'/asset/'models.json')
        row={'asset':asset,'label':label,'version':version,'models':len(data['Models']),
             'status':'制作中','note':note,'game_test':'pending_user_manual'}
        preview=None
        if path.exists():
            manifest=common.read(path)
            assert manifest['production_sha256']==common.digest(ROOT/manifest['production_resource'])
            actual_leaf_frames=sum(any(common.read(ROOT/path)['Levels'].values()) for path in manifest['leaf_cards_resources'])
            row.update(status='已离线实装',production_sha256=manifest['production_sha256'],
                       leaf_frames=actual_leaf_frames)
            preview=out.parent/manifest['representative_preview']
            focus={'Fern_tree':'main_5','Dark_tree':'main_3'}.get(asset)
            if focus:
                preview=out/'previews'/focus/'front.png'
            url=common.link(out/'index.html',OUT)
        elif asset=='Oak_tree':
            sample=out.parent/'leaf-cards-v2'
            row['status']='main_2已实装；其余外观制作中'
            preview=sample/'previews/cross-hybrid-near/front.png'
            url=common.link(sample/'index.html',OUT)
        else:
            url=common.link(out/'index.html',OUT) if (out/'index.html').is_file() else ''
        if preview:
            cards.append('<div class="card"><a href="'+url+'"><img src="'+common.link(preview,OUT)+'"><b>'+label+'</b></a><p>'+note+'</p></div>')
        if url:
            row['inspection_page']=url
        records.append(row)
    result={'date':'2026-10-17','assets':records,'target_assets':len(records),
            'published_assets':sum(r['status']=='已离线实装' for r in records),
            'covered_models':sum(r['models'] for r in records),'game_test':'pending_user_manual'}
    common.write(OUT/'progress.json',result)
    html='<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>群系透明叶片</title><style>'+common.CSS+'</style><body><h1>群系树类：透明叶片与叶簇</h1>'
    html+='<p>已完整实装 '+str(result['published_assets'])+' / '+str(len(records))+' 类；共覆盖 '+str(result['covered_models'])+' 个近中远模型，包含原样保留的无叶状态。真正叶片改PNG，书册、骨刺、器官和树干按原图保留实体。图片来自最终GLB独立重新导入；游戏效果待用户自行查看。</p>'
    html+='<p>2K透明来源保留；运行图为原生允许的512/256。共享图集已扩为6144²，单张图片仍不超过512。竖直双面交叉片从接近正上、正下看仍会收窄；各物体的仰俯视单独保留。</p>'
    html+='<p><a href="progress.json">逐项进度</a> · <a href="../biome-flora-leaf-cards-v1/index.html">花草的叶片、海藻与贝壳</a> · <a href="atlas-check.json">实际贴图容量</a> · <a href="atlas-integration-check.json">原生图集指令核对</a> · <a href="runtime-cleanup.json">过期运行图清理</a> · <a href="../biome-sizing-v1/index.html">原人物比例与尺寸</a> · <a href="../biome-native-v1/index.html">全部群系模型与原图</a></p><table><tr><th>群系</th><th>模型数</th><th>状态</th><th>分区</th></tr>'
    for row in records:
        title='<a href="'+row['inspection_page']+'">'+row['label']+'</a>' if row.get('inspection_page') else row['label']
        html+='<tr><td>'+title+'</td><td>'+str(row['models'])+'</td><td>'+row['status']+'</td><td>'+row['note']+'</td></tr>'
    html+='</table><div class="cards">'+''.join(cards)+'</div><p>恐虐的裸枝、骨刺与器物、奸奇的晶体与符号，以及烛火、墓园、珊瑚、血肉、巨灵的非叶物件保留各自实体，不额外添加树冠。</p></body></html>'
    (OUT/'index.html').write_text(html,encoding='utf-8')
    doc='# 透明叶片资产结果\n\n以正式文件和离线发布清单汇总；游戏效果仍待用户查看。\n\n| 群系 | 模型数 | 状态 | 分区 |\n| --- | ---: | --- | --- |\n'
    # 文档中的链接使用各资产目录，不复用相对网页的链接。
    for row in records:
        page=ROOT/'Art/Worldfall'/row['asset']/row['version']/'index.html'
        title='['+row['label']+']('+common.link(page,ROOT/'Doc/修仙贴图三维化/群系透明叶片')+')' if page.is_file() else row['label']
        doc+='| '+title+' | '+str(row['models'])+' | '+row['status']+' | '+row['note']+' |\n'
    flora=common.read(ROOT/'Art/Worldfall/biome-flora-leaf-cards-v1/progress.json')
    doc+='\n## 花草\n\n15类按真实材料检查，其中11类、102模型含透明叶片或薄花瓣；竹笋、火团、骨片和器官4类保留实体。游戏效果待用户查看。\n\n| 群系 | 模型数 | 状态 | 部位 |\n| --- | ---: | --- | --- |\n'
    for row in flora['assets']:
        title=row['label']
        if row.get('inspection_page'):
            page=(ROOT/'Art/Worldfall/biome-flora-leaf-cards-v1'/row['inspection_page']).resolve()
            title='['+title+']('+common.link(page,ROOT/'Doc/修仙贴图三维化/群系透明叶片')+')'
        doc+='| '+title+' | '+str(row['models'])+' | '+row['status']+' | '+row['note']+' |\n'
    (ROOT/'Doc/修仙贴图三维化/群系透明叶片/资产结果.md').write_text(doc,encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('published_assets','target_assets','covered_models')},ensure_ascii=False))


if __name__=='__main__':
    main()
