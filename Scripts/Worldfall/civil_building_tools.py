"""文明建筑共用的格式、原图分组及检查工具；建筑形状由各物体脚本决定。"""
import base64
import copy
import hashlib
import json
import math
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from blender_worldfall import Asset, bytes_list, packed_color
from biome_native import Mesh, rgb, linear
import leaf_card_tools as inspect

ROOT = Path(__file__).resolve().parents[2]
LEVELS = {'near':'','middle':'_m','far':'_f'}


def sprite_frames(asset_id):
    """变体分别建模；同一条目的动画帧留作动画来源，不当成新房屋。"""
    if '.EasternHuman' in asset_id or '.Gui' in asset_id:
        species = 'EasternHuman' if '.EasternHuman' in asset_id else 'Gui'
        folder = ROOT/f'GameResources/buildings/civ_main/Cultiway.{species}'/asset_id
    else:
        folder = ROOT/'GameResources/buildings/mobs'/asset_id
    sources = [('base', folder)]
    sources.extend((p.name,p/asset_id) for p in sorted((ROOT/'GameResources/cultiway/building_skins').glob('*')) if (p/asset_id).is_dir())
    result = []
    for style,folder in sources:
        groups = {}
        for path in sorted(folder.glob('*.png')):
            match = re.fullmatch(r'(main|ruin|construction)_\s*(\d+)(?:\s*_(\d+))?',path.stem)
            if not match:
                continue
            state,index,animation = match.groups()
            groups.setdefault((state,int(index)),[]).append((int(animation or 0),path))
        for (state,index),choices in sorted(groups.items(),key=lambda x:({'main':0,'construction':1,'ruin':2}[x[0][0]],x[0][1])):
            choices.sort()
            result.append({'style':style,'state':'site' if state=='construction' else state,'frame':f'{state}_{index}',
                           'index':index,'path':choices[0][1],'animations':[p for _,p in choices]})
    return result


def roof(mesh, center, half, rise, slot='roof', trim='roof_dark', ridge_fraction=.52, upturn=.025, thickness=.018):
    """有闭合檐底的四坡中式屋顶；折线边缘上翘，避免从下看消失。"""
    x,y,z=center; w,d=half
    r=w*ridge_fraction
    top=[(x-w,y-d,z+upturn),(x+w,y-d,z+upturn),(x+w,y+d,z+upturn),(x-w,y+d,z+upturn),
         (x-r,y,z+rise),(x+r,y,z+rise)]
    # 四片坡面与檐底完全闭合，内点在几何内部用于定向。
    vertices=top+[(a,b,c-thickness) for a,b,c in top[:4]]
    faces=[(0,1,5,4),(1,2,5),(2,3,4,5),(3,0,4),(6,7,1,0),(7,8,2,1),(8,9,3,2),(9,6,0,3),(9,8,7,6)]
    mesh.solid(vertices,faces,[slot]*4+[trim]*5,interior=(x,y,z+rise*.25))
    if mesh.level!='far':
        mesh.tube((x-r,y,z+rise+.004),(x+r,y,z+rise+.004),.009,trim,sides=4)


def gable(mesh, center, half, rise, slot='roof', trim='roof_dark', thickness=.018):
    """实心双坡茅顶或瓦顶，前后和底面均封闭。"""
    x,y,z=center;w,d=half
    mesh.solid([(x-w,y-d,z),(x+w,y-d,z),(x+w,y+d,z),(x-w,y+d,z),(x-w,y,z+rise),(x+w,y,z+rise),
                (x-w,y-d,z-thickness),(x+w,y-d,z-thickness),(x+w,y+d,z-thickness),(x-w,y+d,z-thickness)],
               [(0,1,5,4),(2,3,4,5),(3,0,4),(1,2,5),(6,7,1,0),(7,8,2,1),(8,9,3,2),(9,6,0,3),(9,8,7,6)],
               [slot]*4+[trim]*5,interior=(x,y,z+rise*.2))


def beam(mesh, start, end, radius=.012, slot='timber'):
    """方截面梁柱连接两个真实端点。"""
    mesh.tube(start,end,radius,slot,tip_radius=radius,sides=4)


def building_body(mesh, center, half, wall='wall', timber='timber', door='door', windows='gold', columns=3):
    """木构墙体和独立门窗；对象脚本可用它组合独有的各座楼。"""
    x,y,z=center;w,d,h=half
    mesh.box(center,half,wall)
    front=y-d-.004
    mesh.box((x,front,z-h*.35),(min(.06,w*.2),.009,h*.65),door)
    if mesh.level!='far':
        for side in (-1,1):
            mesh.box((x+side*w*.62,front-.006,z+.015),(min(.04,w*.17),.006,h*.25),windows)
        for i in range(columns):
            px=x-w+(2*w*i/max(1,columns-1))
            mesh.box((px,front,z),(.009,.012,h),timber)
        mesh.box((x,front,z+h-.008),(w,.014,.01),timber)


class CivilAsset:
    """一个资产、一份可编辑文件、所有原图变体及三档状态。"""
    def __init__(self, asset_id, version='native-civ-v1', budgets=(3500,1800,700)):
        self.id=asset_id;self.out=ROOT/'Art/Worldfall'/asset_id/version
        self.out.mkdir(parents=True,exist_ok=True)
        self.asset=Asset(asset_id);self.seed=copy.deepcopy(self.asset.data)
        self.family=self.seed['Family'];self.width=self.seed['Width'];self.depth=self.seed['Depth'];self.ratio=self.depth/self.width
        self.frames=sprite_frames(asset_id);self.budgets=dict(zip(LEVELS,budgets));self.records=[];self.model_info={}
        bpy.context.preferences.filepaths.save_version=0
        for model in self.asset.models.values():
            for obj in list(model['collection'].objects):
                bpy.data.objects.remove(obj,do_unlink=True)
            bpy.data.collections.remove(model['collection'])
        self.asset.models={}

    def name(self, frame, level):
        """基础外观保留原名，文化与变体追加后缀，状态前缀沿用原生。"""
        name=self.family+('' if frame['style']=='base' else '_s_'+frame['style'])
        if frame['index'] and frame['state']!='site':
            name+='_v'+str(frame['index'])
        if frame['state']!='main':
            name=('site_' if frame['state']=='site' else 'ruin_')+name
        return name+LEVELS[level]

    def add(self, frame, level, parts, colors, kinds, kingdom_slots, name_override=None):
        """把本物体手工构形放回原部件，保留原槽前缀与动作轴。"""
        name=name_override or self.name(frame,level)
        if isinstance(parts,Mesh):parts={'base':parts}
        state_base=('site_' if frame['state']=='site' else 'ruin_' if frame['state']=='ruin' else '')+self.family
        source=next((copy.deepcopy(m) for m in self.seed['Models'] if m['Name']==name),None)
        if source is None:
            source=next((copy.deepcopy(m) for m in self.seed['Models'] if m['Name']==state_base+LEVELS[level]),None)
        if source is None:
            source=next((copy.deepcopy(m) for m in self.seed['Models'] if m['Name']==state_base),copy.deepcopy(self.seed['Models'][0]))
        source['Name']=name
        names=list(source['SlotNames']);base_colors=list(source['SlotColor']);base_kinds=bytes_list(source['SlotKind']);base_flags=bytes_list(source['SlotFlags'])
        for slot,color in colors.items():
            if slot not in names:
                names.append(slot);base_colors.append(packed_color(rgb(color)));base_kinds.append(kinds.get(slot,0));base_flags.append(0)
            else:
                index=names.index(slot);base_colors[index]=packed_color(rgb(color))
        assert len(names)<=64
        for slot,kind in kinds.items():
            base_kinds[names.index(slot)]=kind
        source.update(SlotNames=names,SlotColor=base_colors,SlotKind=base_kinds,SlotFlags=base_flags)
        source['Info']=dict(source.get('Info') or {})
        source['Info'].update(width='1',cultiway_building='1',cultiway_asset=self.id,cultiway_style=frame['style'],
                              cultiway_frame=frame['frame'],kingdom_slots=kingdom_slots)
        source['Info'].update(self.model_info)
        if frame['state']=='ruin':source['Info']['ruin']='1'
        else:source['Info'].pop('ruin',None)
        collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
        # Blender会截断过长的集合名；完整模型名另存，运行时名称保持原约定。
        collection['worldfall_model_name']=name
        material_list=[]
        for i,(slot,color) in enumerate(zip(names,base_colors)):
            mat=inspect.solid_material(name+'.'+slot,(color&255,(color>>8)&255,(color>>16)&255));mat['worldfall_slot']=i
            material_list.append(mat)
        ordered=copy.deepcopy(source['Parts']);known={p['Name'] for p in ordered}
        for part_name in parts:
            if part_name not in known:
                ordered.append({'Name':part_name,'Parent':0,'Pivot':{'X':0,'Y':0,'Z':0}})
        objects={}
        for order,part in enumerate(ordered):
            mesh_builder=parts.get(part['Name'],Mesh(level))
            pivot=Vector(tuple(part['Pivot'][a] for a in 'XYZ'))
            geometry=bpy.data.meshes.new(name+'.'+part['Name']);geometry.from_pydata([v-pivot for v in mesh_builder.vertices],[],mesh_builder.faces);geometry.update()
            obj=bpy.data.objects.new(part['Name'],geometry);collection.objects.link(obj);obj.location=pivot
            for mat in material_list:geometry.materials.append(mat)
            for poly,slot in zip(geometry.polygons,mesh_builder.slots):poly.material_index=names.index(slot)
            obj['wf_part']=part['Name'];obj['wf_parent']=part['Parent'];obj['wf_pivot']=list(pivot);obj['wf_order']=order
            objects[part['Name']]=obj
        bpy.context.view_layer.update()
        for obj in objects.values():
            if obj['wf_parent']>=0:
                matrix=obj.matrix_world.copy();obj.parent=list(objects.values())[obj['wf_parent']];obj.matrix_world=matrix
        bpy.context.view_layer.update()
        self.asset.models[name]={'source':source,'collection':collection,'parts':objects}
        count=sum(len(m.faces) for m in parts.values());assert count<=self.budgets[level],(name,count,self.budgets[level])
        self.records.append({'name':name,'frame':frame['frame'],'style':frame['style'],'state':frame['state'],'level':level,
            'triangles':count,'budget':self.budgets[level],'source':frame['path'].relative_to(ROOT).as_posix(),
            'animation_sources':[p.relative_to(ROOT).as_posix() for p in frame['animations']]})

    def build(self, draw, colors, kinds=None, kingdom_slots='none', notes='', assumptions=None, model_info=None):
        """draw(frame,level) 返回部件字典；配色和国家槽可按frame分别返回。"""
        self.model_info=model_info or {}
        for frame in self.frames:
            for level in LEVELS:
                self.add(frame,level,draw(frame,level),colors(frame) if callable(colors) else colors,kinds or {},
                         kingdom_slots(frame) if callable(kingdom_slots) else kingdom_slots)
        # 原生用 site_/ruin_ 加在当前完整变体名前；单一施工图也要覆盖全部完整变体。
        main_frames=[f for f in self.frames if f['state']=='main']
        for main in main_frames:
            for state in ('site','ruin'):
                choices=[f for f in self.frames if f['style']==main['style'] and f['state']==state]
                if not choices:continue
                frame=next((f for f in choices if f['index']==main['index']),choices[0])
                for level in LEVELS:
                    name=('site_' if state=='site' else 'ruin_')+self.name(main,level)
                    if name in self.asset.models:continue
                    self.add(frame,level,draw(frame,level),colors(frame) if callable(colors) else colors,kinds or {},
                             kingdom_slots(frame) if callable(kingdom_slots) else kingdom_slots,name)
                    self.records[-1]['alias_of']=self.name(frame,level)
        target=self.out/'models.candidate.json'
        self.asset.export(notes,output_path=target.relative_to(ROOT).as_posix())
        data=json.loads(target.read_text(encoding='utf-8-sig'))
        # GLB从最终落盘JSON重建；独立进程再导入核对逐三角面、朝向、槽号。
        for model in self.asset.models.values():model['collection'].hide_viewport=True
        (self.out/'glbs').mkdir(exist_ok=True)
        for record,model in zip(self.records,data['Models']):
            obj=inspect.mesh_object(model,model['Name']);path=self.out/'glbs'/(model['Name']+'.glb')
            inspect.export_glb([obj],path);bpy.data.objects.remove(obj,do_unlink=True)
            record['glb']=path.relative_to(self.out).as_posix()
        report={'asset':self.id,'method':'Blender自行构形；未调用三维生成平台','models':self.records,
            'notes':notes,'assumptions':assumptions or [],'game_test':'pending_user_manual',
            'candidate_sha256':hashlib.sha256(target.read_bytes()).hexdigest(),
            'animation_note':'三维为每条目固定造型；原图动画帧列表保留，运行时动画须另验'}
        (self.out/'strategy.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('CIVIL_BUILDING_EXPORTED',self.id,len(self.records))


def check_and_render(folder, render_frames=None):
    """在新的Blender进程打开唯一来源并实导全部最终GLB，再渲染代表方向图。"""
    folder=Path(folder);doc=json.loads((folder/'models.candidate.json').read_text(encoding='utf-8-sig'))
    strategy=json.loads((folder/'strategy.json').read_text(encoding='utf-8-sig'))
    bpy.ops.wm.open_mainfile(filepath=str(folder/'model.blend'))
    assert bpy.context.scene.get('asset_id')==doc['Id']
    expected_names={m['Name'] for m in doc['Models']}
    editable_count=sum(c.get('worldfall_model_name',c.name) in expected_names for c in bpy.data.collections)
    assert editable_count==len(doc['Models'])
    results=[]
    for model,record in zip(doc['Models'],strategy['models']):
        results.append(inspect.inspect_glb(folder/record['glb'],model,0))
    report={'IndependentBlenderProcess':True,'ReopenedEditableSource':True,'EditableCollections':editable_count,
            'ImportedAllFinalGLBs':True,'Models':results,'Passed':True,'GameTest':'pending_user_manual',
            'candidate_sha256':hashlib.sha256((folder/'models.candidate.json').read_bytes()).hexdigest()}
    (folder/'reopen-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # 每种基础/文化外观四面和所有状态、三档同相机，统一放置于同一尺度。
    representatives={}
    for record,model in zip(strategy['models'],doc['Models']):
        if record['level']=='near':representatives[(record['style'],record['frame'])]=record
    requested=set(render_frames or [])
    assert requested <= {style+'/'+frame for style,frame in representatives}
    for (style,frame),record in representatives.items():
        if requested and style+'/'+frame not in requested:continue
        state=record['state']
        model=next(m for m in doc['Models'] if m['Name']==record['name'])
        h=model['Max']['Z'];d=doc['Depth']/doc['Width'];target=(0,0,h*.48);span=max(1.12,d,h)*1.4
        views=[('front',(0,-3,h*.7),target,span),('side',(3,0,h*.7),target,span),
               ('back',(0,3,h*.7),target,span),('top',(1.7,-2.8,3),target,span)]
        if state!='main':views=[views[0],views[3]]
        destination=folder/'preview'/f'{style}-{frame}'
        inspect.render_glb(folder/record['glb'],destination,views)
        if state=='main':
            for level in ('middle','far'):
                sibling=next(r for r in strategy['models'] if r['style']==style and r['frame']==frame and r['level']==level)
                inspect.render_glb(folder/sibling['glb'],destination,[(level,views[3][1],target,span)])
    print('CIVIL_REOPEN_PASSED',doc['Id'],len(results))


if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:]
    if args[0]=='check':check_and_render(ROOT/args[1],args[2:])
