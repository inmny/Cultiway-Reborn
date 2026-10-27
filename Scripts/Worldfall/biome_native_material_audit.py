"""统计群系正式资源与当前候选中实际用到的材质，忽略未使用颜色槽。"""
import base64
from collections import Counter
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BIOMES=['Bamboo','Candle','Cemetery','Coral','Dark','Fern','FleshBlood','Knowledge','Oak','Rice',
        'Titans','Nurgle','Khorne','Tzeentch','Slaanesh']
NAMES={0:'Plain纯色',1:'Leaves树叶',2:'Bark树皮',3:'Wood木材',4:'Stone建筑砖石',5:'Roof瓦顶',
       6:'Thatch草顶',7:'Cloth布料',8:'Skin皮肤',9:'Hair头发',10:'Metal金属',11:'Fire火焰',
       12:'Glass玻璃',13:'Plaster灰泥',14:'Rock自然石',15:'Eyes眼睛',16:'Ancient古石',
       17:'Obsidian黑曜石',18:'Plates板片',19:'Marble大理石'}


def byte_values(value):
    return list(base64.b64decode(value)) if isinstance(value,str) else value


def inspect(path,asset,stage):
    data=json.loads(path.read_text(encoding='utf-8-sig'))
    totals=Counter(); suspicious=[]
    for model in data['Models']:
        kinds=byte_values(model['SlotKind']);used=Counter()
        for part in model['Parts']:used.update(byte_values(part['Slot']))
        for slot,count in used.items():
            kind=kinds[slot];totals[kind]+=count
            if kind==4:
                suspicious.append({'model':model['Name'],'slot':model['SlotNames'][slot],
                                   'kind':kind,'triangles':count})
    return {'asset':asset,'stage':stage,'path':path.relative_to(ROOT).as_posix(),
            'models':len(data['Models']),'used_material_triangle_counts':{NAMES[k]:n for k,n in sorted(totals.items())},
            'used_stone_4_slots':suspicious,'used_stone_4_triangles':totals[4]}


if __name__=='__main__':
    results=[];missing=[]
    for biome in BIOMES:
        for asset in (biome+'_tree',biome):
            production=ROOT/'Content/Worldfall/Models'/asset/'models.json'
            if production.exists():results.append(inspect(production,asset,'正式'))
            else:missing.append(asset)
            candidate=ROOT/'Art/Worldfall'/asset/'native-biome-v1/models.candidate.json'
            if candidate.exists():results.append(inspect(candidate,asset,'候选'))
    for asset in ('Cultiway.BloodCastle','Cultiway.FishPeopleTower'):
        path=ROOT/'Content/Worldfall/Models'/asset/'models.json'
        if path.exists():results.append(inspect(path,asset,'已实装群系建筑'))
    output={'scope':'30类群系树木花草的实际正式/候选资源及两类已实装群系建筑；未制作的基础显示另查源代码',
            'worldfall_mesh_kinds':NAMES,'assets':results,'no_published_resource':missing,
            'game_test':'用户提供的珊瑚截图证实Stone=4有砖纹；其余是字段检查，不能代替实际游戏看图'}
    out=ROOT/'Art/Worldfall/biome-native-v1/material-audit.json'
    out.write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for row in results:
        print(row['stage'],row['asset'],row['used_material_triangle_counts'],'Stone4=',row['used_stone_4_triangles'])
    print('无独立正式资源、仍使用基础显示',','.join(missing))
