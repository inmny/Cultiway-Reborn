# Tripo 官方接口生成

2026-10-13 的当前流程是：模组原贴图 → Imagegen 高清参考 → Tripo P2 直出。固定 5000 面控制和 2K 贴图，生成后保留原始输出，不在本机减面、重建或改材质。当前先验证黑森林的成熟树 `Dark_tree/main_3.png`。

## 安装与密钥

本机已在 `Tools/Tripo/` 安装官方 JavaScript 工具包 `@vastai/tripo-sdk@0.3.0`，需要 Node 18 或更高版本。重新安装：

```powershell
npm.cmd install --prefix Tools/Tripo @vastai/tripo-sdk@0.3.0 --save-exact --cache tmp/npm-cache --ignore-scripts --no-audit --no-fund
```

在 [Tripo 开发者平台](https://platform.tripo3d.com/) 取得 Secret API Key，并将密钥本身存为 Windows 用户环境变量 `TRIPO_API_KEY`。密钥应以 `tsk_` 开头；`tcli_` 是客户端编号，不能代替密钥。网页工作台的登录状态也不能代替 API 密钥。

`tripo.ps1` 优先使用进程环境变量，再读取当前 Windows 用户、系统环境变量。受限终端与用户账户不同的时候，需在正确的用户账户下执行。脚本不打印密钥，也不把密钥放进命令参数或结果记录。

## 使用

在仓库根目录运行。默认输入是已保存的黑森林高清参考，默认新结果目录为 `Art/Worldfall/Dark_tree/tripo-api-v1/`；旧平台结果保留在 `tripo-v1/`。

```powershell
# 只显示输入校验值和提交参数，不调用服务。
& './Scripts/Worldfall/tripo.ps1' -Mode Plan

# 验证鉴权和余额，不生成模型。
& './Scripts/Worldfall/tripo.ps1' -Mode Balance

# 创建一项生成任务，保存编号，等待并下载原始结果。
& './Scripts/Worldfall/tripo.ps1' -Mode Generate

# 暂停或断开后继续查询和下载同一任务，不重复提交。
& './Scripts/Worldfall/tripo.ps1' -Mode Resume

# 查询已知任务，TaskId 填写服务返回的任务编号。
& './Scripts/Worldfall/tripo.ps1' -Mode Task -TaskId '实际任务编号'

# 为其他资产或另一次试验显式指定输入和新的输出目录。
& './Scripts/Worldfall/tripo.ps1' -Mode Plan -AssetId '资产ID' -InputFile '高清参考图.png' -OutputDirectory 'Art/Worldfall/资产ID/tripo-api-v1'
```

地区默认为 `cn`，地址 `https://openapi.tripo3d.com/v3`。国际账号使用 `-Region global`；续接时地区须与原记录相同，不自动切换。

## 参数与检查

使用 `P2-20260801`、`quad=true`、`face_limit=5000`。贴图使用 `v3.0-20250812`、`texture_quality=standard`、`pbr=false`，以原高清图颜色为依据。关闭输入自动修改和自动尺寸调整，保留 UV。几何和贴图种子均为 `20261013`，便于记录条件。

5000 是接口的面数控制；四边面、混合多边面与导出三角面数分别统计。官方 P2 四边面输出可能是 FBX，按服务的真实文件类型保存，不把 FBX 改名成 GLB。API 的贴图质量档位与工作台的“2K”标签分别记录，下载后实际检查是否为 2048×2048，不在本机缩放来冒充达标。

`generation.json` 保存输入校验值、参数、任务编号、阶段、下载文件和实际服务消耗；`task-result.json` 保存服务结果。任务提交请求不自动重试，输出目录已有记录时拒绝再次生成。若请求超时但没有拿到编号，先查看开发者平台的任务列表，再决定如何续接，避免重复计费。

只读检查可用 Blender 执行，实际路径按下载文件填写；原始 ZIP 先解包，保持 FBX 与贴图相对路径。此脚本导入模型并写统计，不保存或重导模型：

```powershell
& 'D:/Program Files/Blender Foundation/Blender 4.4/blender.exe' --background --factory-startup --python 'Scripts/Worldfall/inspect_tripo.py' -- 'Art/Worldfall/Dark_tree/tripo-api-v1/model-original.fbx' 'Art/Worldfall/Dark_tree/tripo-api-v1/inspection.json'
```

当前成熟树参考图为901×1746，轮廓重合84.50%，未达到90%的参考图目标，作为实验输入保存。API 接入或导出检查不代表已经完成 Worldfall 适配、其他生长变体和游戏验收。

接口依据：[官方 P 系列图生模型文档](https://developers.tripo3d.ai/en/docs/generation-image-to-model/p)、[官方工具包](https://github.com/VAST-AI-Research/tripo-js-sdk)。
