"""群系透明叶片共用的Blender导出与独立重新导入工具；不含物体几何。"""
import base64
import hashlib
import json
import math
import struct
from collections import Counter
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree

def unpack(value):
    """兼容生产文件当前数组表示与原始字节表示。"""
    return list(base64.b64decode(value)) if isinstance(value, str) else list(value)

def triangles(part):
    """将逐面数据与三角形保持一一对应。"""
    positions = part["Pos"]
    verts = [positions[i:i + 3] for i in range(0, len(positions), 3)]
    slots = unpack(part["Slot"])
    axes = unpack(part["Axis"])
    for i, slot in enumerate(slots):
        indices = part["Tri"][i * 3:i * 3 + 3]
        yield [verts[j] for j in indices], slot, axes[i] if axes else 0

def clear_scene():
    """清空当前实验场景。"""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)

def setup_scene():
    """四线程CPU低样本固定照明，所有对照共用。"""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 8
    scene.cycles.use_denoising = True
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 4
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast" if "Medium High Contrast" in [i.name for i in scene.view_settings.bl_rna.properties["look"].enum_items] else "None"
    world = bpy.data.worlds.new("neutral_world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.65, .73, .82, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .75
    scene.world = world

def camera_at(position, target, ortho=2.5, name="camera"):
    """所有检查图使用同一正交尺度。"""
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = position
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    data.type = "ORTHO"
    data.ortho_scale = ortho
    bpy.context.scene.camera = obj
    return obj

def add_area(position, energy, size):
    """柔和顶侧光帮助显示叶片自身折面，避免黑底烘焙。"""
    data = bpy.data.lights.new("softbox", "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(data.name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (-obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj

def linear(value):
    """材质与原始像素色使用同一sRGB转换。"""
    value /= 255
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4

def solid_material(name, color):
    """以实际原图的五档叶色建立表面。"""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    rgba = tuple(linear(c) for c in color) + (1,)
    material.diffuse_color = rgba
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Roughness"].default_value = .86
    return material

def mesh_object(model, object_name):
    """从实际生产/候选JSON重建表面，不从旧预览猜几何。"""
    verts, faces, slots, normals = [], [], [], []
    materials = []
    for slot, color in zip(model["SlotNames"], model["SlotColor"]):
        materials.append(solid_material(object_name + "." + slot, (color & 255, (color >> 8) & 255, (color >> 16) & 255)))
        materials[-1]['worldfall_slot'] = len(materials) - 1
    for part in model["Parts"]:
        for number, (vertices, slot, _) in enumerate(triangles(part)):
            start = len(verts)
            verts.extend(vertices)
            faces.append((start, start + 1, start + 2))
            slots.append(slot)
            normals.extend(tuple(part['Nrm'][index*3:index*3+3])
                           for index in part['Tri'][number*3:number*3+3])
    mesh = bpy.data.meshes.new(object_name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(object_name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for material in materials:
        mesh.materials.append(material)
    for poly, index in zip(mesh.polygons, slots):
        poly.material_index = index
        poly.use_smooth = True
    mesh.normals_split_custom_set_from_vertices(normals)
    obj["source_model"] = model["Name"]
    return obj

def card_material(out, image_name, data):
    """纹理颜色与Alpha分开接入；双面，透明像素直接裁掉。"""
    material = bpy.data.materials.new(image_name)
    material.use_nodes = True
    material.use_backface_culling = False
    material.surface_render_method = "DITHERED"
    material.alpha_threshold = .42
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .9
    image = bpy.data.images.load(str(Path(out) / data["Textures"][image_name]), check_existing=True)
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.interpolation = "Linear"
    node.extension = "CLIP"
    material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    alpha = material.node_tree.nodes.new("ShaderNodeMath")
    alpha.operation = "GREATER_THAN"
    alpha.inputs[1].default_value = .42
    material.node_tree.links.new(node.outputs["Alpha"], alpha.inputs[0])
    material.node_tree.links.new(alpha.outputs[0], shader.inputs["Alpha"])
    material["alpha_cutoff"] = .42
    material["double_sided"] = True
    return material

def card_objects(cards, materials):
    """构造可和原生Cross逐点对照的面；倾斜版只作为离线对照。"""
    objects = []
    for card in cards:
        verts, faces = [], []
        x, y, z = card["X"], card["Y"], card["Z"]
        width, height = card["Width"], card["Height"]
        for plane in range(2):
            angle = card["Angle"] + plane * math.pi / 2
            across = Vector((math.cos(angle), math.sin(angle), 0)) * width
            base = Vector((x, y, z)) - across * .5
            rise = Vector((0, 0, height))
            start = len(verts)
            verts.extend((base, base + across, base + across + rise, base + rise))
            faces.append((start, start + 1, start + 2, start + 3))
        mesh = bpy.data.meshes.new(card["Name"])
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        for polygon in mesh.polygons:
            for loop, pair in zip(polygon.loop_indices, ((0, 0), (1, 0), (1, 1), (0, 1))):
                uv.data[loop].uv = pair
        obj = bpy.data.objects.new(card["Name"], mesh)
        bpy.context.scene.collection.objects.link(obj)
        mesh.materials.append(materials[card["Image"]])
        for key, value in card.items():
            obj[key] = value
        obj["native_cross_match"] = True
        objects.append(obj)
    return objects

def export_glb(objects, path):
    """导出含嵌入PNG、UV与双面材质的独立查看文件。"""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", use_selection=True,
                              export_extras=True, export_materials="EXPORT", export_normals=True,
                              export_texcoords=True, export_yup=True)
    bpy.ops.object.select_all(action="DESELECT")


def glb_json(path):
    """读最终文件的材质和内嵌图片声明。"""
    raw = Path(path).read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    assert magic == 0x46546C67 and version == 2 and length == len(raw)
    length, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4E4F534A
    return json.loads(raw[20:20+length])


def import_glb(path):
    """从最终GLB重新导入，不沿用导出时的对象。"""
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()
    return [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']


def inspect_glb(path, candidate_model=None, expected_cards=None):
    """在新的Blender进程调用，核对实际几何、PNG引用、双面裁剪与树体。"""
    path = Path(path)
    declaration = glb_json(path)
    objects = import_glb(path)
    vertex_count = triangle_count = uv_count = card_count = 0
    bounds = [[], [], []]
    body_faces = []
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        vertex_count += len(mesh.vertices)
        triangle_count += len(mesh.loop_triangles)
        is_card = obj.get('Kind') == 'Cross'
        if is_card:
            card_count += 1
            assert len(mesh.loop_triangles) == 4 and mesh.uv_layers.active is not None
            uv_count += len(mesh.uv_layers.active.data)
            assert all(-1e-5 <= v <= 1.00001 for loop in mesh.uv_layers.active.data for v in loop.uv)
        for vertex in mesh.vertices:
            point = obj.matrix_world @ vertex.co
            assert all(math.isfinite(v) for v in point)
            assert .99 < vertex.normal.length < 1.01
            for axis, value in enumerate(point):
                bounds[axis].append(value)
        for tri in mesh.loop_triangles:
            assert all(0 <= i < len(mesh.vertices) for i in tri.vertices)
            points = [obj.matrix_world @ mesh.vertices[i].co for i in tri.vertices]
            assert (points[1]-points[0]).cross(points[2]-points[0]).length > 1e-10
            if not is_card:
                slot = mesh.materials[tri.material_index]['worldfall_slot']
                body_faces.append((points, slot))
    if expected_cards is not None:
        assert card_count == expected_cards
    textured = [m for m in declaration.get('materials', [])
                if 'baseColorTexture' in m.get('pbrMetallicRoughness', {})]
    for material in textured:
        assert material.get('alphaMode') == 'MASK' and material.get('doubleSided') is True
        image_index = declaration['textures'][material['pbrMetallicRoughness']['baseColorTexture']['index']]['source']
        image = declaration['images'][image_index]
        assert image['mimeType'] == 'image/png' and 'bufferView' in image
    images = []
    seen = set()
    for material in {m for obj in objects for m in obj.data.materials if m is not None}:
        for node in material.node_tree.nodes if material.use_nodes else []:
            if node.type != 'TEX_IMAGE' or node.image is None or node.image.as_pointer() in seen:
                continue
            image = node.image
            seen.add(image.as_pointer())
            assert image.channels == 4 and 0 < max(image.size) <= 512
            alpha = set(round(v*255) for v in list(image.pixels)[3::4])
            assert alpha == {0,255}
            images.append({'Name':image.name,'Width':image.size[0],'Height':image.size[1],
                           'AlphaValues':sorted(alpha),'Packed':image.packed_file is not None})
    if card_count:
        assert textured and images
    result = {'File':path.name,'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
              'Vertices':vertex_count,'Triangles':triangle_count,'UVLoops':uv_count,'CardObjects':card_count,
              'Bounds':{axis:[min(bounds[i]),max(bounds[i])] for i,axis in enumerate('XYZ')},
              'AlphaMaskMaterials':len(textured),'ReferencedImages':images,
              'FiniteGeometry':True,'UnitNormals':True,'ValidUVAndIndices':True}
    if candidate_model is not None:
        source = [(verts,slot) for part in candidate_model['Parts'] for verts,slot,_ in triangles(part)]
        unique = sorted({tuple(point) for verts,_ in source for point in verts})
        tree = KDTree(len(unique))
        for i,point in enumerate(unique):
            tree.insert(point,i)
        tree.balance()
        def cycle(points):
            return min(tuple(points[i:]+points[:i]) for i in range(3))
        expected = Counter((cycle([tuple(p) for p in verts]),slot) for verts,slot in source)
        actual = Counter()
        error = 0
        for points,slot in body_faces:
            mapped=[]
            for point in points:
                _,index,distance=tree.find(point)
                assert distance < .000002
                error=max(error,distance)
                mapped.append(unique[index])
            actual[(cycle(mapped),slot)]+=1
        assert actual == expected, path.name
        result.update(CandidateJsonTrianglePositionsWindingAndSlotsMatch=True,MaximumGlbPositionError=error)
    return result


def render_glb(path, destination, views, save_blend=None):
    """从重新导入文件渲染同尺度检查图；views为(名字,位置,目标,正交范围)。"""
    import_glb(path)
    setup_scene()
    scene = bpy.context.scene
    scene.cycles.samples = 4
    scene.cycles.transparent_max_bounces = 40
    add_area((-3,-4,6),400,5)
    destination = Path(destination)
    destination.mkdir(parents=True,exist_ok=True)
    for name,position,target,scale in views:
        camera = camera_at(position,target,scale)
        scene.render.resolution_x = scene.render.resolution_y = 320
        scene.render.resolution_percentage = 100
        scene.render.filepath = str(destination/(name+'.png'))
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(camera,do_unlink=True)
    if save_blend:
        for image in bpy.data.images:
            if image.source == 'FILE' and image.has_data:
                image.pack()
        bpy.ops.wm.save_as_mainfile(filepath=str(save_blend))
