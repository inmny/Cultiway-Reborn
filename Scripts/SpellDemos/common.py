"""两个演示入口共用的游戏连接、用例选择、录像与目录生成。"""
import math
import argparse
import datetime
import html
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import zipfile

PROJECT = Path(__file__).resolve().parents[2]
CONTROL = Path(os.environ.get("CULTIWAY_DEMO_CONTROL", PROJECT / "artifacts/spell_demo"))
CLIENT = Path(os.environ.get("CULTIWAY_DEBUG_CLIENT", PROJECT.parent / "DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll"))
LABELS = {"base": "原始效果", "modifiers": "添加词条", "trajectory": "改变轨迹", "combined": "词条与轨迹"}


def version_stamp():
    """计算实际文件内容，未提交的代码和资源变化也会使旧录像失效。"""
    digest = hashlib.sha256()
    paths = [PROJECT / "mod.json"]
    for folder in ["Source", "Content", "GameResources", "Scripts/SpellDemos"]:
        paths.extend(path for path in (PROJECT / folder).rglob("*")
                     if path.is_file() and "__pycache__" not in path.parts)
    latest = 0
    for path in sorted(paths):
        relative = path.relative_to(PROJECT).as_posix()
        if relative == "mod.json":
            # 加载器会写回运行信息，只有模组声明参与比较。
            metadata = json.loads(path.read_text(encoding="utf-8"))
            content = json.dumps({key: metadata.get(key) for key in
                ["name", "author", "version", "description", "GUID", "RepoUrl", "iconPath", "tags"]}, sort_keys=True).encode()
        else:
            content = path.read_bytes()
        digest.update(relative.encode() + b"\0" + content + b"\0")
        if relative.startswith(("Source/", "Content/", "GameResources/")):
            latest = max(latest, path.stat().st_mtime)
    commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=PROJECT, capture_output=True, text=True, check=True).stdout.strip()
    return dict(sha256=digest.hexdigest(), commit=commit), latest


def same_recording(previous, current):
    """提交号用于追溯；内容未变的提交不要求重录，内容和参数必须一致。"""
    left = dict(previous, version=previous.get("version", {}).get("sha256"))
    right = dict(current, version=current["version"]["sha256"])
    return left == right


def write_json(path, value):
    """完整写入后替换，避免游戏读到半份请求。"""
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def export_inventory(output, version):
    """保存完整实际登记及人工可查的迁移清单，不加载测试世界或施放技能。"""
    data = json.loads((CONTROL / "inventory.json").read_text(encoding="utf-8"))
    data["version"] = version
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "inventory.json", data)
    groups = {}
    for entry in data["entries"]:
        groups.setdefault(entry["kind"], []).append(entry)
    lines = ["# 能力迁移基线", "", f"生成时间：{data['generatedAt']}；提交：`{version['commit']}`。", "",
             "本清单来自运行中的实际登记，迁移状态由实际执行入口标明。不同登记可能引用同一能力，不相加为可学习法术数量。", "",
             "| 登记类别 | 项数 |", "|---|---|"]
    lines.extend(f"| {kind} | {len(entries)} |" for kind, entries in groups.items())
    for kind, entries in groups.items():
        lines.extend(["", f"## {kind}", "", "| 编号 | 名称 | 迁移状态 |", "|---|---|---|"])
        for entry in entries:
            name = str(entry["name"]).replace("|", "\\|").replace("\n", " ")
            lines.append(f"| `{entry['key']}` | {name} | {entry['migration']} |")
    lines.extend(["", "## 尚需核对的来源", ""] + [f"- {item}" for item in data["pending"]])
    (output / "能力迁移清单.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"迁移清单：{len(data['entries'])} 项登记，{len(data['references'])} 项引用；输出 {output}", flush=True)


def rpc(*arguments):
    """只在安装工具时调用调试通道，不为每一帧重复编译脚本。"""
    result = subprocess.run(["dotnet", str(CLIENT), *arguments], capture_output=True, text=True, cwd=PROJECT)
    if result.returncode:
        raise RuntimeError(result.stderr or result.stdout)
    data = json.loads(result.stdout)
    if "Success" in data and not data["Success"]:
        raise RuntimeError(json.dumps(data, ensure_ascii=False, indent=2))
    return data.get("Result", data)


def connect(launch):
    """连接当前游戏；需要时用独立配置启动本机游戏。"""
    if not CLIENT.exists():
        raise RuntimeError("请先构建 DebugToolbox 客户端，见本目录 README.md")
    if launch and not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        # 握手成功就复用；连接失败且明确要求启动时才启动独立游戏。
        hello = subprocess.run(["dotnet", str(CLIENT), "hello"], capture_output=True, cwd=PROJECT)
        if hello.returncode:
            profile = CONTROL / "profile"
            profile.mkdir(parents=True, exist_ok=True)
            environment = dict(os.environ, XDG_CONFIG_HOME=str(profile))
            log = open(CONTROL / "game-console.log", "w", encoding="utf-8")
            subprocess.Popen([str(PROJECT.parents[1] / "worldbox"), "--class=ai-owned", "-logFile",
                              str(CONTROL / "game.log")], cwd=PROJECT.parents[1], env=environment,
                             stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            deadline = time.monotonic() + 240
            while time.monotonic() < deadline:
                check = subprocess.run(["dotnet", str(CLIENT), "hello"], capture_output=True, cwd=PROJECT)
                if check.returncode == 0:
                    break
                time.sleep(2)
            else:
                raise RuntimeError("游戏尚未就绪，请查看 artifacts/spell_demo/game.log")
    rpc("hello")
    deadline = time.monotonic() + 240
    ready = 'return !SmoothLoader.isLoading() && AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType("Cultiway.ModClass", false) != null);'
    while not rpc("eval", ready):
        if time.monotonic() >= deadline:
            raise RuntimeError("Cultiway 或游戏世界仍未加载完成，请检查游戏日志")
        time.sleep(2)
    rpc("run", str(Path(__file__).with_name("runtime.script")), "60000")
    return json.loads((CONTROL / "catalog.json").read_text(encoding="utf-8"))


def resolve(entries, name):
    """接受中文名、完整编号或编号末段，含糊的选择直接列出候选。"""
    matches = [entry for entry in entries if name.casefold() in
               {entry["id"].casefold(), entry["name"].casefold(), entry["id"].split(".")[-1].casefold()}]
    if len(matches) != 1:
        raise ValueError(f"找不到唯一项目：{name}。请用 --list 查看名称与编号。")
    return matches[0]


def read_constructions(path):
    """在文件入口读取完整资料；目录按相对路径排序，重复编号直接拒绝。"""
    files = sorted(path.rglob("*.json")) if path.is_dir() else [path]
    if not files:
        raise ValueError(f"构造目录没有 JSON 文件：{path}")
    entries, identifiers = [], set()
    for file in files:
        definition = json.loads(file.read_text(encoding="utf-8"))
        if not isinstance(definition, dict) or not isinstance(definition.get("Id"), str) or not definition["Id"].strip():
            raise ValueError(f"构造资料须为带非空 Id 的对象：{file}；完整规则由游戏内编译器检查")
        identifier = definition["Id"]
        if identifier in identifiers:
            raise ValueError(f"构造编号重复：{identifier}；文件：{file}")
        identifiers.add(identifier)
        entries.append(dict(id=identifier, name=definition.get("Name") or identifier, group="新构造验证",
                            construction=definition, constructionSource=file.relative_to(path).as_posix() if path.is_dir() else file.name))
    return entries


def construction_cases(entries, single_file):
    """每段携带自己的完整输入，继续共用录制、续录和目录生成。"""
    return [dict(entry, key="construction" if single_file else f"construction-{index:04d}",
                 label="真实施放", variant=0, modifiers=[], trajectory="构造资料")
            for index, entry in enumerate(entries, 1)]


def modifier_spec(text, catalog):
    """词条写作 巨化:Value=1.8，省略参数则使用游戏默认值。"""
    name, _, parameters = text.partition(":")
    modifier = resolve(catalog["modifiers"], name)
    spec = {"AssetId": modifier["id"], "Parameters": dict(modifier["parameters"])}
    for assignment in filter(None, parameters.split(",")):
        key, separator, value = assignment.partition("=")
        if not separator or key not in spec["Parameters"]:
            raise ValueError(f"词条参数无效：{assignment}；可用参数：{', '.join(spec['Parameters'])}")
        spec["Parameters"][key] = value
    return spec


def choose_cases(spells, catalog, args):
    """按游戏导出的兼容结果选择变化，不把不支持的轨迹硬塞给法术。"""
    rows, skipped = [], []
    modifier_by_id = {entry["id"]: entry for entry in catalog["modifiers"]}
    trajectory_by_id = {entry["id"]: entry for entry in catalog["trajectories"]}
    for spell in spells:
        variants = range(spell["variants"]) if args.variants == "all" else [args.variant]
        for variant in variants:
            if not 0 <= variant < spell["variants"]:
                raise ValueError(f"{spell['name']} 的变体范围为 0～{spell['variants'] - 1}")
            compatible = spell["modifiers"]
            specs = [modifier_spec(text, catalog) for text in args.modifier]
            if not specs:
                # 选择有明显观感变化的词条，攻击和辅助法术均遵守各自兼容限制。
                for preferred in ["Huge", "Volley", "Haste"]:
                    entry = next((modifier_by_id[key] for key in compatible
                                  if key.split(".")[-1] == preferred), None)
                    if entry:
                        spec = {"AssetId": entry["id"], "Parameters": dict(entry["parameters"])}
                        if preferred == "Huge":
                            spec["Parameters"]["Value"] = "1.8"
                        specs.append(spec)
                    if len(specs) == 2:
                        break
            alternatives = spell["trajectories"]
            trajectory = resolve(catalog["trajectories"], args.trajectory)["id"] if args.trajectory else None
            if not trajectory:
                priorities = ["SineWave", "SpiralHoming", "RainFall", "TrackingBeam", "FieldFollowTarget", "FieldAdvance", "SlowVortex"]
                trajectory = next((key for name in priorities for key in alternatives
                                   if key.split(".")[-1] == name and key != spell["trajectory"]), None)
                trajectory = trajectory or next((key for key in alternatives if key != spell["trajectory"]), None)
            modes = args.cases.split(",")
            for mode in modes:
                if mode not in LABELS:
                    raise ValueError(f"未知演示类型：{mode}")
                if mode in {"modifiers", "combined"} and (not spell["adjustable"] or not specs):
                    skipped.append({"id": spell["id"], "case": mode, "reason": "来源专属实体或没有兼容词条"})
                    continue
                if mode in {"trajectory", "combined"} and (not spell["adjustable"] or not trajectory):
                    skipped.append({"id": spell["id"], "case": mode, "reason": "没有可替换的兼容轨迹"})
                    continue
                applied = specs if mode in {"modifiers", "combined"} else []
                actual_trajectory = trajectory if mode in {"trajectory", "combined"} else spell["trajectory"]
                if actual_trajectory not in alternatives and actual_trajectory != spell["trajectory"]:
                    raise ValueError(f"{spell['name']} 不支持轨迹 {actual_trajectory}")
                for spec in applied:
                    if spec["AssetId"] not in compatible:
                        raise ValueError(f"{spell['name']} 不支持词条 {spec['AssetId']}")
                key = f"{len(rows) + 1:04d}-{spell['id'].removeprefix('Cultiway.')}-v{variant}-{mode}"
                rows.append(dict(key=key, id=spell["id"], name=spell["name"], group=spell["group"],
                                 mode=mode, label=LABELS[mode], variant=variant,
                                 trajectory=actual_trajectory, modifiers=applied,
                                 trajectoryName=trajectory_by_id.get(actual_trajectory, {"name": actual_trajectory})["name"],
                                 modifierLabels=[modifier_by_id[spec["AssetId"]]["name"] for spec in applied]))
    return rows, skipped


def encode(output, case, keep_frames):
    """按世界时间编码，标题直接写明词条与轨迹，避免误读为原始招式。"""
    folder = output / case["key"]
    data = json.loads((folder / "result.json").read_text(encoding="utf-8"))
    if not data["times"]:
        raise RuntimeError(f"录像没有截图：{case['key']}")
    lines = ["ffconcat version 1.0"]
    for index, at in enumerate(data["times"]):
        path = folder / "frames" / f"{index:05d}.png"
        until = data["times"][index + 1] if index + 1 < len(data["times"]) else data["duration"]
        lines.extend(["file '" + str(path).replace("'", "'\\''") + "'", "option framerate 1000",
                      f"duration {max(.001, until - at):.6f}"])
    lines.extend(["file '" + str(path).replace("'", "'\\''") + "'", "option framerate 1000"])
    (folder / "frames.ffconcat").write_text("\n".join(lines) + "\n", encoding="utf-8")
    modifier_names = "、".join(case.get("modifierLabels", [spec["AssetId"].split(".")[-1] for spec in case["modifiers"]])) or "无额外词条"
    title = f"{case['name']} · {case['label']} · 变体 {case['variant']}\n轨迹：{case.get('trajectoryName', case['trajectory'].split('.')[-1])}　词条：{modifier_names}"
    view = {"native": "原版", "worldfall": "三维旁观", "firstperson": "第一人称"}[data.get("view", "native")]
    if data.get("lighting") == "night":
        view += " · 夜景"
    if data.get("scene") == "battle":
        title = f"修士自主交战 · {view}\n法术：{'、'.join(case['spellNames'])}"
    elif data.get("scene") == "construction":
        resource = {"none": "无消耗", "wakan": "真元", "vigor": "斗气", "mana": "法力", "stamina": "体力"}[data["resource"]]
        detail = case.get('caption', '真实接触验证')
        title = f"{case['name']} · {view} · 实际施放\n消耗：{data['paid']:g} {resource}　{detail}"
    elif data.get("scene") == "resources":
        title = f"形体资源接入验证 · {view}\n独立部件、表面坐标、材质和连续姿态；不代表战斗或美术验收"
    else:
        title = title.replace(" · " + case['label'], " · " + view + " · " + case['label'], 1)
    (folder / "title.txt").write_text(title, encoding="utf-8")
    font_arg = f":fontfile='{caption_font()}'"
    video_filter = f"scale=1280:-2,pad=iw:ih+84:0:84:color=0x111820,drawtext=textfile='{folder / 'title.txt'}'{font_arg}:fontsize=22:fontcolor=white:x=18:y=12:line_spacing=8"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", str(folder / "frames.ffconcat"),
                    "-vf", video_filter, "-fps_mode", "vfr", "-c:v", "libx264", "-threads", "2", "-crf", "19",
                    "-pix_fmt", "yuv420p", str(folder / "demo.mp4")], check=True)
    # 构造可能早已命中并结束，不能用空白收尾作为缩略图。
    visible = [index for index, frame in enumerate(data.get("materialFrames", [])) if frame["forms"]]
    preview = data.get("previewFrame", visible[len(visible) // 2] if visible else len(data["times"]) // 2)
    shutil.copy2(folder / "frames" / f"{preview:05d}.png", folder / "preview.png")
    if not keep_frames:
        shutil.rmtree(folder / "frames")
    return data


def caption_font():
    """选择实际存在的中文字体，避免把缺字方框写进成片。"""
    candidates = [Path("/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc"),
                  Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"),
                  Path("/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"),
                  Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/msyh.ttc",
                  Path("/System/Library/Fonts/PingFang.ttc")]
    for path in candidates:
        if path.exists():
            return path
    raise RuntimeError("未找到中文字体，请安装 Noto Sans CJK 后再录制")


def publish(output, cases, results, skipped):
    """生成逐招网页、中文目录、分类合集和完整下载包。"""
    body, markdown = [], ["# 法术实机演示", "", "单招标题标明变体、词条与轨迹；自主交战由游戏 AI 出手。通过标记只表示运行检查通过，美术验收须观看录像。", ""]
    groups = {}
    for case in cases:
        data = results.get(case["key"])
        if data is None:
            continue
        video = case["key"] + "/demo.mp4"
        title = f"{case['name']} · {case['label']} · 变体 {case['variant']}"
        details = case.get("trajectoryName", case["trajectory"]) + " · " + ("、".join(case.get("modifierLabels", [])) or "无额外词条")
        body.append(f'<article><h2>{html.escape(title)}</h2><p>{html.escape(case["id"])} · {html.escape(details)}</p><video controls preload="none" poster="{case["key"]}/preview.png" src="{video}"></video></article>')
        markdown.append(f"- [{title}]({video})")
        groups.setdefault(case["group"], []).append((output / video).resolve())
    for group, videos in groups.items():
        concat = output / f"{group}.ffconcat"
        concat.write_text("ffconcat version 1.0\n" + "\n".join("file '" + str(path).replace("'", "'\\''") + "'" for path in videos) + "\n", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", str(concat),
                        "-c", "copy", "-movflags", "+faststart", str(output / f"{group}-合集.mp4")], check=True)
        markdown.insert(4, f"- [{group}合集]({group}-合集.mp4)")
    if len(results) > 1:
        playlist = output / "全部法术.ffconcat"
        playlist.write_text("ffconcat version 1.0\n" + "\n".join("file '" + str((output / case["key"] / "demo.mp4").resolve()).replace("'", "'\\''") + "'" for case in cases if case["key"] in results) + "\n", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", str(playlist),
                        "-c", "copy", "-movflags", "+faststart", str(output / "全部法术-总览.mp4")], check=True)
        markdown.insert(4, "- [全部法术总览](全部法术-总览.mp4)")
    if skipped:
        markdown.extend(["", f"跳过 {len(skipped)} 项不适用变化，具体原因见 manifest.json。"])
    (output / "演示目录.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")
    navigation = ('<a href="全部法术-总览.mp4">全部总览</a>　' if len(results) > 1 else "") + "　".join(f'<a href="{html.escape(group)}-合集.mp4">{html.escape(group)}</a>' for group in groups)
    (output / "index.html").write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>法术实机演示</title><style>body{margin:30px;background:#111820;color:#eee;font-family:sans-serif}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(500px,1fr));gap:24px}article{background:#202b38;padding:16px;border-radius:10px}h2{font-size:19px}p{font-size:12px;color:#b8c6d6}video{width:100%}a{color:#9bd6ff}nav{margin-bottom:20px}input{width:95%;padding:12px;margin-bottom:20px}</style><h1>法术实机演示</h1><nav>' + navigation + '</nav><input placeholder="搜索中文名、编号、词条或轨迹" oninput="for(const a of document.querySelectorAll(\'article\'))a.hidden=!a.textContent.toLowerCase().includes(this.value.toLowerCase())"><main>' + "".join(body) + "</main></html>", encoding="utf-8")
    with zipfile.ZipFile(output / "全部演示.zip", "w", compression=zipfile.ZIP_STORED) as archive:
        for file in output.rglob("*"):
            if file.is_file() and file.suffix in {".mp4", ".json", ".md", ".html", ".png"}:
                archive.write(file, file.relative_to(output))


def main(all_spells):
    """命令入口；所有文件都放进独立输出目录。"""
    parser = argparse.ArgumentParser(description=__doc__)
    if not all_spells:
        parser.add_argument("spell", nargs="?", help="中文名、完整编号或编号末段")
    parser.add_argument("--list", action="store_true", help="列出游戏中的法术、词条与轨迹")
    parser.add_argument("--inventory", action="store_true", help="导出全部登记、法宝被动事件、器官引用及迁移清单，不施法或录像")
    parser.add_argument("--resource-probe", action="store_true", help="验证共用形体、材质和连续姿态，单独标记为开发资源验证")
    parser.add_argument("--construction", type=Path, help="构造 JSON 文件或目录；目录递归遍历，单招入口可按名称或编号选择")
    parser.add_argument("--opponent-construction", type=Path, help="构造演示中让敌方目标经正式入口同时施放另一份构造，用于交锋和损耗验证")
    parser.add_argument("--isolate-target", action="store_true", help="构造演示只保留主目标在交锋区域，其他测试人物移到远处")
    parser.add_argument("--launch", action="store_true", help="没有运行中的游戏时，用独立配置启动")
    parser.add_argument("--scope", choices=["base", "all"], default="all", help="base 为基础法术，all 还含来源专属与天劫实体")
    parser.add_argument("--filter", default="", help="按名称或编号筛选，可用逗号分隔")
    parser.add_argument("--cases", default="base,modifiers,trajectory", help="base,modifiers,trajectory,combined")
    parser.add_argument("--modifier", action="append", default=[], help="词条名称:参数=值，可重复指定")
    parser.add_argument("--trajectory", help="指定兼容轨迹名称或编号")
    parser.add_argument("--variant", type=int, default=0, help="单个动画变体编号")
    parser.add_argument("--variants", choices=["first", "all"], default="first")
    parser.add_argument("--duration", type=float, default=3, help="每段世界秒数")
    parser.add_argument("--fps", type=int, default=20, help="每世界秒截图上限")
    parser.add_argument("--battle-artifact", help="自主交战时装备一件指定主动能力的法宝，由 AI 决定使用")
    parser.add_argument("--speed", choices=[1, 2, 3, 5], type=int, default=1, help="游戏自身的模拟速度；成片仍按世界时间播放")
    parser.add_argument("--motion-trace", action="store_true", help="随单招原帧记录真实外形、方向、身体姿态和阻挡")
    parser.add_argument("--diagnostics", action="store_true", help="记录大块黄色精灵及粒子的材质来源")
    parser.add_argument("--view", choices=["native", "worldfall", "firstperson"], default="native", help="原版俯视、Worldfall 三维观察或实际附身第一人称")
    parser.add_argument("--lighting", choices=["day", "night"], default="day", help="Worldfall固定昼夜条件，用于材质和自发光对照")
    parser.add_argument("--scene", choices=["single", "shield", "battle"], default="single", help="单招、护盾迎击或真正自主交战")
    parser.add_argument("--shield-break", action="store_true", help="迎击录像先挡住小石弹，再用真实重击打穿")
    parser.add_argument("--realm", type=int, default=2, choices=range(5), help="修炼境界 0～4")
    parser.add_argument("--caster-walk", type=float, default=0, help="构造演示中让施法者通过原版附体移动沿地图X轴行走指定格数")
    parser.add_argument("--target-offset", type=float, nargs=2, metavar=("X", "Y"), help="单招或迎击场景中，目标相对施法者的地图位置")
    parser.add_argument("--terrain-slope", type=float, default=0, help="每格地图X方向增加的原版地形高度，范围 -4 至 4")
    parser.add_argument("--zoom", type=float, default=22, help="镜头缩放；城域演示建议 38")
    parser.add_argument("--power", type=float, default=6, help="测试人物战力，默认便于观察单招")
    parser.add_argument("--keep-frames", action="store_true")
    parser.add_argument("--resume", action="store_true", help="复用指定输出目录中已经完成的录像")
    parser.add_argument("--output", type=Path, help="独立输出目录")
    args = parser.parse_args()
    if args.duration <= 0 or not 1 <= args.fps <= 60 or args.power <= 0 or args.zoom <= 0:
        parser.error("时长、战力须大于零，截图频率须为 1～60")
    if args.opponent_construction and not args.construction:
        parser.error("--opponent-construction 需要同时指定 --construction")
    if (args.caster_walk or args.isolate_target) and not args.construction:
        parser.error("行走和移开旁观者只用于 --construction")
    if args.shield_break and args.scene != "shield":
        parser.error("--shield-break 只用于迎击场景")
    if args.target_offset and args.scene == "battle":
        parser.error("--target-offset 只用于单招或迎击场景")
    if not math.isfinite(args.terrain_slope) or abs(args.terrain_slope) > 4:
        parser.error("测试地形坡度须为 -4 至 4 的有限数")
    if not math.isfinite(args.caster_walk) or abs(args.caster_walk) > 20 or (args.target_offset and any(not math.isfinite(v) or abs(v) > 20 for v in args.target_offset)):
        parser.error("行走和目标偏移须为有限数，范围为 -20 至 20 格")
    if args.lighting == "night" and args.view == "native":
        parser.error("--lighting night 需要 --view worldfall 或 firstperson")
    CONTROL.mkdir(parents=True, exist_ok=True)
    recording_requested = False
    try:
        constructions = read_constructions(args.construction) if args.construction else None
        if constructions is not None:
            if args.resource_probe or args.inventory or args.scene != "single":
                raise ValueError("构造演示请使用单招场景，不与资源探针或清单导出同时运行")
            if args.modifier or args.trajectory or args.variant or args.variants != "first" or args.cases not in {"base", "base,modifiers,trajectory"}:
                raise ValueError("构造的词条和轨迹变化请写入各自资料；旧词条、轨迹和动画变体参数只用于旧法术")
            if args.list:
                for entry in constructions:
                    print(f"{entry['name']}\t{entry['id']}\t{entry['constructionSource']}")
                return
            if not all_spells:
                if args.spell:
                    constructions = [resolve(constructions, args.spell)]
                elif args.construction.is_dir():
                    raise ValueError("请指定目录中的构造名称或编号，或用 demo_all.py 遍历整个目录")
            if args.filter:
                needles = args.filter.casefold().split(",")
                constructions = [entry for entry in constructions if any(needle in (entry["id"] + entry["name"]).casefold() for needle in needles)]
            if not constructions:
                raise ValueError("筛选后没有构造资料")
        opponent = read_constructions(args.opponent_construction) if args.opponent_construction else None
        if opponent is not None:
            if len(opponent) != 1 or any(entry["id"] == opponent[0]["id"] for entry in constructions):
                raise ValueError("对手构造需要独立的单份资料，编号不能与本批任何主构造重复")
            opponent = opponent[0]["construction"]
        starting_version, _ = version_stamp()
        catalog = connect(args.launch)
        if args.inventory:
            export_inventory((args.output or CONTROL / "inventory").resolve(), starting_version)
            return
        if args.list:
            for entry in catalog["spells"]:
                print(f"{entry['name']}\t{entry['id']}\t{entry['group']}\t{entry['variants']} 个变体")
            for category in ["modifiers", "trajectories"]:
                print("\n" + ("词条" if category == "modifiers" else "轨迹"))
                for entry in catalog[category]:
                    print(entry["name"], entry["id"], entry.get("parameters", ""))
            return
        if constructions is not None:
            spells = constructions
        elif args.resource_probe:
            spells = [dict(id="Cultiway.ResourceProbe", name="形体资源接入验证", group="开发资源验证")]
        elif all_spells:
            spells = catalog["spells"]
            if args.scope == "base":
                spells = [entry for entry in spells if entry["adjustable"]]
            if args.filter:
                needles = args.filter.casefold().split(",")
                spells = [entry for entry in spells if any(needle in (entry["id"] + entry["name"]).casefold() for needle in needles)]
        else:
            if not args.spell:
                parser.error("请指定法术名称，或使用 --list")
            spells = [resolve(catalog["spells"], args.spell)]
        if not spells:
            parser.error("筛选后没有法术")
        if constructions is not None:
            cases, skipped = construction_cases(constructions, args.construction.is_file()), []
        elif args.resource_probe:
            cases, skipped = [dict(key="resource-probe", id="Cultiway.ResourceProbe", name="形体资源接入验证",
                group="开发资源验证", label="模型与材质", variant=0, modifiers=[], trajectory="共同姿态")], []
        else:
            cases, skipped = choose_cases(spells, catalog, args)
        if not cases:
            parser.error("没有可用演示，请检查 --cases 和兼容范围")
        if args.scene == "battle":
            if any(not spell["adjustable"] for spell in spells) or args.cases != "base":
                parser.error("自主交战请筛选基础法术并指定 --cases base；每位修士会学会筛选出的全部法术")
            cases = [dict(cases[0], key="自主交战", name="修士自主交战", label="自主交战", spellIds=[spell["id"] for spell in spells], spellNames=[spell["name"] for spell in spells])]
        if args.scene == "shield" and args.cases != "base":
            parser.error("护盾迎击请使用 --cases base")
        output = (args.output or CONTROL / datetime.datetime.now().strftime("%Y%m%d-%H%M%S")).resolve()
        output.mkdir(parents=True, exist_ok=True)
        battle_artifact = resolve(catalog["spells"], args.battle_artifact)["id"] if args.battle_artifact else None
        if battle_artifact and (args.scene != "battle" or not battle_artifact.startswith("Cultiway.ArtifactAbility.")):
            raise ValueError("--battle-artifact 只用于自主交战，并须指定法宝主动能力")
        version, latest = version_stamp()
        if version["sha256"] != starting_version["sha256"]:
            raise ValueError("连接和准备期间代码或资源发生了修改，请重新运行")
        if latest > catalog["processStarted"]:
            raise ValueError("游戏启动后代码或资源发生了修改，请重启游戏再录制，避免把旧程序录为新效果")
        fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
        if not (fixture / "map.wbox").is_file():
            raise ValueError(f"缺少演示测试存档：{fixture}")
        manifest = dict(version=version, fixture=str(fixture), motionTrace=args.motion_trace, cases=cases, skipped=skipped, duration=args.duration, fps=args.fps, power=args.power, realm=args.realm, zoom=args.zoom, view=args.view, lighting=args.lighting, scene=args.scene, diagnostics=args.diagnostics, speed=args.speed, battleArtifact=battle_artifact)
        if args.shield_break:
            manifest["shieldBreak"] = True
        if args.target_offset or constructions is not None:
            manifest["targetOffset"] = args.target_offset
        if args.terrain_slope:
            manifest["terrainSlope"] = args.terrain_slope
        if constructions is not None:
            manifest["casterWalk"] = args.caster_walk
            manifest["isolateTarget"] = args.isolate_target
            if opponent is not None:
                manifest["opponentConstruction"] = opponent
        if (output / "manifest.json").exists():
            previous = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
            if args.resume and not same_recording(previous, manifest):
                raise ValueError("代码、资源版本或续录参数与原清单不同，请重新录制或使用新的输出目录")
            if not args.resume:
                current_keys = {case["key"] for case in cases}
                for old in previous["cases"]:
                    folder = output / old["key"]
                    if old["key"] not in current_keys and folder.exists():
                        shutil.rmtree(folder)
                generated = ["全部演示.zip", "全部法术-总览.mp4", "index.html", "演示目录.md"]
                generated.extend(group + "-合集.mp4" for group in {case["group"] for case in previous["cases"]})
                for filename in generated:
                    (output / filename).unlink(missing_ok=True)
        write_json(output / "catalog.json", catalog)
        write_json(output / "manifest.json", manifest)
        for stale in [output / "status.json", CONTROL / "cancel"]:
            stale.unlink(missing_ok=True)
        if not args.resume:
            for case in cases:
                for filename in ["result.json", "demo.mp4"]:
                    (output / case["key"] / filename).unlink(missing_ok=True)
        remaining = [case for case in cases if not (args.resume and (output / case["key"] / "demo.mp4").exists())]
        for case in remaining:
            (output / case["key"] / "result.json").unlink(missing_ok=True)
        request = dict(manifest, id=str(time.time_ns()), action="construction" if constructions is not None else "resources" if args.resource_probe else "record", output=str(output), cases=remaining)
        write_json(CONTROL / "request.json", request)
        recording_requested = True
        print(f"开始：{len(spells)} 个演示项目，{len(remaining)} 段待录；输出 {output}", flush=True)
        results, last_change, last_status = {}, time.monotonic(), ""
        while len(results) < len(cases):
            for case in cases:
                report = output / case["key"] / "result.json"
                if case["key"] not in results and report.exists():
                    data = json.loads(report.read_text(encoding="utf-8"))
                    if not data.get("passed"):
                        raise RuntimeError(f"演示失败：{case['name']}；查看 {report}")
                    cached = args.resume and (report.parent / "demo.mp4").exists()
                    if not cached:
                        data = encode(output, case, args.keep_frames)
                    results[case["key"]] = data
                    last_change = time.monotonic()
                    if not cached:
                        print(f"[{len(results)}/{len(cases)}] {case['name']} · {case['label']}：{data['frames']} 帧，{data['duration']:.2f} 世界秒", flush=True)
            status_file = output / "status.json"
            if status_file.exists():
                status = json.loads(status_file.read_text(encoding="utf-8"))
                if status.get("error"):
                    raise RuntimeError(status["error"])
                marker = json.dumps(status, ensure_ascii=False)
                if marker != last_status:
                    last_status, last_change = marker, time.monotonic()
                if status.get("done") and len(results) < len(cases):
                    raise RuntimeError("游戏已结束录制，但存在缺失结果，请查看 status.json")
            if time.monotonic() - last_change > 180:
                raise RuntimeError("三分钟没有进展，请检查游戏窗口和 status.json；可用 --resume 继续")
            time.sleep(.5)
        # 最后一段落盘后，等待游戏完成回收和界面恢复。
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            if not (output / "status.json").exists():
                time.sleep(.1)
                continue
            status = json.loads((output / "status.json").read_text(encoding="utf-8"))
            if status.get("error"):
                raise RuntimeError(status["error"])
            if status.get("done"):
                break
            time.sleep(.5)
        else:
            raise RuntimeError("录像已完成，但游戏尚未完成收尾，请查看 status.json")
        if version_stamp()[0]["sha256"] != version["sha256"]:
            raise ValueError("录制期间代码或资源发生了修改，本目录不可作为同一版本的验收结果，请重新录制")
        publish(output, cases, results, skipped)
        print(f"完成：{output / '演示目录.md'}", flush=True)
    except KeyboardInterrupt:
        if recording_requested:
            (CONTROL / "cancel").touch()
        parser.exit(130, "已请求游戏结束演示并恢复界面。\n")
    except (OSError, RuntimeError, ValueError, subprocess.CalledProcessError) as error:
        if recording_requested:
            (CONTROL / "cancel").touch()
        parser.exit(1, str(error) + "\n")
