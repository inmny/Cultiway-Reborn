---
name: debug
description: 在运行中的 WorldBox 游戏里通过 DebugToolbox 执行 C# 调试脚本：查游戏对象状态、调用模组方法、推进世界状态做验证、读运行时事件。完整流程是「启动游戏 → 等 %TEMP%\DebugToolbox\session.json 刷新 → DebugToolbox.Client.exe run 脚本.csx」，结果以 JSON 直接返回，编译错误带行号。当用户要求游戏内验证、实测功能、查看运行中对象、复现运行时问题，或提到 DebugToolbox、session.json、script.run 时使用。仅适用于已安装 DebugToolbox 的本机环境。
---

# 游戏内调试通道

在运行中的 WorldBox 里通过 DebugToolbox 执行 C#：脚本在 Unity 主线程编译运行，返回值以 JSON 直接传回，编译错误带行号，不需要再翻 Player.log。

## 获取与构建

1. DebugToolbox 源码在公开仓库 <https://github.com/inmny/DebugToolbox>，克隆到 WorldBox 的 Mods 目录（与 Cultiway 平级）：
   ```bash
   cd "C:\Program Files (x86)\Steam\steamapps\common\worldbox\Mods"
   git clone git@github.com:inmny/DebugToolbox.git
   ```
2. 游戏内部分由 NML 自动编译，无需手动构建；在模组列表里保持 DebugToolbox 启用。
3. 命令行客户端构建一次（需要 .NET 10 SDK）：
   ```bash
   cd DebugToolbox && dotnet build Client/DebugToolbox.Client.csproj -c Debug
   ```

## 启动与就绪判断

1. 启动游戏：直接运行 `worldbox.exe` 即可，不必通过 Steam。
2. 就绪标志：`$TEMP/DebugToolbox/session.json` 的修改时间晚于游戏启动时间（模组加载后几秒内写出）。游戏每次重启端口都会变，客户端自动读会话文件拿端口和令牌，不要自己解析端口。

## 客户端与常用命令

```bash
CLIENT="C:\Program Files (x86)\Steam\steamapps\common\worldbox\Mods\DebugToolbox\Client\bin\Debug\net10.0\DebugToolbox.Client.exe"

"$CLIENT" hello                    # 握手，确认通道可用
"$CLIENT" run 脚本.csx             # 执行脚本文件（正式调试都用这种）
"$CLIENT" eval 'return 1+1;'      # 执行一小段内联代码
"$CLIENT" events 0 100            # 读取日志/异常/脚本事件
"$CLIENT" inspect obj:1 2         # 查看脚本返回的句柄对象（深度 2）
"$CLIENT" stats                   # 脚本程序集数量、内存等运行统计
```

## 脚本写法

脚本是 `.csx` 风格的顶层语句，`return` 的值就是返回给命令行的结果：

- 在 Unity 主线程执行，可直接使用 WorldBox、Unity、NML、Harmony 和所有模组的类型（包括 `Cultiway.*`），不需要 using。
- 需要「等一帧再继续」时用 `await Game.NextFrame();`。
- `Log.Info(...)` 的内容出现在响应的 `Events` 数组里。
- 基类提供辅助门面：`Game`（帧、主线程调度）、`Objects`（查找场景对象）、`Inspect`（对象摘要）、`Trace`（挂载方法跟踪）、`Errors`（读错误事件）、`Log`、`Screen`（截图）、`Mods`（列出模组程序集）。
- 返回值里无法 JSON 化的对象会变成句柄（如 `obj:12`），之后用 `inspect obj:12` 继续查看。
- 编译失败时响应的 `Diagnostics` 数组带错误码、行号、列号和出错代码上下文，按提示改脚本，不要靠猜。

```csharp
// 例：查世界里的生物数量并等一帧
var units = World.world.units.getSimpleList();
await Game.NextFrame();
return new { total = units.Count };
```

## UI 画面验证脚本

脚本通道不解决画面问题。截图、点按钮、悬停看提示这类 UI 验证用本技能 `scripts/` 下的 PowerShell 辅助脚本（直接调系统 API，不依赖其他工具）：

```bash
S=".agents/skills/debug/scripts"

powershell -NoProfile -File "$S/shot.ps1" -OutPath shot.png   # 全屏截图（覆盖所有屏幕）
powershell -NoProfile -File "$S/click.ps1" -x 960 -y 540      # 移动并左键单击
powershell -NoProfile -File "$S/click.ps1" -x 960 -y 540 -Double   # 双击
powershell -NoProfile -File "$S/click.ps1" -x 960 -y 540 -Wheel -3 # 滚轮，负值向下滚
powershell -NoProfile -File "$S/hover.ps1" -x 960 -y 540      # 仅移动指针（触发 Tooltip）
powershell -NoProfile -File "$S/keys.ps1" COPY                # 发 Ctrl+A、Ctrl+V
powershell -NoProfile -File "$S/keys.ps1" ENTER               # 发回车
```

坐标约定：脚本都声明了 DPI 感知，`shot.ps1` 截图里的像素坐标可以直接喂给 `click.ps1`/`hover.ps1` 的 `-x/-y`，不需要换算。向输入框贴文本先用 PowerShell `Set-Clipboard '内容'` 放进剪贴板，再对输入框点一下获得焦点，然后 `keys.ps1 COPY`。

## 注意事项

- 先 `hello` 再干活；连接被拒基本就是游戏没开或还在加载，等 session.json 刷新后重试。
- Git Bash 里 `eval` 的代码用单引号包裹，避免 `$` 被展开。
- 原版 `World.world` 在主菜单可能为空，访问世界数据前先判空。
- 脚本与游戏共用主线程，写死循环会把游戏卡死只能重启恢复；长等待一律用 `await Game.NextFrame()` 分帧。
- Mono 无法卸载单个脚本程序集，`stats` 会在接近上限时给出建议，约 256 个脚本后重启一次游戏。
