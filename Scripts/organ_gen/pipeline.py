"""从字符矩阵制作与原版帧对齐的部件，生成游戏和预览共用的组合目录。"""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw

from Scripts.pixel_text import parse_text
from Scripts.organ_overlay_preview import assets, compositor

ROOT = assets.REPO_ROOT
DATA = ROOT / "Content/CreatureVisuals"
OUTPUT = ROOT / "GameResources/cultiway/yao/appearance"
ARTIFACTS = ROOT / "artifacts/creature_appearance"
PADDING = 12

# 定义只在这里维护；导出目录也是游戏及预览实际读取的组合规则。
RECIPES = {
    "horn_thunder": ("horn", 65, 2, (), False),
    "horn_evilspotter": ("horn", 65, 2, (), False),
    "wing_fupeng": ("back", 70, 2, (), False),
    "wing_fierce": ("back", 70, 2, (), False),
    "tails1": ("tail", 75, 0, (), False),
    "tails3": ("tail", 76, 0, (), False),
    "tails6": ("tail", 77, 0, (), False),
    "shell": ("back", 85, 2, ("surface",), False),
    "pattern_scale": ("surface", 45, 1, (), True),
    "pattern_scale_cold": ("surface", 45, 1, (), True),
    "pattern_fire": ("surface", 45, 1, (), True),
    "pattern_moon": ("surface", 45, 1, (), True),
    "eye_glow": ("eye", 55, 2, (), True),
}
MAMMALS = {"wolf", "bear", "fox", "rabbit", "rat", "sheep"}
WINGED = MAMMALS | {"chicken", "penguin"}
ICONS = {"shell": "shell_black", "tails1": "tails_rank1", "tails3": "tails_rank3",
         "tails6": "tails_rank6", "pattern_fire": "pattern_feather", "pattern_moon": "pattern_feather",
         "pattern_scale_cold": "pattern_scale"}
ROOTS = {"horn_thunder": (2, 5), "horn_evilspotter": (2, 5), "wing_fupeng": (8, 5),
         "wing_fierce": (8, 5), "tails1": (7, 5), "tails3": (10, 9), "tails6": (13, 10), "shell": (5, 5)}
# 出口由制作模板明确声明，不在运行时猜最高像素；缩小时取距离该点最近的实际角像素。
OUTLETS = {"horn_thunder": (1, 0)}
PARTS = {**{f"tails{count}": [f"tail.{i}" for i in range(count)] for count in (1, 3, 6)},
         "wing_fupeng": ["wing.right", "wing.left"], "wing_fierce": ["wing.right", "wing.left"]}
FUR_PALETTES = {
    "fox": {"K": "593b32", "D": "a46b4a", "M": "df9b62", "H": "f2e8d6"},
    "wolf": {"K": "373333", "D": "796b60", "M": "b9a68b", "H": "eee2c9"},
    "bear": {"K": "32271f", "D": "6e5139", "M": "ab8059", "H": "d7bb8f"},
    "rabbit": {"K": "4b4143", "D": "a19092", "M": "dfd3cc", "H": "f6eee2"},
    "rat": {"K": "3b3740", "D": "77717b", "M": "b4a9b7", "H": "e7dee7"},
    "sheep": {"K": "4a4035", "D": "ab9c80", "M": "ded0ab", "H": "f3e8cd"}}


def frame_names(species):
    """直接读取原版实际存在的动作，游泳与龙的飞行动作使用自己的帧。"""
    if species == "dragon":
        directory = assets.GAME_ASSETS / assets.SPECIES_DIRS[species]
        return sorted(p.stem for p in directory.glob("dragon_fly[0-9].asset"))
    return compositor.list_clip_frames(species, "walk") + compositor.list_clip_frames(species, "swim")


def supported(species, organ):
    """身体结构决定可见造型；没有合适位置的器官仍能提供玩法效果。"""
    if organ.startswith("tails"):
        return species in MAMMALS
    if organ.startswith("wing"):
        return species in WINGED
    if organ == "shell":
        return species not in {"snake", "dragon", "chicken"}
    if organ.startswith("horn"):
        return species not in {"chicken", "penguin", "frog", "dragon"}
    return True


def template(organ, species, family=None):
    """模板保留独立的像素轮廓与材料色板；编辑时只需改字符矩阵。"""
    path = DATA / "templates" / f"{organ}.txt"
    palette, rows = parse_text(path.read_text(encoding="utf-8"), str(path))
    if (family or organ).startswith("tails"):
        for key, color in FUR_PALETTES[species].items():
            palette[key] = tuple(bytes.fromhex(color)) + (255,)
    image = Image.new("RGBA", (len(rows[0]), len(rows)))
    image.putdata([palette[ch] for row in rows for ch in row])
    return image


def anatomy(body):
    """主体朝右：从真实轮廓找颅顶、肩背和臀部，不把旧尾巴尖当成新尾根。"""
    profile = compositor.measure_profile(body)
    columns = sorted(profile)
    left, right = columns[0], columns[-1]
    span = right - left + 1
    head_columns = [x for x in columns if x >= left + span * .65]
    skull_x = max(head_columns, key=lambda x: profile[x])
    shoulder = min(columns, key=lambda x: abs(x - (left + span * .56)))
    hip = min(columns, key=lambda x: abs(x - (left + span * .30)))
    return dict(head=(skull_x, body.height - profile[skull_x]),
                back=(shoulder, body.height - profile[shoulder]),
                hip=(hip, body.height - profile[hip]), length=span)


def make_layer(species, frame_name, organ, component=None):
    """先按朝右的轮廓制作，再整体转回原版朝向；输出位置与运行时完全一致。"""
    if component is None and organ in PARTS:
        pieces = [make_layer(species, frame_name, organ, name) for name in PARTS[organ]]
        combined = Image.new("RGBA", pieces[0][0].size)
        for piece, _, _ in pieces:
            combined.alpha_composite(piece)
        return combined, pieces[0][1], pieces[0][2]
    raw, _, _ = compositor._load_native_frame(species, frame_name)
    body = raw.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    points = anatomy(body)
    canvas = Image.new("RGBA", (body.width + PADDING * 2, body.height + PADDING * 2))
    anchors = {}
    if organ.startswith("pattern"):
        colors = {"pattern_scale": (118, 161, 157), "pattern_scale_cold": (130, 175, 196),
                  "pattern_fire": (222, 137, 75), "pattern_moon": (185, 191, 219)}
        tint = colors[organ]
        pixels = body.load()
        for y in range(1, body.height - 1):
            for x in range(1, body.width - 1):
                p = pixels[x, y]
                # 只点染内部，不覆盖描边、眼睛和水面白线。
                if p[3] and min(p[:3]) < 215 and max(p[:3]) > 90 and (x + y * 2) % 4 == 0:
                    if all(pixels[x + dx, y + dy][3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                        canvas.putpixel((x + PADDING, y + PADDING), (*tint, 120))
    elif organ == "eye_glow":
        choices = [(x, y) for y in range(body.height - 1) for x in range(round(body.width * .60), body.width)
                   if body.getpixel((x, y))[3] and min(body.getpixel((x, y))[:3]) > 200]
        if choices:
            x, y = min(choices, key=lambda p: (p[1], -p[0]))
            canvas.putpixel((x + PADDING, y + PADDING), (211, 240, 255, 230))
    else:
        source = f"parts/{organ}/{component}" if component and organ in {"tails3", "tails6"} else organ
        part = template(source, species, organ)
        original_size = part.size
        if organ.startswith("horn"):
            h = max(3, min(5, round(points["length"] * .30)))
            part = part.resize((max(3, round(part.width * h / part.height)), h), Image.Resampling.NEAREST)
            x, y = points["head"]
            target = (x, y)
        elif organ.startswith("tails"):
            factor = {"tails1": .65, "tails3": .90, "tails6": 1.15}[organ]
            # 多尾保留模板的独立尖端，不能缩小后又退回同一团尾巴。
            w = max(5 if organ == "tails1" else part.width, round(points["length"] * factor))
            part = part.resize((w, max(4, round(part.height * w / part.width))), Image.Resampling.NEAREST)
            x, y = points["hip"]
            target = (x, y + 1)
        elif organ.startswith("wing"):
            w = max(6, round(points["length"] * .75))
            part = part.resize((w, max(5, round(part.height * w / part.width))), Image.Resampling.NEAREST)
            x, y = points["back"]
            target = (x, y + 1)
        else:
            w = max(5, round(points["length"] * .55))
            part = part.resize((w, max(3, round(w * .45))), Image.Resampling.NEAREST)
            x, y = points["back"]
            target = (x, y + 2)
        root = ROOTS[organ]
        position = tuple(target[i] - round((root[i] + .5) * part.size[i] / original_size[i] - .5)
                         for i in range(2))
        canvas.alpha_composite(part, (position[0] + PADDING, position[1] + PADDING))
        mount = tuple((root[i] + .5) * part.size[i] / original_size[i] for i in range(2))
        # 所有实体部件沿用制作时的根点；体表纹样不提供可单独驱动的安装点。
        anchors["mount"] = dict(X=body.width - position[0] - mount[0],
                                Y=body.height - position[1] - mount[1])
        if organ in OUTLETS:
            wanted = tuple((OUTLETS[organ][i] + .5) * part.size[i] / original_size[i] for i in range(2))
            visible = [(x + .5, y + .5) for y in range(part.height) for x in range(part.width)
                       if part.getpixel((x, y))[3]]
            tip = min(visible, key=lambda p: sum((p[i] - wanted[i]) ** 2 for i in range(2)))
            mount = tuple((root[i] + .5) * part.size[i] / original_size[i] for i in range(2))
            # 整张图随后水平翻回原版朝向，挂点同时转换为原身体左下角坐标，不含PADDING。
            for name, point in (("mount", mount), ("cast", tip)):
                anchors[name] = dict(X=body.width - position[0] - point[0],
                                     Y=body.height - position[1] - point[1])
    return canvas.transpose(Image.Transpose.FLIP_LEFT_RIGHT), raw, anchors


def export():
    """一次生成图片和目录，所有引用都来自实际产物，不写通配或缺帧回退。"""
    catalog = {"Rigs": [], "Layers": []}
    for sp in assets.SPECIES_DIRS:
        catalog["Rigs"].append({"id": f"yao.rig.{sp}", "CompatibleActorAssetIds": [sp]})
    for organ, (channel, priority, depth, occludes, clip) in RECIPES.items():
        layer = dict(id=f"yao.layer.{organ}", Channel=channel, Priority=priority,
                     Depth=depth, OccludesChannels=list(occludes), ClipToBody=clip,
                     IconPath=f"cultiway/yao/organs/{ICONS.get(organ, organ)}", FramesByRig={})
        for species in assets.SPECIES_DIRS:
            if not supported(species, organ):
                continue
            frames = {}
            has_ink = False
            for name in frame_names(species):
                image, body, anchors = make_layer(species, name, organ)
                has_ink |= image.getchannel("A").getbbox() is not None
                relative = Path(organ) / f"yao.rig.{species}" / f"{name}.png"
                target = OUTPUT / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                image.save(target)
                frames[name] = dict(SpritePath="cultiway/yao/appearance/" + relative.with_suffix("").as_posix(),
                                    BodyWidth=body.width, BodyHeight=body.height, X=-PADDING, Y=-PADDING)
                if anchors:
                    frames[name]["Points"] = anchors
                if organ in PARTS:
                    combined = Image.new("RGBA", image.size)
                    components = {}
                    for component in PARTS[organ]:
                        piece, _, mounts = make_layer(species, name, organ, component)
                        combined.alpha_composite(piece)
                        piece_relative = relative.with_name(name + "_" + component + ".png")
                        piece.save(OUTPUT / piece_relative)
                        components[component] = dict(SpritePath="cultiway/yao/appearance/" + piece_relative.with_suffix("").as_posix(),
                                                     BodyWidth=body.width, BodyHeight=body.height, X=-PADDING, Y=-PADDING, Points=mounts)
                    combined.save(target)
                    frames[name]["Parts"] = components
            if frames and has_ink:
                layer["FramesByRig"][f"yao.rig.{species}"] = frames
        catalog["Layers"].append(layer)
    (DATA / "catalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    expected = {ROOT / "GameResources" / (piece["SpritePath"] + ".png")
                for l in catalog["Layers"] for fs in l["FramesByRig"].values() for f in fs.values()
                for piece in [f, *f.get("Parts", {}).values()]}
    for path in OUTPUT.rglob("*.png"):
        if path not in expected:
            path.unlink()
    print(f"已生成 {sum(len(fs) for layer in catalog['Layers'] for fs in layer['FramesByRig'].values())} 张部件动画")
    return catalog


def load_catalog():
    return json.loads((DATA / "catalog.json").read_text(encoding="utf-8"))


def select(catalog, species, layer_ids, limit=3):
    """预览复用导出目录里的位置与竞争规则；与运行时按相同顺序选择。"""
    candidates = [l for l in catalog["Layers"] if l["id"] in layer_ids and f"yao.rig.{species}" in l["FramesByRig"]]
    occupied, selected = set(), []
    for layer in sorted(candidates, key=lambda l: (-l["Priority"], l["id"])):
        if layer["Channel"] in occupied:
            continue
        selected.append(layer)
        occupied.add(layer["Channel"])
        occupied.update(layer["OccludesChannels"])
        if len(selected) == limit:
            break
    return sorted(selected, key=lambda l: (l["Depth"], l["id"]))


def preview_frame(catalog, species, frame_name, layer_ids):
    """使用落盘图片合成，不走另一套锚点或缩放计算。"""
    body, _, _ = compositor._load_native_frame(species, frame_name)
    image = Image.new("RGBA", (body.width + PADDING * 2, body.height + PADDING * 2))
    layers = select(catalog, species, layer_ids)
    for layer in layers:
        if layer["Depth"] == 0:
            frame = layer["FramesByRig"][f"yao.rig.{species}"][frame_name]
            image.alpha_composite(Image.open(ROOT / "GameResources" / (frame["SpritePath"] + ".png")))
    image.alpha_composite(body, (PADDING, PADDING))
    for layer in layers:
        if layer["Depth"] != 0:
            frame = layer["FramesByRig"][f"yao.rig.{species}"][frame_name]
            image.alpha_composite(Image.open(ROOT / "GameResources" / (frame["SpritePath"] + ".png")))
    return image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)


def gallery():
    """同时检查正常组合、等级、位置冲突及入水动作。"""
    catalog = load_catalog()
    scenarios = [("wolf", ["horn_thunder", "pattern_scale_cold"]),
                 ("wolf", ["horn_evilspotter", "wing_fierce"]),
                 ("wolf", ["shell", "wing_fupeng", "pattern_scale"]),
                 ("fox", ["tails1", "pattern_moon"]),
                 ("fox", ["tails3", "eye_glow"]),
                 ("fox", ["tails6", "eye_glow"]),
                 ("snake", ["horn_thunder", "pattern_scale_cold"]),
                 ("turtle", ["shell", "pattern_scale_cold"])]
    sheet = Image.new("RGBA", (800, 140 * len(scenarios)), (34, 35, 42, 255))
    draw = ImageDraw.Draw(sheet)
    for row, (species, organs) in enumerate(scenarios):
        for col, frame in enumerate(("walk_0", "walk_1", "walk_2", "swim_1")):
            im = preview_frame(catalog, species, frame, ["yao.layer." + o for o in organs])
            im = im.resize((im.width * 4, im.height * 4), Image.Resampling.NEAREST)
            x, y = col * 200, row * 140
            draw.text((x + 4, y + 4), f"{species} {'+'.join(organs)}\n{frame}", fill="white")
            sheet.alpha_composite(im, (x + 8, y + 12))
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    sheet.save(ARTIFACTS / "combinations.png")
    print(ARTIFACTS / "combinations.png")


def check():
    """校验真实目录覆盖动作、像素尺寸和组合冲突，而非仅检查脚本能运行。"""
    catalog = load_catalog()
    count = 0
    for layer in catalog["Layers"]:
        for rig, frames in layer["FramesByRig"].items():
            species = rig.removeprefix("yao.rig.")
            assert set(frames) == set(frame_names(species)), (layer["id"], species, "动作不完整")
            for name, frame in frames.items():
                for piece in [frame, *frame.get("Parts", {}).values()]:
                    path = ROOT / "GameResources" / (piece["SpritePath"] + ".png")
                    with Image.open(path) as image:
                        assert image.size == (piece["BodyWidth"] + PADDING * 2, piece["BodyHeight"] + PADDING * 2), path
                        assert image.mode == "RGBA", path
                        bounds = image.getchannel("A").getbbox()
                        assert not bounds or (bounds[0] > 0 and bounds[1] > 0 and bounds[2] < image.width and bounds[3] < image.height), (path, "部件被画布裁断")
                        if layer["id"].removeprefix("yao.layer.") in ROOTS:
                            assert "mount" in piece["Points"], (path, "缺少制作根点")
                        if layer["id"] == "yao.layer.horn_thunder":
                            points = piece["Points"]
                            assert set(points) == {"mount", "cast"}, (path, "缺少制作挂点")
                            point = points["cast"]
                            px, py = round(point["X"] - piece["X"] - .5), round(point["Y"] - piece["Y"] - .5)
                            assert image.getpixel((px, image.height - 1 - py))[3] > 0, (path, "出口不在实际角像素中")
                    count += 1
    for species in ("wolf", "fox", "snake"):
        chosen = select(catalog, species, [l["id"] for l in catalog["Layers"]])
        channels = [l["Channel"] for l in chosen]
        assert len(channels) == len(set(channels)) and len(channels) <= 3
        if "back" in channels and any(l["id"] == "yao.layer.shell" for l in chosen):
            assert "surface" not in channels
    print(f"通过：{count} 张动画图片，完整动作覆盖，位置竞争与数量限制")
