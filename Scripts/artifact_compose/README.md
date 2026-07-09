# Artifact Compose

这个目录先只做离线生成器，不接入 `Source/`，也不直接写入 `GameResources/`。

当前重点是 `compose3d`：用低模 3D 模块生成 28x28 法器 PNG，并在预览时同时导出 OBJ/MTL。`compose` 是早期 2D 矢量原型，保留用于对照。

## 3D 概念边界

- `template`：只定义组合规则。它声明哪些 slot 使用哪些 `module`，用 module variant 的哪个 anchor key 对齐到什么位置，以及相机、光线、视角参数。模板不指定具体 variant，也不保存配色。
- `module`：同一类可替换部件的容器，例如剑身、剑格、镜体、鼎体。module 只持有自己的 `variant` 列表。
- `variant`：module 下的固定形态。variant 只保存锚点和几何模型；同一 module 内所有 variant 必须拥有完全相同的 anchor key 集合，但锚点坐标和具体模型可以不同。
- `Instance`：一次具体生成结果。它记录使用的 template、每个 slot 选中的 variant，以及每个 slot 的实际配色。PNG、OBJ、MTL 都从 Instance 渲染。
- `colors.json`：独立颜色方案库，不属于 template、module 或 variant。生成 Instance 时只从这里取候选颜色，并把最终颜色写回 Instance。

## 常用命令

```powershell
python Scripts/artifact_compose/cli.py list3d
python Scripts/artifact_compose/cli.py validate3d --render-smoke --output Temp/artifact_compose_validate3d
python Scripts/artifact_compose/cli.py compose3d --all --count 3 --output Scripts/artifact_compose/output3d
python Scripts/artifact_compose/cli.py compose3d --shape sword --count 8 --seed test_sword
python Scripts/artifact_compose/cli.py compose3d --template sword_diagonal_steep --count 8 --output Temp/artifact_compose_sword
```

输出包含：

- `*.png`：28x28 像素图，硬 alpha。
- `*_preview.png`：最近邻放大预览图，便于人工看形状。
- `*.obj` / `*.mtl`：预览时默认导出的低模组合结果；需要只看 PNG 时加 `--no-obj`。
- `manifest.json`：记录每个 Instance 的 template、variant 选择、slot 配色和输出路径。

## 数据文件

- `data3d/modules.json`：module、variant、锚点和低模几何体。
- `data3d/templates.json`：五种基础器形的模板，只包含 slot 放置、锚点匹配、相机和光照。
- `data3d/colors.json`：Instance 生成时使用的颜色方案库。

3D 几何当前支持 `box`、`poly_prism`、`blade`、`cylinder`、`frustum`、`ellipsoid`。坐标使用以模型中心附近为原点的局部空间，最终由 template placement 变换并投影到 28x28。

后续如果要接入游戏，只需要把人工筛选后的 PNG 移到 `GameResources/cultiway/icons/item_shapes/...`，再配置对应 `ItemShape` 的 texture folder。

## 2D 原型

早期 2D 矢量路径仍可运行：

```powershell
python Scripts/artifact_compose/cli.py list
python Scripts/artifact_compose/cli.py validate --render-smoke --output Temp/artifact_compose_validate
python Scripts/artifact_compose/cli.py compose --all --count 3 --output Scripts/artifact_compose/output
```

这一路径的数据仍在 `data/` 下，用于和 3D 管线对照，不作为当前炼器贴图方案的主结构。
