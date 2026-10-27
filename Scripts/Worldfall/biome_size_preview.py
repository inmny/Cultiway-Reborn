"""从正式游戏网格渲染同尺度的人物与群系植被，供离线比较。"""
import argparse
import json
from pathlib import Path
import sys
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Art/Worldfall/biome-sizing-v1"
sys.path.insert(0, str(ROOT / "Scripts/Worldfall"))
from biome_native_check import setup_scene


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_model(row, at):
    """实际游戏文件中的绝对坐标、原色和法线直接进入检查场景。"""
    model = next(model for model in read(ROOT / row["resource"])["Models"] if model["Name"] == row["name"])
    materials = []
    for index, color in enumerate(model["SlotColor"]):
        material = bpy.data.materials.new(model["Name"] + "." + model["SlotNames"][index])
        material.use_nodes = True
        channels = [(color >> shift & 255) / 255 for shift in (0, 8, 16)]
        linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in channels]
        material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (*linear, 1)
        material.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = .85
        materials.append(material)
    scale = row.get("place_scale", [row.get("scale", 1)] * 3)
    for part in model["Parts"]:
        if not part["Tri"]:
            continue
        if row["name"] == "person_cultiway_eastern_human" and part["Name"] in ("beard", "hair_long", "tusk"):
            continue
        pos = part["Pos"]
        mesh = bpy.data.meshes.new(model["Name"] + "." + part["Name"])
        mesh.from_pydata([pos[i:i+3] for i in range(0, len(pos), 3)], [],
                        [part["Tri"][i:i+3] for i in range(0, len(part["Tri"]), 3)])
        mesh.update()
        for material in materials:
            mesh.materials.append(material)
        for polygon, slot in zip(mesh.polygons, part["Slot"]):
            polygon.material_index = slot
        obj = bpy.data.objects.new(mesh.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.scale = scale
        obj.location = (at[0], at[1], row.get("ground_offset", 0))
        if len(part["Nrm"]) == len(pos):
            mesh.normals_split_custom_set_from_vertices([part["Nrm"][i:i+3] for i in range(0, len(pos), 3)])
    load_leaf_cards(row, at, scale)
    return model


def load_leaf_cards(row, at, scale):
    """样板的透明叶簇来自正式图片和放置数据，其余原生纹理仍不模拟。"""
    folder = ROOT / "Content/Worldfall/LeafCards"
    for path in folder.glob("*.json"):
        recipe = read(path)
        names = {recipe["ModelName"]: "Near", recipe["ModelName"]+"_m": "Middle", recipe["ModelName"]+"_f": "Far"}
        if row["name"] not in names:
            continue
        materials = {}
        for card in recipe["Levels"][names[row["name"]]]:
            key = card["Image"]
            if key not in materials:
                image_path = ROOT / "GameResources" / (recipe["Textures"][key] + ".png")
                material = bpy.data.materials.new("叶簇." + key)
                material.use_nodes = True
                material.use_backface_culling = False
                texture = material.node_tree.nodes.new("ShaderNodeTexImage")
                texture.image = bpy.data.images.load(str(image_path), check_existing=True)
                shader = material.node_tree.nodes.get("Principled BSDF")
                material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
                material.node_tree.links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
                shader.inputs["Roughness"].default_value = .85
                materials[key] = material
            vertices, faces = [], []
            for angle in (card["Angle"], card["Angle"] + math.pi/2):
                start = len(vertices)
                for u, v in ((0,0),(1,0),(1,1),(0,1)):
                    d = (u-.5) * card["Width"]
                    vertices.append((card["X"]+math.cos(angle)*d, card["Y"]+math.sin(angle)*d, card["Z"]+v*card["Height"]))
                faces.append((start,start+1,start+2,start+3))
            mesh = bpy.data.meshes.new("交叉叶簇")
            mesh.from_pydata(vertices, [], faces)
            mesh.materials.append(materials[key])
            uv = mesh.uv_layers.new(name="UVMap")
            for face in mesh.polygons:
                for loop, point in zip(face.loop_indices, ((0,0),(1,0),(1,1),(0,1))):
                    uv.data[loop].uv = point
            obj = bpy.data.objects.new(mesh.name, mesh)
            bpy.context.scene.collection.objects.link(obj)
            obj.scale = scale
            obj.location = (at[0],at[1],row.get("ground_offset",0))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', nargs='*')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    report = read(OUT / "size-check.json")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene, camera = setup_scene()
    scene.render.resolution_x = 720
    scene.render.resolution_y = 680
    scene.render.resolution_percentage = 100
    camera.data.ortho_scale = 15.5
    camera.location = (0, -22, 8.1)
    camera.rotation_euler = (Vector((0, 0, 6.6)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    floor_material = bpy.data.materials.new("尺寸检查地面")
    floor_material.diffuse_color = (.17, .20, .23, 1)
    selected = set(args.assets or (group["asset"] for group in report["groups"]))
    previous = read(OUT / "preview-check.json") if (OUT / "preview-check.json").exists() else {"images": []}
    images = [row for row in previous["images"] if row["asset"] not in selected]
    for group in report["groups"]:
        if group["asset"] not in selected:
            continue
        old = set(bpy.data.objects)
        load_model(report["person"], (-1.5, 0))
        load_model(group["representative"], (.65, 0))
        plants = group["plant_examples"]
        for index, plant in enumerate(plants):
            load_model(plant, (-.55 + index * .36, -.85))
        bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.018))
        bpy.context.object.data.materials.append(floor_material)
        picture = group["asset"] + ".png"
        scene.render.filepath = str(OUT / picture)
        bpy.ops.render.render(write_still=True)
        images.append(dict(asset=group["asset"], label=group["label"], image=picture,
            tree=group["representative"]["name"], tree_height=group["representative"]["top_above_ground"],
            human_height=report["adult_height"], plants=[plant["name"] for plant in plants]))
        for obj in set(bpy.data.objects) - old:
            bpy.data.objects.remove(obj, do_unlink=True)
    (OUT / "preview-check.json").write_text(json.dumps(dict(images=images,
        source="实际Content游戏网格与叶簇样板图片/放置数据；同一相机与游戏单位，人物采用标准身高，CPU8样本；离线图不替代游戏验收",
        camera_ortho_scale=camera.data.ortho_scale, game_test="pending_user_manual"), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"已更新{len(selected)}群系的人物比例对照")


if __name__ == "__main__":
    main()
