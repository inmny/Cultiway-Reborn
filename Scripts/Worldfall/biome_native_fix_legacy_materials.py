"""修正两类旧群系模型的自然部分材质；仅改SlotKind，不重新生成网格。"""
import base64
import copy
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def write(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def byte_values(value):return list(base64.b64decode(value)) if isinstance(value,str) else list(value)


def amend(asset):
    production=ROOT/'Content/Worldfall/Models'/asset/'models.json'
    before=read(production);old_sha=sha(production);after=copy.deepcopy(before)
    changed=[]
    for model in after['Models']:
        kinds=byte_values(model['SlotKind'])
        for index,name in enumerate(model['SlotNames']):
            if kinds[index]!=4:continue
            color=model['SlotColor'][index]
            r,g,b=((color>>shift)&255 for shift in (0,8,16))
            natural_shell=asset=='Cultiway.FishPeopleTower' and name.startswith(('fixed_shell_','fixed_coral_','fixed_interior_'))
            natural_castle=asset=='Cultiway.BloodCastle' and name.startswith('jpeg_') and (
                (g>r*1.3 and g>b*.9) or (r>g*1.6 and r>b*1.5 and g<=45))
            if natural_shell or natural_castle:
                kinds[index]=0
                changed.append({'model':model['Name'],'slot':name,'index':index,'rgb':[r,g,b],'before':4,'after':0})
        model['SlotKind']=kinds
    for old,new in zip(before['Models'],after['Models']):
        assert {k:v for k,v in old.items() if k!='SlotKind'}=={k:v for k,v in new.items() if k!='SlotKind'}
    assert {k:v for k,v in before.items() if k!='Models'}=={k:v for k,v in after.items() if k!='Models'}
    if not changed:
        print('ALREADY_FIXED',asset);return None
    write(production,after);new_sha=sha(production)
    proof={'asset':asset,'date':'2026-10-14','old_production_sha256':old_sha,'production_sha256':new_sha,
           'change':'自然螺壳/珊瑚/洞口或树篱/鲜血色去掉建筑石材砖缝，4改0；墙体砖石保留',
           'changed_slots':changed,'models':len(after['Models']),'only_slot_kind_changed':True,
           'geometry_normals_rgb_flags_parts_pivots_dimensions_unchanged':True,
           'glb_blend_original_texture_unchanged':True,'no_new_import_or_render':'材质编号属于游戏专用字段，Blender不模拟程序砖缝，旧几何检查仍对应同一网格',
           'game_test':'pending_user_restart'}
    folder=ROOT/'Art/Worldfall'/asset
    write(folder/'material-kind-fix.json',proof)
    # 仅同步原正式资源的重复候选，不改不同版本的历史模型。
    for candidate in (folder/'candidate-models.json',folder/'tripo-v4/worldfall-v1/models.candidate.json'):
        if candidate.exists() and sha(candidate)==old_sha:write(candidate,after)
    rows=read(ROOT/'Doc/修仙贴图三维化/Blender模型清单.json')
    row=next(row for row in rows if row['Id']==asset)
    row.update({'RuntimeSha256':new_sha,'MaterialKindFix':'2026-10-14，仅自然表面4改0，几何原色不变',
                'MaterialGameTest':'pending_user_restart','MaterialFixRecord':'material-kind-fix.json'})
    write(ROOT/'Doc/修仙贴图三维化/Blender模型清单.json',rows)
    if asset=='Cultiway.FishPeopleTower':
        paths=[folder/'validation.json',folder/'trellis/publication.json']
    else:paths=[folder/'tripo-v4/worldfall-v1/conversion.json',folder/'tripo-v4/worldfall-v1/independent-check.json']
    for path in paths:
        record=read(path)
        record['original_geometry_checked_sha256']=old_sha
        record['metadata_only_amendment']='仅自然部分材质编号4改0；没有重新导入或渲染，原几何及颜色未改'
        record['production_sha256']=new_sha
        if 'candidate_json_sha256' in record:record['candidate_json_sha256']=new_sha
        if 'reexport_byte_identical' in record:record['reexport_byte_identical_before_material_amendment']=record.pop('reexport_byte_identical')
        record['material_fix_record']='material-kind-fix.json'
        record['material_game_test']='pending_user_restart'
        write(path,record)
    print('MATERIAL_FIXED',asset,len(changed),new_sha)
    return proof


if __name__=='__main__':
    proofs=[proof for asset in ('Cultiway.FishPeopleTower','Cultiway.BloodCastle') if (proof:=amend(asset))]
    if proofs:write(ROOT/'Art/Worldfall/biome-native-v1/previous-assets-kind-fix.json',proofs)
