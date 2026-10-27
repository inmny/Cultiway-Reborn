"""按实际发布记录、Blender 导出与生成网格更新制作清单。"""
import json
import hashlib
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root / "Doc/修仙贴图三维化/Blender模型清单.json"
items = json.loads(path.read_text(encoding="utf-8-sig"))
for item in items:
    asset = root / "Art/Worldfall" / item["Id"]
    runtime = root / "Content/Worldfall/Models" / item["Id"] / "models.json"
    references = sorted(p.name for p in (asset / "reference").glob("*.png"))
    generated = sorted(p.name for p in (asset / "trellis").glob("generated-*.glb"))
    unit_publication = asset / "native-unit-v1/manifest.json"
    if unit_publication.is_file() and runtime.is_file():
        unit = json.loads(unit_publication.read_text(encoding="utf-8-sig"))
        unit_sha = hashlib.sha256(runtime.read_bytes()).hexdigest()
        if (unit.get("publication") == "published_offline" and unit.get("production_sha256") == unit_sha
                and all((asset / p).is_file() for p in unit["source_glbs"] + unit["editable_sources"])):
            item.update({"Models": unit["models"], "ExportedModels": unit["models"],
                         "Shape": unit.get("shape", item["Shape"]),
                         "RuntimeSha256": unit_sha, "Triangles": unit["triangles"],
                         "Authoring": "Blender 4.4 原图约束独立自建", "PublishedNative": True,
                         "NativeVersion": "native-unit-v1", "PublishedTrellis": False,
                         "EditableSources": unit["editable_sources"], "source_glbs": unit["source_glbs"],
                         "inspection_files": unit["inspection_files"], "WorldfallPaletteValid": True,
                         "GameTest": "pending_user_manual", "Status": "三档独立生物已离线发布；游戏验收待用户查看"})
            continue
    item.pop("PublishedNative", None)
    item.pop("NativeVersion", None)
    native_publication = asset / "native-biome-v1/manifest.json"
    if native_publication.is_file() and runtime.is_file():
        native = json.loads(native_publication.read_text(encoding="utf-8-sig"))
        runtime_sha = hashlib.sha256(runtime.read_bytes()).hexdigest()
        sources = native.get("source_glbs", [])
        editable = native.get("editable_sources", [])
        if (native.get("publication") == "published_offline"
                and native.get("production_sha256") == runtime_sha
                and sources and all((asset / source).is_file() for source in sources)
                and editable and all((asset / source).is_file() for source in editable)):
            item.update({"Models": native["models"], "ExportedModels": native["models"],
                         "RuntimeSha256": runtime_sha, "Triangles": native["triangles"],
                         "Authoring": "Blender 4.4 自建实体", "PublishedNative": True,
                         "NativeVersion": "native-biome-v1", "PublishedTrellis": False,
                         "EditableSources": editable, "source_glbs": sources,
                         "inspection_files": native["inspection_files"],
                         "WorldfallPaletteValid": True, "GameTest": "pending_user_manual",
                         "Status": "全部外观、残骸和三档离线发布；游戏验收待用户查看"})
            item.pop("PaletteIssues", None)
            continue
    item["ReferenceImages"] = references
    item["TrellisFiles"] = generated
    item.pop("PublishedTrellis", None)
    item.pop("EditableSources", None)
    item.pop("RuntimeSha256", None)
    item.pop("WorldfallPaletteValid", None)
    item.pop("PaletteIssues", None)
    if runtime.is_file():
        runtime_bytes = runtime.read_bytes()
        document = json.loads(runtime_bytes.decode("utf-8-sig"))
        item["RuntimeSha256"] = hashlib.sha256(runtime_bytes).hexdigest()
        item["ExportedModels"] = len(document["Models"])
        item["Authoring"] = document.get("Authoring", "")
        item["Status"] = "Blender 已导出；Trellis 候选待适配" if generated else "Blender 已导出；等待 Trellis 生成"
        item["Triangles"] = {
            model["Name"]: sum(len(part["Tri"]) // 3 for part in model["Parts"])
            for model in document["Models"]
        }
        palette_issues = []
        for model in document["Models"]:
            slots = max(len(model["SlotNames"]), len(model["SlotColor"]))
            affected = sum(slot >= 64 for part in model["Parts"] for slot in part["Slot"])
            if slots > 64 or affected:
                palette_issues.append({"Model": model["Name"], "Slots": slots, "AffectedFaces": affected})
        item["WorldfallPaletteValid"] = not palette_issues
        if palette_issues:
            item["PaletteIssues"] = palette_issues
        publication = asset / "trellis/publication.json"
        if publication.is_file():
            receipt = json.loads(publication.read_text(encoding="utf-8-sig"))
            sources = receipt.get("source_glbs", [])
            editable = receipt.get("editable_sources", [])
            item["PublishedTrellis"] = (
                receipt.get("published") is True
                and receipt.get("production_sha256", "").lower() == item["RuntimeSha256"]
                and bool(sources) and all((asset / source).is_file() for source in sources)
                and bool(editable) and all((asset / source).is_file() for source in editable)
            )
            if item["PublishedTrellis"]:
                item["EditableSources"] = editable
        if item.get("PublishedTrellis"):
            item["Status"] = "Trellis 已经 Blender 适配导出；游戏验收另见记录"
        if palette_issues:
            item["Status"] = "已导出模型超过 Worldfall 的 64 颜色槽上限；等待原代理修正和游戏复测"
    else:
        item.pop("ExportedModels", None)
        item.pop("Triangles", None)
        item.pop("Authoring", None)
        if generated:
            item["Status"] = "真实 Trellis 网格已生成；等待独立 Blender 适配"
        else:
            item["Status"] = "参考图已生成；等待 Trellis 与独立 Blender 适配" if references else "等待 imagegen 参考图、Trellis 生成和独立 Blender 适配"
    review_path = asset / "design-review.json"
    if review_path.is_file():
        review = json.loads(review_path.read_text(encoding="utf-8-sig"))
        if review.get("accepted") is False:
            item["Status"] = review["notes"]
            item["PublishedTrellis"] = False
path.write_text(json.dumps(items, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"assets": len(items), "blender_exports": sum(bool(i.get("ExportedModels")) for i in items),
    "with_references": sum(bool(i["ReferenceImages"]) for i in items),
    "with_trellis": sum(bool(i["TrellisFiles"]) for i in items),
    "published_trellis": sum(bool(i.get("PublishedTrellis")) for i in items),
    "published_native": sum(bool(i.get("PublishedNative")) for i in items),
    "palette_valid_trellis": sum(bool(i.get("PublishedTrellis")) and i.get("WorldfallPaletteValid") is True for i in items),
    "palette_invalid_assets": sum(i.get("WorldfallPaletteValid") is False for i in items)}, ensure_ascii=False))
