# 可复现构造输入

这些资料通过同一个正式构造登记、学习和施放入口运行。制作资源、运行代码及演示工具固定后，新增“浪中藏锋”仅修改资料，没有增加对应整招类或按能力编号处理的分支。

| 输入 | 实际构造 |
|---|---|
| 翻卷厚浪 | 厚曲面沿共同动作抬升、翻卷和铺开；实际有效窗口绑定crest至spread阶段 |
| 浪中藏锋 | 同一浪体在四个表面挂点承载剑形；刚性剑保持自身尺寸，1.45秒脱离并继承挂点运动，随后独立追踪 |
| 厚浪凝冰与解冻 | 实际命中后本体凝成冰，内部形变暂停；3秒解冻，从保留进度继续，整体飞行始终继续 |
| 熔铁凝壳与复流 | 同一曲面以实际液态铁成立，命中后凝固成金属，3秒重新熔化；实际伤害与显示表面一起变化 |
| 弧线掠袭 | 同一剑形使用目标相对三次曲线，抬升后下降，朝向真实切线并在实际命中处驻留 |
| 盘旋小刃 | 同一剑形沿目标相对曲线盘旋收束，真实经过横向与高度变化，不绘制替代作用的轨迹线 |

复现命令：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/翻卷厚浪.json --view worldfall --duration 3.1 --zoom 14 --keep-frames --output artifacts/skill_rebuild/wave-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/浪中藏锋.json --view worldfall --duration 3.4 --zoom 14 --keep-frames --output artifacts/skill_rebuild/wave-blades-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/厚浪凝冰与解冻.json --view worldfall --duration 4.3 --zoom 14 --keep-frames --output artifacts/skill_rebuild/matter-water-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/熔铁凝壳与复流.json --view worldfall --duration 4.3 --zoom 14 --keep-frames --output artifacts/skill_rebuild/matter-iron-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/弧线掠袭.json --view worldfall --duration 2.3 --zoom 14 --keep-frames --output artifacts/skill_rebuild/flight-arc-current/worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/盘旋小刃.json --view worldfall --duration 2.8 --zoom 14 --keep-frames --output artifacts/skill_rebuild/flight-spiral-current/worldfall
```

将view换为native或firstperson可分别核对原版与实际第一人称。游戏未启动时可加launch；须等启动器完整结束再运行独立检查。制作源或资源修改后先退出游戏、完成构建，再启动验证。

六段已录画面的共同内容指纹为`7a7c697f4083444adfa62070469b23cc34d1188f8a5d62977bc578ff1c1db2d3`。翻卷厚浪每个视角正式施放一次、支付4点真元、对指定目标实际作用一次；浪中藏锋每个视角正式施放一次、支付8点真元、峰值5个形体，对指定目标实际作用4次，包含脱离后3把剑的真实命中。完整接触记录保留在各录像的result.json，不用模型数量替代实际受伤。

这些是构造表达能力和资源接入的开发输入，完整流体、折射、地形承接和最终城市画面仍按全程TODO完成。运行检查通过不代表美术验收通过。


## 身体改造的投送输入

| 输入 | 来源及实际过程 |
|---|---|
| 掌击改追踪飞掌 | 共用资料命令从原掌击补齐冰质显化、腕点、离手、追踪、实际接触及4点真元；身体收势后独立继续 |
| 自编凝冰飞掌 | 真实游戏内表单修改末端侧向、躯干、转体及掌面朝向，再显化、选择冰质并把速度改为10、消耗改为6、选择距离改为14 |

两份资料仅将导出结果的编号、作者、名称及初始修订稳定为演示身份，其余动作、投送、材料、接触及支付保留实际结果。它们是指定单招的可复现输入，不是多招组合配方。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/掌击改追踪飞掌.json --view native --duration 2.7 --zoom 8 --keep-frames --output artifacts/skill_rebuild/body-projection-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/自编凝冰飞掌.json --view native --duration 2.7 --zoom 8 --keep-frames --output artifacts/skill_rebuild/body-edited-current/native
```

将view换为worldfall或firstperson分别检查三维及真实附身，输出目录对应更换。近身的身体发力、离手后的独立形体、一次支付和真实命中仍经同一入口；完整战技资源和最终美术验收继续按全程TODO完成。
