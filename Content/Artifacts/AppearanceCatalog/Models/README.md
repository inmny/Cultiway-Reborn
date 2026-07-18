# 法器模型资源

目录结构固定为 `Models/<module>/<variant>.obj`，每个模型同时包含：

- `*.obj`：C# 运行时实际读取的低模几何；材质名编码材质角色和表面类型。
- `*.mtl`：只用于通用 3D 查看器预览，不参与游戏内最终配色。
- `*.anchors.json`：variant 锚点坐标；运行时以此为准，模块内所有 variant 必须具有相同的锚点 key。

从精细低模配方重建并检查资源：

```powershell
python -m Scripts.artifact_compose.rebuild_runtime_models
python -m Scripts.artifact_compose.rebuild_runtime_models --check
python -m Scripts.artifact_compose.export_runtime_models --check
```

第一条命令从同一份配方重建全部 OBJ 和 MTL，第二条检查这些文件是否与配方一致。
第三条只校验当前 OBJ、表面语义和锚点。

旧 `parts` 仅保留作迁移回退。需要覆盖回旧模型时必须显式运行：

```powershell
python -m Scripts.artifact_compose.export_runtime_models --force-legacy
```
