---
name: codeline-stats
description: 统计 Cultiway 项目及原版 WorldBox 的 C# 代码行数，区分全量与排除堆量注册器（Cultiway 的 ExtendLibrary 继承类、原版的 *Library.cs）后的纯逻辑代码量。使用预验证的 bash 脚本（基于 rg/find/awk），规避本环境的 python 缺失、grep 卡顿、PowerShell 路由等已知问题。当用户请求统计代码行数、代码规模、对比 Cultiway 与原版体量、评审变更范围、或提到 count_source_lines、ExtendLibrary、纯逻辑代码时使用。
---

# Cultiway 代码行数统计

本 skill 封装了一套在本机 Windows Git Bash 环境下**经过验证可靠**的统计流程。所有命令已固化在 `scripts/stats.sh`，直接调用即可，不要在现场临时拼 shell。

## 何时使用

- 用户问"统计代码行数 / 代码规模 / 有多少行"
- 评审变更范围、PR 体量评估
- 对比 Cultiway 与原版 WorldBox 的代码体量
- 提到 ExtendLibrary、纯逻辑代码、堆量文件

## 快速使用

始终从项目根（`Cultiway/`）运行：

```bash
bash .agents/skills/codeline-stats/scripts/stats.sh            # 全量对比（Cultiway + 原版）
bash .agents/skills/codeline-stats/scripts/stats.sh cultiway   # 仅 Cultiway 三层统计
bash .agents/skills/codeline-stats/scripts/stats.sh vanilla    # 仅原版三层统计
bash .agents/skills/codeline-stats/scripts/stats.sh dirs       # Cultiway 按顶层目录分组
```

输出是 TSV 纯文本，agent 负责整理成 markdown 表格呈现给用户。

## 三个统计维度

| 维度 | Cultiway | 原版 WorldBox |
|------|----------|--------------|
| 统计目录 | `Source/**/*.cs` | `.GameSource/Assets/Scripts/Assembly-CSharp/**/*.cs` |
| 堆量识别 | ExtendLibrary 类的主文件 + partial 分文件（A∪B 并集） | 文件名 `*Library.cs` |
| 纯逻辑 | 全量 − ExtendLibrary 文件 | 全量 − Library 文件 |

**数据口径**：物理行，含空行与注释。与仓库自带 `Scripts/count_source_lines.py` 一致（但该脚本因 python 不可用而无法运行，见下）。

## 核心环境约束（必读，避免重复踩坑）

以下结论来自实测，**不要在现场违背**：

1. **Python 不在 PATH**：`python` / `py` / `python3` 均返回 exit 9009。仓库自带的 `Scripts/count_source_lines.py` 无法直接调用。→ 全程用 bash 工具链。

2. **禁用 GNU `grep -rE` + 复杂正则**：在本环境对 700+ 文件扫描会出现严重卡顿（50s+ 无输出）。→ **必须用 `rg`（ripgrep，已预装）**，秒级返回。

3. **`rg` 输出 Windows 反斜杠路径**，`xargs`/`wc` 无法打开。→ 必须 `tr '\\' '/'` 转正斜杠后再统计。`find` 输出正斜杠，无需转换。

4. **避免 PowerShell 复杂命令**：含中文/特殊字符的多语句 PowerShell 会被错误路由到 bash 报语法错误。→ 统一用 bash。

5. **用 `find -print0 | xargs -0 wc`**，不要用 `find -exec wc {} +`，前者减少 fork 次数，明显更快。

6. **命令可能被转入后台**：长命令执行超过约 30s 会被自动后台化。若 Shell 返回 `isBackground: true`，用 `AwaitShell` 轮询等待，pattern 匹配关键输出（如 `files:`、`pct`）。

## ExtendLibrary 识别口径（关键：必须覆盖 partial 分文件）

Cultiway 的"堆量"特指 ExtendLibrary 注册器类。**一个堆量类的代码常被拆成多个 partial 文件**——主文件含继承声明（`class Foo : ExtendLibrary<...>`），分文件只写 `partial class Foo`（如 `SkillEntities.Fire.cs`、`ArtifactAbilities.Bell.cs`、`Actors.Ming.cs`）。**只匹配继承声明会漏掉约一半堆量**（实测漏 47 个分文件、约 1.5 万行）。

因此脚本用并集识别：

- **(A)** 含继承声明的文件：`class\s+\w+[^{]*:\s*[^{]*\bExtendLibrary\b`（主文件，`sealed`/`class`/`partial` 均命中）
- **(B)** 这些类的 partial 分文件：先从 (A) 用 `sed` 提取类名集合，再用 `partial\s+class\s+(<类名>|...)\b` 反查

堆量文件集 = A ∪ B（`sort -u` 去重）。非 partial 的 ExtendLibrary 类（如 `sealed class`）只在 (A)，无分文件，同样正确覆盖。

典型 partial 堆量分片模式（按维度切分纯数据注册）：
- 按元素：`SkillEntities.Fire/Water/Wind/Wood/Ice/Metal/Poison/Lightning/YinYangEntropy.cs`
- 按种族：`Actors.Ming/EasternHuman/FantasyCreature/ConstraintSpirit.cs`
- 按法宝：`ArtifactAbilities.Banner/Bell/Ding/Fan/Gourd/Mirror/Pearl/...cs`

## 输出解读要点（汇报时附带）

- **三层结构**：FULL / 堆量 / Logic，附堆量百分比。
- **架构对比信号**：Cultiway 堆量占比（约 20%，含 partial 分文件）仍低于原版（约 29%），印证 Cultiway 把数据堆量外移到 `Tables/*.csv → JSON`，C# 侧只保留薄注册器。
- **Top 堆量大头**：列出全部堆量文件（A∪B）按行数降序，单独跑：

  ```bash
  names=$(rg -n 'class\s+\w+[^{]*:\s*[^{]*\bExtendLibrary\b' Source -g '*.cs' | sed -E 's/.*class[[:space:]]+([A-Za-z0-9_]+).*/\1/' | sort -u)
  pat=$(printf '%s' "$names" | paste -sd'|' -)
  { rg -l 'class\s+\w+[^{]*:\s*[^{]*\bExtendLibrary\b' Source -g '*.cs';
    rg -l "partial\s+class\s+($pat)\b" Source -g '*.cs'; } 2>/dev/null \
    | tr '\\' '/' | sort -u | xargs wc -l \
    | awk '/total$/{next}{print $1"\t"$2}' | sort -rn | head -20
  ```

  （此命令复刻脚本的 A∪B 逻辑；`$2` 假设路径无空格，Cultiway 源码文件名均无空格，安全。）

## 故障排查

- **脚本无输出或转后台**：用 `AwaitShell` 等待；若卡死，确认没有残留后台 grep 进程（`kill` 掉）。
- **rg 未找到**：理论上预装；若缺失，回退到 `find -name` + `xargs wc` 做全量统计，但 ExtendLibrary 精确识别会受限。
- **原版目录不存在**：`.GameSource/` 是 gitignored 的本地反编译源码，路径以脚本内 `VANILLA` 变量为准；若用户机器未放置，`vanilla` 模式会提示 not found，属正常。
