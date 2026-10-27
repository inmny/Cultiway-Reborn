"""群系自建模型共用的小几何工具、原图分组与 Worldfall 导出入口。"""
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Scripts/Worldfall"))
from blender_worldfall import Asset, packed_color

LEVELS = {"near": "", "middle": "_m", "far": "_f"}


def frames(asset_id):
    """按外观编号分组；第二个数字是动画帧，不把动画或小地图当新物种。"""
    grouped = {}
    for path in sorted((ROOT / "GameResources/buildings" / asset_id).glob("*.png")):
        match = re.fullmatch(r"(main|ruin)_\s*(\d+)(?:_(\d+))?", path.stem)
        if match:
            state, index, animation = match.groups()
            grouped.setdefault((state, int(index)), []).append((int(animation or 0), animation is not None, path))
    result = []
    for (state, index), choices in sorted(grouped.items(), key=lambda item: (item[0][0] == "ruin", item[0][1])):
        choices.sort(key=lambda value: (value[0], value[1]))
        result.append({"frame": f"{state}_{index}", "state": state, "index": index,
                       "path": choices[0][2], "animations": [row[2] for row in choices]})
    return result


def rgb(value):
    """支持原图十六进制色或三个整数颜色通道。"""
    if isinstance(value, str):
        value = value.lstrip("#")
        return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))
    return tuple(int(c) for c in value[:3])


def linear(value):
    """原图字节色转换成 Blender 灯光使用的线性通道。"""
    value /= 255
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


class Mesh:
    """单个部件的立体几何；颜色用槽名记录，不提前依赖槽号。"""
    def __init__(self, level="near"):
        self.level = level
        self.vertices = []
        self.faces = []
        self.slots = []
        self.features = {}

    def solid(self, vertices, faces, slot, feature=None, interior=None, orient=True):
        """追加实体并向外定向；自定义凹面可明确关闭自动定向。"""
        points = [Vector(point) for point in vertices]
        center = Vector(interior) if interior is not None else sum(points, Vector()) / len(points)
        start = len(self.vertices)
        self.vertices.extend(points)
        if feature:
            if isinstance(feature, str): feature = {"name": feature, "kind": "detail"}
            self.features[feature["name"]] = feature
        for face_index, source in enumerate(faces):
            face = list(source)
            normal = (points[face[1]] - points[face[0]]).cross(points[face[2]] - points[face[0]])
            face_center = sum((points[i] for i in face), Vector()) / len(face)
            if orient and normal.dot(face_center - center) < 0: face.reverse()
            color = slot[face_index] if isinstance(slot, (list, tuple)) else slot
            for i in range(1, len(face) - 1):
                triangle = (face[0], face[i], face[i + 1])
                area = (points[triangle[1]] - points[triangle[0]]).cross(points[triangle[2]] - points[triangle[0]]).length
                if area <= 1e-10: raise ValueError("零面积面：" + str(feature))
                self.faces.append(tuple(start + j for j in triangle))
                self.slots.append(color)

    def tube(self, start, end, radius, slot, tip_radius=None, sides=None, feature=None, root=False, cap=True):
        """圆截面枝干；贴地根杆底环为水平面，不生成外露尖脚。"""
        sides = sides or {"near": 8, "middle": 6, "far": 4}[self.level]
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        helper = Vector((1, 0, 0)) if abs(axis.x) < .8 else Vector((0, 1, 0))
        u = axis.cross(helper).normalized(); v = axis.cross(u).normalized()
        tip_radius = radius * .75 if tip_radius is None else tip_radius
        rr = max(1e-5, tip_radius)
        vertices = []
        for ring, (center, r) in enumerate(((a, radius), (b, rr))):
            ru, rv = u, v
            if root and ring == 0:
                ru = Vector((u.x, u.y, 0))
                if ru.length < 1e-5: ru = Vector((1, 0, 0))
                ru.normalize(); rv = Vector((0, 0, 1)).cross(ru)
            vertices.extend(center + r * (ru * math.cos(i * math.tau / sides) + rv * math.sin(i * math.tau / sides)) for i in range(sides))
        faces = [(i, (i + 1) % sides, sides + (i + 1) % sides, sides + i) for i in range(sides)]
        if cap:
            faces.extend((tuple(reversed(range(sides))), tuple(range(sides, 2 * sides))))
        self.solid(vertices, faces, slot, feature, (a + b) * .5)

    def box(self, center, half, slot, feature=None):
        """方形台、石块和书册的基础体，不替代本物体的识别造型。"""
        c = Vector(center); h = Vector(half)
        vv = [c + Vector((x*h.x, y*h.y, z*h.z)) for x,y,z in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))]
        ff = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
        self.solid(vv, ff, slot, feature, c)

    def blob(self, center, radii, slot, sides=None, rings=None, feature=None, phase=0):
        """有完整背面的分段椭球；适合叶簇、肉质团和骨关节。"""
        sides = sides or {"near": 12, "middle": 8, "far": 6}[self.level]
        rings = rings or {"near": 5, "middle": 3, "far": 2}[self.level]
        c = Vector(center); r = Vector(radii)
        vv = [c + Vector((0,0,-r.z))]
        for j in range(1, rings + 1):
            lat = -math.pi/2 + math.pi*j/(rings+1)
            for i in range(sides):
                t = math.tau*i/sides + phase
                vv.append(c + Vector((r.x*math.cos(lat)*math.cos(t),r.y*math.cos(lat)*math.sin(t),r.z*math.sin(lat))))
        vv.append(c + Vector((0,0,r.z))); top = len(vv)-1
        ff = [(0,1+i,1+(i+1)%sides) for i in range(sides)]
        for j in range(rings-1):
            a=1+j*sides; b=a+sides
            ff.extend((a+i,a+(i+1)%sides,b+(i+1)%sides,b+i) for i in range(sides))
        ff.extend((top,1+(rings-1)*sides+i,1+(rings-1)*sides+(i+1)%sides) for i in range(sides))
        self.solid(vv, ff, slot, feature, c)

    def leaf(self, base, tip, width, thickness, slot, feature=None, twist=0):
        """带厚度和中脊的完整叶片；三档保持轮廓和前后展开。"""
        a, b = Vector(base), Vector(tip); axis=(b-a).normalized()
        n=Vector((0,-1,0)); n=n-axis*n.dot(axis)
        if n.length < 1e-5: n=Vector((1,0,0))-axis*axis.x
        n.normalize(); n=n*math.cos(twist)+axis.cross(n)*math.sin(twist)
        u=axis.cross(n).normalized(); center=a+(b-a)*.48
        vv=[a,center+u*width*.5,b,center-u*width*.5,center+n*thickness*.5,center-n*thickness*.5]
        ff=[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(1,0,5),(2,1,5),(3,2,5),(0,3,5)]
        self.solid(vv,ff,slot,feature,center)

    def lathe(self, center, profile, slot, sides=None, feature=None):
        """旋转剖面生成蜡杆、菌帽或圆台；端点可为单个尖端。"""
        sides=sides or {"near":12,"middle":8,"far":6}[self.level]
        c=Vector(center); vv=[]; rows=[]
        for radius,z in profile:
            if radius <= 1e-8:
                rows.append([len(vv)]); vv.append(c+Vector((0,0,z)))
            else:
                rows.append(list(range(len(vv),len(vv)+sides)))
                vv.extend(c+Vector((radius*math.cos(i*math.tau/sides),radius*math.sin(i*math.tau/sides),z)) for i in range(sides))
        ff=[]
        for a,b in zip(rows,rows[1:]):
            if len(a)==1: ff.extend((a[0],b[i],b[(i+1)%sides]) for i in range(sides))
            elif len(b)==1: ff.extend((a[i],b[0],a[(i+1)%sides]) for i in range(sides))
            else: ff.extend((a[i],b[i],b[(i+1)%sides],a[(i+1)%sides]) for i in range(sides))
        if len(rows[0])>1: ff.append(tuple(reversed(rows[0])))
        if len(rows[-1])>1: ff.append(tuple(rows[-1]))
        self.solid(vv,ff,slot,feature)

    def merge(self, other):
        """将同部件的枝叶合并，保留识别特征标签。"""
        offset=len(self.vertices); self.vertices.extend(other.vertices)
        self.faces.extend(tuple(i+offset for i in face) for face in other.faces)
        self.slots.extend(other.slots); self.features.update(other.features)


class NativeAsset:
    """每个资产独立建模，共用文件格式与检查约定。"""
    def __init__(self, asset_id, output_dir, tree=True, base_variant=0, wind=True):
        bpy.context.preferences.filepaths.save_version=0
        self.asset_id=asset_id; self.out=Path(output_dir); self.out.mkdir(parents=True,exist_ok=True)
        self.tree=tree; self.base_variant=base_variant; self.wind=wind
        self.family="building_cultiway_"+asset_id
        self.asset=Asset(asset_id)
        for model in list(self.asset.models.values()):
            for obj in list(model['collection'].objects): bpy.data.objects.remove(obj,do_unlink=True)
            bpy.data.collections.remove(model['collection'])
        self.asset.models={}; self.records=[]
        self.asset.data['Family']=self.family

    def add(self, frame, level, parts, colors, kinds=None, flags=None, info=None, budget=5000, normalize_height=None):
        """一幅原图的一档实体；可传单 Mesh 或有父子与轴的部件字典。"""
        if isinstance(parts,Mesh): parts={"tree" if self.tree else "plant":parts}
        specs={name:({"mesh":value} if isinstance(value,Mesh) else value) for name,value in parts.items()}
        all_points=[v for spec in specs.values() for v in spec['mesh'].vertices]
        min_z=min(v.z for v in all_points); max_z=max(v.z for v in all_points)
        height=normalize_height if normalize_height is not None else (2.0 if self.tree else max_z-min_z)
        scale=height/(max_z-min_z)
        shift=Vector((0,0,min_z))
        name=self.family+("" if frame['index']==self.base_variant else "_v"+str(frame['index']))+LEVELS[level]
        if frame['state']=="ruin": name="ruin_"+name
        slots=list(colors); assert len(slots)<=64
        kinds=kinds or {}; flags=flags or {}
        materials=[]
        for slot in slots:
            data=bpy.data.materials.new(name+'.'+slot); data.use_nodes=True
            rgba=tuple(linear(c) for c in rgb(colors[slot]))+(1,)
            data.diffuse_color=rgba
            shader=data.node_tree.nodes.get('Principled BSDF'); shader.inputs['Base Color'].default_value=rgba
            shader.inputs['Roughness'].default_value=.82
            if flags.get(slot,0)&1:
                shader.inputs['Emission Color'].default_value=rgba; shader.inputs['Emission Strength'].default_value=1
            data['source_rgb']=list(rgb(colors[slot])); data['wf_slot']=slot
            materials.append(data)
        collection=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(collection)
        objects={}; names=list(specs); features={}
        for order,(part_name,spec) in enumerate(specs.items()):
            builder=spec['mesh']; points=[(v-shift)*scale for v in builder.vertices]
            pivot=(Vector(spec.get('pivot',(0,0,0)))-shift)*scale
            mesh=bpy.data.meshes.new(name+'.'+part_name); mesh.from_pydata([v-pivot for v in points],[],builder.faces); mesh.update()
            obj=bpy.data.objects.new(part_name,mesh); collection.objects.link(obj); obj.location=pivot
            for material in materials: mesh.materials.append(material)
            for poly,slot in zip(mesh.polygons,builder.slots):
                poly.material_index=slots.index(slot); poly.use_smooth=bool(spec.get('smooth',False))
            parent=spec.get('parent',-1 if order==0 else 0)
            if isinstance(parent,str): parent=names.index(parent)
            obj['wf_part']=part_name; obj['wf_parent']=parent; obj['wf_pivot']=list(pivot); obj['wf_order']=order
            objects[part_name]=obj; features.update(builder.features)
        bpy.context.view_layer.update()
        for obj in objects.values():
            if obj['wf_parent']>=0:
                transform=obj.matrix_world.copy(); obj.parent=list(objects.values())[obj['wf_parent']]; obj.matrix_world=transform
        bpy.context.view_layer.update()
        metadata={'kind':'tree' if self.tree else 'plant','palette':'fixed','height':str(round(height,6)),
                  'cultiway_tree' if self.tree else 'cultiway_plant':'1','cultiway_asset':self.asset_id,
                  'cultiway_frame':frame['frame'],'cultiway_wind':'1' if self.tree and self.wind and frame['state']=='main' else '0',
                  'kingdom_slots':'none'}
        if info: metadata.update({key:str(value) for key,value in info.items()})
        source={'Name':name,'SlotNames':slots,'SlotKind':[kinds.get(slot,0) for slot in slots],
                'SlotFlags':[flags.get(slot,0) for slot in slots],'SlotColor':[packed_color(rgb(colors[slot])) for slot in slots],'Info':metadata}
        self.asset.models[name]={'source':source,'collection':collection,'parts':objects}
        count=sum(len(spec['mesh'].faces) for spec in specs.values()); assert count<=budget,(name,count,budget)
        glb=self.out/'glbs'/f"{frame['frame']}{LEVELS[level]}.glb"; glb.parent.mkdir(exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects.values(): obj.select_set(True)
        bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,export_extras=True,export_normals=True,export_materials='EXPORT',export_yup=True)
        bpy.ops.object.select_all(action='DESELECT')
        self.records.append({'name':name,'frame':frame['frame'],'level':level,'triangles':count,'budget':budget,
                             'features':list(features.values()),'feature_counts':dict(Counter(f.get('kind','detail') for f in features.values())),
                             'glb':glb.relative_to(self.out).as_posix(),'source':frame['path'].relative_to(ROOT).as_posix()})

    def finish(self, notes, assumptions=None):
        """保存候选和唯一可编辑来源，正式资源由主代理统一发布。"""
        path=self.out/'models.candidate.json'
        self.asset.export(notes,output_path=path.relative_to(ROOT).as_posix())
        data={'asset':self.asset_id,'tree':self.tree,'base_variant':self.base_variant,'method':'Blender自行构形；未使用三维生成平台',
              'notes':notes,'assumptions':assumptions or [],'models':self.records,'game_test':'pending_user_manual',
              'candidate_sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
        (self.out/'native-strategy.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('NATIVE_BIOME_EXPORTED',self.asset_id,len(self.records))
