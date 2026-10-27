"""在 Blender 中只读检查 Tripo 导出，不改网格、不保存或重导模型。"""
import json
import sys
from pathlib import Path

import bpy

model_path, report_path = map(Path, sys.argv[sys.argv.index('--') + 1:])
bpy.ops.wm.read_factory_settings(use_empty=True)
if model_path.suffix.lower() == '.fbx':
    bpy.ops.import_scene.fbx(filepath=str(model_path))
elif model_path.suffix.lower() == '.glb':
    bpy.ops.import_scene.gltf(filepath=str(model_path))
else:
    raise ValueError('请先解开原始 ZIP；此检查接收 FBX 或 GLB。')

meshes = [obj.data for obj in bpy.data.objects if obj.type == 'MESH']
textures = [dict(name=image.name, path=image.filepath,
                 width=image.size[0], height=image.size[1])
            for image in bpy.data.images if image.size[0] > 0]
polygons = sum(len(mesh.polygons) for mesh in meshes)
triangles = sum(sum(len(face.vertices) - 2 for face in mesh.polygons) for mesh in meshes)
report = dict(model=str(model_path), polygons=polygons, triangles=triangles,
              vertices=sum(len(mesh.vertices) for mesh in meshes),
              textures=textures, face_control=5000,
              within_5000_polygons=polygons <= 5000,
              texture_2k_verified=bool(textures) and all(
                  item['width'] == 2048 and item['height'] == 2048 for item in textures),
              model_modified=False, game_test='pending')
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('TRIPO_INSPECTION ' + json.dumps(report, ensure_ascii=False))
