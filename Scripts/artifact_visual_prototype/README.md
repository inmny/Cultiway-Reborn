# 法宝视觉 Blockbench 原型

该目录是新美术生产管线的独立参考实现，用于快速生成对照图和验证确定性。
运行时对应实现位于 `Source/Content/Artifacts/ArtifactAppearance*.cs`，实际模型资源位于
`Content/Artifacts/AppearanceCatalog/Models/`。

## 数据边界

- `Template`：只定义模块槽位、锚点 key、放置变换、相机和光线。
- `Module`：收纳一组 variant，并强制所有 variant 的锚点 key 相同。
- `Variant`：引用一个固定 OBJ 模型；几何体与锚点坐标可以自由变化。
- `Instance`：选择 template、各槽位 variant、实例级调色板和可选专属图标。

模块先分别烘焙为 coverage、depth、material、shade、emission 语义层。所有模块按统一深度组合后，才应用实例调色板和最终外轮廓，因此模块边界不会产生额外黑边。

## 运行

```powershell
python -m Scripts.artifact_visual_prototype
```

默认结果位于 `artifacts/artifact_visual_prototype/`，包括：

- `blockbench_models/*.obj`、`*.mtl`、`*.anchors.json`：可导入 Blockbench 的模块 variant 源模型。
- `<instance>/assembled.obj`：该 Instance 选择并变换后的组合模型。
- `<instance>/<view>/body.png`：主体像素层。
- `<instance>/<view>/emission.png`：发光叠加层。
- `<instance>/<view>/shadow.png`：地面阴影层。
- `<instance>/<view>/modules/*.png`：模块烘焙调试层。
- `comparison_sheet.png`：三件样例在图标、常态实体和激活实体视图下的对照。
- `manifest.json`：选择结果、尺寸、像素占用与确定性哈希。

重制 Content 中的全部运行时模型后，可生成覆盖每个 variant 的组合画廊：

```powershell
python -m Scripts.artifact_visual_prototype.runtime_gallery --check
```

输出位于 `artifacts/artifact_model_rebuild/runtime_gallery/`，每种器形分别提供 28 像素图标视角和
56 像素激活实体视角。每个模板固定生成三套组合并轮换 variant，画廊会拒绝任何未被覆盖的模型。

## Blockbench 往返

1. 验证原型时导入 `blockbench_models` 下的 OBJ；编辑游戏资源时导入 Content 的 `Models` 目录。
2. 编辑几何体时保留材质名；它们是 `metal`、`trim`、`jade`、`crystal`、`grip`、`cloth`、`glow`、`dark` 等语义角色，不是最终颜色。
3. 以 OBJ 覆盖导出，保留同名 `*.anchors.json`。若修改接合点，只调整 sidecar 中该锚点的三维坐标。
4. 原型资源重新运行本脚本验证；Content 资源启动游戏时由 C# 目录加载器校验同一 Module 的锚点 key。

OBJ 是首阶段用于验证 Blockbench 接口的交换格式。后续换为 GLB 时，只需替换模型导入器，不改变 Template、Module、Variant、Instance 和图层合成协议。
