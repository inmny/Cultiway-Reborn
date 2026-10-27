# Tripo 黑森林树木

当前工作见[黑森林树木实装](黑森林树木实装-摘要.md)：补齐 Dark_tree 四种正常树形与四种枯树，使用 Imagegen 高清参考和 Tripo 模型，接入 Worldfall；游戏效果由用户自行查看。

成熟树 main_3 复用2026-10-13在 Tripo 工作台生成的原始2K模型，其余七种形态于2026-10-14通过 API 生成。正常和枯树编号按原版 animData_index 成对匹配；mini_0 是二维小地图图标，无独立模型。

## 当前采用的来源

- 成熟树参考：Art/Worldfall/Dark_tree/reference/tripo-v1/tripo-input.png。
- 成熟树原模型：Art/Worldfall/Dark_tree/tripo-v1/model-2k.glb，嵌入2048×2048纹理，7628三角面。工作台当时显示5452多边面、5381顶点，与三角化GLB的统计分开记录。
- 七张新参考：Art/Worldfall/Dark_tree/reference/stages-v2/<frame>/reference.png，各自保存原图对应信息与实际 Imagegen 提示词。
- 七份 API 原模型：Art/Worldfall/Dark_tree/tripo-api-v2/<frame>/model-original.fbx，任务记录、参数、原纹理和实际面数可追溯。
- 转换与检查：Art/Worldfall/Dark_tree/tripo-api-v2/worldfall-v1/。

## 控制与限制

用户要求平台5000面控制、2K贴图，生成后保持原几何。本轮不本地减面、重建或改绘贴图。5000是 P2 四边面控制值，不能等同于实际多边面数或游戏三角面数；均记录实测。

成熟树选定参考的轮廓重合为84.50%，未通过此前90%目标，作为已选定的实验来源复用。七张新参考做了单体、透明、枝组、配色和完整性目视检查，没有将目视核对写成90%轮廓量化通过。

Worldfall 1.0.0 的当前格式最多64种逐面颜色。2K原纹理完整保留在原始模型及带纹理来源中，游戏模型从原UV取色转为色表；运行外观存在这一颜色细节限制。

API工具及续接说明见[Tripo使用说明](../../Scripts/Worldfall/Tripo使用.md)。此前零余额与工作台编号无法由 API 查询的状态已被新任务结果取代；七个 API 任务全部成功下载，实际消耗770积分。