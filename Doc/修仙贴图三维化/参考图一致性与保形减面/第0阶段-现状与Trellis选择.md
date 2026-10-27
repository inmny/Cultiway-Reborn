# 第0阶段：现状与 Trellis 选择

## TODO

- [x] 核对本机实际运行器、权重、参数和显卡。
- [x] 对照原图、清晰参考图及旧提示词，定位一致性偏差。
- [x] 查官方源码和说明，区分各版本的多图支持。
- [x] 区分显卡容量、整机当前占用与 Trellis 推理峰值。
- [ ] 按第3阶段采样一次完整 512/Q4 生成的显存曲线。

## 具体说明

### 旧参考图偏差

实际查看了三栋群系建筑的原图和 `reference/trellis-input-lowpoly-v1.png`，并读取 `prompt-lowpoly-v1.txt`。

| 资产 | 原图可见特征 | 当前参考图变化 | 需要锁定的内容 |
| --- | --- | --- | --- |
| 七重溃烂熔炉 | 骨色分叉、绿色主体、低处开口、根部结构 | 分叉和绿色体块明显变粗，入口及基部重新组合 | 主体与冠部比例、分叉位置、低处开口及基部轮廓 |
| 颅献黄铜王座 | 高长的黄铜背柱、顶部冠饰、红色尖刺与面具、底座 | 尖刺数量和粗细被重新解释，基座与阶梯体量增大 | 背柱比例、冠饰宽度、尖刺布置、面具与底座的相对位置 |
| 万变星璇祭坛 | 四根细长蓝柱、中央眼状结构与蓝色卷曲框架、低台阶 | 柱体和台基变厚，前后柱高不同，中央结构和阶梯重新组合 | 四柱位置与比例、卷曲框架、中央眼状结构的高度和台阶轮廓 |

旧提示词要求“粗大闭合多面体”和约 1,500 面以下，明确允许减少尖刺或改变柱高；同时从原图视角换到前右四分之三视角。它们适用于风格探索，但会让参考图提前失去原图结构。新方案取消这些几何简化要求，先完成忠实的清晰参考图。

### 本机实际使用的版本

读取 `Scripts/Worldfall/trellis-config.json`、`trellis.ps1`、安装时保留的说明及实际 CLI 帮助后确认：

| 项目 | 本机值 |
| --- | --- |
| 生成模型系列 | TRELLIS.2-4B |
| 运行器 | 第三方 `pwilkin/trellis.cpp v0.8.1`，Windows CUDA |
| 权重 | `ilintar/trellis2-gguf`，Q4 |
| 生成档 | 512，12 步，seed 42 |
| 输出贴图 | 2048×2048，PNG |
| 权重文件 | 8 份，共 5,085,194,688 字节，约 4.736 GiB 的磁盘文件 |
| 显卡 | RTX 3060 Laptop，6,144 MiB 专用显存 |

权重文件的磁盘总大小不能当作推理显存。C++ 源码明确按阶段加载和释放模型；其默认使用半精度计算和 FlashAttention，并提供较轻的 512 路径。Q4 降低权重存储，生成分辨率和中间计算也影响占用。[C++ 实现](https://github.com/pwilkin/trellis.cpp/blob/v0.8.1/src/trellis_cli.cpp)、[运行器说明](https://github.com/pwilkin/trellis.cpp/tree/v0.8.1)。

历史王座日志记录 CUDA 设备和真实网格导出成功：`generated-lowpoly-v1.glb` 为 149,646 个三角面，生成耗时 157.8 秒。日志中的 `Total VRAM: 6143 MiB` 是检测到的显卡总容量，`bvh-mem ... 0.11 GiB` 只是某一处理环节的工作量估计；两者都不是全流程显存峰值。

当前 C++ 后处理自身已经执行过一次减面：该次王座日志记录 4,782,904 面到 149,646 面。项目保存的“三维初稿”指这个实际导出的 GLB，不能将它描述为未经任何处理的数百万面网格。

### 三视图支持

| 路径 | 支持情况 | 本项目如何使用 |
| --- | --- | --- |
| 官方 TRELLIS 1 | 有 `run_multi_image`，支持 `stochastic` / `multidiffusion` 两种实验方案 | 可以提交同一物体的三张独立视图；比例、姿态和细节须一致 |
| 当前官方 TRELLIS 2 | `run(image: Image.Image, ...)` 是单图入口，当前公开生成管线未提供 TRELLIS 1 那样的多图入口 | 单图生成；其他视图用于检查和手工修正 |
| 本机 trellis.cpp v0.8.1 | CLI 接收一个 `--image`；本机封装也仅传入一张图 | 维持单个完整物体输入，三视图另存 |

TRELLIS 1 的多图模式没有专门重新训练，官方明确表示不一致姿态或细节可能产生较差结果。它也不是有相机标定的严格三维重建；当前公开调用没有传入每幅图的相机位置。[官方多图示例](https://github.com/microsoft/TRELLIS/blob/main/example_multi_image.py)、[多图实现](https://github.com/microsoft/TRELLIS/blob/main/trellis/pipelines/trellis_image_to_3d.py)、[官方演示说明](https://github.com/microsoft/TRELLIS/blob/main/app.py)。

TRELLIS 2 的内部 `get_cond` 可以接收图像列表，但生成入口实际调用的是 `[image]`；这一点不足以证明支持多视图融合，也不能直接把列表塞入单图 `run`。[官方 TRELLIS 2 生成源码](https://github.com/microsoft/TRELLIS.2/blob/main/trellis2/pipelines/trellis2_image_to_3d.py)。

检查用三视图拼图可以展示给人看；单图模式输入应裁出一个视图，保留透明背景和单个完整物体。拼接三视图直接输入可能被解释为多个物体。

### 显存差距与能否使用 Trellis 2

| 对照项 | 官方要求或本机事实 | 可得结论 |
| --- | --- | --- |
| 官方 TRELLIS 1 | 至少 16GB，官方测试 Linux | 本机标称 6GB，容量相差约 10GB；没有验证官方原版 |
| 官方 TRELLIS 2 | 至少 24GB，官方测试 Linux | 本机容量相差约 18GB；没有验证官方原版 |
| 本机量化 TRELLIS 2 | 多次 512/Q4 完整生成成功 | 已能使用这条路径，不能据此保证官方原版或高档分辨率也能运行 |
| 本轮实时 GPU 快照 | 2026-10-12 18:33:29 +08:00，已用 2,291 MiB、空闲 3,704 MiB | 当时 WorldBox PID 22268 正在运行，Trellis 未运行；这是整机占用 |
| 历史推理峰值 | 没有连续采样记录 | 无法给出可靠的“实际节省了多少峰值显存”数值 |

官方要求来源：[TRELLIS 1](https://github.com/microsoft/TRELLIS#prerequisites)、[TRELLIS 2](https://github.com/microsoft/TRELLIS.2#prerequisites)。最低容量要求和某次实际推理峰值是不同指标。当前实现使用量化权重、按阶段释放和较低生成档，不能把容量差全部归因于量化，也不能保证与原版输出逐项相同。

本机 `nvidia-smi --query-compute-apps` 的逐进程显存返回 `N/A`，因此后续须把整卡采样峰值、背景占用和进程统计能否取得分别记录。当前游戏实例未关闭、未修改，本轮没有启动新的 GPU 推理。

选择：继续用已有的 **TRELLIS 2 Q4 / 512 / Windows C++** 生成。先改善输入参考图和减面流程。升级为官方原版、1024/1536 档或额外模型应另做完整实测；当前本地权重目录仅具备这条 512 路径所需的模型。

## 资源说明

| 资源 | 作用 |
| --- | --- |
| `Scripts/Worldfall/trellis-config.json` | 固定本机版本、权重来源与参数 |
| `Scripts/Worldfall/trellis.ps1` | 实际单图调用、任务锁及生成记录 |
| `Art/Worldfall/Cultiway.KhorneTower/reference/trellis-lowpoly-v1.log` | 设备、步骤、已有减面与成功导出证据 |
| `Art/Worldfall/检查包/2026-10-12-群系建筑低面风格/` | 已实际查看的原图与参考图 |

本轮核对的是安装配置、运行日志和官方公开能力，没有将未执行的峰值采样或模型新版本记为通过。
