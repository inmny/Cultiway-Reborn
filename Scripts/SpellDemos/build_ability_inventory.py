#!/usr/bin/env python3
"""用实际运行登记、本地中文和源码位置生成任务 0.01 的完整清单。"""

import argparse
import csv
import hashlib
import json
import re
import subprocess
from collections import Counter, defaultdict
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[2]
DESTINATION = PROJECT / "Doc/技能系统全面重构/全部能力登记表.md"

# 这些是取得方式的说明，不是新增的游戏登记，也不表示已迁入新系统。
CATEGORIES = {
    "技能资产": ("旧技能容器", "由来源授予或学习服务取得", "内部执行", ["Source/Core/SkillLibV3/Manager.cs"]),
    "法宝能力": ("法宝组合", "炼器组合生成能力实例；取得并控制对应法宝", "被动", ["Source/Content/Artifacts/ArtifactAbilityDispatcher.cs"]),
    "战技定义": ("骑士流派", "对应流派、武器和等级条件下学习战技", "主动", ["Source/Content/KnightCombat/KnightTechniqueSkills.cs"]),
    "器官等级": ("妖兽身体组成", "初始身体、血脉成长或吞噬炼化形成器官", "被动身体属性", ["Source/Content/Yao/YaoContent.cs"]),
    "器官被动效果": ("妖兽器官", "携带引用该效果族的实际器官等级", "被动事件", ["Source/Content/Yao/YaoOrganEffects.cs"]),
    "修炼形成原子": ("修炼成果组合", "真气、仙基、金丹、元婴及元神形成时按境界、评分和槽位选择", "来源资料", ["Source/Content/CoreFormationAtoms.cs"]),
    "修炼形成效果": ("修炼成果组合", "对应形成原子显化；同效果族按等级合并覆盖", "被动事件", ["Source/Content/CoreFormationAtoms.cs", "Source/Content/CoreFormationEffectHandlers.cs"]),
    "妖丹神通来源": ("妖丹方向", "凝丹时按语义、血脉及身体稳定度选择方向，授予所列技能", "主动来源", ["Source/Content/Yao/YaoCorePatterns.cs", "Source/Content/Yao/YaoCoreActiveAbilityProvider.cs"]),
    "元神功能命令": ("元神专属", "随元神层数、命魂、聚焦节点、离体或肉身状态开放；不单独学习", "主动命令", []),
    "状态定义": ("附属状态", "由引用该状态的法术、伤害或玩法施加；不是可单独学习的招式", "被动状态", []),
    "丹药效果原子": ("丹药组合", "炼丹时按材料语义、品质和种子组合；使用成品时产生状态、永久收益或恢复", "物品使用效果", ["Source/Content/ElixirEffectAtoms.cs", "Source/Content/Libraries/ElixirEffectComposer.cs"]),
    "城市功能工程": ("城市需求", "城市需求规划建立工程；由具备相应已学法术或构造的执行者认领", "自主工程", ["Source/Content/CollectiveProjects/MagicUtilityProjectContent.cs", "Source/Content/CollectiveProjects/MagicUtilitySpellResolver.cs"]),
    "身体动作": ("徒手普通攻击", "符合徒手出手条件时由掌击入口提供", "主动", ["Source/Content/Combat/PalmStrikeCombat.cs", "Source/Content/Skills/BodyActionCatalog.cs"]),
    "构造资料": ("共同构造目录", "基础动作仅登记；正式学习需明确授予，后续创作与传承读取实际构造", "主动资料", ["Source/Content/Skills/BodyActionCatalog.cs", "Source/Content/Skills/ConstructedAbilityTransmission.cs"]),
}

UTILITY_NAMES = {
    "EmergencyClean": "应急净化", "RoutineClean": "日常净化", "NatureGrowth": "植被催生",
    "CropFertilization": "麦田催熟", "HousingTerrain": "住房地形整理", "FarmTerrain": "农田地形整理",
}
EFFECT_NAMES = {
    "yao.venom": "毒素", "yao.regen": "再生", "yao.turtle_stance": "龟息守势",
    "yao.gluttony": "饕餮吞噬", "yao.nirvana": "涅槃", "yao.nine_tail": "九尾护命",
}

# 无独立技能资产的过程也要登记。这里的编号是清单定位符，沿用实际负责类名，
# 不伪称游戏里存在同名技能或新增保存编号。
PROCESSES = [
    ("Cultiway.Content.YaoBeasts.YaoTribulationService", "妖兽天劫", "妖修进阶触发", "自动过程", "Source/Content/Yao/YaoTribulationService.cs"),
    ("Cultiway.Content.BalefulWindTribulationSkillService", "赑风天劫出手", "对应渡劫阶段驱动中心及风浪执行体", "自动过程", "Source/Content/BalefulWindTribulationSkillService.cs"),
    ("Cultiway.Content.Sects.SectLectureService", "宗门讲经", "宗门安排具备条件的讲师与听众；传授功法", "自主传承", "Source/Content/Sects/SectLectureService.cs"),
    ("Cultiway.Content.Sects.SectScriptureStudyService", "宗门典籍学习", "宗门藏经阁及学习条件允许时读书", "自主传承", "Source/Content/Sects/SectScriptureStudyService.cs"),
    ("Cultiway.Content.Sects.SectTreasureService", "宗门宝库借用", "贡献及借用权限允许时取得实际物品；能力仍由物品拥有", "来源转移", "Source/Content/Sects/SectTreasureService.cs"),
    ("Cultiway.Content.SpiritVeins.SpiritVeinManager", "灵脉与聚灵地", "世界生成、地形变化及每月演化；提供环境灵气，不是已登记的可施放阵法", "长期环境", "Source/Content/SpiritVeins/SpiritVeinManager.cs"),
    ("Cultiway.Content.Systems.Logic.TeleportArraySystem", "传送阵通行", "存在有效传送阵及通行请求时由设施系统维护", "长期设施", "Source/Content/Systems/Logic/TeleportArraySystem.cs"),
    ("Cultiway.Content.Skills.ConstructedAbilityService", "修士自主创法与传承", "修炼、突破提供创法机会；生成实际构造并学习，可通过书籍和功法传承", "能力生成", "Source/Content/Skills/ConstructedAbilityCreation.cs"),
    ("Cultiway.Content.Libraries.ElixirEffectGenerator", "丹药实际效果", "丹药原子组合成实际状态、数据收益或恢复行为；生成状态随实际丹药编号登记", "效果生成", "Source/Content/Libraries/ElixirEffectGenerator.cs"),
]


def source_index():
    """只索引仓库源码；回调位置保留到真实声明文件。"""
    files = subprocess.check_output(["rg", "--files", "Source"], cwd=PROJECT, text=True).splitlines()
    texts = {file: (PROJECT / file).read_text(encoding="utf-8-sig") for file in files if file.endswith(".cs")}
    types, declarations = defaultdict(set), defaultdict(set)
    for file, text in texts.items():
        namespace = re.search(r"namespace\s+([\w.]+)", text)
        if namespace:
            for name in re.findall(r"\b(?:class|struct)\s+(\w+)", text):
                types[namespace[1] + "." + name].add(file)
        for asset_type, name in re.findall(r"\bstatic\s+(SkillEntityAsset|Entity|ArtifactAbilityAsset|CoreFormationAtomAsset)\s+(\w+)\s*\{", text):
            declarations[asset_type, name].add(file)
    return declarations, types, texts


def locale_index():
    """直接读取仓库中文，避免游戏当前语言影响清单。"""
    names = {}
    for file in sorted((PROJECT / "Locales").rglob("*.csv")):
        with file.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                if row.get("key") and row.get("cz"):
                    names[row["key"]] = row["cz"]
    return names


def type_files(name, types):
    """委托生成的内部类归回声明类型，不解析或执行回调。"""
    owner = name.split("+")[0]
    files = types.get(owner, set())
    primary = {file for file in files if Path(file).stem == owner.rsplit(".", 1)[-1]}
    return primary or files


def entry_files(entry, declarations, types, texts):
    """把实际编号、声明及委托对应到文件，保留类别的共同入口。"""
    files = set(CATEGORIES[entry["kind"]][3])
    for callback in entry.get("callbacks", []):
        owner = callback.split("+")[0] if "+" in callback else callback.rsplit(".", 1)[0]
        generated = re.search(r"<([^<>]+)>b__", callback)
        method = generated[1] if generated else callback.rsplit(".", 1)[-1]
        pattern = re.compile(r"\b(?:public|private|internal|protected)\s+(?:static\s+)?[^;={}\n]+\b" + re.escape(method) + r"\s*\(")
        matches = {file for file in types.get(owner, set()) if pattern.search(texts[file])}
        files.update(matches or type_files(owner, types))
    files.update(type_files(entry.get("execution", ""), types))
    leaf = entry["id"].rsplit(".", 1)[-1]
    declaration_types = {"技能资产": ["SkillEntityAsset", "Entity"], "法宝能力": ["ArtifactAbilityAsset"], "修炼形成原子": ["CoreFormationAtomAsset"]}
    if entry["kind"] in declaration_types:
        for asset_type in declaration_types[entry["kind"]]:
            candidates = declarations[asset_type, leaf]
            if entry["kind"] == "技能资产":
                candidates = {file for file in candidates if
                              ("ArtifactSkillExecutions" in file if entry["id"].startswith("Cultiway.ArtifactSkillExecution.") else
                               "CoreFormationSkills" in file if entry["id"].startswith("Cultiway.CoreFormation.Skill.") else
                               "SkillEntities" in file or "WeaponControlSkills" in file)}
            files.update(candidates)
    if entry["kind"] == "状态定义":
        files.add("Source/Content/CoreFormationSkills.cs" if entry["id"].startswith("CoreFormationState_") else
                  "Source/Content/KnightCombat/KnightTechniqueStatuses.cs" if entry["id"].startswith("Knight") else "Source/Content/StatusEffects.cs")
    if entry["kind"] == "技能资产" and entry["id"].endswith(".Execution"):
        files.add("Source/Content/KnightCombat/KnightTechniqueSkills.cs")
    if entry["kind"] == "技能资产" and entry["id"] == "raw_anim":
        files.add("Source/Content/SkillEntities.cs")
    if entry["kind"] in ("构造资料", "身体动作"):
        for path in sorted((PROJECT / "Content/Skills/Body").glob("*.json")):
            if json.loads(path.read_text())["Id"] == entry["id"]:
                files.add(path.relative_to(PROJECT).as_posix())
    return sorted(files)


def enrich(entry, names, declarations, types, texts):
    """补齐 0.01 的七项信息；不把登记当成自然可用或迁移完成。"""
    entry = dict(entry)
    source, acquisition, mode, _ = CATEGORIES[entry["kind"]]
    key = entry.get("nameKey", entry["id"])
    display = names.get(key) or names.get("Cultiway." + key) or entry["name"]
    if entry["kind"] == "器官等级":
        organ, rank = entry["id"].rsplit(".rank", 1)
        display = names["Cultiway.CreatureOrgan." + organ] + "（等级" + rank + "）"
        mode = "主动授予及被动身体效果" if entry["skills"] else "被动效果及身体属性"
    elif entry["kind"] == "器官被动效果":
        display = EFFECT_NAMES[entry["id"]] + "（清单说明名）"
    elif entry["kind"] == "城市功能工程":
        display = UTILITY_NAMES[entry["name"]]
    elif entry["kind"] == "技能资产":
        if entry["learnable"] and not entry["id"].startswith("Cultiway.KnightTechnique."):
            source, acquisition, mode = "普通已学法术", "法网或卷轴学习、万法阁明确授予；须通过学习服务的实际条件", "主动法术"
        elif entry["id"].startswith("Cultiway.CoreFormation.Skill."):
            source, acquisition = "修炼成果派生", "由形成效果授予执行容器，不能作为普通法术学习"
        elif entry["id"].startswith("Cultiway.KnightTechnique."):
            source, acquisition = "骑士战技执行", "对应已学战技出手时派生"
        elif entry["id"].startswith("Cultiway.ArtifactSkillExecution."):
            source, acquisition = "法宝内部执行", "对应法宝主动能力启动或剑阵内部派生"
        elif "Tribulation" in entry["id"]:
            source, acquisition = "赑风天劫", "渡劫过程创建；不由人物学习"
        elif entry["id"] == "Cultiway.WeaponControl":
            source, acquisition, mode = "筑基御器", "筑基后具备实际武器及御器条件", "主动"
        else:
            acquisition = "仅为内部动画资产，不提供独立学习入口"
    elif entry["kind"] == "法宝能力":
        mode = "主动，另有被动或持续规则" if entry["active"] else "被动事件、属性或专用流程"
    elif entry["kind"] == "修炼形成效果":
        mode = "主动及被动" if entry["active"] else "被动事件或维持"
    elif entry["kind"] == "状态定义":
        source, acquisition, mode = "附属状态", "由引用该状态的法术、伤害或玩法施加；不是可单独学习的招式", "被动状态"
    entry.update(displayName=display, origin=source, acquisition=acquisition, mode=mode,
                 files=entry_files(entry, declarations, types, texts))
    return entry


def render(data, declarations, types, texts, names):
    """生成人工可查的表，并核对引用和文件位置。"""
    entries = [enrich(entry, names, declarations, types, texts) for entry in data["entries"]]
    references = list(data["references"])
    for owner, child in [("FlyingSwordAttack", "FlyingSword"), ("SplittingSwordArray", "SwordArray")]:
        references.append(dict(owner="artifact:Cultiway.ArtifactAbility." + owner,
                               target="skill:Cultiway.ArtifactSkillExecution." + child, role="内部执行"))
    references.append(dict(owner="skill:Cultiway.ArtifactSkillExecution.SwordArray",
                           target="skill:Cultiway.ArtifactSkillExecution.SwordArrayBlade", role="剑阵派生剑影"))
    for entry in entries:
        if entry["kind"] == "修炼形成效果":
            for family, child in [("wood", "WoodLifeReturn"), ("fire", "FireEmberBurst"), ("earth", "EarthWardImpact"), ("reservoir", "ReservoirOrb")]:
                if entry["id"] == family:
                    references.append(dict(owner=entry["key"], target="skill:Cultiway.CoreFormation.Skill." + child, role="事件分支派生"))
    keys = {entry["key"] for entry in entries}
    if len(keys) != len(entries):
        raise ValueError("运行登记包含重复编号")
    for reference in references:
        if reference["owner"] not in keys or reference["target"] not in keys:
            raise ValueError("引用未登记：" + str(reference))
    for entry in entries:
        if not entry["files"] or any(not (PROJECT / file).exists() for file in entry["files"]):
            raise ValueError("缺少真实文件位置：" + entry["key"] + " " + str(entry["files"]))
    incoming, outgoing = defaultdict(list), defaultdict(list)
    for reference in references:
        outgoing[reference["owner"]].append(reference["role"] + " → `" + reference["target"] + "`")
        incoming[reference["target"]].append("来自 `" + reference["owner"] + "`")
    counts = Counter(entry["kind"] for entry in entries)
    lines = ["# 全部能力登记表", "", "对应 TODO **0.01**。本表由实际运行登记与仓库源码生成，不表示迁移、表现或最终验收通过。", "",
             f"实际导出时间：{data['generatedAt']}；模组程序集：`{data['assembly']}`。", "",
             f"共 **{len(entries)} 项登记、{len(references)} 项明确引用、{len(data['sourceEntrances'])} 个主动来源入口**。来源、执行体、状态及资料分别计数，不能相加成独立法术数量。", "",
             "`key` 是清单中的稳定定位符；冒号后沿用实际资产编号。形成效果由原子、效果族和等级共同定位，元神命令由来源编号及条目编号定位。", "",
             "名称优先读取仓库中文。没有玩家名称的内部编号保留原样；器官效果族的说明名明确标注，不修改游戏本地化。旧动画变体是同一技能的索引，不另算一招。", "",
             "## 类别与数量", "", "| 类别 | 项数 |", "|---|---:|"]
    lines += [f"| {kind} | {count} |" for kind, count in counts.items()]
    lines += ["", "## 全部主动来源", "", "同一个能力可能经不同来源使用。符箓和卷轴封存实际技能，不另登记一组虚构法术。", "",
              "| 来源编号 | 负责类与文件 |", "|---|---|"]
    for provider in data["sourceEntrances"]:
        files = sorted(type_files(provider["type"], types))
        if not files:
            raise ValueError("来源缺少源码：" + provider["type"])
        lines.append(f"| `{provider['id']}` | `{provider['type']}`；{file_links(files)} |")
    for kind in counts:
        lines += ["", "## " + kind, "", "| 稳定定位符 | 显示名 | 来源及取得方式 | 主动/被动 | 内部派生与引用 | 相关文件 |", "|---|---|---|---|---|---|"]
        for entry in sorted((entry for entry in entries if entry["kind"] == kind), key=lambda entry: entry["key"]):
            related = outgoing[entry["key"]] + incoming[entry["key"]]
            if entry.get("variants"):
                related.append(f"旧变体索引 0 至 {entry['variants'] - 1}")
            cells = ["`" + entry["key"] + "`", entry["displayName"], entry["origin"] + "；" + entry["acquisition"], entry["mode"],
                     "；".join(related) or "无单独派生登记；详见相关文件", file_links(entry["files"])]
            lines.append("| " + " | ".join(cell.replace("|", "\\|").replace("\n", " ") for cell in cells) + " |")
    lines += ["", "## 没有独立技能资产的过程与设施", "",
              "以下是必须纳入后续入口追踪的现有玩法。`process:` 后为实际负责类名，只是清单定位符。它们没有被伪装成可学习法术。宗门建筑提供场所和权限，物品能力继续归实际物品所有；当前灵脉代码也不等于已经实现阵法法术。", "",
              "| 定位符 | 名称 | 来源及取得方式 | 性质 | 文件 |", "|---|---|---|---|---|"]
    for owner, name, acquisition, mode, file in PROCESSES:
        if not (PROJECT / file).exists():
            raise ValueError("过程文件不存在：" + file)
        lines.append(f"| `process:{owner}` | {name} | {acquisition} | {mode} | {file_links([file])} |")
    lines += ["", "## 动态结果与范围边界", "",
              f"- 本次实际游戏目录有表内 {counts['构造资料']} 项构造。修士新创法沿用 `Cultiway.Created.*` 正式编号，定义数量随世界增长；重新导出会逐项列出当前世界已登记结果，不能穷举未来生成结果。",
              "- 词条及轨迹是改造输入，不是独立能力。当前可选择的登记见同次导出的 `catalog.json`；内部运动入口由技能实际轨迹编号和相关文件追踪。0.04 再逐项固定资料与操作边界。",
              "- 法宝形制、材料、修炼原子和器官中只有提供能力或实际作用的登记进入对应表。普通建筑、美术变体、图标与数值属性不额外算招式。",
              "- `body:Cultiway.PalmStrike` 与 `construction:Cultiway.PalmStrike` 是同一掌击的来源入口和构造定义，不是两招。九项战技与九个 `.Execution` 也分别是入口与内部执行。",
              "- 本表核对登记、取得类型、派生和文件。逐项学习、支付、动作、作用、显示、人工与自主使用、保存及旧路径删除仍属于 **0.03**，不会因本表生成而提前勾选。", "",
              "## 重新生成", "", "```bash", "python3 Scripts/SpellDemos/demo_all.py --launch --inventory --output artifacts/skill_rebuild/ability-inventory",
              "python3 Scripts/SpellDemos/build_ability_inventory.py artifacts/skill_rebuild/ability-inventory/inventory.json", "```", "",
              "实际原始登记保留在上述产物目录；脚本不加载测试世界、不出手、不授予或保存人物能力。", "",
              "登记内容的 SHA-256（JSON 按键排序后）：`" + hashlib.sha256(json.dumps(data, ensure_ascii=False, sort_keys=True).encode()).hexdigest() + "`。", ""]
    return "\n".join(lines)


def file_links(files):
    """从文档位置链接回真实文件。"""
    return "；".join(f"[{file}](../../{file})" for file in files)


def main():
    """生成一次可提交清单，读取错误或悬空引用会直接报告。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inventory", type=Path, help="demo_all.py --inventory 导出的实际 inventory.json")
    parser.add_argument("--output", type=Path, default=DESTINATION)
    arguments = parser.parse_args()
    data = json.loads(arguments.inventory.read_text(encoding="utf-8"))
    if data["schema"] != 2:
        raise ValueError("请先用当前工具重新导出运行登记，需要格式第二版")
    declarations, types, texts = source_index()
    arguments.output.write_text(render(data, declarations, types, texts, locale_index()), encoding="utf-8")
    print(f"已生成 {arguments.output}（{len(data['entries'])} 项实际登记）")


if __name__ == "__main__":
    main()
