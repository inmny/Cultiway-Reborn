# 游戏内调试：DebugToolbox 远程脚本通道

## 独立 worktree 开发

新开发任务使用仓库 [develop 技能](../.agents/skills/develop/SKILL.md)准备独立游戏环境。它统一设置游戏、客户端、配置和临时目录，避免多个任务读取同一个 `session.json`。

在目标 worktree 中，首次执行：

```bash
python3 .agents/skills/develop/scripts/dev.py setup --game <游戏目录> --toolbox <DebugToolbox目录>
python3 .agents/skills/develop/scripts/dev.py start
python3 .agents/skills/develop/scripts/dev.py run <脚本.script>
```

已有验证命令通过 `exec -- <命令>`继承该环境；`stop`退出游戏并保留存档，`finish --pr <PR地址>`在正式交付后归档证据、回收游戏副本。代码或资源改变后先 stop/start，再验证。第一版支持 Linux 原生游戏，需要 Python 3.11 以上、.NET SDK 和已安装的游戏及 DebugToolbox。

以下为调试通道本身的说明。并行任务的启动与会话选择以 develop 技能为准。

调试本模组时，推荐通过 DebugToolbox 在**运行中的游戏**里直接执行 C#：脚本在 Unity 主线程上编译运行，返回值以 JSON 直接传回，编译错误也带行号结构化返回。旧的「打开游戏内 C# Console → 模拟粘贴 → 点运行 → 翻 Player.log」流程已废弃。

获取安装、启动连接、命令与脚本写法等全部操作步骤见 debug 技能（[`.agents/skills/debug/SKILL.md`](../.agents/skills/debug/SKILL.md)）；本文只讲背景与约束。

## 工作原理

DebugToolbox 模组在游戏加载时启动一个只监听本机的 TCP 服务器（NDJSON 协议、会话令牌校验），并把端口和令牌写进 `%TEMP%\DebugToolbox\session.json`；命令行客户端读这个文件建立连接，把脚本送进游戏内的 Roslyn 编译器执行。除了跑脚本，服务器还支持免编译的对象读写与调用（`objects.*`）、日志事件读取（`events.read`）和方法跟踪（`trace.*`）。

## 约束

- 脚本与游戏共用主线程，写死循环会把游戏卡死，只能重启游戏恢复；长等待一律用 `await Game.NextFrame()` 分帧。
- Mono 无法卸载单个脚本程序集，每编译一个脚本就多占一份；约 256 个脚本后重启一次游戏，`stats` 会在接近上限时给出建议。
- 原版 `World.world` 在主菜单下可能为空，访问世界数据前先判空。

## 与其他调试手段的分工

「查状态、改数据、调方法」一律走脚本通道；截图、点按钮这类 **UI 画面验证**用 debug 技能 `scripts/` 里的模拟点击与截屏脚本。GameResources 与贴图资源的核对仍遵循 `ARCHITECTURE.md`。
