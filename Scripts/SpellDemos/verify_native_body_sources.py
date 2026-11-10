#!/usr/bin/env python3
"""核对四族现有衣装的地面帧，检查部位制作位置确有原像素或明确补画来源。"""
import json
import re
from pathlib import Path

from PIL import Image

from common import PROJECT, write_json


def main():
    """直接读取原版导出的精确矩形；不把此检查当作游戏绘制、遮挡或动画验证。"""
    profile = json.loads((PROJECT / "Content/BodyVisuals/humanoid-regions.json").read_text())
    roles = profile["Roles"]
    expected = set(roles.values()) - {"head"}  # 头来自独立头图，实际合成另由游戏检查。
    samples = []
    for race in ("human", "elf", "dwarf", "orc"):
        folder = PROJECT / ".GameSource/Assets/Resources/actors/species/civs" / race
        textures = {}
        for meta in folder.glob("*.png.meta"):
            png = meta.with_suffix("")
            if png.stem.startswith(("male", "female", "warrior")):
                textures[re.search(r"guid: (\w+)", meta.read_text())[1]] = png
        for asset in sorted(folder.glob("walk_*.asset")):
            if "_head" in asset.stem or "_item" in asset.stem:
                continue
            source = asset.read_text()
            frame = re.search(r"m_Name: (\w+)", source)[1]
            if frame not in profile["Frames"]:
                continue
            guid = re.search(r"texture: \{fileID: \d+, guid: (\w+)", source)[1]
            if guid not in textures:
                continue
            rect = re.search(r"m_Rect:\s+serializedVersion: 2\s+x: (\d+)\s+y: (\d+)\s+"
                             r"width: (\d+)\s+height: (\d+)", source)
            x, y, width, height = map(int, rect.groups())
            if (width, height) != (profile["Width"], profile["Height"]):
                continue
            png = textures[guid]
            image = Image.open(png).convert("RGBA")
            body = image.crop((x, image.height - y - height, x + width, image.height - y))
            key = profile["Variants"].get(race + "/" + png.stem, {}).get(frame, frame)
            fill = set(map(tuple, profile["MissingHandPixels"].get(key, [])))
            counts = dict.fromkeys(expected, 0)
            for row, line in enumerate(profile["Frames"][key]):
                for col, mark in enumerate(line):
                    part = roles[mark]
                    if part in counts and (body.getpixel((col, row))[3] or (col, height - 1 - row) in fill):
                        counts[part] += 1
            samples.append(dict(race=race, texture=png.stem, frame=frame, profile=key,
                                source=str(asset.relative_to(PROJECT)), counts=counts,
                                missing=sorted(part for part, count in counts.items() if count == 0)))
    failures = [sample for sample in samples if sample["missing"]]
    report = dict(passed=bool(samples) and not failures, frames=len(samples), samples=samples)
    output = PROJECT / "artifacts/skill_rebuild/body-native-source-coverage"
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "result.json", report)
    print(json.dumps(dict(passed=report["passed"], frames=len(samples), failures=len(failures)), ensure_ascii=False))
    for sample in failures:
        print(sample["race"], sample["texture"], sample["frame"], ", ".join(sample["missing"]))
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
