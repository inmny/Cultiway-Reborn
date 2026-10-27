"""用实际编译比例与正式模型整理人物、群系植被的尺寸检查与预览输入。"""
import json
from pathlib import Path
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Art/Worldfall/biome-sizing-v1"
LABELS = dict(zip(
    ("Bamboo", "Candle", "Cemetery", "Coral", "Dark", "Fern", "FleshBlood", "Knowledge", "Oak", "Rice", "Titans", "Nurgle", "Khorne", "Tzeentch", "Slaanesh"),
    ("竹林", "烛火", "墓园", "珊瑚", "黑暗森林", "蕨类", "血肉", "知识", "橡树", "稻田", "巨灵", "纳垢", "恐虐", "奸奇", "色孽")
))


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sprite_rows(asset):
    """按实际外观分组，保留动画帧的尺寸范围。"""
    result = {}
    for path in sorted((ROOT / "GameResources/buildings" / asset).glob("*.png")):
        match = re.fullmatch(r"(main|ruin)_\s*(\d+)(?:_(\d+))?", path.stem)
        if not match:
            continue
        image = Image.open(path).convert("RGBA")
        bbox = image.getchannel("A").point(lambda value: 255 if value >= 128 else 0).getbbox()
        frame = f"{match[1]}_{match[2]}"
        result.setdefault(frame, []).append(dict(path=str(path.relative_to(ROOT)).replace("\\", "/"),
            width=image.width, height=image.height, visible_height=bbox[3]-bbox[1]))
    return result


def main():
    compiled = read(OUT / "compiled-check.json")
    assert compiled["passed"] and compiled["worldfall_version"] == "1.0.0.0"
    adult = compiled["adult_height"]
    tree_scales = {row["asset"]: row["scale"] for row in compiled["tree_scales"] if row["asset"].endswith("_tree")}
    plant_scales = {row["asset"]: row["scale"] for row in compiled["plant_scales"]}
    growth = {row["pixels"]: row for row in compiled["native_growth"]}
    sink_source = (ROOT / "Source/Content/Worldfall/WorldfallModelIntegration.Trees.cs").read_text(encoding="utf-8-sig")
    root_sink = float(re.search(r"MatureDarkTreeRootSink\s*=\s*([\d.]+)f", sink_source)[1])
    rows = []
    groups = []
    for base, label in LABELS.items():
        group = {"asset": base, "label": label, "trees": [], "plants": []}
        for asset, kind in ((base + "_tree", "tree"), (base, "plant")):
            resource = ROOT / "Content/Worldfall/Models" / asset / "models.json"
            document = read(resource)
            sprites = sprite_rows(asset)
            assert len({model["Name"] for model in document["Models"]}) == len(document["Models"])
            for model in document["Models"]:
                frame = model["Info"]["cultiway_frame"]
                marker = "cultiway_tree" if kind == "tree" else "cultiway_plant"
                assert str(model["Info"][marker]) == "1" and frame in sprites
                variants = sprites[frame]
                if kind == "tree":
                    heights = [s["visible_height"] * .25 * growth[s["height"]]["grow"] for s in variants]
                    factor = tree_scales[asset]
                    reference = max(variants, key=lambda s: s["visible_height"] * .25 * growth[s["height"]]["grow"])
                    old_height = max(heights)
                    new_height = old_height * factor
                    vertical_scale = new_height / max(.5, model["Max"]["Z"])
                    offset = root_sink * vertical_scale if model["Name"] in (
                        "building_cultiway_Dark_tree", "building_cultiway_Dark_tree_m", "building_cultiway_Dark_tree_f") else 0
                    if asset == "Candle_tree":
                        offset = compiled["candle_root_sink"] * vertical_scale
                    elif asset == "FleshBlood_tree":
                        offset = compiled["fleshblood_root_sink"] * vertical_scale
                    elif asset == "Titans_tree":
                        offset = compiled["titans_root_sink"] * vertical_scale
                    top = new_height - offset
                    assert max(h * factor for h in heights) <= adult * 15.21, (asset, frame, heights, factor)
                    # 缩小后均按原生普通树的树宽比例显示。
                    assert reference["height"] * .25 * growth[reference["height"]]["grow"] * factor < 16.5
                    place_scale = [vertical_scale * .8, vertical_scale * .8, vertical_scale]
                    random_range = [.9 * top, 1.1 * top]
                else:
                    factor = plant_scales.get(asset, compiled["plant_scale"])
                    vertical_scale = .625 * factor
                    old_height = (model["Max"]["Z"] - model["Min"]["Z"]) * .625
                    new_height = old_height * factor
                    offset = .01
                    if asset == "Candle":
                        offset += compiled["candle_plant_sink"] * vertical_scale
                    top = new_height - offset
                    place_scale = [vertical_scale] * 3
                    random_range = [new_height * .8 - .01 - (offset-.01)*.8, new_height * 1.2 - .01 - (offset-.01)*1.2]
                    assert new_height <= adult * .501 * factor / compiled["plant_scale"]
                row = dict(asset=asset, label=label, kind=kind, name=model["Name"], frame=frame,
                    level="middle" if model["Name"].endswith("_m") else "far" if model["Name"].endswith("_f") else "near",
                    resource=str(resource.relative_to(ROOT)).replace("\\", "/"), factor=factor,
                    previous_nominal_height=old_height, nominal_height=new_height, top_above_ground=top,
                    adult_ratio=top/adult, random_height_range=random_range, place_scale=place_scale, ground_offset=-offset)
                rows.append(row)
                group["trees" if kind == "tree" else "plants"].append(row)
            frame_levels = {}
            for row in rows:
                if row["asset"] == asset:
                    frame_levels.setdefault(row["frame"], []).append(row)
            for levels in frame_levels.values():
                assert {row["level"] for row in levels} == {"near", "middle", "far"}
                assert len({round(row["factor"], 7) for row in levels}) == 1
        living = [row for row in group["trees"] if row["frame"].startswith("main_") and row["level"] == "near"]
        group["representative"] = max(living, key=lambda row: row["nominal_height"])
        group["plant_examples"] = [row for row in group["plants"] if row["level"] == "near"]
        groups.append(group)
    assert len(rows) == 909 and len(tree_scales) == 15
    # 采用真正加载给人物渲染器的网格，以基础普通男装的有效高度归一。
    person_resource = ROOT / "Content/Worldfall/Models/Cultiway.EasternHuman/models.json"
    person = next(model for model in read(person_resource)["Models"] if model["Name"] == "person_cultiway_eastern_human")
    person_scale = adult / (person["Max"]["Z"] - max(0, person["Min"]["Z"]))
    adult_sprites = ROOT / "GameResources/actors/species/civs/Cultiway.EasternHuman/male_1"
    body_rows = []
    for path in sorted(adult_sprites.glob("walk_[0-3].png")):
        bbox = Image.open(path).getchannel("A").point(lambda value: 255 if value >= 128 else 0).getbbox()
        body_rows.append(bbox[3] - bbox[1])
    assert body_rows == [9] * 4 and abs(adult - .9) < 1e-6
    result = dict(passed=True, assets=30, models=len(rows), adult_height=adult, plant_scale=compiled["plant_scale"],
        adult_reference="male_1四幅普通行走图均9有效像素，基础缩放0.1，像素单位1；基因、特性和其他皮肤仍有自身高低差",
        current_dark_root_sink=root_sink, current_candle_root_sink=compiled["candle_root_sink"], game_test="pending_user_manual", native_compiled_check="compiled-check.json",
        current_fleshblood_root_sink=compiled["fleshblood_root_sink"], current_candle_plant_sink=compiled["candle_plant_sink"],
        current_titans_root_sink=compiled["titans_root_sink"],
        person=dict(resource=str(person_resource.relative_to(ROOT)).replace("\\", "/"), name=person["Name"], scale=person_scale),
        groups=groups, models_checked=rows)
    (OUT / "size-check.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(passed=True, assets=30, models=len(rows), adult_height=round(adult, 2),
        plant_ratio_range=[round(min(row["adult_ratio"] for row in rows if row["kind"] == "plant"), 3),
                           round(max(row["adult_ratio"] for row in rows if row["kind"] == "plant"), 3)]), ensure_ascii=False))


if __name__ == "__main__":
    main()
