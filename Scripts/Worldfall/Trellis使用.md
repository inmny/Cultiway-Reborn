# 本地 Trellis 直出模型

制作顺序是本物体的原贴图 → imagegen 单物体参考图 → Trellis 带贴图网格 → Blender 适配 → Worldfall 游戏验收。每个物体交给独立子代理，每批最多 3 个；显卡生成使用同一把任务锁，逐个执行。

## 本机配置

实际生成模型是 **TRELLIS.2-4B**，使用第三方 [pwilkin/trellis.cpp](https://github.com/pwilkin/trellis.cpp) 的 Windows CUDA 原生运行包 v0.8.1，权重来自 [ilintar/trellis2-gguf](https://huggingface.co/ilintar/trellis2-gguf)。机器为 RTX 3060 Laptop、6GB 显存、32GB 内存。当前选择 Q4 权重、512 生成档、12 步、2048 贴图；输出 PNG 贴图的 GLB，便于 Blender 4.4 直接导入。

2026-10-12 核对：当前已经在使用 Trellis 2 的量化实现。官方原版 TRELLIS 1 最低要求16GB显存，TRELLIS 2最低要求24GB；本机量化512路径已有成功记录。早期日志没有峰值采样，本轮三栋样板已经连续采样，整卡专用显存峰值3437–3586 MiB；这是整卡值，不是进程独占值，也不证明官方实现或1024档可运行。具体条件见[三个样板执行记录](../../Doc/修仙贴图三维化/参考图一致性与保形减面/2026-10-12-三个样板执行记录.md)。新参考图与保形减面按 [设计方案](<D:/Program Files (x86)/Steam/steamapps/common/worldbox/Mods/Cultiway-Reborn/Doc/修仙贴图三维化/参考图一致性与保形减面-摘要.md>) 执行。

配置保存在 `trellis-config.json`，运行包与权重的大小、校验值已固定。`Tools/Trellis/` 保存本机运行包、权重和独立 Python 下载环境，受现有 Git 忽略规则排除。工具目录不进入模组发布包。原生推理不依赖全局 Python、CUDA 工具链或 Linux 环境。

2026-10-10 已完成本机实测：八份权重共 5,085,194,688 字节，全部通过 SHA256 校验。大殿首次显卡推理用时 154.9 秒，输出 142,352 个三角面、2048×2048 PNG 贴图的真实 GLB。原始文件约 27.8MB；它还要经过 Blender 适配才能用于 Worldfall。记录见 `Art/Worldfall/Cultiway.SectHall/trellis/generation-v1.json`。

## 使用

在仓库根目录执行：

```powershell
& 'Scripts/Worldfall/trellis.ps1' -Mode Setup
& 'Scripts/Worldfall/trellis.ps1' -Mode Check
& 'Scripts/Worldfall/trellis.ps1' -Mode Generate -AssetId Cultiway.SectHall
```

首次在新电脑安装时，为 Setup 增加 `-PythonExecutable '已有 Python 3.12 的绝对路径'`。脚本会在 `Tools/Trellis/.venv/` 建立独立下载环境并安装固定版本依赖；完成安装后的 Generate 只调用原生运行包。已有权重会按已记录的大小和校验值复用。

输入是 `Art/Worldfall/<资产ID>/reference/trellis-input-v1.png`，一张图只包含一个完整物体。保留透明背景，避免三视图拼接被生成成多个物体。本机运行器是单图入口；官方 TRELLIS 1 有实验性的多图入口，需要独立视图文件，不能将该能力直接套用到本机 TRELLIS 2 运行器。生成结果是 `trellis/generated-v1.glb`；同目录的 `generation-v1.json` 记录输入、输出校验值和实际参数。脚本要求使用显卡，生成失败会报错，不会把空文件算作模型。

人物不同服装、性别分别制作单体输入图，例如：

```powershell
& 'Scripts/Worldfall/trellis.ps1' -Mode Generate -AssetId Cultiway.EasternHuman -Variant fem
```

它读取 `reference/trellis-input-fem-v1.png`，写 `trellis/generated-fem-v1.glb`。东人的四种输入分别使用默认款、`fem`、`warrior`、`warrior-fem`；每种都由自己的图片生成。迭代用 `-Revision 2` 读取和生成 `v2`，保留旧结果。已经存在的输出不会被覆盖。

## 适配要求

- 原始 GLB 和贴图保留，导入 Blender 后先检查正、侧、背面，再按真实网格和 UV 处理。
- 原贴图紫色区是国家色模板。大殿、经藏阁、藏宝阁等文明木构建筑声明 `kingdom_slots=roof,roof_dark`，仅瓦屋顶随国家变色，墙柱梁和檐底保留木色；金饰和石台固定。人物国家衣料进入原生衣色槽，皮肤和头发仍按原规则处理。
- 建筑保持实际占地，台基宽为 1、深为占地深宽比，入口朝 -Y；单位保留原动作部件、转动轴和脚底位置。
- Blender 再生成近、中、远三档与建筑施工、废墟。Trellis 直出本身没有游戏动作、占地和状态，不把这些步骤列为自动完成。
- 生产资源仍为 `Content/Worldfall/Models/<资产ID>/models.json`。先输出候选文件并实际预览，通过后替换；没有真正生成的变体继续保留已有有效模型。

实际安装和出图结果另记在阶段文档及验收目录中。
