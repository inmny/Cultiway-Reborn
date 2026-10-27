"""在 Blender 中编辑并导出 Worldfall 的分部件网格；坐标 X 横向、Y 前后、Z 向上，正面为 -Y。"""
import bpy
import bmesh
import json
import math
import base64
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]

def bytes_list(value):
    return list(base64.b64decode(value)) if isinstance(value, str) else list(value)

def vec(value):
    if isinstance(value, dict):
        return Vector((value['X'], value['Y'], value['Z']))
    return Vector(value)

def packed_color(rgb):
    return 0xFF000000 | int(rgb[0]) | (int(rgb[1]) << 8) | (int(rgb[2]) << 16)

class Asset:
    """一个资产对应一个独立 .blend，收藏集对应近景、中景、远景和状态。"""
    def __init__(self, asset_id):
        self.data = json.loads((ROOT/'Art/Worldfall/Seeds'/f'{asset_id}.json').read_text(encoding='utf-8-sig'))
        self.id = asset_id
        self.folder = ROOT/'Art/Worldfall'/asset_id
        self.folder.mkdir(parents=True, exist_ok=True)
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        for collection in list(bpy.data.collections):
            if collection.name != 'Collection':
                bpy.data.collections.remove(collection)
        self.models = {}
        for source in self.data['Models']:
            collection = bpy.data.collections.new(source['Name'])
            bpy.context.scene.collection.children.link(collection)
            self.models[source['Name']] = {'source': source, 'collection': collection, 'parts': {}}
            materials = []
            for i, slot in enumerate(source['SlotNames']):
                color = source['SlotColor'][i]
                material_name = f"{source['Name']}.{slot}"
                material = bpy.data.materials.get(material_name) or bpy.data.materials.new(material_name)
                material.diffuse_color = ((color&255)/255, ((color>>8)&255)/255, ((color>>16)&255)/255, 1)
                material.use_nodes = True
                shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
                shader.inputs['Base Color'].default_value = material.diffuse_color
                shader.inputs['Roughness'].default_value = .78
                materials.append(material)
            for i, part in enumerate(source['Parts']):
                positions = part['Pos']
                vertices = [positions[j:j+3] for j in range(0, len(positions), 3)]
                faces = [part['Tri'][j:j+3] for j in range(0, len(part['Tri']), 3)]
                mesh = bpy.data.meshes.new(f"{source['Name']}.{part['Name']}")
                pivot = vec(part['Pivot'])
                mesh.from_pydata([Vector(p)-pivot for p in vertices], [], faces)
                mesh.update()
                obj = bpy.data.objects.new(part['Name'], mesh)
                obj.location = pivot
                obj['wf_part'] = part['Name']
                obj['wf_parent'] = part['Parent']
                obj['wf_pivot'] = list(pivot)
                obj['wf_order'] = i
                for material in materials:
                    mesh.materials.append(material)
                for polygon, slot in zip(mesh.polygons, bytes_list(part['Slot'])):
                    polygon.material_index = slot
                    polygon.use_smooth = False
                collection.objects.link(obj)
                self.models[source['Name']]['parts'][part['Name']] = obj
            # 用 Blender 的父子关系保留动作轴；模型导出仍使用同一套部件名称。
            bpy.context.view_layer.update()
            parts = list(self.models[source['Name']]['parts'].values())
            for obj in parts:
                index = obj['wf_parent']
                if index >= 0:
                    transform = obj.matrix_world.copy()
                    obj.parent = parts[index]
                    obj.matrix_world = transform
                    bpy.context.view_layer.update()
        bpy.context.view_layer.update()
        self.show(self.data['Family'])

    def select_models(self, levels=('near','middle'), states=False, variants=True):
        for name, model in self.models.items():
            if name.startswith(('site_', 'ruin_')):
                if states: yield name, model
                continue
            if not variants and name not in (self.data['Family'], self.data['Family']+'_m', self.data['Family']+'_f'): continue
            level = 'middle' if name.endswith('_m') else 'far' if name.endswith('_f') else 'near'
            if level in levels: yield name, model

    def show(self, name, viewport=False):
        for model_name, model in self.models.items():
            visible = model_name == name
            model['collection'].hide_render = not visible
            model['collection'].hide_viewport = not visible if viewport else False

    def slot(self, model, slot):
        return model['source']['SlotNames'].index(slot)

    def set_color(self, slot, rgb, states=False):
        for name, model in self.models.items():
            if name.startswith(('site_', 'ruin_')) and not states: continue
            if slot in model['source']['SlotNames']:
                index = self.slot(model, slot)
                model['source']['SlotColor'][index] = packed_color(rgb)
                material = bpy.data.materials.get(f'{name}.{slot}')
                if material:
                    material.diffuse_color = tuple(c/255 for c in rgb)+(1,)
                    next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED').inputs['Base Color'].default_value = material.diffuse_color

    def transform_part(self, part, scale=(1,1,1), offset=(0,0,0), levels=('near','middle','far'), variants=True):
        for name, model in self.select_models(levels, variants=variants):
            obj = model['parts'].get(part)
            if not obj: continue
            pivot = Vector(obj['wf_pivot'])
            matrix = obj.matrix_world.copy()
            inverse = matrix.inverted()
            for vertex in obj.data.vertices:
                point = matrix @ vertex.co
                point = Vector(((point.x-pivot.x)*scale[0], (point.y-pivot.y)*scale[1], (point.z-pivot.z)*scale[2])) + pivot + Vector(offset)
                vertex.co = inverse @ point
            obj.data.update()

    def _add(self, model, part, parent, center, pivot, slot, vertices, faces, suffix):
        if part in model['parts']:
            parent_obj = model['parts'][part]
            target_part = part
            parent_index = parent_obj['wf_parent']
            order = parent_obj['wf_order']
            pivot = Vector(parent_obj['wf_pivot'])
        else:
            parent_obj = model['parts'].get(parent)
            parent_index = parent_obj['wf_order'] if parent_obj else -1
            order = max((p['wf_order'] for p in model['parts'].values()), default=-1)+1
            pivot = Vector(pivot if pivot is not None else center)
            target_part = part
        mesh = bpy.data.meshes.new(f'{part}.{suffix}')
        mesh.from_pydata([Vector(v)-pivot for v in vertices], [], faces)
        mesh.update()
        obj = bpy.data.objects.new(f'{part}.{suffix}', mesh)
        obj.location = pivot
        obj['wf_part'] = target_part
        obj['wf_parent'] = parent_index
        obj['wf_pivot'] = list(pivot)
        obj['wf_order'] = order
        for material in (parent_obj.data.materials if parent_obj else list(model['parts'].values())[0].data.materials): mesh.materials.append(material)
        for polygon in mesh.polygons: polygon.material_index = self.slot(model, slot)
        model['collection'].objects.link(obj)
        bpy.context.view_layer.update()
        if parent_obj:
            transform = obj.matrix_world.copy()
            obj.parent = parent_obj
            obj.matrix_world = transform
        if part not in model['parts']: model['parts'][part] = obj
        bpy.context.view_layer.update()
        return obj

    def box(self, part, center, half, slot, parent=None, pivot=None, levels=('near','middle'), variants=True):
        c = Vector(center); x,y,z = half
        vertices = [c+Vector((sx*x,sy*y,sz*z)) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        faces = [(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]
        for name, model in self.select_models(levels, variants=variants):
            self._add(model, part, parent, c, pivot, slot, vertices, faces, 'box')

    def ellipsoid(self, part, center, radii, slot, parent=None, pivot=None, sides=8, rings=4, levels=('near','middle'), variants=True):
        c = Vector(center); vertices=[]
        for ring in range(rings+1):
            latitude = math.pi*(ring/rings-.5)
            for side in range(sides):
                angle = 2*math.pi*side/sides
                vertices.append(c+Vector((radii[0]*math.cos(latitude)*math.cos(angle), radii[1]*math.cos(latitude)*math.sin(angle), radii[2]*math.sin(latitude))))
        faces=[]
        for ring in range(rings):
            for side in range(sides):
                a=ring*sides+side; b=ring*sides+(side+1)%sides
                faces.append((a,b,b+sides,a+sides))
        for name, model in self.select_models(levels, variants=variants):
            self._add(model, part, parent, c, pivot, slot, vertices, faces, 'ellipsoid')

    def tube(self, part, start, end, radius, slot, parent=None, pivot=None, tip_radius=None, sides=6, levels=('near','middle'), variants=True):
        a,b=Vector(start),Vector(end); direction=(b-a).normalized()
        ref=Vector((0,0,1)) if abs(direction.z)<.9 else Vector((1,0,0))
        u=direction.cross(ref).normalized(); v=direction.cross(u).normalized()
        vertices=[]
        for point,r in ((a,radius),(b,tip_radius if tip_radius is not None else radius)):
            for i in range(sides): vertices.append(point+(u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides))*r)
        faces=[tuple(reversed(range(sides))), tuple(range(sides,sides*2))]
        faces += [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
        for name, model in self.select_models(levels, variants=variants):
            self._add(model, part, parent, a, pivot, slot, vertices, faces, 'tube')

    def surface(self, part, vertices, faces, slot, parent=None, pivot=(0,0,0), levels=('near','middle'), variants=True):
        for name, model in self.select_models(levels, variants=variants):
            self._add(model, part, parent, (0,0,0), pivot, slot, vertices, faces, 'surface')

    def export(self, notes, output_path=None):
        """导出模型；指定候选路径时，模型和检查记录也保存到候选目录。"""
        for model in self.models.values(): model['collection'].hide_viewport = False
        bpy.context.view_layer.update()
        models=[]; stats=[]
        for name, model in self.models.items():
            groups={}
            for obj in model['collection'].objects:
                if obj.type!='MESH' or 'wf_part' not in obj: continue
                groups.setdefault(obj['wf_part'],[]).append(obj)
            ordered=sorted(groups.items(), key=lambda item:item[1][0]['wf_order'])
            order_map={objects[0]['wf_order']:index for index,(part,objects) in enumerate(ordered)}
            parts=[]; all_vertices=[]
            for part_name, objects in ordered:
                first=objects[0]; pos=[]; normal=[]; triangles=[]; slots=[]; axes=[]
                for obj in objects:
                    mesh=obj.data; mesh.calc_loop_triangles(); matrix=obj.matrix_world
                    for tri in mesh.loop_triangles:
                        points=[matrix@mesh.vertices[i].co for i in tri.vertices]
                        n=(points[1]-points[0]).cross(points[2]-points[0])
                        if n.length < 1e-10: continue
                        n.normalize(); base=len(pos)//3
                        for point in points: pos.extend(round(x,6) for x in point); normal.extend(round(x,6) for x in n); all_vertices.append(point)
                        triangles.extend([base,base+1,base+2]); slots.append(tri.material_index)
                        axes.append(max(range(3),key=lambda i:abs(n[i])))
                parts.append({'Name':part_name, 'Parent':order_map.get(first['wf_parent'],-1), 'Pivot':dict(zip(('X','Y','Z'),first['wf_pivot'])), 'Pos':pos,'Nrm':normal,'Tri':triangles,'Slot':slots,'Axis':axes})
            source=model['source']
            result={key:source[key] for key in ('Name','SlotNames','SlotColor','Info')}
            result['SlotKind']=bytes_list(source['SlotKind']); result['SlotFlags']=bytes_list(source['SlotFlags'])
            if len(result['SlotNames']) > 64 or len(result['SlotColor']) > 64:
                raise ValueError(f'{name} 颜色槽超过 Worldfall 的 64 槽上限')
            result['Parts']=parts
            result['Min']={key:round(min(p[i] for p in all_vertices),6) for i,key in enumerate(('X','Y','Z'))}
            result['Max']={key:round(max(p[i] for p in all_vertices),6) for i,key in enumerate(('X','Y','Z'))}
            # 校验动作父子关系、取色槽和有效三角面。
            for index,part in enumerate(parts):
                assert -1<=part['Parent']<index, (name,part['Name'],'parent')
                assert all(0<=slot<len(result['SlotNames']) for slot in part['Slot']), (name,part['Name'],'slot')
            count=sum(len(part['Tri'])//3 for part in parts)
            models.append(result); stats.append({'name':name,'parts':len(parts),'triangles':count,'min':result['Min'],'max':result['Max']})
        document={key:value for key,value in self.data.items() if key!='Models'}
        document.update({'Format':1,'Authoring':'Blender 4.4','Models':models})
        target=ROOT/output_path if output_path is not None else ROOT/'Content/Worldfall/Models'/self.id/'models.json'
        if output_path is not None:
            self.folder=target.parent
        target.parent.mkdir(parents=True,exist_ok=True)
        temporary=target.with_suffix('.json.tmp')
        temporary.write_text(json.dumps(document,separators=(',',':'),ensure_ascii=False),encoding='utf-8')
        temporary.replace(target)
        self.show(self.data['Family'], viewport=True)
        bpy.context.scene['asset_id']=self.id
        bpy.context.scene['worldfall_front']='-Y'
        bpy.context.scene['worldfall_contract']='parts+pivots+materials+LOD+states'
        bpy.context.scene['model_notes']=notes
        bpy.ops.wm.save_as_mainfile(filepath=str(self.folder/'model.blend'),compress=True)
        (self.folder/'validation.json').write_text(json.dumps({'asset':self.id,'authoring':'Blender 4.4','notes':notes,'models':stats},ensure_ascii=False,indent=2),encoding='utf-8')
        print('WORLDFALL_EXPORTED',self.id,len(models),sum(s['triangles'] for s in stats))

    def preview(self):
        self.show(self.data['Family'], viewport=True)
        objects=[o for o in self.models[self.data['Family']]['collection'].objects if o.type=='MESH']
        points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
        low=Vector(tuple(min(p[i] for p in points) for i in range(3))); high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
        center=(low+high)*.5; span=max(high-low)
        scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE_NEXT'
        scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
        scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
        scene.world.color=(.15,.15,.15)
        scene.view_settings.view_transform='Standard'
        for direction, energy in [((-3,-4,6),850),((4,-1,3),450),((0,4,4),500)]:
            data=bpy.data.lights.new('Studio','AREA'); data.energy=energy; data.shape='DISK'; data.size=span*3
            obj=bpy.data.objects.new('Studio',data); scene.collection.objects.link(obj)
            obj.location=center+Vector(direction)*span
            obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
        data=bpy.data.cameras.new('Preview'); camera=bpy.data.objects.new('Preview',data); scene.collection.objects.link(camera); scene.camera=camera
        data.type='ORTHO'; data.ortho_scale=span*1.42
        camera.location=center+Vector((2.5,-4.5,2.4))*span
        camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(self.folder/'preview.png'); bpy.ops.render.render(write_still=True)
        # 演示用灯光和相机不参与游戏导出。
        bpy.ops.wm.save_as_mainfile(filepath=str(self.folder/'model.blend'),compress=True)
