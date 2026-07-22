"""为核心形成特效生成逐帧 pivot，消除帧内容自身漂移造成的抖动。"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


# 具有明确落地基线的序列用下缘特征对齐，其余序列按视觉主体中心对齐。
BOTTOM_ANCHORED = {
    "body_counter",
    "illusion_decoy",
    "infant_guard",
    "infant_guard_loop",
    "primal_body",
    "primal_body_counter",
    "primal_body_loop",
    "spirit_platform",
    "spirit_platform_loop",
    "wood_venom_bloom",
}


def weighted_quantile(values: list[float], weights: list[float], quantile: float) -> float:
    """返回按 alpha 权重计算的分位值，避免少量远端粒子决定锚点。"""
    ordered = sorted(zip(values, weights), key=lambda pair: pair[0])
    threshold = sum(weights) * quantile
    accumulated = 0.0
    for value, weight in ordered:
        accumulated += weight
        if accumulated >= threshold:
            return value
    return ordered[-1][0]


def semantic_anchor(path: Path, bottom_anchored: bool) -> tuple[float, float]:
    """提取一帧的稳健视觉中心，或具有生长/站立语义的下缘中心。"""
    with Image.open(path).convert("RGBA") as image:
        alpha = image.getchannel("A")
        xs: list[float] = []
        ys: list[float] = []
        weights: list[float] = []
        for y in range(image.height):
            for x in range(image.width):
                value = alpha.getpixel((x, y))
                if value < 24:
                    continue
                xs.append(float(x))
                ys.append(float(y))
                weights.append((value / 255.0) ** 2)

    if not weights:
        return (image.width - 1) * 0.5, (image.height - 1) * 0.5

    left = weighted_quantile(xs, weights, 0.02)
    right = weighted_quantile(xs, weights, 0.98)
    anchor_x = (left + right) * 0.5
    if bottom_anchored:
        anchor_y = weighted_quantile(ys, weights, 0.96)
    else:
        top = weighted_quantile(ys, weights, 0.02)
        bottom = weighted_quantile(ys, weights, 0.98)
        anchor_y = (top + bottom) * 0.5
    return anchor_x, anchor_y


def median(values: list[float]) -> float:
    """返回序列中位值，用作不改变平均落点的补偿基准。"""
    ordered = sorted(values)
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return ordered[middle]
    return (ordered[middle - 1] + ordered[middle]) * 0.5


def build_settings(directory: Path) -> tuple[dict, float]:
    """计算目录内全部帧相对序列基准的 pivot，并返回原始最大漂移。"""
    frames = sorted(directory.glob("[0-9][0-9][0-9].png"))
    if not frames:
        raise ValueError(f"{directory} 中没有按三位数字命名的帧")

    with Image.open(frames[0]) as first:
        width, height = first.size
    anchors = [semantic_anchor(frame, directory.name in BOTTOM_ANCHORED) for frame in frames]
    baseline_x = median([anchor[0] for anchor in anchors])
    baseline_y = median([anchor[1] for anchor in anchors])
    max_drift = max(
        ((anchor[0] - baseline_x) ** 2 + (anchor[1] - baseline_y) ** 2) ** 0.5
        for anchor in anchors
    )

    specific = []
    for frame, (anchor_x, anchor_y) in zip(frames, anchors):
        pivot_x = 0.5 + (anchor_x - baseline_x) / width
        pivot_y = 0.5 - (anchor_y - baseline_y) / height
        specific.append(
            {
                "Path": frame.name,
                "PivotX": round(pivot_x, 6),
                "PivotY": round(pivot_y, 6),
            }
        )
    return {
        "Default": {"PivotX": 0.5, "PivotY": 0.5},
        "Specific": specific,
    }, max_drift


def main() -> None:
    """为资源根目录下每套动画重建 sprites.json，并打印补偿前漂移。"""
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "root",
        nargs="?",
        type=Path,
        default=Path("GameResources/cultiway/effect/core_formation"),
    )
    args = parser.parse_args()

    for directory in sorted(path for path in args.root.iterdir() if path.is_dir()):
        settings, max_drift = build_settings(directory)
        output = directory / "sprites.json"
        output.write_text(json.dumps(settings, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")
        print(f"{directory.name}: compensated {max_drift:.2f}px")


if __name__ == "__main__":
    main()
