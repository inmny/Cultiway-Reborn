"""生成辅助与功能法术使用的确定性像素图元和独立 UI 图标。"""

from __future__ import annotations

import argparse
import json
import math
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
EFFECT_ROOT = ROOT / "GameResources" / "cultiway" / "effect"
ICON_ROOT = ROOT / "GameResources" / "cultiway" / "icons" / "skills"
PRIMITIVE_ROOT = EFFECT_ROOT / "world_primitives"

SKILLS = (
    "healing_light",
    "rejuvenation_field",
    "purification_wave",
    "battle_blessing",
    "guard_blessing",
    "haste_blessing",
    "raise_terrain",
    "lower_terrain",
    "fill_water",
    "drain_water",
    "nature_growth_field",
    "clean_land_field",
)

PROJECTILE_SKILLS = {
    "healing_light": "healing",
    "battle_blessing": "battle",
    "guard_blessing": "guard",
    "haste_blessing": "haste",
}

WHITE = (255, 255, 255, 255)
LIGHT = (213, 230, 230, 255)
MID = (143, 172, 174, 255)
SHADOW = (77, 105, 110, 255)
TRANSPARENT = (0, 0, 0, 0)


def save(image: Image.Image, path: Path) -> None:
    """以无调色板 RGBA PNG 保存资源。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGBA").save(path, optimize=True)


def write_sprite_meta(folder: Path, pivot_x: float = 0.5, pivot_y: float = 0.5) -> None:
    """写入资源加载器读取的默认 pivot 元数据。"""
    folder.mkdir(parents=True, exist_ok=True)
    data = {"Default": {"PivotX": pivot_x, "PivotY": pivot_y}}
    (folder / "sprites.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=4) + "\n", encoding="utf-8"
    )


def image(width: int, height: int) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    """创建透明像素画画布和禁用平滑的绘图器。"""
    result = Image.new("RGBA", (width, height), TRANSPARENT)
    return result, ImageDraw.Draw(result)


def extract_icons() -> None:
    """在替换旧世界动画前提取其代表帧作为独立 UI 图标。"""
    ICON_ROOT.mkdir(parents=True, exist_ok=True)
    for skill in SKILLS:
        destination = ICON_ROOT / f"{skill}.png"
        if destination.exists():
            continue
        runtime = EFFECT_ROOT / skill / "0" / "runtime"
        frames = sorted(runtime.glob("*.png"))
        if not frames:
            raise FileNotFoundError(f"找不到可提取的技能图标: {runtime}")
        source = frames[min(7, len(frames) // 2)]
        shutil.copyfile(source, destination)


def clear_png_frames(folder: Path) -> None:
    """只删除指定技能片段目录内的旧 PNG 帧。"""
    resolved = folder.resolve()
    if EFFECT_ROOT.resolve() not in resolved.parents:
        raise RuntimeError(f"拒绝清理技能资源目录以外的路径: {resolved}")
    folder.mkdir(parents=True, exist_ok=True)
    for frame in folder.glob("*.png"):
        frame.unlink()


def projectile_frame(kind: str, phase: float, alpha: float = 1.0, extent: float = 1.0) -> Image.Image:
    """绘制沿正 X 方向飞行的光体，避免把状态徽记当作世界实体。"""
    result, draw = image(16, 16)

    def color(rgb: tuple[int, int, int], value: float = 1.0) -> tuple[int, int, int, int]:
        return (*rgb, max(0, min(255, round(255 * alpha * value))))

    pulse = 0.5 + 0.5 * math.sin(phase * math.tau)
    if kind == "healing":
        core = (204, 255, 224)
        edge = (60, 220, 151)
        trail = (41, 145, 110)
        length = max(1, round(6 * extent))
        draw.line((9 - length, 8, 8, 8), fill=color(trail, 0.7), width=1)
        draw.point((5, 7 + round(pulse)), fill=color(edge, 0.75))
        draw.polygon(((8, 6), (11, 8), (8, 10), (6, 8)), fill=color(edge))
        draw.rectangle((8, 7, 9, 8), fill=color(core))
        draw.point((11, 8), fill=color(core))
    elif kind == "battle":
        core = (255, 224, 104)
        edge = (255, 83, 45)
        shadow = (155, 31, 30)
        length = max(1, round(8 * extent))
        draw.line((10 - length, 8, 10, 8), fill=color(shadow), width=1)
        draw.line((7, 7, 11, 8), fill=color(edge), width=2)
        draw.polygon(((9, 5), (13, 8), (9, 11), (10, 8)), fill=color(edge))
        draw.line((8, 8, 12, 8), fill=color(core), width=1)
        draw.point((5, 6 + round(pulse * 3)), fill=color(edge, 0.75))
    elif kind == "guard":
        core = (255, 244, 188)
        edge = (205, 178, 88)
        steel = (103, 138, 153)
        draw.polygon(((7, 4), (12, 8), (7, 12), (4, 8)), fill=color(steel))
        draw.polygon(((8, 5), (11, 8), (8, 11), (6, 8)), fill=color(edge))
        draw.line((7, 6, 9, 8, 7, 10), fill=color(core), width=1)
        offset = round(pulse)
        draw.rectangle((3, 6 + offset, 4, 7 + offset), fill=color(edge, 0.75))
    elif kind == "haste":
        core = (218, 255, 250)
        edge = (64, 219, 226)
        shadow = (38, 119, 166)
        length = max(2, round(9 * extent))
        draw.line((12 - length, 6, 12, 6), fill=color(shadow, 0.75), width=1)
        draw.line((10 - length, 8, 13, 8), fill=color(edge), width=1)
        draw.line((11 - length, 10, 11, 10), fill=color(shadow, 0.65), width=1)
        draw.polygon(((10, 6), (14, 8), (10, 10), (12, 8)), fill=color(edge))
        draw.point((12, 8), fill=color(core))
    return result


def replace_runtime_animations() -> None:
    """用真实飞行光体或透明载体替换十二组旧徽记世界动画。"""
    for skill in SKILLS:
        root = EFFECT_ROOT / skill / "0"
        for clip in ("appearance", "runtime", "dissipation"):
            folder = root / clip
            clear_png_frames(folder)
            write_sprite_meta(folder)

        kind = PROJECTILE_SKILLS.get(skill)
        if kind is None:
            # 保留一个近乎不可见的像素，避免资源导入器把全透明 PNG 判定为空资源。
            blank = Image.new("RGBA", (1, 1), (255, 255, 255, 1))
            for clip in ("appearance", "runtime", "dissipation"):
                save(blank, root / clip / "000.png")
            continue

        for index in range(6):
            t = (index + 1) / 6
            save(projectile_frame(kind, t, t, t), root / "appearance" / f"{index:03}.png")
        for index in range(12):
            save(projectile_frame(kind, index / 12), root / "runtime" / f"{index:03}.png")
        for index in range(6):
            t = index / 5
            save(projectile_frame(kind, t, 1 - t, 1 - t * 0.5), root / "dissipation" / f"{index:03}.png")


def draw_seed_flow(variant: int) -> Image.Image:
    """绘制中央水滴种籽、两条尾弧和端点。"""
    result, draw = image(12, 12)
    draw.polygon(((7, 2), (9, 5), (8, 8), (6, 9), (5, 7), (5, 5)), fill=LIGHT)
    draw.rectangle((6, 5, 7, 7), fill=WHITE)
    if variant == 0:
        draw.line((5, 6, 3, 5, 2, 3), fill=MID, width=1)
        draw.line((5, 8, 3, 9, 1, 8), fill=SHADOW, width=1)
        draw.point((1, 2), fill=WHITE)
    else:
        draw.line((5, 6, 3, 7, 2, 9), fill=MID, width=1)
        draw.line((6, 4, 4, 3, 2, 4), fill=SHADOW, width=1)
        draw.point((1, 9), fill=WHITE)
    return result


def draw_branch_vine(variant: int) -> Image.Image:
    """绘制二像素弯曲茎秆、两处分叉和尖/阔叶。"""
    result, draw = image(16, 12)
    stem = ((1, 9), (4, 8), (6, 6), (9, 6), (12, 3), (15, 2))
    draw.line(stem, fill=MID, width=2)
    draw.line((5, 7, 4, 3), fill=LIGHT, width=1)
    draw.line((10, 5, 12, 9), fill=LIGHT, width=1)
    if variant == 0:
        draw.polygon(((3, 1), (6, 3), (4, 5), (2, 3)), fill=WHITE)
        draw.polygon(((11, 8), (15, 8), (14, 11), (11, 10)), fill=LIGHT)
    else:
        draw.polygon(((2, 2), (5, 2), (5, 5), (3, 4)), fill=LIGHT)
        draw.polygon(((11, 8), (13, 6), (15, 9), (13, 11)), fill=WHITE)
    draw.point((4, 3), fill=SHADOW)
    draw.line((12, 9, 14, 9), fill=SHADOW, width=1)
    return result


def draw_root_y() -> Image.Image:
    """绘制朝内聚拢的 Y 形根纹。"""
    result, draw = image(12, 12)
    draw.line((6, 10, 6, 6), fill=LIGHT, width=2)
    draw.line((6, 7, 2, 3), fill=MID, width=2)
    draw.line((6, 7, 10, 3), fill=MID, width=2)
    draw.line((6, 10, 4, 11), fill=SHADOW, width=1)
    draw.line((6, 10, 8, 11), fill=SHADOW, width=1)
    draw.point((2, 2), fill=WHITE)
    draw.point((10, 2), fill=WHITE)
    return result


def draw_filter_wedge() -> Image.Image:
    """绘制三颗杂质进入、两层收束、一个净点离开的过滤楔。"""
    result, draw = image(14, 10)
    draw.point((1, 2), fill=SHADOW)
    draw.rectangle((1, 5, 2, 6), fill=MID)
    draw.point((2, 8), fill=SHADOW)
    draw.line((4, 1, 7, 4, 4, 8), fill=MID, width=1)
    draw.line((7, 3, 9, 5, 7, 7), fill=LIGHT, width=1)
    draw.line((9, 5, 11, 5), fill=LIGHT, width=1)
    draw.point((12, 5), fill=WHITE)
    return result


def find_chinese_font() -> Path:
    """选择系统中可用的中文字体用于生成可识别题字。"""
    candidates = (
        Path("C:/Windows/Fonts/simhei.ttf"),
        Path("C:/Windows/Fonts/msyh.ttc"),
        Path("C:/Windows/Fonts/simsun.ttc"),
    )
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise FileNotFoundError("找不到可用于生成法阵题字的中文字体")


def draw_inscription(character: str) -> Image.Image:
    """把单个中文题字居中绘制到透明 16x16 图元。"""
    result, draw = image(16, 16)
    font = ImageFont.truetype(str(find_chinese_font()), 14)
    bounds = draw.textbbox((0, 0), character, font=font, stroke_width=0)
    width = bounds[2] - bounds[0]
    height = bounds[3] - bounds[1]
    x = (16 - width) / 2 - bounds[0]
    y = (16 - height) / 2 - bounds[1]
    draw.text((x, y), character, font=font, fill=WHITE)
    return result


def draw_leaf(kind: str) -> Image.Image:
    """绘制尖叶、阔叶或心形叶片，并保留一像素叶脉。"""
    result, draw = image(8, 8)
    if kind == "pointed":
        draw.polygon(((4, 1), (6, 4), (4, 7), (2, 4)), fill=LIGHT)
        draw.line((4, 2, 4, 6), fill=SHADOW, width=1)
    elif kind == "broad":
        draw.polygon(((1, 3), (3, 1), (7, 3), (5, 6), (2, 6)), fill=LIGHT)
        draw.line((2, 5, 6, 3), fill=SHADOW, width=1)
    else:
        draw.polygon(((1, 2), (3, 1), (4, 3), (5, 1), (7, 2), (4, 7)), fill=LIGHT)
        draw.line((4, 3, 4, 6), fill=SHADOW, width=1)
    draw.point((3, 2), fill=WHITE)
    return result


def draw_sprout(frame: int) -> Image.Image:
    """绘制种籽开裂、茎秆伸长、两片子叶展开的八帧萌芽。"""
    result, draw = image(12, 16)
    base_y = 14
    draw.ellipse((4, base_y - 2, 7, base_y), fill=SHADOW)
    if frame >= 1:
        draw.line((5, base_y - 2, 6, base_y - 3, 7, base_y - 2), fill=WHITE, width=1)
    stem_height = max(0, frame - 1) * 2
    if stem_height > 0:
        top = base_y - 2 - stem_height
        draw.line((6, base_y - 2, 6, top), fill=MID, width=1)
        draw.point((6, top), fill=WHITE)
    if frame >= 4:
        top = base_y - 2 - stem_height
        spread = frame - 3
        draw.polygon(((6, top + 1), (max(1, 6 - spread), top - 1), (5, top + 2)), fill=LIGHT)
    if frame >= 5:
        top = base_y - 2 - stem_height
        spread = frame - 4
        draw.polygon(((6, top + 1), (min(10, 6 + spread), top - 1), (7, top + 2)), fill=LIGHT)
    if frame >= 6:
        draw.point((4, max(1, base_y - stem_height - 3)), fill=WHITE)
    if frame >= 7:
        draw.point((8, max(1, base_y - stem_height - 3)), fill=WHITE)
    return result


def draw_small_primitives() -> dict[str, Image.Image]:
    """构造尘土、岩屑、气泡、冰屑、火星和祝福局部图元。"""
    assets: dict[str, Image.Image] = {}

    def pixels(name: str, size: tuple[int, int], points: list[tuple[int, int, tuple[int, int, int, int]]]) -> None:
        result, draw = image(*size)
        for x, y, color in points:
            draw.point((x, y), fill=color)
        assets[name] = result

    pixels("dust_0", (6, 6), [(2, 2, LIGHT), (3, 2, MID), (2, 3, MID)])
    pixels("dust_1", (6, 6), [(1, 2, MID), (2, 2, LIGHT), (2, 3, MID), (3, 3, SHADOW)])
    pixels("dust_2", (6, 6), [(1, 3, SHADOW), (2, 2, MID), (3, 2, LIGHT), (3, 3, MID), (4, 3, SHADOW)])

    rock0, draw = image(6, 6)
    draw.polygon(((3, 0), (5, 3), (3, 5), (0, 3)), fill=MID)
    draw.line((2, 1, 4, 3), fill=LIGHT, width=1)
    assets["rock_0"] = rock0
    rock1, draw = image(6, 6)
    draw.polygon(((1, 1), (5, 2), (4, 5), (0, 4)), fill=MID)
    draw.line((1, 2, 4, 2), fill=LIGHT, width=1)
    assets["rock_1"] = rock1

    bubble, draw = image(6, 6)
    draw.rectangle((1, 1, 4, 4), outline=LIGHT, width=1)
    draw.point((2, 1), fill=WHITE)
    assets["bubble_hollow"] = bubble
    pixels("bubble_point", (4, 4), [(1, 1, WHITE), (2, 1, LIGHT), (1, 2, LIGHT)])

    ice_tall, draw = image(6, 8)
    draw.polygon(((3, 0), (5, 5), (3, 7), (1, 5)), fill=LIGHT)
    draw.line((3, 1, 3, 6), fill=WHITE, width=1)
    assets["ice_shard_tall"] = ice_tall
    ice_short, draw = image(6, 6)
    draw.polygon(((2, 0), (5, 3), (3, 5), (1, 3)), fill=MID)
    draw.line((2, 1, 4, 3), fill=WHITE, width=1)
    assets["ice_shard_short"] = ice_short

    pixels("spark_short", (5, 6), [(2, 0, WHITE), (2, 1, LIGHT), (1, 2, MID), (1, 3, SHADOW)])
    pixels("spark_hook", (6, 6), [(2, 0, WHITE), (3, 1, LIGHT), (3, 2, MID), (2, 3, MID), (1, 3, SHADOW)])
    pixels("wasteland_debris_a", (6, 6), [(1, 2, MID), (2, 1, LIGHT), (3, 2, MID), (2, 3, SHADOW)])
    pixels("wasteland_debris_b", (6, 6), [(1, 3, SHADOW), (2, 2, MID), (3, 2, LIGHT), (4, 3, MID), (3, 4, SHADOW)])

    shard, draw = image(6, 6)
    draw.polygon(((3, 0), (5, 3), (3, 5), (2, 3)), fill=LIGHT)
    draw.point((3, 1), fill=WHITE)
    assets["purify_shard"] = shard
    metal, draw = image(6, 6)
    draw.polygon(((3, 0), (5, 3), (3, 5), (0, 3)), fill=MID)
    draw.line((2, 2, 4, 3), fill=WHITE, width=1)
    assets["metal_shard"] = metal
    streak, draw = image(10, 5)
    draw.line((0, 1, 8, 1), fill=MID, width=1)
    draw.line((2, 2, 9, 2), fill=WHITE, width=1)
    draw.line((0, 3, 6, 3), fill=SHADOW, width=1)
    assets["wind_streak"] = streak
    seed, draw = image(6, 6)
    draw.polygon(((3, 0), (5, 3), (3, 5), (1, 3)), fill=LIGHT)
    draw.rectangle((2, 2, 3, 3), fill=WHITE)
    assets["seed_light"] = seed
    return assets


def generate_primitives() -> None:
    """生成法阵图元、局部粒子和八帧萌芽动画。"""
    PRIMITIVE_ROOT.mkdir(parents=True, exist_ok=True)
    write_sprite_meta(PRIMITIVE_ROOT)
    save(draw_seed_flow(0), PRIMITIVE_ROOT / "seed_flow_a.png")
    save(draw_seed_flow(1), PRIMITIVE_ROOT / "seed_flow_b.png")
    save(draw_branch_vine(0), PRIMITIVE_ROOT / "branch_vine_a.png")
    save(draw_branch_vine(1), PRIMITIVE_ROOT / "branch_vine_b.png")
    save(draw_root_y(), PRIMITIVE_ROOT / "root_y.png")
    save(draw_filter_wedge(), PRIMITIVE_ROOT / "filter_wedge.png")

    inscriptions = {
        "sheng": "生",
        "xi": "息",
        "fu": "复",
        "yuan": "元",
        "mu": "木",
        "fan": "繁",
        "rong": "荣",
        "jing": "净",
        "chen": "尘",
        "di": "涤",
        "hui": "秽",
    }
    for name, character in inscriptions.items():
        save(draw_inscription(character), PRIMITIVE_ROOT / f"inscription_{name}.png")

    save(draw_leaf("pointed"), PRIMITIVE_ROOT / "leaf_pointed.png")
    save(draw_leaf("broad"), PRIMITIVE_ROOT / "leaf_broad.png")
    save(draw_leaf("heart"), PRIMITIVE_ROOT / "leaf_heart.png")
    for name, primitive in draw_small_primitives().items():
        save(primitive, PRIMITIVE_ROOT / f"{name}.png")

    sprout_root = PRIMITIVE_ROOT / "sprout"
    clear_png_frames(sprout_root)
    write_sprite_meta(sprout_root, 0.5, 0.0)
    for frame in range(8):
        save(draw_sprout(frame), sprout_root / f"{frame:03}.png")


def create_preview(path: Path) -> None:
    """生成仅用于静态检查的最近邻放大预览，不写入游戏资源目录。"""
    files = sorted(p for p in PRIMITIVE_ROOT.glob("*.png"))
    samples = files + [EFFECT_ROOT / skill / "0" / "runtime" / "006.png" for skill in PROJECTILE_SKILLS]
    cell = 128
    columns = 6
    rows = math.ceil(len(samples) / columns)
    preview = Image.new("RGBA", (columns * cell, rows * cell), (39, 47, 42, 255))
    draw = ImageDraw.Draw(preview)
    font = ImageFont.load_default()
    for index, sample in enumerate(samples):
        sprite = Image.open(sample).convert("RGBA")
        max_size = 84
        scale = max(1, min(max_size // max(1, sprite.width), max_size // max(1, sprite.height)))
        sprite = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
        x = (index % columns) * cell + (cell - sprite.width) // 2
        y = (index // columns) * cell + 8
        preview.alpha_composite(sprite, (x, y))
        draw.text(((index % columns) * cell + 4, (index // columns) * cell + 104), sample.stem,
                  fill=(235, 242, 237, 255), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    preview.save(path)
    create_field_preview(path.with_name(f"{path.stem}_fields{path.suffix}"))


def tint_sprite(sprite: Image.Image, color: tuple[int, int, int]) -> Image.Image:
    """按源像素明度给法阵预览图元着色，并保留原透明度。"""
    source = sprite.convert("RGBA")
    result = Image.new("RGBA", source.size, TRANSPARENT)
    source_pixels = source.load()
    target_pixels = result.load()
    for y in range(source.height):
        for x in range(source.width):
            r, g, b, a = source_pixels[x, y]
            value = max(r, g, b) / 255
            target_pixels[x, y] = (
                round(color[0] * value),
                round(color[1] * value),
                round(color[2] * value),
                a,
            )
    return result


def create_field_preview(path: Path) -> None:
    """按运行时半径比例静态合成三种法阵，检查层次、间距和中心留白。"""
    canvas = Image.new("RGBA", (768, 256), (39, 47, 42, 255))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    fields = (
        {
            "name": "Rejuvenation",
            "color": (73, 205, 142),
            "primary": (["seed_flow_a", "seed_flow_b"], 8, 0.78, 0.18),
            "secondary": None,
            "text": (["inscription_sheng", "inscription_xi", "inscription_fu", "inscription_yuan"], 4, 0.58, 0.12),
        },
        {
            "name": "Nature growth",
            "color": (77, 181, 69),
            "primary": (["branch_vine_a", "branch_vine_b"], 10, 0.80, 0.18),
            "secondary": (["root_y"], 5, 0.55, 0.14),
            "text": (["inscription_mu", "inscription_sheng", "inscription_fan", "inscription_rong"], 4, 0.67, 0.10),
        },
        {
            "name": "Clean land",
            "color": (151, 198, 194),
            "primary": (["filter_wedge"], 8, 0.78, 0.18),
            "secondary": None,
            "text": (["inscription_jing", "inscription_chen", "inscription_di", "inscription_hui"], 4, 0.56, 0.12),
        },
    )
    radius = 96
    for field_index, field in enumerate(fields):
        center = (128 + field_index * 256, 126)
        boundary = (*field["color"], 210)
        box = (center[0] - radius, center[1] - radius, center[0] + radius, center[1] + radius)
        for segment in range(12):
            start = -90 + segment * 30
            draw.arc(box, start=start, end=start + 24, fill=boundary, width=2)

        for ring_key in ("primary", "secondary", "text"):
            ring = field[ring_key]
            if ring is None:
                continue
            names, count, ratio, size_ratio = ring
            for index in range(count):
                angle = index * math.tau / count
                sprite = Image.open(PRIMITIVE_ROOT / f"{names[index % len(names)]}.png").convert("RGBA")
                sprite = tint_sprite(sprite, field["color"])
                target = max(1, round(radius * size_ratio))
                scale = target / max(sprite.width, sprite.height)
                sprite = sprite.resize(
                    (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))),
                    Image.Resampling.NEAREST,
                )
                if ring_key != "text":
                    sprite = sprite.rotate(-math.degrees(angle) + 90, resample=Image.Resampling.NEAREST, expand=True)
                x = round(center[0] + math.cos(angle) * radius * ratio - sprite.width / 2)
                y = round(center[1] + math.sin(angle) * radius * ratio - sprite.height / 2)
                canvas.alpha_composite(sprite, (x, y))
        draw.text((field_index * 256 + 8, 238), field["name"], fill=(235, 242, 237, 255), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path)


def main() -> None:
    """执行图标提取、动画替换、图元生成与可选预览输出。"""
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    extract_icons()
    replace_runtime_animations()
    generate_primitives()
    if args.preview is not None:
        create_preview(args.preview)


if __name__ == "__main__":
    main()
