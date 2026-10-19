"""从原版资源读取成体动画帧与像素轮廓；组合规则见 organ_gen.pipeline。"""

import re
from PIL import Image
from . import assets

def _find_texture_path(directory, guid):
    """按 guid 在目录里找到对应 PNG。"""
    for meta in directory.glob("*.png.meta"):
        text = meta.read_text(encoding="utf-8", errors="ignore")
        if f"guid: {guid}" in text:
            return meta.with_suffix("")  # 去掉 .meta 得到 png 路径
    raise FileNotFoundError(f"目录 {directory} 中没有 guid 为 {guid} 的贴图")


def _parse_sprite_asset(path):
    """解析 Unity Sprite YAML：返回 (贴图guid, 矩形, 枢轴, 偏移)。数值均为 Unity 坐标。"""
    text = path.read_text(encoding="utf-8", errors="ignore")
    rect = re.search(
        r"m_Rect:\s*\n\s*serializedVersion: \d+\s*\n\s*x: ([\d.-]+)\s*\n\s*y: ([\d.-]+)"
        r"\s*\n\s*width: ([\d.-]+)\s*\n\s*height: ([\d.-]+)", text)
    pivot = re.search(r"m_Pivot: \{x: ([\d.-]+), y: ([\d.-]+)\}", text)
    offset = re.search(r"m_Offset: \{x: ([\d.-]+), y: ([\d.-]+)\}", text)
    guid = re.search(r"texture: \{fileID: \d+, guid: ([0-9a-f]+)", text)
    if not (rect and pivot and guid):
        raise ValueError(f"无法从 {path} 解析矩形/枢轴/贴图")
    x, y, w, h = (float(v) for v in rect.groups())
    ox, oy = (float(v) for v in offset.groups()) if offset else (0.0, 0.0)
    return (guid.group(1), (x, y, w, h), (float(pivot.group(1)), float(pivot.group(2))), (ox, oy))


def list_clip_frames(species, clip="walk"):
    """列出某物种某动画剪辑的全部主帧名（walk_0、walk_1…；忽略 _0/_1 分件与 item 帧）。"""
    directory = assets.GAME_ASSETS / assets.SPECIES_DIRS[species]
    pattern = re.compile(rf"^{clip}_(\d+)\.asset$")
    names = []
    for path in directory.glob(f"{clip}_*.asset"):
        match = pattern.match(path.name)
        if match:
            names.append((int(match.group(1)), f"{clip}_{match.group(1)}"))
    return [name for _, name in sorted(names)]


def _load_native_frame(species, sprite_name):
    """从图集裁出单帧，返回 (RGBA 帧, 枢轴比例, 偏移)。

    同一帧名可能存在多个皮肤变体（如狼 walk_0.asset→child.png 幼崽、walk_0_0.asset→main.png
    成体、walk_0_1.asset→zombie.png）：优先选与该物种默认帧（walk_1 等）相同贴图的变体，
    其次选非 child 贴图的变体，避免成体动画混入幼崽帧。
    """
    directory = assets.GAME_ASSETS / assets.SPECIES_DIRS[species]
    pattern = re.compile(rf"^{re.escape(sprite_name)}(?:_(\d+))?\.asset$")
    parsed = []
    for path in sorted(directory.glob(f"{sprite_name}*.asset")):
        if not pattern.match(path.name):
            continue
        guid, rect, pivot, offset = _parse_sprite_asset(path)
        tex = _find_texture_path(directory, guid).name
        sub = pattern.match(path.name).group(1)
        parsed.append((int(sub) if sub else -1, tex, rect, pivot, offset))
    if not parsed:
        raise FileNotFoundError(f"{directory} 中没有帧 {sprite_name}")

    ref_tex = _frame_texture(species, assets.SPECIES_SPRITE[species])
    # 非 child 优先(成体皮肤)，其次与默认帧同贴图，再按分件序号取最小
    non_child = [p for p in parsed if not p[1].startswith("child")] or parsed
    same_tex = [p for p in non_child if p[1] == ref_tex] or non_child
    exact_named = [p for p in same_tex if p[0] == -1] or same_tex
    _, tex, rect, pivot, offset = min(exact_named, key=lambda p: p[0])

    png = _find_texture_path(directory, _texture_guid(directory, tex))
    sheet = Image.open(png).convert("RGBA")
    # Unity 矩形 y 自底向上，PIL 自顶向下
    x, y, w, h = rect
    top = int(round(sheet.height - y - h))
    frame = sheet.crop((int(round(x)), top, int(round(x + w)), int(round(top + h))))
    return frame, pivot, offset


def _frame_texture(species, sprite_name):
    """解析某帧绑定的贴图文件名（用于皮肤变体选择）。"""
    directory = assets.GAME_ASSETS / assets.SPECIES_DIRS[species]
    path = directory / f"{sprite_name}.asset"
    guid = re.search(r"texture: \{fileID: \d+, guid: ([0-9a-f]+)",
                     path.read_text(encoding="utf-8", errors="ignore"))
    return _find_texture_path(directory, guid.group(1)).name if guid else None


def _texture_guid(directory, tex_name):
    """反查贴图文件名的 guid。"""
    meta = directory / (tex_name + ".meta")
    m = re.search(r"guid: ([0-9a-f]+)", meta.read_text(encoding="utf-8", errors="ignore"))
    if not m:
        raise FileNotFoundError(f"{meta} 中没有 guid")
    return m.group(1)


def measure_profile(base):
    """逐列测量身体轮廓：列 x → 最高不透明像素距脚底的高度（像素）。"""
    w, h = base.size
    pixels = base.load()
    profile = {}
    for x in range(w):
        for y in range(h):
            if pixels[x, y][3] >= 128:
                profile[x] = h - y  # PIL y 向下，换算成距脚底高度
                break
    return profile
