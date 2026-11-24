"""把本批已通过、失败和跳过的项目写成同一份可搜索目录。"""
from html import escape
import json
from pathlib import Path
from string import Template
from urllib.parse import quote

STATUSES = {"passed": "通过", "failed": "失败", "skipped": "跳过"}
FILE_LABELS = {"result": "结果", "image": "截图", "video": "视频", "definition": "实际构造", "original": "原始构造"}
VIEW_LABELS = {"native": "原版", "worldfall": "三维旁观", "firstperson": "第一人称"}
INDEX_FILE = "index.json"


def short_reason(reason):
    prefix, separator, message = reason.partition(": ")
    if separator and prefix.endswith("Exception"):
        reason = message
    return reason.split("；可用", 1)[0]


def row(output, case, state, result=None, reason=""):
    """收录实际存在的资料，并保留请求参数供搜索和复验。"""
    result = result or {}
    key = case.get("key")
    files = {}
    if key:
        for label, name in (("result", "result.json"), ("image", "preview.png"), ("video", "demo.mp4"),
                            ("definition", "definition.json"), ("original", "source-definition.json")):
            path = f"{key}/{name}"
            if (label != "video" or state == "passed") and (output / path).is_file():
                files[label] = path
    parameters = {}
    for field, label in (("variant", "变体"), ("trajectory", "轨迹编号"), ("modifiers", "词条参数"),
                         ("constructionSource", "构造来源")):
        if field in case:
            parameters[label] = case[field]
    modes = {"base": "原始效果", "modifiers": "添加词条", "trajectory": "改变轨迹", "combined": "词条与轨迹",
             "cross-form": "跨形态", "material": "换材", "structure": "增加子部位"}
    mode = case.get("mode", "")
    label = case.get("label") or modes.get(mode, mode)
    variant = f"变体 {case['variant']}" if "variant" in case else ""
    facts = [label, variant, case.get("group"), case.get("trajectoryName") or case.get("trajectory"),
             VIEW_LABELS.get(result.get("view")), *case.get("modifierLabels", [])]
    name = case.get("name") or case["id"]
    detail = " · ".join(dict.fromkeys(filter(None, facts)))
    effect = (result.get("effect") or {}).get("confirmed")
    if state == "passed" and (result.get("execution") or {}).get("started"):
        reason = {True: "已出手，实际作用成立", False: "已出手，作用未成立", None: "已出手，作用待核对"}[effect]
    tags = [text for values in case.get("labels", {}).values() if isinstance(values, dict)
            for text in (*values.keys(), *values.values())]
    return dict(status=state, id=case["id"], name=name, key=key, mode=mode, label=label,
                detail=detail, summary=short_reason(reason), reason=reason, parameters=parameters, tags=tags, files=files)


def render_card(item):
    title, detail, summary = (escape(item[key]) for key in ("name", "detail", "summary"))
    parameters = json.dumps(item["parameters"], ensure_ascii=False, indent=2)
    search = " ".join([item["name"], item["id"], item["mode"], item["detail"], item["summary"], parameters, *item["tags"]])
    links = " ".join(link(path, FILE_LABELS[label]) for label, path in item["files"].items())
    image = item["files"].get("image")
    video = item["files"].get("video")
    preview = f'<video controls preload="none" poster="{quote(image or "")}" src="{quote(video)}"></video>' if video else ""
    params = f"<details><summary>施放参数</summary><pre>{escape(parameters)}</pre></details>" if item["parameters"] else ""
    explanation = f'<p class="outcome">{summary}</p>' if summary else ""
    if item["summary"] != item["reason"] and "result" not in item["files"]:
        explanation += f'<details><summary>完整原因</summary><p>{escape(item["reason"])}</p></details>'
    return (f'<article data-search="{escape(search)}"><h3>{title}</h3><p class="id">{escape(item["id"])}</p>'
            f"<p>{detail}</p>{explanation}{preview}{params}<p class=\"links\">{links}</p></article>")


def link(path, label):
    return f'<a href="{quote(path)}">{escape(label)}</a>'


def write_index(output, cases, results, skipped, failures, playlists, batch_error=None):
    """网页、文字目录和机器索引共用状态及文件链接。"""
    by_key = {case["key"]: case for case in cases}
    catalog_path = output / "catalog.json"
    spells = json.loads(catalog_path.read_text(encoding="utf-8"))["spells"] if catalog_path.is_file() else []
    by_id = {spell["id"]: spell for spell in spells}
    rows = {
        "passed": [row(output, case, "passed", results[case["key"]]) for case in cases if case["key"] in results],
        "failed": [row(output, failure["case"], "failed", reason=failure["reason"]) for failure in failures],
        "skipped": [],
    }
    for skip in skipped:
        case = by_key.get(skip.get("key"))
        if case is None:
            known = by_id.get(skip["id"], {})
            case = {field: known[field] for field in ("name", "group", "labels") if field in known}
            case.update(id=skip["id"], mode=skip["case"])
        rows["skipped"].append(row(output, case, "skipped", reason=skip["reason"]))
    navigation = [(label, path) for label, path in [
        ("批次参数", "manifest.json"), ("结束状态", "status.json"), ("中文目录", "演示目录.md"),
        ("目录数据", INDEX_FILE)] if path in {"演示目录.md", INDEX_FILE} or (output / path).is_file()]
    navigation.extend(playlists)
    indexed = dict(counts={status: len(items) for status, items in rows.items()}, entries=rows,
                   files={label: path for label, path in navigation}, batchError=batch_error)
    (output / INDEX_FILE).write_text(json.dumps(indexed, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    markdown = ["# 法术实机演示", "", *(f"- [{label}]({quote(path)})" for label, path in navigation
                                      if path != "演示目录.md"), ""]
    if batch_error:
        markdown.extend([f"批次中止。{short_reason(batch_error)}", ""])
    sections = []
    for status, items in rows.items():
        if not items:
            continue
        heading = f"{STATUSES[status]}（{len(items)}）"
        sections.append(f'<section data-status="{status}"><h2>{heading}</h2><div class="items">'
                        + "".join(render_card(item) for item in items) + "</div></section>")
        markdown.extend([f"## {heading}", ""])
        for item in items:
            artifacts = "　".join(f"[{FILE_LABELS[key]}]({quote(path)})" for key, path in item["files"].items())
            markdown.append(f"- {item['name']} · {item['detail']}　{item['summary']}　{artifacts}".rstrip())
        markdown.append("")
    (output / "演示目录.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")
    template = Template(Path(__file__).with_name("demo_index.html").read_text(encoding="utf-8"))
    (output / "index.html").write_text(template.substitute(
        summary=" · ".join(f"{STATUSES[status]} {count}" for status, count in indexed["counts"].items()),
        navigation=" ".join(link(path, label) for label, path in navigation),
        error=f'<p class="batch-error">{escape(short_reason(batch_error))}</p>' if batch_error else "",
        sections="".join(sections),
    ), encoding="utf-8")
