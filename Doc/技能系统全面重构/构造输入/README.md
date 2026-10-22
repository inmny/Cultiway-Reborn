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

## 真实接触推开与牵引

“接触推开厚浪”和“接触牵引厚浪”保留已有翻卷形体及运动，分别在真实接触后向外推开或向来源牵引，不造成伤害。两者共用同一施放和碰撞入口，实际消耗4点真元，未增加整招专用执行类型。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触推开厚浪.json --view native --duration 4 --target-offset 16 0 --zoom 20 --keep-frames --output artifacts/skill_rebuild/contact-push-current/native
```

资料的推拉力分别为正负18，离地力度为0.5；这些是原版受力参数，不是保证移动的距离。完整语义、检查和边界见[目标筛选与接触推拉](../目标筛选与接触推拉.md)。

## 事件之后才开始的部位

| 输入 | 同次施放内的真实过程 |
|---|---|
| 接触后生长冰枝 | 冰质投送物真实接触后结束，延迟0.12秒才从接触位置生长枝体；后段自己的0.25秒有效起点控制实际作用 |
| 掌击接触后凝枝 | 真实掌击接触后延迟0.1秒开始枝体，自己的0.35秒有效起点晚于身体0.48秒收势，后段继续独立作用 |

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触后生长冰枝.json --view native --duration 3.4 --target-offset 8 0 --zoom 8 --keep-frames --output artifacts/skill_rebuild/activation-seed-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/掌击接触后凝枝.json --view native --duration 2.2 --zoom 6 --keep-frames --output artifacts/skill_rebuild/activation-palm-current/native
```

对应更换视角与输出目录可检查Worldfall三维及附身。两份输入均一次施放、一次支付，同一目标由前后两个真实作用部位分别接触；不是多招配方。完整语义和已知边界见[事件驱动的部位生成](../事件驱动的部位生成.md)。


## 同一主体上的事件运动

| 输入 | 同次施放内的真实过程 |
|---|---|
| 接触停止后继续飞行 | 原剑体实际接触后保持姿态，0.4秒后在同一主体上重新飞行；命中次数、余力和身份继续保留 |
| 冻结后真实坠落 | 高处浪体在1.35秒凝冰并保持形变，延迟0.12秒后受地图高度方向加速度下落，以实际作用形状触地停止 |

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触停止后继续飞行.json --view native --duration 4.2 --target-offset 8 0 --zoom 8 --keep-frames --output artifacts/skill_rebuild/motion-resume-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/冻结后真实坠落.json --view native --duration 4.6 --target-offset 6 0 --zoom 20 --keep-frames --output artifacts/skill_rebuild/motion-fall-current/native
```

对应更换视角及输出目录核对三维与附身。冻结、下落、地面接触共享实际主体及材料，不通过先删除水体再生成冰块实现。现阶段地面是地图高度零平面，完整地形及坡地承托继续按TODO推进，见[规则与边界](../事件驱动的运动变化.md)。
