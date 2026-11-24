"""两个演示入口共用的游戏连接、用例选择、录像与目录生成。"""
import math
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import zipfile

from demo_index import INDEX_FILE, write_index

PROJECT = Path(__file__).resolve().parents[2]
CONTROL = Path(os.environ.get("CULTIWAY_DEMO_CONTROL", PROJECT / "artifacts/spell_demo"))
CLIENT = Path(os.environ.get("CULTIWAY_DEBUG_CLIENT", PROJECT.parent / "DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll"))
LABELS = {"base": "原始效果", "modifiers": "添加词条", "trajectory": "改变轨迹", "combined": "词条与轨迹"}
CONSTRUCTION_ONLY = {"cross-form": "跨形态", "material": "换材", "structure": "增加子部位"}
MOD_INPUT_DIRS = ("Source", "Content", "GameResources", "Locales", "Assemblies")
MOD_INPUT_FILES = ("mod.json", "icon.png", "default_config.json")
FIXTURE_DIR = "Scripts/WorldSaveTests/Fixtures/world-format-1"


def demo_modes(text):
    modes = list(dict.fromkeys(part.strip() for part in text.split(",")))
    for mode in modes:
        if mode not in LABELS and mode not in CONSTRUCTION_ONLY:
            raise ValueError(f"未知演示类型：{mode}")
    return modes


def input_fingerprint(root, directories, files=()):
    """以相对路径和文件内容核对输入，独立游戏可使用相同内容的安装副本。"""
    digest = hashlib.sha256()
    paths = [root / name for name in files if (root / name).is_file()]
    for folder in directories:
        paths.extend(path for path in (root / folder).rglob("*")
                     if path.is_file() and "__pycache__" not in path.parts)
    latest = 0
    for path in sorted(paths):
        relative = path.relative_to(root).as_posix()
        if relative == "mod.json":
            # 加载器会写回运行信息，只有模组声明参与比较。
            metadata = json.loads(path.read_text(encoding="utf-8"))
            content = json.dumps({key: metadata.get(key) for key in
                ["name", "author", "version", "description", "GUID", "RepoUrl", "iconPath", "tags"]}, sort_keys=True).encode()
        else:
            content = path.read_bytes()
        digest.update(relative.encode() + b"\0" + content + b"\0")
        if relative != "mod.json":
            latest = max(latest, path.stat().st_mtime)
    return digest.hexdigest(), latest


def version_stamp():
    """记录准备录制的模组、工具和测试世界，提交号只用于追溯。"""
    mod, latest = input_fingerprint(PROJECT, MOD_INPUT_DIRS, MOD_INPUT_FILES)
    tools, _ = input_fingerprint(PROJECT, ("Scripts/SpellDemos", FIXTURE_DIR))
    digest = hashlib.sha256(f"{mod}:{tools}".encode()).hexdigest()
    commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=PROJECT, capture_output=True, text=True, check=True).stdout.strip()
    return dict(sha256=digest, modSha256=mod, toolAndFixtureSha256=tools, commit=commit), latest


def verify_loaded_inputs(catalog, version):
    """以运行游戏报告的安装目录为准，核对实际加载所用的文件内容。"""
    mod_path = catalog.get("modPath")
    if not mod_path or not (Path(mod_path) / "mod.json").is_file():
        raise ValueError("游戏未报告可核对的实际模组目录")
    observed, _ = input_fingerprint(Path(mod_path), MOD_INPUT_DIRS, MOD_INPUT_FILES)
    if observed != version["modSha256"]:
        raise ValueError("游戏所读的模组代码或资源与录制输入不同，请用当前版本的独立游戏")


def same_recording(previous, current):
    """提交号用于追溯；内容未变的提交不要求重录，内容和参数必须一致。"""
    return recording_parameters(previous) == recording_parameters(current)


def recording_parameters(manifest):
    """比较可复现的输入，忽略只用于查询提交历史的名称。"""
    return dict(manifest, version=manifest.get("version", {}).get("sha256"))


def file_sha256(path):
    """核对实际文件，可用于较大的影片。"""
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def recording_tools():
    """记录本次实际调用的调试客户端、脚本解释器和编码器。"""
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise ValueError("找不到录制用的 ffmpeg")
    encoder = subprocess.run([ffmpeg, "-version"], capture_output=True, text=True, check=True).stdout
    client_files = [path.name for path in CLIENT.parent.iterdir()
                    if path.is_file() and path.suffix in (".dll", ".json")]
    client, _ = input_fingerprint(CLIENT.parent, (), client_files)
    return dict(client=client, encoder=encoder, encoderSha256=file_sha256(Path(ffmpeg)),
                python=sys.version, captionFontSha256=file_sha256(caption_font()))


def recording_environment(manifest):
    """不同招式可以有各自参数，同一合集须使用相同运行环境。"""
    return {name: manifest[name] for name in ("version", "runtime", "recordingTools", "view")}


def recording_stamp(manifest):
    """把每段凭据绑定到这批的实际输入、运行版本和录制参数。"""
    parameters = json.dumps(recording_parameters(manifest), sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(parameters.encode()).hexdigest()


def clip_facts(output, key, batch):
    """通过已验收的录像、结果、缩略图及独立构造资料重建这段的凭据。"""
    folder = output / key
    names = ["demo.mp4", "result.json", "preview.png"]
    names.extend(name for name in ("definition.json", "source-definition.json") if (folder / name).is_file())
    return dict(case=key, batch=batch, files={name: file_sha256(folder / name) for name in names})


def checked_clip(output, key, batch):
    """续录先证实原片完整，拒绝失去凭据或已经修改的旧片。"""
    try:
        known = json.loads((output / key / "clip.json").read_text(encoding="utf-8"))
        if known == clip_facts(output, key, batch):
            return known
    except (OSError, ValueError):
        pass
    raise ValueError(f"旧片段与本批凭据不符：{key}。请用新目录录制")


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
    return install_recorder(rpc)


def run_with_runtime(call, script):
    """临时调试脚本共用实际运行身份的查询，不改变发布给玩家的模组。"""
    identity = Path(__file__).with_name("runtime_identity.script")
    with tempfile.TemporaryDirectory() as folder:
        program = Path(folder) / "recording.script"
        program.write_text(script + "\n" + identity.read_text(encoding="utf-8"), encoding="utf-8")
        return call("run", str(program), "60000")


def install_recorder(call, profile=None):
    if profile is None:
        profile = Path(os.environ["CULTIWAY_DEV_PROFILE"]) if os.environ.get("CULTIWAY_DEV_ACTIVE") else CONTROL / "profile"
    script = Path(__file__).with_name("runtime.script").read_text(encoding="utf-8")
    script = script.replace("/* TEST_PROFILE */ null", json.dumps(str(profile.resolve())))
    run_with_runtime(call, script)
    return json.loads((CONTROL / "catalog.json").read_text(encoding="utf-8"))


def specialized_recording(call, view):
    """专用录制同样核对游戏实际所读的安装目录、程序集和录制工具。"""
    catalog = run_with_runtime(call, """return new {
        modPath = Cultiway.ModClass.I.GetDeclaration().FolderPath,
        runtime = CultiwayDemoRuntime.Loaded()
    };""")
    version, _ = version_stamp()
    verify_loaded_inputs(catalog, version)
    return dict(view=view, version=version, runtime=catalog["runtime"], recordingTools=recording_tools())


def verify_specialized_recording(call, recording):
    if not same_recording(recording, specialized_recording(call, recording["view"])):
        raise ValueError("录制期间实际游戏、模组或工具版本发生了变化")


def resolve(entries, name):
    """接受中文名、完整编号或编号末段，含糊的选择直接列出候选。"""
    matches = [entry for entry in entries if name.casefold() in
               {entry["id"].casefold(), entry["name"].casefold(), entry["id"].split(".")[-1].casefold()}]
    if len(matches) != 1:
        raise ValueError(f"找不到唯一项目：{name}。请用 --list 查看名称与编号。")
    return matches[0]


CATALOG_FILTERS = dict(source="来源", school="流派", scale="尺度", effect="作用")


def filter_catalog(entries, args):
    """名称部分匹配。每个维度内任一值命中，多个维度需要同时满足。"""
    def terms(text):
        return {value.strip().casefold() for value in text.split(",") if value.strip()}

    names = terms(args.filter)
    dimensions = {key: terms(getattr(args, key)) for key in CATALOG_FILTERS if getattr(args, key)}
    matches = []
    for entry in entries:
        if args.scope == "base" and not entry["adjustable"]:
            continue
        if names and not any(word in entry["id"].casefold() or word in entry["name"].casefold() for word in names):
            continue
        labels = entry.get("labels", {})
        for key, wanted in dimensions.items():
            values = labels.get(key) or {"unmarked": "未标注"}
            indexed = {value.casefold() for value in (*values.keys(), *values.values())}
            if wanted and indexed.isdisjoint(wanted):
                break
        else:
            matches.append(entry)
    return matches


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


def construction_case(entry, key, mode):
    """同一份完整原式配上改造类型，交给游戏生成并校验独立候选。"""
    label = {"base": LABELS["base"], "modifiers": "巨化", "trajectory": "波形轨迹",
             "combined": "巨化与波形轨迹", **CONSTRUCTION_ONLY}[mode]
    return dict(entry, key=key, mode=mode, label=label, variant=0, modifiers=[],
                trajectory=entry.get("trajectory", "构造资料"))


def construction_cases(entries, single_file, modes):
    rows = []
    for entry in entries:
        for mode in modes:
            key = "construction" if single_file and len(modes) == 1 else f"construction-{len(rows) + 1:04d}"
            rows.append(construction_case(entry, key, mode))
    return rows


def battle_case(left, right):
    """双方从真实目录独立取原式，保存全部构造以便重载后精确授予。"""
    by_id = {}
    for spell in [*left, *right]:
        reason = spell.get("unavailable")
        if reason or "construction" not in spell and not spell.get("adjustable"):
            raise ValueError(f"无法用于自主交战：{spell['name']}，{reason or '需要对应的实际能力来源'}")
        entry = {key: spell[key] for key in ("id", "name", "construction") if key in spell}
        if by_id.setdefault(spell["id"], entry) != entry:
            raise ValueError(f"交战两边的同编号能力资料不一致：{spell['id']}")
    sides = [[by_id[identifier] for identifier in dict.fromkeys(spell["id"] for spell in side)]
             for side in (left, right)]
    names = list(dict.fromkeys(spell["name"] for side in sides for spell in side))
    return dict(id="Cultiway.SpellDemo.Battle", key="自主交战", name="修士自主交战", group="自主交战",
                label="自主交战", variant=0, modifiers=[], trajectory="自主选招", battleSides=sides,
                spellIds=[spell["id"] for spell in left], spellNames=names)


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
    modes = demo_modes(args.cases)
    legacy_modes = [mode for mode in modes if mode in LABELS]
    modifier_by_id = {entry["id"]: entry for entry in catalog["modifiers"]}
    trajectory_by_id = {entry["id"]: entry for entry in catalog["trajectories"]}
    for spell in spells:
        unavailable = spell.get("unavailable")
        if unavailable:
            skipped.extend(dict(id=spell["id"], case=mode, reason=unavailable) for mode in modes)
            continue
        if "construction" in spell:
            if args.variant != 0:
                raise ValueError(f"{spell['name']} 的构造原式没有旧动画变体")
            for mode in modes:
                rows.append(construction_case(spell, f"registered-{len(rows) + 1:04d}", mode))
            continue
        skipped.extend(dict(id=spell["id"], case=mode, reason="需要构造资料中的实际形体和部位")
                       for mode in modes if mode in CONSTRUCTION_ONLY)
        if not legacy_modes:
            continue
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
            for mode in legacy_modes:
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
                                 labels=spell.get("labels", {}),
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
        resource = {
            "none": "无消耗", "wakan": "真元", "vigor": "斗气",
            "mana": "法力", "stamina": "体力", "talisman": "张符箓", "yao_power": "妖力",
        }[data["resource"]]
        detail = case.get('caption', '真实接触验证')
        title = f"{case['name']} · {view} · {case['label']}\n消耗：{data['paid']:g} {resource}　{detail}"
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


def publish(output, cases, results, skipped, failures=(), batch_error=None):
    """保留每段原片和分类成片，再生成离线目录与完整压缩包。"""
    groups = {}
    selected = []
    for case in cases:
        if case["key"] in results:
            video = (output / case["key"] / "demo.mp4").resolve()
            groups.setdefault(case["group"], []).append(video)
            selected.append(video)
    reels = [(f"{group}合集", group, f"{group}-合集.mp4", videos) for group, videos in groups.items()]
    if len(selected) > 1:
        reels.insert(0, ("全部总览", "全部法术", "全部法术-总览.mp4", selected))
    playlists = []
    for label, prefix, filename, videos in reels:
        concat = output / f"{prefix}.ffconcat"
        lines = ["file '" + str(path).replace("'", "'\\''") + "'" for path in videos]
        concat.write_text("ffconcat version 1.0\n" + "\n".join(lines) + "\n", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", str(concat),
                        "-c", "copy", "-movflags", "+faststart", str(output / filename)], check=True)
        playlists.append((label, filename))
    write_index(output, cases, results, skipped, failures, playlists, batch_error)
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
    for key, title in CATALOG_FILTERS.items():
        parser.add_argument("--" + key, default="", help=f"按登记{title}筛选，支持标签名称或编号，可用逗号分隔")
    parser.add_argument("--cases", help="base,modifiers,trajectory,combined,cross-form,material,structure；文件默认只录原式")
    parser.add_argument("--edit-form", default="spirit_bolt", help="跨形态演示的新形体编号")
    parser.add_argument("--edit-matter", default="ice", help="换材演示的材料编号，可用材料:物态")
    parser.add_argument("--edit-child", help="新增子部位的形体编号，缺省沿用主体形体")
    parser.add_argument("--modifier", action="append", default=[], help="词条名称:参数=值，可重复指定")
    parser.add_argument("--trajectory", help="指定兼容轨迹名称或编号")
    parser.add_argument("--variant", type=int, default=0, help="单个动画变体编号")
    parser.add_argument("--variants", choices=["first", "all"], default="first")
    parser.add_argument("--duration", type=float, default=3, help="每段世界秒数")
    parser.add_argument("--fps", type=int, default=20, help="每世界秒截图上限")
    parser.add_argument("--battle-artifact", help="自主交战时装备一件指定主动能力的法宝，由 AI 决定使用")
    parser.add_argument("--battle-opponent", action="append", default=[], help="指定对手实际掌握的登记能力，可重复指定")
    parser.add_argument("--battle-event", choices=["clash", "guard", "break", "reflection", "element"],
                        help="要求双方自主施放有实际相撞、格挡、破盾、反射或水火反应")
    parser.add_argument("--battle-seed", type=int, help="固定自主交战的原版随机种子")
    parser.add_argument("--speed", choices=[1, 2, 3, 5], type=int, default=1, help="游戏自身的模拟速度；成片仍按世界时间播放")
    parser.add_argument("--motion-trace", action="store_true", help="随单招原帧记录真实外形、方向、身体姿态和阻挡")
    parser.add_argument("--diagnostics", action="store_true", help="记录大块黄色精灵及粒子的材质来源")
    parser.add_argument("--view", choices=["native", "worldfall", "firstperson"], default="native", help="原版俯视、Worldfall 三维观察或实际附身第一人称")
    parser.add_argument("--lighting", choices=["day", "night"], default="day", help="Worldfall固定昼夜条件，用于材质和自发光对照")
    parser.add_argument("--scene", choices=["single", "shield", "battle"], default="single", help="单招、护盾迎击或真正自主交战")
    parser.add_argument("--shield-break", action="store_true", help="迎击录像先挡住小石弹，再用真实重击打穿")
    parser.add_argument("--realm", type=int, default=2, choices=range(5), help="修炼境界 0～4")
    parser.add_argument("--caster-species", help="为依赖真实关节的构造选择原版生物编号")
    parser.add_argument("--require-source", help="要求单招使用指定的专属主动能力入口")
    parser.add_argument("--caster-walk", type=float, default=0, help="构造演示中让施法者通过原版附体移动沿地图X轴行走指定格数")
    parser.add_argument("--caster-position", type=float, nargs=2, metavar=("X", "Y"), help="单招录像复用指定的施法者地图坐标")
    parser.add_argument("--target-offset", type=float, nargs=2, metavar=("X", "Y"), help="单招或迎击场景中，目标相对施法者的地图位置")
    parser.add_argument("--aim-offset", type=float, nargs=2, metavar=("X", "Y"), help="单招录像中，瞄准相对施法者的地面坐标；构造需允许点地")
    parser.add_argument("--terrain-slope", type=float, default=0, help="每格地图X方向增加的原版地形高度，范围 -4 至 4")
    parser.add_argument("--zoom", type=float, default=22, help="镜头缩放；城域演示建议 38")
    parser.add_argument("--power", type=float, default=6, help="测试人物战力，默认便于观察单招")
    parser.add_argument("--keep-frames", action="store_true")
    parser.add_argument("--resume", action="store_true", help="复用指定输出目录中已经完成的录像")
    parser.add_argument("--output", type=Path, help="独立输出目录")
    args = parser.parse_args()
    if args.cases is None:
        args.cases = "base" if args.construction or args.scene == "battle" else "base,modifiers,trajectory"
    if args.duration <= 0 or not 1 <= args.fps <= 60 or args.power <= 0 or args.zoom <= 0:
        parser.error("时长、战力须大于零，截图频率须为 1～60")
    if args.opponent_construction and not args.construction and args.scene != "battle":
        parser.error("--opponent-construction 需要同时指定 --construction")
    if (args.battle_opponent or args.battle_event or args.battle_seed is not None) and args.scene != "battle":
        parser.error("交战对手、事件和种子参数只用于 --scene battle")
    if args.scene == "battle" and (args.view == "firstperson" or args.cases != "base"):
        parser.error("自主交战请用 --cases base 和原版或三维旁观镜头")
    if args.battle_opponent and args.opponent_construction:
        parser.error("请从登记或构造文件中选择一种对手来源")
    if (args.caster_walk or args.isolate_target) and not args.construction:
        parser.error("行走和移开旁观者只用于 --construction")
    if args.shield_break and args.scene != "shield":
        parser.error("--shield-break 只用于迎击场景")
    if args.target_offset and args.scene == "battle":
        parser.error("--target-offset 只用于单招或迎击场景")
    if args.caster_position and (args.scene != "single" or any(not math.isfinite(v) for v in args.caster_position)):
        parser.error("--caster-position 只用于单招，坐标须为有限数")
    if args.caster_species and args.scene != "single":
        parser.error("--caster-species 只用于单招场景")
    if args.require_source and (args.scene != "single" or args.construction or args.resource_probe or args.inventory):
        parser.error("--require-source 只用于目录中专属能力的单招录像")
    if args.aim_offset and (args.scene != "single" or
                            any(not math.isfinite(v) or abs(v) > 20 for v in args.aim_offset)):
        parser.error("--aim-offset 只用于单招，偏移须在 -20 至 20 格")
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
            if any(getattr(args, key) for key in CATALOG_FILTERS):
                raise ValueError("四类标签来自游戏登记；本地构造文件可用 --filter 按名称或编号查找")
            if args.resource_probe or args.inventory or args.scene not in ("single", "battle"):
                raise ValueError("构造演示请使用单招或自主交战场景")
            if args.modifier or args.trajectory or args.variant or args.variants != "first":
                raise ValueError("构造改造由 --cases 选择；--modifier、--trajectory 和动画变体参数用于旧法术")
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
            if len(opponent) != 1 or any(entry["id"] == opponent[0]["id"] for entry in constructions or []):
                raise ValueError("对手构造需要独立的单份资料，编号不能与本批任何主构造重复")
        starting_version, _ = version_stamp()
        catalog = connect(args.launch)
        if args.inventory:
            export_inventory((args.output or CONTROL / "inventory").resolve(), starting_version)
            return
        available = filter_catalog(catalog["spells"], args)
        if args.list:
            if args.output:
                args.output.mkdir(parents=True, exist_ok=True)
                write_json(args.output / "catalog.json", dict(catalog, spells=available))
            if not available:
                print("没有匹配的能力")
            for entry in available:
                if entry.get("triggerScene"):
                    status = f"专用录像 record_artifact_passives.py --case {entry['triggerScene']}"
                elif entry.get("unavailable"):
                    status = f"暂不可录：{entry['unavailable']}"
                else:
                    status = f"{entry['variants']} 个变体"
                labels = entry.get("labels", {})
                fields = [title + "=" + ("、".join(labels.get(key, {}).values()) or "未标注")
                          for key, title in CATALOG_FILTERS.items()]
                print("\t".join([entry["name"], entry["id"], entry["group"], status, *fields]))
            if args.filter or any(getattr(args, key) for key in CATALOG_FILTERS):
                return
            for category in ["modifiers", "trajectories"]:
                print("\n" + ("词条" if category == "modifiers" else "轨迹"))
                for entry in catalog[category]:
                    print(entry["name"], entry["id"], entry.get("parameters", ""))
            return
        if not catalog.get("recordingAllowed"):
            raise ValueError("当前游戏的存档目录不是本工具的独立测试配置。请通过 dev.py exec 或 --launch 连接测试游戏")
        if constructions is not None:
            spells = constructions
        elif args.resource_probe:
            spells = [dict(id="Cultiway.ResourceProbe", name="形体资源接入验证", group="开发资源验证")]
        elif all_spells:
            spells = available
        else:
            if not args.spell:
                parser.error("请指定法术名称，或使用 --list")
            spells = [resolve(available, args.spell)]
        if not spells:
            parser.error("筛选后没有法术")
        if args.scene == "battle":
            right = opponent or [resolve(catalog["spells"], name) for name in args.battle_opponent] or spells
            cases, skipped = [battle_case(spells, right)], []
        elif constructions is not None:
            cases, skipped = construction_cases(constructions, args.construction.is_file(), demo_modes(args.cases)), []
        elif args.resource_probe:
            cases, skipped = [dict(key="resource-probe", id="Cultiway.ResourceProbe", name="形体资源接入验证",
                group="开发资源验证", label="模型与材质", variant=0, modifiers=[], trajectory="共同姿态")], []
        else:
            cases, skipped = choose_cases(spells, catalog, args)
        if args.caster_species and any("construction" not in case for case in cases):
            raise ValueError("--caster-species 需要选择依赖真实关节的构造")
        if args.require_source and any("construction" in case for case in cases):
            raise ValueError("--require-source 需要选择目录中的专属主动能力")
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
        verify_loaded_inputs(catalog, version)
        fixture = PROJECT / FIXTURE_DIR
        if not (fixture / "map.wbox").is_file():
            raise ValueError(f"缺少演示测试存档：{fixture}")
        runtime = catalog["runtime"]
        tools = recording_tools() if cases else None
        manifest = dict(version=version, runtime=runtime, recordingTools=tools, fixture=str(fixture),
                        motionTrace=args.motion_trace, cases=cases, skipped=skipped, duration=args.duration,
                        fps=args.fps, power=args.power, realm=args.realm, zoom=args.zoom, view=args.view,
                        lighting=args.lighting, scene=args.scene, diagnostics=args.diagnostics,
                        speed=args.speed, battleArtifact=battle_artifact)
        if args.scene == "battle":
            manifest.update(battleEvent=args.battle_event, battleSeed=args.battle_seed)
        if any(case["mode"] in CONSTRUCTION_ONLY for case in cases if "construction" in case):
            manifest["editOptions"] = dict(form=args.edit_form, matter=args.edit_matter, child=args.edit_child)
        if args.shield_break:
            manifest["shieldBreak"] = True
        if args.caster_position:
            manifest["casterPosition"] = args.caster_position
        if args.caster_species:
            manifest["casterSpecies"] = args.caster_species
        if args.require_source:
            manifest["requiredSource"] = args.require_source
        if args.aim_offset:
            manifest["aimOffset"] = args.aim_offset
        if args.target_offset or constructions is not None:
            manifest["targetOffset"] = args.target_offset
        if args.terrain_slope:
            manifest["terrainSlope"] = args.terrain_slope
        if constructions is not None:
            manifest["casterWalk"] = args.caster_walk
            manifest["isolateTarget"] = args.isolate_target
            if opponent is not None and args.scene == "single":
                manifest["opponentConstruction"] = opponent[0]["construction"]
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
                generated = ["全部演示.zip", "全部法术-总览.mp4", "index.html", INDEX_FILE, "演示目录.md"]
                generated.extend(group + "-合集.mp4" for group in {case["group"] for case in previous["cases"]})
                for filename in generated:
                    (output / filename).unlink(missing_ok=True)
        batch = recording_stamp(manifest)
        cached_clips = {case["key"]: checked_clip(output, case["key"], batch) for case in cases
                        if args.resume and (output / case["key"] / "demo.mp4").is_file()}
        write_json(output / "catalog.json", catalog)
        write_json(output / "manifest.json", manifest)
        (output / "status.json").unlink(missing_ok=True)
        if not cases:
            publish(output, cases, {}, skipped)
            raise ValueError(f"筛选后没有可录制项。查看 {output / '演示目录.md'}")
        (CONTROL / "cancel").unlink(missing_ok=True)
        if not args.resume:
            for case in cases:
                for filename in ("result.json", "demo.mp4", "clip.json"):
                    (output / case["key"] / filename).unlink(missing_ok=True)
        remaining = [case for case in cases if case["key"] not in cached_clips]
        for case in remaining:
            for filename in ("result.json", "definition.json", "source-definition.json", "clip.json", "preview.png"):
                (output / case["key"] / filename).unlink(missing_ok=True)
        has_edits = any("construction" in case and case.get("mode") != "base" for case in cases)
        continue_on_failure = (all_spells or has_edits) and args.scene == "single" and not args.resource_probe
        request = dict(manifest, id=str(time.time_ns()), action="construction" if constructions is not None else "resources" if args.resource_probe else "record",
                       output=str(output), cases=remaining, continueOnFailure=continue_on_failure)
        write_json(CONTROL / "request.json", request)
        recording_requested = True
        print(f"开始：{len(spells)} 个演示项目，{len(remaining)} 段待录；输出 {output}", flush=True)
        results, failures, last_change, last_status = {}, [], time.monotonic(), ""
        clips = dict(cached_clips)
        status_file = output / "status.json"
        while True:
            status = json.loads(status_file.read_text(encoding="utf-8")) if status_file.exists() else {}
            for case in cases:
                report = output / case["key"] / "result.json"
                if case["key"] not in results and report.exists():
                    data = json.loads(report.read_text(encoding="utf-8"))
                    cached = case["key"] in cached_clips
                    if data.get("runtime") != runtime or not cached and data.get("requestId") != request["id"]:
                        raise ValueError(f"片段的实际运行版本或请求不符：{case['key']}")
                    if data.get("skipped"):
                        skipped.append(dict(id=case["id"], case=case.get("mode", ""), reason=data["reason"],
                                            key=case["key"]))
                        results[case["key"]] = data
                        last_change = time.monotonic()
                        print(f"[{len(results)}/{len(cases)}] 跳过：{case['name']} · {case['label']}，{data['reason']}", flush=True)
                        continue
                    if not data.get("passed"):
                        reason = (data.get("error") or "").split("\n", 1)[0] or "实际结果未通过"
                        failures.append(dict(case=case, reason=reason))
                        results[case["key"]] = data
                        last_change = time.monotonic()
                        print(f"[{len(results)}/{len(cases)}] 失败：{case['name']} · {case['label']}，{reason}", flush=True)
                        continue
                    if not cached:
                        data = encode(output, case, args.keep_frames)
                        clips[case["key"]] = clip_facts(output, case["key"], batch)
                    results[case["key"]] = data
                    last_change = time.monotonic()
                    if not cached:
                        print(f"[{len(results)}/{len(cases)}] {case['name']} · {case['label']}：{data['frames']} 帧，{data['duration']:.2f} 世界秒", flush=True)
            if status.get("done"):
                recording_requested = False
                break
            marker = json.dumps(status, ensure_ascii=False)
            if marker != last_status:
                last_status, last_change = marker, time.monotonic()
            timeout = 60 if len(results) == len(cases) else 180
            if time.monotonic() - last_change > timeout:
                raise RuntimeError("录制没有进展，请检查游戏窗口和 status.json；可用 --resume 继续")
            time.sleep(.5)
        batch_error = status.get("error")
        if not batch_error and len(results) < len(cases):
            raise RuntimeError("游戏已结束录制，但存在缺失结果，请查看 status.json")
        if version_stamp()[0]["sha256"] != version["sha256"]:
            raise ValueError("录制期间代码或资源发生了修改，本目录不可作为同一版本的验收结果，请重新录制")
        verify_loaded_inputs(catalog, version)
        if status.get("requestId") != request["id"] or status.get("runtime") != runtime or recording_tools() != tools:
            raise ValueError("录制期间实际游戏或工具版本发生了变化")
        if not status.get("sceneRestored"):
            raise RuntimeError("演示结束后未恢复测试世界，请查看 status.json")
        for key, checked in clips.items():
            if clip_facts(output, key, batch) != checked:
                raise ValueError(f"录制期间片段发生了变化：{key}")
        for key, checked in clips.items():
            if key not in cached_clips:
                write_json(output / key / "clip.json", checked)
        accepted = {key: result for key, result in results.items() if result["passed"]}
        if batch_error:
            skipped.extend(dict(id=case["id"], case=case.get("mode", ""), key=case["key"],
                                reason="批次中止，未执行本项") for case in cases if case["key"] not in results)
        publish(output, cases, accepted, skipped, failures, batch_error.splitlines()[0] if batch_error else None)
        if batch_error:
            raise RuntimeError(f"{batch_error.splitlines()[0]}。查看 {output / '演示目录.md'}")
        if failures:
            if not continue_on_failure and len(cases) == 1:
                raise RuntimeError(f"演示失败：{cases[0]['name']}，{failures[0]['reason']}。查看 {output / '演示目录.md'}")
            raise RuntimeError(f"批次已结束，{len(failures)} 项失败。查看 {output / '演示目录.md'}")
        if not accepted:
            raise RuntimeError(f"本次改造均未成立。查看 {output / '演示目录.md'}")
        print(f"完成：{output / '演示目录.md'}", flush=True)
    except KeyboardInterrupt:
        if recording_requested:
            (CONTROL / "cancel").touch()
        parser.exit(130, "已请求游戏结束演示并恢复界面。\n")
    except (OSError, RuntimeError, ValueError, subprocess.CalledProcessError) as error:
        if recording_requested:
            (CONTROL / "cancel").touch()
        parser.exit(1, str(error) + "\n")
