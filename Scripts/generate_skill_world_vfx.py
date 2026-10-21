"""生成辅助与功能法术使用的确定性像素图元和独立 UI 图标。"""

from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
EFFECT_ROOT = ROOT / "GameResources" / "cultiway" / "effect"
ICON_ROOT = ROOT / "GameResources" / "cultiway" / "icons" / "skills"

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


def main() -> None:
    """保留技能图标和旧变体的计时帧；世界效果已改用空间图形，不再生成贴图图元。"""
    extract_icons()
    replace_runtime_animations()


if __name__ == "__main__":
    main()
