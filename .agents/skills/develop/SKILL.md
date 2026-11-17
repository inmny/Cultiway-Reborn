---
name: develop
description: 在 Cultiway 中用独立 worktree 完成开发、游戏内验证、提交 PR 和环境收尾；也用于准备或恢复独立调试环境、PR 返工及合并后的工作目录回收。纯讨论和只读分析不创建环境。
---

# 独立开发与调试

一个任务使用自己的 worktree、功能分支和游戏环境。工具当前支持 Linux 原生 WorldBox，使用已安装的游戏和 DebugToolbox，不下载或分发游戏。Windows 环境不启动游戏，说明尚未完成该系统的存档隔离。

实现前读仓库 `Prompts/ARCHITECTURE.md` 和 `Prompts/CODING.md`；UI 和复杂设计继续遵守仓库对应约定。这里只说明环境与交付流程。

## 进入正确的工作目录

- 已在任务所属 worktree 时继续使用它；不要每轮新建目录。
- 需要新建时，先查看现有工作目录与当前修改，选用明确的主线提交。桌面端优先使用 `list_artifacts/create_worktree`，随后显式使用返回路径；命令行可使用 Git worktree。
- 命名分支为 `codex/<任务名>`。应用创建的目录可能起初没有分支，推送前创建功能分支。
- 主工作区的其他任务改动不带入，不整体暂存或重置。用户要求搬运的改动才单独迁移。

以下命令中的 `<工具>` 是本技能目录下 `scripts/dev.py` 的绝对路径；在目标 worktree 运行。脚本可从其他位置调用，并用 `--worktree <路径>` 明确目标。

## 一次准备

首次提供本机路径；脚本记住它们，后续 worktree 可直接执行 setup：

```bash
python3 <工具> setup --game <WorldBox游戏目录> --toolbox <DebugToolbox仓库目录>
python3 <工具> doctor
dotnet build Cultiway.csproj -c Debug
```

默认运行目录为 `~/.local/state/cultiway-dev`，位于源码和玩家安装之外。`--root <目录>` 可更改位置；同一环境后续命令使用相同选项。`--name <名称>` 可在一个 worktree 内区分多个环境。

需要 Python 3.11 以上、.NET SDK、Git、GNU cp；PR 交付检查使用已登录的 `gh`。本机图形驱动无法建立 OpenGL 上下文时，可在 setup 加 `--software-rendering`，仅对该环境使用 Mesa 软件渲染；它的结果不能作为性能验收。

setup 准备私有游戏和客户端，不启动游戏。它生成被 Git 忽略的 `.cultiway-dev.props`，为当前 worktree 设置游戏引用。不要把这些本机路径提交到仓库。

文档改动无需准备游戏。无需反复 clean 或构建客户端；setup 会复用相同工具版本。

## 开发与验证

```bash
python3 <工具> start
python3 <工具> run <验证脚本.script> --timeout 120
python3 <工具> exec --timeout 600 -- python3 Scripts/SpellDemos/demo_all.py --list
python3 <工具> status
python3 <工具> stop
```

- 每次 start 在私有游戏内冻结当前代码、资源、本地化和依赖；启动后修改这些输入，需 stop/start 再验证。不能用旧进程验证新代码。
- 保存重启时只执行 stop/start，同一环境的测试资料保留。
- run 保存实际脚本和响应；exec 为现有验证命令注入同一个客户端、配置、临时目录和输出位置。使用共用 `common.py/baseline_scenarios.py` 的入口可接入；其余有固定目录的旧入口按具体需要迁移，不能直接绕回玩家游戏。
- 当前输出目录可由游戏脚本通过 `Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT")` 获取。固定写入模组 `artifacts` 的旧脚本也会写到私有快照，交付时归档。
- 同一环境一次只接受一条控制命令。另一个任务必须用自己的环境；桌面输入与性能测量还要单独安排。
- 命令失败或中断会标记状态不确定，不自动重发世界修改。检查已保存的错误，stop/start 后重新验证，通过后才能 finish。
- 使用 `debug` 技能中的脚本写法和调试约束，但启动和客户端选择统一走本技能，不能使用系统默认会话文件。

## 提交 PR 与收尾

1. 按实际改动完成必要验证，明确没有完成的范围。提交中文 Conventional Commit 信息。
2. 推送所属分支，创建或更新 PR；对照开工时的基线确认差异仅包含本任务。若远端主线落后本地已选基线，先明确评审基线，避免把历史开发提交夹进 PR。描述玩法影响、验证、资源新增及手动迁移。桌面端将 PR 附加到当前聊天。
3. 草稿 PR 可以提前创建。正式交付时，确认本地修改已提交推送、输入与最后运行快照一致，再执行：

```bash
python3 <工具> finish --pr <完整PR地址>
```

finish 使用已登录的 `gh` 只读核对 PR 与提交，先归档输入、结果、必要日志和测试资料，再回收所属游戏副本。证据保存在运行根目录的 `evidence/` 下，不随 worktree 删除。分支和 worktree 继续保留。

PR 创建或推送失败时不视为交付。不要跳过 finish 的失败检查直接删目录。暂停任务时 stop 即可，保留现场。

仅结束试用或放弃任务时，可执行 `clean`：同样先归档再回收游戏，保留代码和 worktree，但不会标记 PR 已交付。若归档后回收中断，重试原命令会继续清理剩余目录并保留已有证据。

## 返工、恢复和合并后回收

- 返工在原 worktree 再执行 setup/start，使用同一个 PR；不要创建重复任务。
- 异常退出后，status 查看已有进程。start 不另起一个重复实例；必要时 stop 后恢复。同环境的锁会随进程退出释放。
- 合并后执行 `reap`，它会核对远端实际合并状态和本地是否有新改动：

```bash
python3 <工具> reap
```

- 应用管理的 worktree 通过检查后使用应用的 `archive_worktree`；普通 Git worktree 可执行 `reap --remove-worktree`。保留本地分支和证据，不删除远端分支。
- 回收不是合并权限。是否合并按用户与仓库约定；不把一轮对话停止当作开发结束，也不承诺程序被强制关闭后立即完成清理。

完整设计及当前交付范围见仓库 `Doc/并行开发与隔离调试-摘要.md`。
