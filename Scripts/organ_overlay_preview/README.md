# 妖兽外观预览

```bash
~/.venvs/cultiway/bin/python -m Scripts.organ_overlay_preview
```

该入口读取 `Content/CreatureVisuals/catalog.json` 和实际交付的部件图片，输出 `artifacts/creature_appearance/combinations.png`。

原版帧定位保留在本目录的 `assets.py` 与 `compositor.py`。外观制作、组合规则、资源导出及验证统一见 [organ_gen](../organ_gen/README.md)。旧的静态挂点、轨道调参和注册数据镜像已由主体帧合成取代。
