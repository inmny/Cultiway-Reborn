"""新的 Blender 进程实际导入群系 GLB，检查几何/颜色并用 CPU 渲染检查图。"""
import argparse
from collections import Counter
import hashlib
from itertools import permutations
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector, Matrix, kdtree
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
LEVELS={'near':'','middle':'_m','far':'_f'}
VIEWS={'front':((0,-1,0),(1,0,0),(0,0,1)), 'side':((1,0,0),(0,1,0),(0,0,1)),
       'back':((0,1,0),(-1,0,0),(0,0,1)), 'top':((0,0,1),(1,0,0),(0,1,0))}


def read(path):
    """读取本批模型和来源记录。"""
    return json.loads(path.read_text(encoding='utf-8-sig'))


def material_slot(material, names):
    """优先读导出的槽标签，再处理 Blender 自动追加的数字后缀。"""
    value=material.get('wf_slot')
    if value in names: return names.index(value)
    for index,name in enumerate(names):
        if material.name.endswith('.'+name) or ('.'+name+'.') in material.name: return index
    raise ValueError('未知材质槽：'+material.name)


def check_model(model, objects):
    """按面中心找真实对应三角，核对位置、颜色分区和法线。"""
    expected=[]
    for part in model['Parts']:
        pos=part['Pos']; nrm=part['Nrm']
        assert all(math.isfinite(v) for v in pos+nrm)
        assert len(pos)==len(nrm) and len(part['Tri'])%3==0
        for offset in range(0,len(nrm),3): assert abs(Vector(nrm[offset:offset+3]).length-1)<1e-4
        for face_index,offset in enumerate(range(0,len(part['Tri']),3)):
            indices=part['Tri'][offset:offset+3]
            assert all(0<=i<len(pos)//3 for i in indices)
            points=[Vector(pos[i*3:i*3+3]) for i in indices]
            expected.append((part['Slot'][face_index],points))
    tree=kdtree.KDTree(len(expected))
    for i,(_,points) in enumerate(expected): tree.insert(sum(points,Vector())/3,i)
    tree.balance(); used=set(); worst=0; slot_counts=Counter(); min_z=math.inf; highest=-math.inf
    vertex_count=0; corners=0
    for obj in objects:
        mesh=obj.data; mesh.calc_loop_triangles(); matrix=obj.matrix_world
        transform=matrix.to_3x3().inverted().transposed()
        slots=[material_slot(mat,model['SlotNames']) for mat in mesh.materials]
        for material,slot in zip(mesh.materials,slots):
            source=model['SlotColor'][slot]; rgb=[source&255,(source>>8)&255,(source>>16)&255]
            color=material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value
            expected_rgb=[v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in rgb]
            assert max(abs(color[i]-expected_rgb[i]) for i in range(3))<2e-5
        vertex_count+=len(mesh.vertices)
        for triangle in mesh.loop_triangles:
            points=[matrix@mesh.vertices[i].co for i in triangle.vertices]
            slot=slots[triangle.material_index]; center=sum(points,Vector())/3
            found=None
            for _,index,distance in tree.find_range(center,3e-6):
                if index in used or expected[index][0]!=slot: continue
                for order in permutations(expected[index][1]):
                    error=max(abs(p[i]-q[i]) for p,q in zip(points,order) for i in range(3))
                    if error<=1e-6: found=index; worst=max(worst,error); break
                if found is not None: break
            assert found is not None,(model['Name'],'GLB三角与正式候选不匹配')
            used.add(found); slot_counts[slot]+=1
            for p in points: min_z=min(min_z,p.z); highest=max(highest,p.z)
            for loop in triangle.loops:
                normal=transform@mesh.corner_normals[loop].vector
                assert all(math.isfinite(v) for v in normal) and abs(normal.length-1)<1e-4
                corners+=1
    assert len(used)==len(expected)
    assert abs(min_z-model['Min']['Z'])<=1e-6 and abs(highest-model['Max']['Z'])<=1e-6
    assert len(model['SlotNames'])<=64 and all(0<=slot<len(model['SlotNames']) for slot in slot_counts)
    return {'name':model['Name'],'triangles':len(expected),'vertices':vertex_count,'normal_corners':corners,
            'max_position_error':worst,'slot_counts':dict(slot_counts),'geometry_color_normals_pass':True}


def setup_scene():
    """采用少量样本、CPU和固定方向光，不占用游戏所需显卡计算。"""
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=8
    scene.render.threads_mode='FIXED'; scene.render.threads=4
    scene.view_settings.view_transform='Standard'; scene.view_settings.look='None'
    scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
    world=bpy.data.worlds.new('InspectionWorld'); world.use_nodes=True; scene.world=world
    world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.07,.085,.1,1)
    world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.8
    for i,(direction,energy) in enumerate([((-3,-4,6),2),((4,2,3),.65)]):
        light=bpy.data.lights.new('Studio'+str(i),'SUN'); light.energy=energy; light.angle=.3
        obj=bpy.data.objects.new(light.name,light); scene.collection.objects.link(obj)
        obj.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()
    camera=bpy.data.objects.new('InspectionCamera',bpy.data.cameras.new('InspectionCamera'))
    scene.collection.objects.link(camera); scene.camera=camera; camera.data.type='ORTHO'; camera.data.sensor_fit='VERTICAL'
    return scene,camera


def crop_grid(path, targets, columns, rows, tile):
    """从实际阵列渲染裁出各物体，保留像素而不是重新生成预览。"""
    source=bpy.data.images.load(str(path),check_existing=False)
    array=np.empty(len(source.pixels),dtype=np.float32); source.pixels.foreach_get(array)
    array=array.reshape(source.size[1],source.size[0],4)
    for index,target in enumerate(targets):
        row=index//columns; column=index%columns
        y=(rows-1-row)*tile; x=column*tile
        image=bpy.data.images.new('Crop',width=tile,height=tile,alpha=True,float_buffer=False)
        image.pixels.foreach_set(np.ascontiguousarray(array[y:y+tile,x:x+tile]).reshape(-1))
        target.parent.mkdir(parents=True,exist_ok=True); image.filepath_raw=str(target); image.file_format='PNG'; image.save()
        bpy.data.images.remove(image)
    bpy.data.images.remove(source)


def render_group(scene,camera,records,loaded,out,name,view='front',targets=None,reference=None):
    """每组最多十二格，采用同一幅导入几何和统一光照。"""
    columns=min(4,len(records)); rows=math.ceil(len(records)/columns); tile=240; cell=2.6
    direction,right,up=(Vector(v) for v in VIEWS[view])
    for data in loaded.values(): data['collection'].hide_render=True
    for index,record in enumerate(records):
        data=loaded[record['name']]; data['collection'].hide_render=False
        limits=reference.get(record['frame'],data) if reference else data
        span=max(limits['span']); fit=2.05/max(.01,span)
        column=index%columns; row=index//columns
        center=right*((column-(columns-1)/2)*cell)+up*(((rows-1)/2-row)*cell)
        data['wrapper'].scale=(fit,fit,fit); data['wrapper'].location=center-limits['center']*fit
    camera.location=direction*40
    camera.rotation_euler=Matrix((right,up,direction)).transposed().to_euler()
    camera.data.ortho_scale=rows*cell
    scene.render.resolution_x=columns*tile; scene.render.resolution_y=rows*tile; scene.render.resolution_percentage=100
    path=out/'previews/groups'/f'{name}.png'; path.parent.mkdir(parents=True,exist_ok=True)
    scene.render.filepath=str(path); bpy.ops.render.render(write_still=True)
    if targets: crop_grid(path,targets,columns,rows,tile)
    for data in loaded.values():
        data['wrapper'].location=(0,0,0); data['wrapper'].scale=(1,1,1)
    return path.relative_to(out).as_posix()


def main():
    """先逐个重导入检查，再从这些文件渲染四面和同相机三档。"""
    parser=argparse.ArgumentParser(); parser.add_argument('--asset',required=True); parser.add_argument('--dir',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    out=Path(args.dir); out=out if out.is_absolute() else ROOT/out
    source=out/'models.candidate.json'
    if not source.exists(): source=ROOT/'Content/Worldfall/Models'/args.asset/'models.json'
    document=read(source); strategy=read(out/'native-strategy.json'); models={m['Name']:m for m in document['Models']}
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    loaded={}; checks=[]
    for record in strategy['models']:
        before=set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=str(out/record['glb']))
        imported=[obj for obj in bpy.data.objects if obj not in before]; meshes=[obj for obj in imported if obj.type=='MESH']
        bpy.context.view_layer.update(); checks.append(check_model(models[record['name']],meshes))
        points=[obj.matrix_world@v.co for obj in meshes for v in obj.data.vertices]
        low=Vector(tuple(min(p[i] for p in points) for i in range(3))); high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
        collection=bpy.data.collections.new(record['name']); bpy.context.scene.collection.children.link(collection)
        wrapper=bpy.data.objects.new('FrameWrapper',None); collection.objects.link(wrapper)
        for obj in imported:
            for previous in list(obj.users_collection): previous.objects.unlink(obj)
            collection.objects.link(obj)
            if obj.parent not in imported:
                transform=obj.matrix_world.copy(); obj.parent=wrapper; obj.matrix_world=transform
        loaded[record['name']]={'collection':collection,'wrapper':wrapper,'center':(low+high)*.5,'span':high-low}
    scene,camera=setup_scene(); records=strategy['models']; near=[r for r in records if r['level']=='near']
    reference={r['frame']:loaded[r['name']] for r in near}; preview_groups=[]
    for start in range(0,len(near),8):
        group=near[start:start+8]
        for view in VIEWS:
            paths=[out/'previews'/r['frame']/(view+'.png') for r in group]
            path=render_group(scene,camera,group,loaded,out,f'views-{start//8:02d}-{view}',view,paths,reference)
            preview_groups.append({'frames':[r['frame'] for r in group],'view':view,'path':path})
    for start in range(0,len(near),4):
        frames=[r['frame'] for r in near[start:start+4]]
        group=[next(r for r in records if r['frame']==frame and r['level']==level) for frame in frames for level in LEVELS]
        targets=[out/'previews'/r['frame']/(r['level']+'.png') for r in group]
        path=render_group(scene,camera,group,loaded,out,f'lod-{start//4:02d}', 'front',targets,reference)
        preview_groups.append({'frames':frames,'view':'near_middle_far','path':path})
    result={'asset':args.asset,'fresh_blender_process':True,'all_glbs_actually_imported':True,'models':checks,
            'all_geometry_color_normals_pass':True,'candidate_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'preview_source':'本进程实际重导入的GLB；CPU8样本，所有状态四面和三档','preview_groups':preview_groups,
            'visual_inspection':'待对象代理实际查看并另写visual-inspection.json','game_test':'pending_user_manual'}
    (out/'reopen-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('BIOME_REOPEN_PASS',args.asset,len(checks))


if __name__=='__main__': main()
