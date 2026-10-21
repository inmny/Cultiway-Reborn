# 可复现构造输入

这些资料通过同一个正式构造登记、学习和施放入口运行。制作资源、运行代码及演示工具固定后，新增“浪中藏锋”仅修改资料，没有增加对应整招类或按能力编号处理的分支。

| 输入 | 实际构造 |
|---|---|
| 翻卷厚浪 | 厚曲面沿共同动作抬升、翻卷和铺开；实际有效窗口绑定crest至spread阶段 |
| 浪中藏锋 | 同一浪体在四个表面挂点承载剑形；刚性剑保持自身尺寸，1.45秒脱离并继承挂点运动，随后独立追踪 |
| 厚浪凝冰与解冻 | 实际命中后本体凝成冰，内部形变暂停；3秒解冻，从保留进度继续，整体飞行始终继续 |
| 熔铁凝壳与复流 | 同一曲面以实际液态铁成立，命中后凝固成金属，3秒重新熔化；实际伤害与显示表面一起变化 |

复现命令：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/翻卷厚浪.json --view worldfall --duration 3.1 --zoom 14 --keep-frames --output artifacts/skill_rebuild/wave-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/浪中藏锋.json --view worldfall --duration 3.4 --zoom 14 --keep-frames --output artifacts/skill_rebuild/wave-blades-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/厚浪凝冰与解冻.json --view worldfall --duration 4.3 --zoom 14 --keep-frames --output artifacts/skill_rebuild/matter-water-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/熔铁凝壳与复流.json --view worldfall --duration 4.3 --zoom 14 --keep-frames --output artifacts/skill_rebuild/matter-iron-current/worldfall
```

将view换为native或firstperson可分别核对原版与实际第一人称。游戏未启动时可加launch；须等启动器完整结束再运行独立检查。制作源或资源修改后先退出游戏、完成构建，再启动验证。

六段已录画面的共同内容指纹为`7a7c697f4083444adfa62070469b23cc34d1188f8a5d62977bc578ff1c1db2d3`。翻卷厚浪每个视角正式施放一次、支付4点真元、对指定目标实际作用一次；浪中藏锋每个视角正式施放一次、支付8点真元、峰值5个形体，对指定目标实际作用4次，包含脱离后3把剑的真实命中。完整接触记录保留在各录像的result.json，不用模型数量替代实际受伤。

这些是构造表达能力和资源接入的开发输入，完整流体、折射、地形承接和最终城市画面仍按全程TODO完成。运行检查通过不代表美术验收通过。
