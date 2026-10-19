# 妖兽外观制作

运行：

```bash
~/.venvs/cultiway/bin/python -m Scripts.organ_gen --export --preview --check
```

`Content/CreatureVisuals/templates/` 保存字符矩阵源稿；使用 `Scripts/pixel_text.py` 编辑和预览。矩阵中的角、羽翼、膜翼、毛尾和甲壳具有各自的轮廓与色板。多尾模板保留独立尖端，不把同一张三尾图简单放大冒充六尾。

`pipeline.py` 从原版 `.GameSource/Assets` 读取成体走路、游泳及龙的飞行帧，按真实身体轮廓找到头、背和臀部；每份部件按整数像素对齐后落盘。原版不具备适合位置的物种不画对应部件，例如蛇不直接长出一套兽尾或龟壳。

生成结果：

| 产物 | 用途 |
| --- | --- |
| `GameResources/cultiway/yao/appearance/<部件>/<物种>/<动作>.png` | 原版朝向的逐帧部件；四周留白让根点与主体像素对齐 |
| `Content/CreatureVisuals/catalog.json` | 游戏和预览共同读取的位置竞争、遮挡、显示优先顺序与帧尺寸 |
| `artifacts/creature_appearance/combinations.png` | 组合、等级与走路入水对照 |

运行时由 `CreatureAppearanceService` 选择最多三处可见变化，按“背后部件、身体、体表、正面部件”合成一张图片，再交回原版绘制。相同可见部件和原版主体帧共用图片，缓存上限为 2048 张。不同但看起来相同的身体不会重复创建图片；不显示的器官仍参与属性、技能和被动效果。

没有实际制作帧的身体、幼体和特殊皮肤保持原版显示。不会使用静态挂点、邻近动作或另一个物种的图片硬套。

新增器官外观时：先制作字符矩阵，再登记本文件旁 `pipeline.py` 中的位置、优先顺序、遮挡和适用身体；重新导出并查看画廊。游戏通过外观目录加载资源，预览不再维护一份 C# 注册数据的镜像。
