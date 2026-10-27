"""生物共享导出、真实GLB重导和原生骨架姿态检查；不包含物体造型。"""
import copy
import hashlib
import json
import math
import struct
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Matrix, Vector
from blender_worldfall import Asset
import leaf_card_tools as inspect

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    """读本物体已有记录。"""
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, data):
    """写可直接检查的记录。"""
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def digest(path):
    """将实导证据绑定到最终文件。"""
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def level_of(name):
    """沿用实际模型族的距离后缀。"""
    return 'middle' if name.endswith('_m') else 'far' if name.endswith('_f') else 'near'


def variant_of(name, family):
    """保留普通男女与男女战士的独立模型族，三档后缀不算外观。"""
    base = name[:-2] if name.endswith(('_m','_f')) else name
    return base.removeprefix(family).lstrip('_') or 'base'


def leaf_recipe(folder):
    """有真叶的树人用与普通树相同的原生透明叶簇，实体数据另存。"""
    path = Path(folder)/'leaf-cards.json'
    return read(path) if path.is_file() else None


def leaf_objects(model, recipe, level, folder, cards=None):
    """只按本物体配方放置透明交叉片，不在共享工具里设计树形。"""
    cards = copy.deepcopy(cards if cards is not None else recipe['Levels'][level.title()])
    names = {part['Name'] for part in model['Parts']}
    for card in cards:
        assert card['Part'] in names
        card.setdefault('Kind', 'Cross')
        card.setdefault('Alpha', 0)
        card.setdefault('PivotX', .5)
        card['Name'] = model['Name']+'.'+card['Name']
    materials = {key:inspect.card_material(folder, key, recipe) for key in {c['Image'] for c in cards}}
    objects = inspect.card_objects(cards, materials)
    for obj in objects:
        for material in obj.data.materials:
            material['wf_preview_double_sided'] = True
    return objects


def source_normals(collection):
    """按原导出三角顺序取真实拐角法线，保留平滑与硬边。"""
    groups = defaultdict(list)
    for obj in collection.objects:
        if obj.type == 'MESH' and 'wf_part' in obj:
            groups[obj['wf_part']].append(obj)
    result = {}
    for name, objects in groups.items():
        normals = []
        for obj in objects:
            mesh = obj.data
            mesh.calc_loop_triangles()
            normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
            for tri in mesh.loop_triangles:
                points = [obj.matrix_world @ mesh.vertices[i].co for i in tri.vertices]
                if (points[1]-points[0]).cross(points[2]-points[0]).length < 1e-10:
                    continue
                for loop in tri.loops:
                    normal = (normal_matrix @ mesh.corner_normals[loop].vector).normalized()
                    normals.extend(round(v, 6) for v in normal)
        result[name] = normals
    return result


def finish(asset, folder, budgets, notes, assumptions=None, sources=None, added_native_parts=None, parent_adjustments=None):
    """保存独立来源和三档运行模型，按真实法线导出最终GLB。"""
    folder = Path(folder)
    if not folder.is_absolute():
        folder = ROOT / folder
    folder.mkdir(parents=True, exist_ok=True)
    recipe = leaf_recipe(folder)
    target = folder / 'models.candidate.json'
    for model in asset.models.values():
        model['collection']['worldfall_model_name'] = model['source']['Name']
    asset.export(notes, output_path=target)
    doc = read(target)
    records = []
    for model in doc['Models']:
        collection = asset.models[model['Name']]['collection']
        collection.hide_viewport = False
        bpy.context.view_layer.update()
        normals = source_normals(collection)
        for part in model['Parts']:
            part['Nrm'] = normals[part['Name']]
        model['Info']['cultiway_native_unit'] = '1'
        model['Info']['cultiway_asset'] = asset.id
        level = level_of(model['Name'])
        count = sum(len(part['Tri']) // 3 for part in model['Parts'])
        if count > budgets[level]:
            raise ValueError(f'{model["Name"]} 为{count}面，超出{budgets[level]}预算')
        records.append({'name': model['Name'], 'level': level, 'variant': variant_of(model['Name'],doc['Family']), 'triangles': count,
            'budget': budgets[level], 'glb': 'glbs/' + model['Name'] + '.glb'})
        if recipe is not None:
            assert recipe['AssetId'] == asset.id and recipe['ModelName'] == doc['Family']
            body_bounds = {key:copy.deepcopy(model[key]) for key in ('Min','Max')}
            points = [Vector([model[key][axis] for axis in 'XYZ']) for key in ('Min','Max')]
            for card in recipe['Levels'][level.title()]:
                for angle in (card['Angle'],card['Angle']+math.pi/2):
                    for side in (-.5,.5):
                        for rise in (0,card['Height']):
                            points.append(Vector((card['X']+math.cos(angle)*card['Width']*side,
                                card['Y']+math.sin(angle)*card['Width']*side,card['Z']+rise)))
            for key,choose in (('Min',min),('Max',max)):
                model[key] = {axis:round(choose(point[index] for point in points),6) for index,axis in enumerate('XYZ')}
            records[-1].update(combined_glb='glbs/'+model['Name']+'.leaves.glb',
                leaf_cards=len(recipe['Levels'][level.title()]),body_bounds=body_bounds,
                combined_bounds={key:copy.deepcopy(model[key]) for key in ('Min','Max')})
    target.write_text(json.dumps(doc, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    for model in asset.models.values():
        colors = model['source']['SlotColor']
        for obj in model['collection'].objects:
            if obj.type != 'MESH':
                continue
            for slot, material in enumerate(obj.data.materials):
                color = colors[slot]
                rgb = [int(color) >> shift & 255 for shift in (0, 8, 16)]
                rgba = tuple(inspect.linear(v) for v in rgb) + (1,)
                material.diffuse_color = rgba
                if material.use_nodes:
                    material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = rgba
    asset.show(asset.data['Family'], viewport=True)
    if recipe is not None:
        for model, record in zip(doc['Models'], records):
            collection = asset.models[model['Name']]['collection']
            for obj in leaf_objects(model, recipe, record['level'], folder):
                for current in list(obj.users_collection): current.objects.unlink(obj)
                collection.objects.link(obj)
        for image in bpy.data.images:
            if image.source == 'FILE' and image.has_data: image.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'model.blend'), compress=True)
    for model in asset.models.values():
        model['collection'].hide_viewport = True
    (folder / 'glbs').mkdir(exist_ok=True)
    for model, record in zip(doc['Models'], records):
        obj = inspect.mesh_object(model, model['Name'])
        inspect.export_glb([obj], folder / record['glb'])
        glb_normals(folder / record['glb'], model, restore=True)
        if recipe is not None:
            cards = leaf_objects(model, recipe, record['level'], folder)
            inspect.export_glb([obj]+cards, folder/record['combined_glb'])
            for card in cards: bpy.data.objects.remove(card, do_unlink=True)
        bpy.data.objects.remove(obj, do_unlink=True)
    source_paths = sources or sorted((ROOT / 'GameResources/actors/species/other/Cultiway' /
        asset.id.removeprefix('Cultiway.')).glob('**/*.png'))
    strategy = {'asset': asset.id, 'method': '原图约束的Blender独立自建',
        'candidate_sha256': digest(target), 'models': records, 'notes': notes,
        'added_native_parts': added_native_parts or [], 'parent_adjustments': parent_adjustments or {},
        'assumptions': assumptions or [], 'sources': {Path(p).relative_to(ROOT).as_posix(): digest(p) for p in source_paths},
        'game_test': 'pending_user_manual'}
    if recipe is not None:
        strategy.update(leaf_card_sha256=digest(folder/'leaf-cards.json'),
            leaf_texture_hashes={key:digest(folder/path) for key,path in recipe['Textures'].items()})
    write(folder / 'strategy.json', strategy)
    print('UNIT_EXPORTED', asset.id, [r['triangles'] for r in records])


def glb_normals(path, model, restore=False):
    """保留文件里的真实浮点法线，避免Blender自定义法线内部量化改变来源。"""
    raw = bytearray(Path(path).read_bytes())
    size = struct.unpack_from('<I', raw, 12)[0]
    declaration = json.loads(raw[20:20+size])
    binary = 20 + size + 8
    assert raw[binary-4:binary] == b'BIN\0'
    expected = defaultdict(list)
    for part in model['Parts']:
        for index in part['Tri']:
            point = tuple(round(v, 6) for v in part['Pos'][index*3:index*3+3])
            expected[point].append(Vector(part['Nrm'][index*3:index*3+3]))
    def accessor(number):
        value = declaration['accessors'][number]
        assert value['componentType'] == 5126 and value['type'] == 'VEC3' and 'sparse' not in value
        view = declaration['bufferViews'][value['bufferView']]
        return value['count'], binary + view.get('byteOffset', 0) + value.get('byteOffset', 0), view.get('byteStride', 12)
    maximum = 0
    count = 0
    for mesh in declaration['meshes']:
        for primitive in mesh['primitives']:
            positions, offset, stride = accessor(primitive['attributes']['POSITION'])
            normals, normal_offset, normal_stride = accessor(primitive['attributes']['NORMAL'])
            assert positions == normals
            for index in range(positions):
                x, z, negative_y = struct.unpack_from('<3f', raw, offset + stride*index)
                key = tuple(round(v, 6) for v in (x, -negative_y, z))
                nx, nz, negative_ny = struct.unpack_from('<3f', raw, normal_offset + normal_stride*index)
                normal = Vector((nx, -negative_ny, nz))
                target = min(expected[key], key=lambda value: (normal-value).length)
                error = (normal-target).length
                assert all(math.isfinite(v) for v in normal) and abs(normal.length-1) < .00001
                assert error < (.02 if restore else .000002), (model['Name'], 'GLB NORMAL', error)
                maximum = max(maximum, error)
                if restore:
                    struct.pack_into('<3f', raw, normal_offset + normal_stride*index, target.x, target.z, -target.y)
                count += 1
    if restore:
        Path(path).write_bytes(raw)
    return {'GlbFloatNormalsCompared': count, 'GlbFloatMaximumNormalError': maximum,
        'GlbFloatNormalsMatchRuntimeNrm': not restore}


def check_normals(model, objects):
    """核对GLB实际角法线，允许Blender内部压缩带来的小量误差。"""
    expected = defaultdict(list)
    for part in model['Parts']:
        for i in part['Tri']:
            key = tuple(round(v, 6) for v in part['Pos'][i*3:i*3+3])
            expected[key].append(Vector(part['Nrm'][i*3:i*3+3]))
    maximum = 0
    count = 0
    for obj in objects:
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        for loop in obj.data.loops:
            point = obj.matrix_world @ obj.data.vertices[loop.vertex_index].co
            key = tuple(round(v, 6) for v in point)
            normal = (normal_matrix @ obj.data.corner_normals[loop.index].vector).normalized()
            error = min((normal-n).length for n in expected[key])
            assert error < .012, (model['Name'], 'Blender导入内部法线量化', error)
            maximum = max(maximum, error)
            count += 1
    return {'TriangleCornersCompared': count, 'MaximumNormalVectorError': maximum,
        'BlenderImporterNormalTolerance': .012}


def posed_model(model, pose):
    """按原生实际输出的矩阵和转轴回放，逐级继承父部件。"""
    result = copy.deepcopy(model)
    basis = Matrix(pose['basis']).transposed()
    for part_index, part in enumerate(result['Parts']):
        if pose.get('hidden',0) >> part_index & 1:
            for field in ('Pos','Nrm','Tri','Slot','Axis'):
                part[field]=[]
            continue
        for v in range(len(part['Pos']) // 3):
            point = Vector(part['Pos'][v*3:v*3+3])
            normal = Vector(part['Nrm'][v*3:v*3+3])
            index = part_index
            while index >= 0:
                state = pose['parts'][index]
                pivot = Vector(state['pivot'])
                rotation = Matrix(state['columns']).transposed()
                point = pivot + rotation @ ((point-pivot)*state['scale']) + Vector(state['offset'])
                normal = rotation @ normal
                index = state['parent']
            point, normal = basis @ point, (basis @ normal).normalized()
            part['Pos'][v*3:v*3+3] = point
            part['Nrm'][v*3:v*3+3] = normal
    return result


def render_scene(destination, views):
    """所有来源统一使用两线程、固定光照和实际背面剔除。"""
    inspect.setup_scene()
    cull_material_backfaces()
    scene = bpy.context.scene
    scene.render.threads = 2
    scene.cycles.samples = 4
    scene.cycles.transparent_max_bounces = 64
    scene.render.resolution_x = scene.render.resolution_y = 360
    inspect.add_area((-3, -4, 6), 400, 5)
    Path(destination).mkdir(parents=True, exist_ok=True)
    for label, position, target, scale in views:
        camera = inspect.camera_at(position, target, scale)
        scene.render.filepath = str(Path(destination) / (label + '.png'))
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(camera, do_unlink=True)


def render_model(model, destination, views):
    """从最终实导文件一致的运行几何回放，保持固定相机与灯光。"""
    inspect.clear_scene()
    inspect.mesh_object(model, model['Name'])
    render_scene(destination, views)


def cull_material_backfaces():
    """只改检查材质；普通方向图也不再用双面着色掩盖朝向错误。"""
    for material in bpy.data.materials:
        if not material.use_nodes or material.get('wf_preview_cull') or material.get('wf_preview_double_sided') or material.get('double_sided'):
            continue
        nodes, links = material.node_tree.nodes, material.node_tree.links
        output = next((node for node in nodes if node.type == 'OUTPUT_MATERIAL'), None)
        if output is None or not output.inputs['Surface'].links:
            continue
        front = output.inputs['Surface'].links[0].from_socket
        mix = nodes.new('ShaderNodeMixShader')
        geometry = nodes.new('ShaderNodeNewGeometry')
        transparent = nodes.new('ShaderNodeBsdfTransparent')
        links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        links.new(front, mix.inputs[1])
        links.new(transparent.outputs[0], mix.inputs[2])
        links.new(mix.outputs[0], output.inputs['Surface'])
        material['wf_preview_cull'] = True


def render_culled_underside(destination, views):
    """补充从下方看的检查机位。"""
    render_scene(destination, views)


def check_and_render(folder):
    """在新Blender进程重开可编辑来源、实导三GLB，核对后渲染视图和动作。"""
    folder = Path(folder)
    if not folder.is_absolute():
        folder = ROOT / folder
    doc, strategy, poses = (read(folder / name) for name in ('models.candidate.json', 'strategy.json', 'poses.json'))
    recipe = leaf_recipe(folder)
    if recipe is not None:
        assert strategy['leaf_card_sha256'] == digest(folder/'leaf-cards.json')
        assert all(digest(folder/path) == strategy['leaf_texture_hashes'][key] for key,path in recipe['Textures'].items())
    assert strategy['candidate_sha256'] == poses['candidate_sha256'] == digest(folder / 'models.candidate.json')
    bpy.ops.wm.open_mainfile(filepath=str(folder / 'model.blend'))
    bpy.context.view_layer.update()
    for collection in bpy.data.collections:
        collection.hide_viewport = False
    bpy.context.view_layer.update()
    source = {}
    for model in doc['Models']:
        collection = next(c for c in bpy.data.collections if c.get('worldfall_model_name', c.name) == model['Name'])
        normals = source_normals(collection)
        for part in model['Parts']:
            assert len(normals[part['Name']]) == len(part['Nrm'])
            assert max((abs(a-b) for a, b in zip(normals[part['Name']], part['Nrm'])), default=0) < .000002
        source[model['Name']] = True
    results = []
    seed = read(ROOT / 'Art/Worldfall/Seeds' / (doc['Id'] + '.json'))
    for model, record in zip(doc['Models'], strategy['models']):
        original = next(m for m in seed['Models'] if m['Name'] == model['Name'])
        prefix = len(original['Parts'])
        original_parts = [(p['Name'], p['Parent']) for p in original['Parts']]
        current_parts = [(p['Name'], p['Parent']) for p in model['Parts'][:prefix]]
        assert [name for name,_ in current_parts] == [name for name,_ in original_parts]
        parent_adjustments = strategy.get('parent_adjustments', {})
        part_indices = {name:index for index,(name,_) in enumerate(original_parts)}
        assert set(parent_adjustments) <= set(part_indices)
        expected_parts = [(name,part_indices[parent_adjustments[name]] if name in parent_adjustments else parent)
            for name,parent in original_parts]
        assert current_parts == expected_parts
        added = [p['Name'] for p in model['Parts'][prefix:]]
        assert added == strategy.get('added_native_parts', [])
        assert all(name in ('wing_l','wing_r','wing_l2','wing_r2','claw_l','claw_r','leg_l','leg_r','weapon',
            'leg_1','leg_2','leg_3','leg_4','leg_5','leg_6','leg_7','leg_8','tail',
            'leg_fl','leg_fr','leg_bl','leg_br') for name in added)
        assert len(set(p['Name'] for p in model['Parts'])) == len(model['Parts'])
        assert all(-1 <= part['Parent'] < index for index,part in enumerate(model['Parts']))
        assert model['SlotNames'][:len(original['SlotNames'])] == original['SlotNames']
        assert len(model['SlotNames']) <= 64
        result = inspect.inspect_glb(folder / record['glb'], model, 0)
        result.update(glb_normals(folder / record['glb'], model))
        result.update(check_normals(model, [o for o in bpy.context.scene.objects if o.type == 'MESH']))
        result.update(EditableNormalsMatchRuntimeNrm=source[model['Name']],
            OriginalPartsAndParentsPreserved=current_parts==original_parts,
            OriginalPartsAndDeclaredParentsVerified=True, DeclaredParentAdjustments=parent_adjustments,
            AddedNativeParts=added)
        if recipe is not None:
            result['LeafCards'] = inspect.inspect_glb(folder/record['combined_glb'],model,record['leaf_cards'])
        results.append(result)
    proof = {'Passed': True, 'IndependentBlenderProcess': True, 'DefaultViewsBackfaceCulled': True,
        'ReopenedEditableSource': True, 'ImportedAllFinalGLBs': True, 'candidate_sha256': digest(folder / 'models.candidate.json'),
        'Models': results, 'game_test': 'pending_user_manual'}
    near = doc['Models'][0]
    low, high = (Vector([near[key][axis] for axis in 'XYZ']) for key in ('Min', 'Max'))
    target = (low+high)*.5
    span = max(high-low)*1.35
    h = high.z
    views = [('front',(0,-3,h*.55),target,span),('side',(3,0,h*.55),target,span),
        ('back',(0,3,h*.55),target,span),('top',(1.7,-2.8,3),target,span)]
    for model, record, result in zip(doc['Models'], strategy['models'], results):
        requested = views if record['level'] == 'near' else [views[0], views[3]]
        inspect.import_glb(folder / record.get('combined_glb',record['glb']))
        variant=variant_of(model['Name'],doc['Family'])
        preview=folder/'preview' if variant=='base' else folder/'preview'/variant
        render_scene(preview / record['level'], requested)
        render_culled_underside(preview / record['level'],
            [('underside_culled', (1.7, -2.8, -2), target, span)])
        result['BackfaceCulledUndersideRendered'] = True
    for current in (model for model in doc['Models'] if level_of(model['Name'])=='near'):
        variant=variant_of(current['Name'],doc['Family'])
        pose_folder=folder/'poses' if variant=='base' else folder/'poses'/variant
        native = next(p for p in poses['models'] if p['name'] == current['Name'])
        for pose in native['poses']:
            if recipe is None:
                render_model(posed_model(current, pose), pose_folder, [(pose['name'], views[3][1], target, span)])
            else:
                inspect.clear_scene()
                inspect.mesh_object(posed_model(current,pose),current['Name'])
                assert 'leaf_cards' in pose, '叶簇姿态必须来自实际C#贴片入口'
                leaf_objects(current,recipe,'near',folder,pose['leaf_cards'])
                render_scene(pose_folder,[(pose['name'],views[3][1],target,span)])
    if doc.get('Humanoid'):
        assert poses['rig']=='PersonRig' and poses['person_dress_used']
        proof.update(HumanoidVariants=sorted({variant_of(model['Name'],doc['Family']) for model in doc['Models']}),
            ActualPersonRigUsed=True,ActualPersonDressUsed=True)
    if recipe is not None:
        proof.update(leaf_card_sha256=strategy['leaf_card_sha256'],
            leaf_texture_hashes=strategy['leaf_texture_hashes'],LeafPosesFromActualBillboardMethod=True)
    write(folder / 'reopen-check.json', proof)
    print('UNIT_REOPEN_PASSED', doc['Id'], len(results))


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--')+1:]
    if args[0] == 'check':
        check_and_render(args[1])
