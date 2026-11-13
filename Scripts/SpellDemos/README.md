# 法术实机演示工具

两个入口共用一套临时游戏内工具。读取运行中模组的实际清单，通过正式技能序列、主动能力与装备入口施放，按游戏世界时间录制。新增法术正常注册后，会自动进入清单，不需要再维护录像名单。

## 使用前

- 本机需要 Python 3.10 以上、ffmpeg，以及 WorldBox/NML 和已启用的 DebugToolbox。
- 客户端缺失时，在 `Mods/DebugToolbox` 执行 `dotnet build Client/DebugToolbox.Client.csproj -c Debug`。
- 演示会读取仓库中的独立测试世界，替换当前内存中的世界；请在测试游戏中运行。不会保存或覆盖玩家存档，也不会修改测试夹具。
- 默认连接已经启动的游戏。Linux 下可加 `--launch`，在游戏未运行时使用 `artifacts/spell_demo/profile` 独立配置启动。第一次启动仍需让 NML 正常加载 Cultiway 和 DebugToolbox。
- 一次运行一个入口；`Ctrl+C` 会请求结束录制、暂停游戏并恢复界面。运行时不必开着额外的调试脚本。

以下命令在项目根目录执行，两个入口也支持从其他工作目录调用。

凝固动量另用 `verify_solidification.script` 检查，正式学习、支付和留置用 `verify_solidification_runtime.script`，真正退出并重启后用 `verify_solidification_restart.script`。对应输入为 `Doc/技能系统全面重构/构造输入/流水凝固留置.json`。

材料与物态验证分别使用 `verify_matter_state.script` 和 `verify_matter_contacts.script`：前者检查真正发布资源的冻结、解冻、凝壳、染色及预测恢复；后者通过正式施放和真实接触追踪伤害系统，检查变化时序与交锋余力。运行前先等待 `demo_all.py --launch --list` 完整结束，再用 DebugToolbox 客户端执行，避免打断原版初次世界载入。保存测试仍使用 `verify_construction.script`，真正退出并重启游戏后执行 `verify_construction_restart.script`。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_matter_state.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_matter_contacts.script 60000
```

状态检查同时在 `artifacts/skill_rebuild/` 生成 `matter-water.json` 和 `matter-iron.json`，用于开发中逐视角录制。每次只运行一个游戏测试或演示；资源包构建必须先退出游戏。`result.json` 的 `materialFrames` 记录每张截图对应的材料、物态、主体表面和染色，`contactsLog` 记录伤害前后材料；它们不能替代实际画面检查。

任意曲线路径使用 `verify_flight_path.script` 检查实际位置、解析速度、切向朝向、循环、延迟、速度继承、反射及预测回退；`verify_flight_contacts.script` 检查高弧线越过中间人物后实际命中后方，以及带非零横向速度的曲线反射。曲线资料、旧轨迹逐项接入范围及两份可复现输入见[曲线说明](../../Doc/技能系统全面重构/可编辑曲线与轨迹接入.md)。仍使用同一 `--construction` 录制入口，不增加单招专用工具。

构造录像的每张 `materialFrames` 同时记录共同状态的地图位置 `position`，便于将路径、截图和实际接触对应。`absoluteHeight` 为真时，高度已包含共同地表；`groundHeight` 是同一快照中的局部地面高度，Worldfall 不再重复抬高。未接入共同地表的旧表现仍保留原来的相对高度。

## 遍历所有法术

```bash
python3 Scripts/SpellDemos/demo_all.py --output artifacts/spell_demo/latest
```

默认每段三世界秒，最多每世界秒截二十帧；分别录制原始效果、添加词条、改变轨迹。默认词条从当前兼容项中选取巨大、齐射、急速，最多两个；巨大使用 1.8 倍。替代轨迹优先展示曲线、螺旋、群落坠击或追踪等与原始轨迹不同的效果。不兼容的变化列入 `manifest.json` 的 `skipped`，不会通过绕过检查制造录像。

默认录像为普通观察视角的无声实机画面；`--view worldfall` 切换为已加载 Worldfall 的三维观察，并固定日光以便对比动作和材质；设置只写入演示使用的游戏配置。缺少该模组或镜头未就绪时会明确失败。一次正式施放可以包含技能原有的连发或被动追击。每招前还原地形、原版及模组状态，隔离背景单位；施肥、排水、净土等分别准备未成熟麦田、水域或火焰，保证展示实际作用条件。

`--lighting night` 可配合 `--view worldfall` 或 `firstperson` 固定夜景，默认 `day` 保持日光对照。结果与清单记录请求条件、实际外部昼夜模式及主光／环境光颜色；原版视角不接受此夜景选项。

`--view firstperson` 使用Worldfall真实附身镜头。身体动作可用 `--zoom 2.2` 检查握持近景，三维旁观会等待相机对准人物；普通观察近景仅覆盖本次开发测试相机。

当前清单包括 67 种基础法术、35 个金丹与元婴专属实体、26 项法宝主动、9 项骑士战技、两项天劫和一次近身掌击，共 140 项。来源专属实体包含内部触发部分，不等于 140 种修士可以学习的法术。法宝和战技使用专用入口，不允许通用词条和轨迹改造；被动法宝的全部触发场景、不同妖兽身体的器官出手点不属于这套单招遍历的验证范围。组合配方及三个组合专用技能已从当前实现移除，清单继续覆盖原有单招与来源能力。

```bash
# 只遍历 67 种基础法术的所有旧变体
python3 Scripts/SpellDemos/demo_all.py --scope base --variants all

# 只看部分法术，并增加“词条与轨迹同时修改”
python3 Scripts/SpellDemos/demo_all.py --filter 火球,飞剑,水刃 --cases base,modifiers,trajectory,combined

# 同一代码和资源版本、同一参数、同一目录续录，跳过已经生成的视频
python3 Scripts/SpellDemos/demo_all.py --output artifacts/spell_demo/latest --resume

# 查看当前游戏的法术、词条及其参数、轨迹名称和编号；不加载测试世界
python3 Scripts/SpellDemos/demo_all.py --list
```

普通遍历只选一种兼容的替代轨迹和少量词条，不穷举所有词条与轨迹的全部排列。指定入口可以继续检查具体搭配。

不加 `--resume` 时，重跑同一输出目录会替换本工具上次生成的演示，并清除已不在新清单里的片段；`--resume` 要求代码、资源、工具内容及参数相同。清单记录文件内容指纹和提交号，未提交修改也会使旧片段失效。游戏启动后再修改代码或资源，须重启游戏；连接、录制期间发生修改也会拒绝发布为同一版本验收。加载器自行写入的运行状态不影响指纹。

## 指定一个法术

中文名、完整编号、编号末段均可用。重名时请指定完整编号。

```bash
python3 Scripts/SpellDemos/demo_spell.py 火球
python3 Scripts/SpellDemos/demo_spell.py Cultiway.FlyingSword --variant 1

# 明确演示巨大词条与波形弹道；参数名称从 --list 查询
python3 Scripts/SpellDemos/demo_spell.py 火球 --cases combined \
  --modifier 'Huge:Value=1.8' --trajectory SineWave --duration 5

# 一招同时携带两个指定词条
python3 Scripts/SpellDemos/demo_spell.py 飞剑 --cases modifiers \
  --modifier 'Huge:Value=1.5' --modifier 'Volley:BurstBonus=3,DamageMultiplier=0.8'

# 持续领域延长观察；五相主动至少录 10.5 世界秒以经过五相
python3 Scripts/SpellDemos/demo_spell.py 五相轮转 --cases base --duration 12

# 法宝与近身动作也走同一个入口
python3 Scripts/SpellDemos/demo_spell.py 金钟护界 --cases base --duration 6
python3 Scripts/SpellDemos/demo_spell.py 原地掌击 --cases base
```

## 实际迎击、自主交战与三维

```bash
# 敌人正式释放石弹；读取真实护盾耐久，未发生吸收就判为失败
python3 Scripts/SpellDemos/demo_all.py --filter 灵盾,炎盾,雷盾,风盾,土墙,水幕,金钟护界 \
  --cases base --scene shield --duration 4

# 把筛选出的法术授予一名修士，由游戏 AI 决定出手时机
# 不逐帧调用规划或施法，不固定搭配；至少两种法术同次出手才通过
python3 Scripts/SpellDemos/demo_all.py --filter Fireball,TidalWave,SpiritVine,LightningStrike \
  --cases base --scene battle --realm 4 --power 18 --zoom 32 --duration 12

# 同一场景切换三维；必须安装并启用用户自己的 Worldfall
python3 Scripts/SpellDemos/demo_spell.py 沧浪 --cases base --view worldfall --realm 4 --zoom 32

# 精确边界检查，加载独立夹具后核对圆内/圆外、墙长轴和护盾边缘
# 先构建 DebugToolbox 客户端，并确保游戏已就绪
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_geometry.script 90000

# 不启动游戏即可检查文件版本规则
python3 -m unittest discover -s Scripts/SpellDemos -p 'test_*.py'
```

`--scene shield` 仅接受实际屏障、护盾或金钟护界。`--scene battle` 目前接受可学习基础法术，双方均获得筛选出的法术并解除冻结，由真实 AI 相互交战；至少一方同时释放两种不同法术、另一方实际出手才通过。可加 `--battle-artifact FlyingSwordAttack` 装备飞剑法宝，额外要求 AI 实际使用。它不覆盖被动法宝的全部触发条件。

法宝演示按能力选择钟、印、幡等代表器形；材料和能力参数来自同一份实际炼制设计，结果记录器形、材料与参数。为观察指定能力，演示仅保留这一项能力，不代表自然炼制一定只产出该能力。

全量遍历可加 `--speed 5` 使用游戏自身的加速档，成片依然按世界时间正常播放；这适合快速查漏。正式动作、迎击和自主交战验收保持 `--speed 1`，避免较大的模拟步长改变接触细节。

其他常用参数：`--fps 15` 降低截图量；`--power 12` 调整测试人物战力；`--realm 4` 使用顶阶境界，`--zoom 32` 拉远镜头；`--diagnostics` 记录可疑大粒子的材质来源；`--keep-frames` 保留逐帧图片。普通法术演示人物为金丹境，来源专属演示按需要建立化神成果；截图频率是上限，实际帧数取决于游戏更新频率。编码保持正常世界时间，不把截图造成的等待算作慢镜头。

## 重构期间的盘点和资源验证

```bash
# 只读取实际注册表，不加载测试世界，不出招
python3 Scripts/SpellDemos/demo_all.py --inventory --output artifacts/skill_rebuild/baseline

# 将同次运行登记、仓库中文及真实源码位置整理为任务 0.01 的完整表
python3 Scripts/SpellDemos/build_ability_inventory.py artifacts/skill_rebuild/baseline/inventory.json

# 逐项生成九类调用及保存入口，和一份可供后续迁移读取的 JSON
python3 Scripts/SpellDemos/build_ability_inventory.py artifacts/skill_rebuild/baseline/inventory.json --entrypoints

# 独立形体、材质和共同姿态的接入录像；没有战斗验收含义
python3 Scripts/SpellDemos/demo_all.py --resource-probe --view native --duration 6 --keep-frames
python3 Scripts/SpellDemos/demo_all.py --resource-probe --view worldfall --duration 6 --keep-frames
```

盘点导出 `inventory.json` 与 `能力迁移清单.md`，包括未列入140项演示的内部技能、被动法宝、器官及状态。第二版还逐项读取修炼形成原子及主动/被动效果、四类妖丹、39项元神命令、丹药效果原子和实际来源关系。法宝被动的事件回调也保留到具体方法。

`build_ability_inventory.py` 生成 [全部能力登记表](../../Doc/技能系统全面重构/全部能力登记表.md)，补充中文名称、取得方式、主动或被动、内部派生、实际文件及无独立资产的过程和设施来源。它检查编号唯一、明确引用无悬空及文件存在；支持 `--output` 指定其他文档位置，文内链接以原文档目录为基准。

加 `--entrypoints` 生成 [各能力调用与保存入口](../../Doc/技能系统全面重构/各能力调用与保存入口.md)及同名 JSON。每项应用完整九字段路径，再覆盖本项的被动/来源适用范围及特殊调用。具体回调定位到真实声明，记录资产默认资源、需求、实际登记的通道、轨迹、结构化效果、状态周期和显示类型；默认资源与需求不冒充某次施法的真实费用。法宝载具、宗门供奉及器灵的专用配置也读取实际回调。

此模式需要当前工具重新导出的 `inventory.json`，不会拿旧快照缺失的资料补成空白。源文件、定位文字、具体委托或分组变动时会报告失效位置；这是源码入口清点，不代替逐来源的实机和冷启动验证。已删除的旧掌击结算、仍在运行的旧路径、八种功能法术的转换范围和独立长期保存都分开记录。

任务0.04的[资料边界及编号合同](../../Doc/技能系统全面重构/资料边界与稳定编号.md)使用 `verify_definition_contract.script` 核对正式首版的15类61项数字、旧数字构造及现有资源编号。先等 `demo_all.py --launch --list` 结束，再运行：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_definition_contract.script 60000
```

`fixtures/construction-ids-v1.json` 是修改前冻结的兼容夹具，新增编号可以追加，旧值及含义不得重用。此检查读取并编译定义，核对默认和正式字段保存的数字，不施放、不修改或保存世界；结果写入 `artifacts/skill_rebuild/definition-contract/verification.json`，不代表全部行为、资源升级或画面验收。

登记条目之间有引用，不能把总数当成独立法术数量。人物载入后生成的丹药状态与当前世界创作出的构造会进入实际登记；本工具临时冻结状态排除在外。仅有基础登记的独立启动不能代表其他存档中动态结果的数量。逐项学习、支付、动作、作用、显示、人工/自主使用、保存及旧路径删除仍在任务0.03继续核对。

资源验证复用独立世界、隔离人物并清空背景。它检查资源和绘制提交，结果中明确区分实际作用与美术验收。两种视角均使用同一组部件矩阵；此项不接管现有法术，也不验证人物身体或兵器握持。

新构造可以直接从完整资料加载，不需要先写一项旧式技能资产：

```bash
# 用独立测试世界检查构造、学习及保存，并输出 created-ability.json
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction.script 60000

python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/created-ability.json --view native --duration 5
python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/created-ability.json --view worldfall --duration 5

# 退出并重启独立游戏后，单独检查真正的冷启动恢复
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction_restart.script 60000
```

这些命令依次执行，不与录像或其他载档任务同时运行。构造录像要求正式施放受理、按资料支付、主体出现及目标实际受伤；完整输入纳入续录比较。冷启动检查对比完整构造、已学关系与剩余冷却；同进程不能冒充重启。

身体动作和纯数值并行检查继续使用同一套工具：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_body_motion.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/benchmark_body_math.script 60000

# 三份资料使用相同的编译、动作、接触及绘制实现，没有各自的整招执行类
python3 Scripts/SpellDemos/demo_all.py --construction Content/Skills/Body/palm-strike.json --zoom 2.2 --view native
python3 Scripts/SpellDemos/demo_all.py --construction Content/Skills/Body/sweeping-palm.json --zoom 2.2 --view worldfall
python3 Scripts/SpellDemos/demo_all.py --construction Content/Skills/Body/sword-thrust.json --view firstperson
```

身体检查会载入独立夹具，检查实际加速、暂停恢复、换器及失去行动能力的中断、支付和切世界释放。并行检查仅对相同数值姿态比较主线程、现有工作池和实体查询，不代表整场战斗帧率。构造录像按资料指定的实际资源支付，支持无消耗、真元、斗气、法力和体力；不将骑士默认变为修士。

连接、实际接触结果和数值扫掠：

```bash
# 正式受理、支付、手部连接、离手后收势及跨形态去重；输出projected-palm.json
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_bindings.script 60000

# 两个实际敌人，检查停止、消散及穿过的先后作用；不靠单位编号挑第一个
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_responses.script 60000

# 固定种子的160组端点运动及半径变化，与4096时点距离参考比较
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_sweep_math.script 60000

python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/projected-palm.json --view native --zoom 16 --duration 3
python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/projected-palm.json --view worldfall --zoom 16 --duration 3
python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/projected-palm.json --view firstperson --duration 3
```

身体构造中带有明确飞行操作时，演示将目标移到八格外，避免只播放近身碰撞就声称验证了飞掌。接触记录包含作用面位置、方向和步内首次接触时间。高速接触检查先让新生人物进入原版空间分区，再开始0.1世界秒的攻击，不把尚未建立的测试场地当作漏判。原版人物分区尚未就绪时的完整筛选改进保留在后续工作包。

先运行连接检查，再运行构造保存检查时，保存检查还会收集生成的飞掌资料，验证根连接、离手交接、共用接触组及命中后驻留。冷启动比较会按正式默认值补全可选字段，避免把新增字段的默认值误报为旧构造被随机重建。

## 输出

每次运行只写入自己的输出目录：

| 文件 | 用途 |
|---|---|
| `index.html` | 可按名称、编号或轨迹搜索的逐招视频页 |
| `演示目录.md` | 中文逐项链接与分类合集 |
| `*-合集.mp4` | 基础法术、金丹元婴、法宝主动、骑士战技等分类录像 |
| `全部法术-总览.mp4` | 按清单顺序连续观看本次所有演示 |
| `全部演示.zip` | 视频、预览图、清单及结果 |
| `catalog.json` | 当前游戏登记的法术、兼容项、进程启动时间与实际加载的程序集标识 |
| `manifest.json` | 本次代码与资源指纹、完整参数、用例和跳过原因 |
| `status.json` | 当前准备或录制进度，错误时保留具体原因 |
| `各段目录/result.json` | 实际出手与命中、范围结算、屏障接触、真实空间数据、资源和生命变化、程序集标识和错误 |
| `各段目录/demo.mp4` | 标注法术、变体、词条与轨迹的单段录像 |

普通单招的 `passed` 表示施法入口受理并实际推进、录制期间没有游戏错误；迎击额外要求真实吸收，自主交战额外要求游戏 AI 同次释放不同法术。它不代表美术验收，也不保证每种状态型能力都会造成伤害。金丹与元婴内部实体按原始容器展示；法宝和骑士按真实能力入口展示。护盾、格挡等还需要实际来袭才能验证防御作用，不能把单招录像当作全部玩法验证。

资源提供测试用充足数值，运行中可能被游戏钳回自然上限；报告中的前后余额用于观察，不能直接当作单次支付金额。

临时工具只编译进当前 DebugToolbox 会话，不放进 `Source/`。结束后恢复界面并暂停；退出游戏后工具消失。测试状态、人物、法宝、地形改动都只存在于未保存的独立测试世界。

## 旧脚本清理

旧的 `CombatVisualTests`、`GrandCombatTests`、`MaterialCombatTests`、`RecipeCombatTests`、`SpellVolleyTests` 由本工具替代。清理时将旧脚本归档到 `artifacts/spell_demo/旧探索脚本备份.zip`；后续开发只维护本目录。正式存档测试夹具与 `WorldSaveTests`、模型和资源生成工具继续保留。

## 共同时间与真实相撞检查

以下两项依次运行，不与录像或其他载档任务并行：

```bash
# 纯数值：两个运动胶囊、变径，以及实际空间筛选与直接范围对照
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_pair_sweep.script 60000

# 独立世界：新生人物、双方高速相撞、强弱抵消、屏障余量、同时攻盾、边缘及移动目标、同刻死亡、五倍速
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_clashes.script 60000
```

结果分别写入 `artifacts/skill_rebuild/pair-sweep-checks.json` 和 `clash-checks.json`。交锋检查经过正式能力入口，故意保留普通攻击冷却以验证独立法术可以并存；其竖直屏障使用现有刃形作为几何夹具，只验证实际部位、容量和余力，不代表护盾最终模型或破裂动画。世界在检查结束时暂停，不自动保存。

反射检查单独运行：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_reflection.script 60000
```

结果写入 `artifacts/skill_rebuild/reflection-checks.json`，覆盖静止及移动表面、斜擦、容量不足、次数耗尽、追踪转手、有限往返、预测恢复、分支归属及原来源死亡。各招通过正式能力入口，核对实际伤害及原版受击归属。反射保留原施放编号，不创建另一招或再次支付；屏障支付来袭部位的实际剩余量。仍使用几何夹具，不能作为最终护盾美术或受力动画交付。

接触频率与次数检查：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_policy.script 60000
```

结果写入 `artifacts/skill_rebuild/contact-policy-checks.json`，覆盖每人一次、世界时间间隔、五倍速、每人上限、贯穿总预算、重叠部位共享、离开重入、暂停及真实身体预算。分离部位的用例通过正式施放后设置一段共同时间积压，调用原有共同推进入口，精确复现一步内穿过两个作用面；正常速度与加速用例仍由真实世界时钟推进。接触记录包含每人及全组累计次数。

接触回调中新施放的时间与位置检查：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_time.script 60000
```

结果写入 `artifacts/skill_rebuild/contact-time-checks.json`。通过正式入口受理后设置0.05世界秒积压及实际人物前后位置，确认接触回调启动的新能力从共同接触时点和位置开始，参加余下推进并产生正常伤害；回调结束后普通入口恢复当前世界时钟。积压设置只用于精确复现，不改速度、伤害或接触结果，不作为自主交锋录像验收。

接触结果改变人物与连接后的检查：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_changes.script 60000
```

结果写入 `artifacts/skill_rebuild/contact-change-checks.json`。正式施放后通过共同积压推进，验证回调内人物移出、移入及瞬移跨过路径、只更新体质时保留正常移动、原版新生和变大后立即参与作用，以及零时点固定与随动的来源／目标连接。瞬移中间空间不造成伤害，实际改变的旧形体路径失效。新增玩法在主线程用`ConstructedAbilityService.NotifyActorChanged`通知已经确认的瞬时变化；普通移动沿用正常世界采集。

实际构造的顺序及并行对照使用同一批量推进入口：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/benchmark_construction_math.script 60000
```

它比较身体、形体、混合、连续骨架及厚曲面构造在16、128、1024、4096次独立施放下的姿态、全部蒙皮矩阵、旋转、生长、脱离、追踪、接触形状、余量和进度，输出 `construction-math-benchmark.json`。计时不包含输入采集、前态复制、候选筛选、实际确认和绘制，不能当作整场战斗性能。`verify_clashes.script` 另有超过计算门槛的实际相撞及同时攻盾，并通过临时追踪确认现有工作线程参与；追踪在用例结束时解除，不用于性能计时。

完整显示交接可单独检查：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_presentation.script 60000
```

此项对正式受理的施放逐步推进积压世界时间，检查完整轮次前后身体、部位、持物及形体一起切换，以及多次读取、换器和接触回调清理。结果写入 `presentation-checks.json`。玩法中的器物控制检查使用 `HasWeaponControl`；绘制中的持物隐藏读取 `IsWeaponBorrowed`，受理尚未发布时仍显示上一完整轮。

连续骨架形体从制作资料到真正发布网格的检查：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_form_rig.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/swimming-whale.json --view native --duration 3 --zoom 14 --keep-frames --output artifacts/skill_rebuild/rig-whale-current/native
```

先通过 `Scripts/ShaderBuild/build_forms.sh` 生成对应制作资料和资源包，再运行检查。结果写入 `form-rig-checks.json`，核对位置、法向、表面坐标、三角形、材质分区、权重、控制及动作导入，遍历多帧全部顶点核对动态边界，并检查共同挂点、作用形状、发布冻结、预测回退及接触停止。脚本同时生成水鲸的独立构造资料；演示命令的 `--view` 可分别换为 `worldfall` 和 `firstperson`。它们是连续形体与正式命中的开发验证，最终城市交锋及水体质感仍需另行完成。


厚曲面与制作阶段可单独验证：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_wave_form.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction artifacts/skill_rebuild/breaking-wave.json --view worldfall --duration 3.1 --zoom 14 --keep-frames --output artifacts/skill_rebuild/wave-current/worldfall
```

检查实际发布的局部厚度、泡沫覆盖、混合挂点和作用端点、动态边界、刚性附着物尺寸，以及动作调速后的阶段作用窗口；结果写入`wave-form-checks.json`。脚本生成独立浪体构造。新构造录像在清场碎片结束后才施放，Worldfall使用自身倾斜观察；第一人称仍为真实附身视角。它们是开发验证，地面承接、最终水体美术及城市战斗须继续验收。


构造改造验证使用三个脚本，依次运行：

```bash
# 正式编辑、其他持有者、正在运行的旧施放、冷却和实际命中；生成 editor-save
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction_editing.script 60000
# 真正退出并重启游戏，再验证完整世界资料和人物掌握关系
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction_editing_restart.script 60000
# 真实窗口按钮、菜单、撤销重做、关闭、人物页和中英文布局
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction_editor_ui.script 60000
```

界面验证加载独立测试世界，临时切换语言和窗口尺寸，正常结束时恢复中文及原尺寸。截图与检查结果位于 `artifacts/skill_rebuild/editor-ui/`。临时定身状态在生成正式测试存档前解除，不能依赖测试进程临时注册的资产通过冷启动。


身体改造检查使用共用命令、真实界面和正式保存：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_body_editor_ui.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_body_editing.script 60000
# 真正退出并重启游戏后执行
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_body_editing_restart.script 60000
```

前者操作身体关键点、躯干、朝向、显化、材料、投送及支付，输出实际编辑结果；后者检查身体交接、实际远距及近身接触、费用、修士创法与自主出手并生成正式保存。输出位于 `artifacts/skill_rebuild/body-editing/`，新构造输入和录制命令见 `Doc/技能系统全面重构/构造输入/README.md`。自主检查先给原版AI接近和出手的时间，再继续推进一段完整投送寿命，不能在刚受理时就判定命中失败。

### 构造的实际行走与相对目标位置

`--construction` 可加 `--caster-walk 4`，让施法者通过原版附体移动沿地图 X 轴行走；`--target-offset 0.5 0` 将主目标放到施法者的指定相对位置。用于检查随动领域或环绕形体，仍必须正式扣费、命中、扣血，指定行走还必须确实移动。`result.json` 记录 `walked`，逐帧 `materialFrames` 同时记录来源和形体位置。

验证单个目标或两种构造的交互时，可加 `--isolate-target`，将其他测试人物移到远处，避免它们提前拦住投送路径。该选项只用于构造演示，仍保留主目标、正式支付、实际运动和真实结算。水火交互的结果包含 `quenchingLog`、`quenchedVolume`、`quenchedWaterSources` 和 `extinguished`；完全灭火时要求没有人物命中，不能用一团蒸汽冒充伤害成功。

木体燃烧可使用 `构造输入/点木流火.json` 与 `构造输入/可燃分枝.json`。`burningLog` 记录实际接收者、传热施放或世界来源、转移量、剩余热量、实际体积和累计烧损；`heatedWoodSources`、`heatedWorld` 与 `burnedVolume` 用于核对实际点火和后续损耗。火体被木体耗尽后，没有人物命中也必须有同次正式施放产生的真实加热结果，不能按火焰模型个数宣称点燃。

燃烧的数值验证为 `verify_burning_math.script`，正式施放为 `verify_burning.script`，活动与世界材料互相传热及灭火为 `verify_burning_world.script`。正式保存进行中的燃烧后，必须重启游戏再运行 `verify_burning_restart.script`；它检查继续剩余过程、最终烧尽，以及正式第二版材料升级后仍保持冷态。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/离体炎流.json --opponent-construction Doc/技能系统全面重构/构造输入/蓄水御炎.json --isolate-target --target-offset 20 0 --view worldfall --keep-frames
```

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/随动木域.json --caster-walk 4 --target-offset 2.5 0 --zoom 10 --keep-frames
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/环绕浪体.json --target-offset 0 3 --zoom 18 --keep-frames
```

验证改造、实际接触与正式保存用 `verify_placement_editing.script`，界面用 `verify_placement_editor_ui.script`；真正退出并重启后运行 `verify_placement_editing_restart.script` 核对完整恢复。仅用于独立测试游戏。

### 数量、尺寸、速度与历时改造

使用 `verify_magnitude_editing.script` 检查共用改造、每份独立显现和收尾、真实总支付、逐份命中及保存；`verify_magnitude_editor_ui.script` 检查游戏内表单，真正重启后用 `verify_magnitude_editing_restart.script` 核对完整恢复。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/弧线掠袭.json --target-offset 10 -4 --zoom 18 --keep-frames
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/数值改造弧线剑.json --target-offset 10 -4 --zoom 18 --keep-frames
```

第二条在同一弧线剑上应用数量、尺寸、速度与历时修改，仍是单个法术。增加 `--view worldfall` 或 `--view firstperson` 使用相同输入切换实际视角。

### 独立维持阶段

`verify_sustain.script` 验证保持原速度、实际迟到接触、接触去重、快照回退与保存；`verify_sustain_editor_ui.script` 检查编辑，真正重启后运行 `verify_sustain_restart.script` 核对恢复。原动作与维持动作分别录制即可对照。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/独立维持厚浪.json --target-offset 16 0 --zoom 20 --keep-frames
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/独立维持掌击.json --zoom 5 --keep-frames
```

维持秒数与完整历时倍率是不同参数。增加 `--view worldfall` 或 `--view firstperson` 使用相同资料核对三维表现。

## 构造的目标筛选与接触推拉

构造头部的可选 `Impact` 决定默认人物关系及接触作用，部位可以覆盖；普通表单、身体和生成形体使用同一资料。演示自动按照敌方、友方或自身关系选取目标。存在推拉时，目标仅冻结自主行为，允许原版物理位移；背景仍隔离。纯推拉录像必须有真实接触、实际位移且不扣血，不再用伤害来代替验证。

`result.json` 新增 `targetMoved`、`targetStart`、`targetEnd`，接触记录新增 `damageAttempted` 与经过现有受力规则后的 `velocityChange`。速度变化不等于已经移动，二者分别记录。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_effects.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_effects_editor_ui.script 60000
# 真正退出并重启游戏后核对上一步保留的独立世界，不能以同进程重载代替。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_contact_effects_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触推开厚浪.json --view native --duration 4 --target-offset 16 0 --zoom 20 --keep-frames --output artifacts/skill_rebuild/contact-push-current/native
```

将输入换为“接触牵引厚浪”、视角换为 `worldfall` 或 `firstperson`，输出目录对应更换。现阶段推拉沿用原版带轻微离地的受力过程；持续贴地滑移、抓持和摔投仍属于后续任务。

## 真实事件开始的部位过程

构造部位的可选 `Activation` 可以等待人物接触、实际交锋、部位结束或真实材料变化；身体提供真实接触和自然收势。等待期间没有形体或作用区，确认后由分支自己的零时点执行已有生长、动作和轨迹。整次寿命仍为上限，内部事件不再扣一次费用。

来源使用父体、身体或明确部位，游戏内菜单显示部位序号和形体名。继续附着与事件处独立生成有不同的结束规则；数量及环绕包装会重绑引用，完整历时会伸缩事件延迟，放大会覆盖独立事件主体。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_activation.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_activation_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_activation_editor_ui.script 60000
# 真正退出、重启游戏后检查保留的正式世界。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_activation_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触后生长冰枝.json --view native --duration 3.4 --target-offset 8 0 --zoom 8 --keep-frames --output artifacts/skill_rebuild/activation-seed-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/掌击接触后凝枝.json --view native --duration 2.2 --zoom 6 --keep-frames --output artifacts/skill_rebuild/activation-palm-current/native
```

将视角和输出目录对应更换为 `worldfall` 或 `firstperson`。接触记录区分同次施放中的实际身体和各生成部位，`materialFrames`增加共用形体编号`form`，可将前段、等待和后段与真实接触时间对照。两份输入都是同一构造内的过程，不是多招配方。


## 同一主体随事件改变运动

`When` 让已有部位在真实接触、交锋、材料变化或其他部位结束后停止、脱离、改变飞行或旋转。条件飞行可选加速度、地图方向和实际形状着地停止。初始飞行和事件后飞行可以分别编辑，不复制主体，不重置接触次数，不另扣费用。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_motion_events.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_motion_events_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_motion_events_editor_ui.script 60000
# 真正退出并重启后再核对正式保存。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_motion_events_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触停止后继续飞行.json --view native --duration 4.2 --target-offset 8 0 --zoom 8 --keep-frames --output artifacts/skill_rebuild/motion-resume-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/冻结后真实坠落.json --view native --duration 4.6 --target-offset 6 0 --zoom 20 --keep-frames --output artifacts/skill_rebuild/motion-fall-current/native
```

对应更换视角与输出目录可检查 Worldfall 及附身。着地目前以地图高度零平面为准，作用形状控制停止，尚不代表坡地、水面或完整地形碰撞。完整规则见 `Doc/技能系统全面重构/事件驱动的运动变化.md`。


## 条件成形、动作过渡与材料变化

`Grow` 条件操作从当前尺寸向 `TargetScale` 变化；`Animate` 用 `BlendSeconds` 从当前局部骨架过渡到资源提供的另一个动作。重新生长、重新播放和飞行分别控制尺寸、内部姿态及整体运动，保持同一主体和原命中记录。条件 `ConvertMatter` 可继续驱动实际材料变化；事件使用发生当时的材料快照按序通知。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_shape_events.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_shape_events_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_shape_events_editor_ui.script 60000
# 真正退出并重启后再核对正式保存。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_shape_events_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触后持续长成.json --view native --duration 4.8 --target-offset 8 0 --zoom 10 --keep-frames --output artifacts/skill_rebuild/shape-growth-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/接触后回卷复形.json --view native --duration 4.8 --target-offset 6 0 --zoom 20 --keep-frames --output artifacts/skill_rebuild/shape-motion-current/native
```

更换视角及输出目录为 `worldfall` 或 `firstperson`。重新制作模型使用原有 `build_forms.sh`；回卷仅新增动作资料，沿用已有网格及资源包。完整语义见 `Doc/技能系统全面重构/事件驱动的成形与动作.md`。


## 损耗及尺寸条件与真实迎击

条件新增 `ForceBelow`、`SizeAbove`、`SizeBelow` 和明确 `Threshold`，分别使用初始作用量比例、局部长大尺寸、局部收缩尺寸。普通事件不携带阈值。开始新部位和改变已有过程共用相同事实；资料改变、并行和正式保存仍走原入口。

指定构造可增加 `--opponent-construction`：让敌方目标同时通过正式入口施放另一份独立构造，支持同一资源规则和实际支付。两份构造必须有不同编号。录像新增 `opponentUsed`、`opponentPaid`、`actualClashes` 和 `clashesLog`；指定对手时必须有正式施放、正确支付及真实交锋才通过。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_thresholds.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_thresholds_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_thresholds_editor_ui.script 60000
# 真正退出并重启后核对正式世界及真实编辑结果。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_thresholds_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/长成后继续飞行.json --view native --duration 4.8 --target-offset 8 0 --zoom 10 --keep-frames --output artifacts/skill_rebuild/threshold-size-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/损耗后独立长枝.json --opponent-construction Doc/技能系统全面重构/构造输入/损耗验证迎击.json --view native --duration 4.4 --target-offset 8 0 --zoom 10 --keep-frames --output artifacts/skill_rebuild/threshold-force-current/native
```

对应更换视角与输出目录为 `worldfall` 或 `firstperson`。主构造的后段仍由真实损耗条件产生，对手只是独立测试输入；原始模型、原费用和正式交锋均保留，不以显示重叠冒充碰撞。完整规则见 `Doc/技能系统全面重构/损耗与成长条件.md`。


`verify_growth_damage.script`独立验证“生长中真实受损”：木壁尚未长成时承受来袭剑的实际交锋，剩余力量非零，尺寸停止并同体凝壳，超过原成形终点也不继续长大。它只写独立结果，不覆写上面的冷启动存档。

资源重载与共享计划验证：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_resource_versions.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_resource_lifetime.script 60000
# 真正退出并重启独立测试游戏，等启动器完整结束后运行：
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_resource_versions_restart.script 60000
```

第一份脚本加载仓库夹具并建立已学构造，临时修改厚浪动作和鲸体资源路径，验证新旧施放、真实身体绑定、材质、费用、作用形状、失败回退及编辑预览。临时文件在`finally`中恢复；若游戏进程被强制终止，应先核对两份资源文件没有遗留修改。第二份脚本必须等第一份返回后再执行，检查切世界、旧组回收、完整停用及重新加载。强制托管回收只用于这份检查，正常游戏不强制回收。结果与独立存档位于`artifacts/skill_rebuild/resource-versions/`；冷启动检查不能重跑保存脚本覆盖先前依据。

部件聚合使用同一指定构造入口，资料为[散刃汇轮](../../Doc/技能系统全面重构/构造输入/散刃汇轮.json)及[散枝合木](../../Doc/技能系统全面重构/构造输入/散枝合木.json)：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_gather.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_gather_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_gather_editor_ui.script 60000
# 真正退出并重启独立测试游戏，等启动器完整结束后运行：
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_gather_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/散刃汇轮.json --view native --duration 5.2 --target-offset 16 0 --zoom 22 --keep-frames --output artifacts/skill_rebuild/gather-edges-current/native
```

另一个输入替换构造路径并使用`--duration 5.8`即可，`--view`同样支持`worldfall`及`firstperson`。行为检查读取真实网格，核对当前布局、作用端点、完整尺寸、父体运动、事件覆盖、材料及余力、脱落继承和回退。正式工作池对照128及512次施放，每次33个部件，分别检查定时、事件聚合及脱落；它不代表整场帧率。界面脚本通过真实表单保存初始及条件聚合，结果与冷启动依据位于`artifacts/skill_rebuild/gather/`，使用冷启动检查前不要重跑保存脚本。

## 按真实部件碎解

`Fracture`在主体的真实接触、交锋、材料或损耗等条件后执行，也能设定明确时点。分件预先计入数量上限，实际碎解保留原网格和当时的骨架姿态；指定继续作用的部分只分配本次实际余力，其余均为装饰。它不随机切出新断面，也不将整把剑重复当成多个碎片。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_fracture.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_fracture_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_fracture_editor_ui.script 60000
# 真正退出并重启独立测试游戏，等启动器完整结束后运行：
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_fracture_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/破锋余刃.json --opponent-construction Doc/技能系统全面重构/构造输入/碎锋验证护锋.json --view native --duration 4.8 --target-offset 16 0 --zoom 14 --keep-frames --output artifacts/skill_rebuild/fracture-sword-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/断枝散落.json --opponent-construction Doc/技能系统全面重构/构造输入/断枝验证护锋.json --view native --duration 5.4 --target-offset 16 0 --zoom 16 --keep-frames --output artifacts/skill_rebuild/fracture-branch-current/native
```

将视角和输出目录改为`worldfall`、`firstperson`可核对另外两个视角。指定对手的检查必须包含真正交锋及目标受击；还需在`materialFrames`中确认出现真实分件编号，而非只看整体重叠。六组顺序及工作池对照包含两次回退，不能代替整场帧率测试。结果与独立冷启动依据位于`artifacts/skill_rebuild/fracture/`，冷启动前不要重跑保存脚本。装饰分件着地采用冻结姿态的近似包围范围，仍限于地图零高度平面。规则见`Doc/技能系统全面重构/按部件碎解与余力继承.md`。

## 结束后的余量交接

部位开始条件的`InheritRemainder`用于`End`事件及事件处独立开始。后段及其普通后代等待时没有初始攻击力量；来源结束时按分支、真实作用部位的`Strength`权重分配本次余量，并继承当时材料、速度、反射及控制身份。同组接触预算保持，零余力仅留下无害形体。它是流散、残留及回收的共同交接入口，尚不代表完整水量、地形流动或资源返还。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_remainder.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_remainder_parallel.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_remainder_reflection.script 60000
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_remainder_editor_ui.script 60000
# 真正退出重启，等启动器完整结束后运行；不要覆写退出前的保存依据。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_remainder_restart.script 60000
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/鲸散后流.json --opponent-construction Doc/技能系统全面重构/构造输入/余流验证迎击.json --view native --duration 5.2 --target-offset 16 0 --zoom 16 --keep-frames --output artifacts/skill_rebuild/remainder-water-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/枯枝余势.json --opponent-construction Doc/技能系统全面重构/构造输入/余流验证迎击.json --view native --duration 5.2 --target-offset 16 0 --zoom 16 --keep-frames --output artifacts/skill_rebuild/remainder-wood-current/native
```

视角可替换为`worldfall`或`firstperson`。检查结果、正式世界及真实表单修改位于`artifacts/skill_rebuild/remainder/`。两类后段仍走同一指定构造入口及正式施放，不新增专用播放路径。规则及边界见`Doc/技能系统全面重构/结束后的余量交接.md`。

## 地表承托与贴地水层

`verify_ground.script` 检查共同数值地表、真正发布的水层、挠曲边界、冻结解冻、落地和创作，并保存独立测试世界。`verify_ground_parallel.script` 比较128及512次水层、刚性枝体及余量交接的顺序、工作池和回退结果。`verify_ground_contacts.script` 核对实际Worldfall入口、地形变更、脚底和头部命中，以及高于整个身体时的挥空。原版人物作用形状由胸口球体改为覆盖脚底至头部的胶囊，筛选范围与重新进入规则也读取相同形状。

`verify_ground_editor_ui.script` 经普通路径表单保存沿地响应，截图覆盖中文、英文及1024分辨率。真正退出游戏再重启后，执行 `verify_ground_restart.script`，核对正式世界和表单世界的构造、学习及当前地形重新采集。结果和冷启动依据位于 `artifacts/skill_rebuild/ground/`；使用冷启动检查前不要重跑保存脚本。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/鲸散贴地后流.json --target-offset 16 0 --terrain-slope 2 --view native --duration 5.2 --zoom 16 --keep-frames --output artifacts/skill_rebuild/ground-flow-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/沿地枝体.json --target-offset 16 0 --terrain-slope 2 --view native --duration 4.4 --zoom 16 --keep-frames --output artifacts/skill_rebuild/ground-rigid-current/native
```

`--terrain-slope` 只改变独立测试世界当前内存中的地图高度，每格X方向的原版高度增量范围为 -4 至 4；不会保存或修改原夹具。改用 `worldfall`、`firstperson` 可核对另外两种视角。该原版高度增量并不等于Worldfall中的实际升高，实际高度由当前地图和外部采样共同决定。

水层使用同一骨架控制模型、挂点和作用区，刚性资源只按底部承托。数值地表及显示高度纹理不保存入世界资料。当前采样有近似误差，流向、水量、岸线、墙面桥面洞穴、永久结果和回收仍需继续实现；录像不代表这些项目完成。

## 守恒流散与动态曲面

`verify_flow.script` 检查体积、静水平衡、地形分流、外流和蒸散去向、冻结解冻、回退及动态网格。`verify_flow_geometry.script` 检查薄层的高速扫掠、擦边、脚底接触及越顶；`verify_flow_contacts.script` 经正式入口核对地形和水层命中，薄层使用水深箱体，水平宽度不会变成空中作用高度。`verify_flow_parallel.script` 比较128及512次平地、坡面及余量交接，使用每边16格的工作区，核对全部水深、体积去向、真实薄层尺寸及两次回退；它不等于整场帧率验收。

`verify_flow_editor_ui.script` 经属性页增加、移除、修改流散和体积，保存独立世界。真正退出再启动游戏后，执行 `verify_flow_restart.script`。结果和两个保存依据在 `artifacts/skill_rebuild/flow/`，冷启动前不要重跑保存脚本。每帧资料新增实际`volume`、`escaped`、`evaporated`与`flowCells`，对应已发布水层。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/鲸散守恒后流.json --target-offset 16 0 --terrain-slope 2 --view native --duration 5.2 --zoom 16 --keep-frames --output artifacts/skill_rebuild/flow-whale-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/缓坡守恒流.json --target-offset 10 0 --terrain-slope 2 --view native --duration 5.2 --zoom 14 --keep-frames --output artifacts/skill_rebuild/flow-current-current/native
```

将视角和输出目录换为`worldfall`、`firstperson`可对照三个视角。体积单位是未缩放地图格的三次方，按实际尺寸换算；交接后不重新按接收体尺寸补满。计算范围边长是最低值，高压或高速输入会扩大格宽，实际格宽以发布资料为准。当前计算是同次施放内的有限浅层范围，格边界、采样和干湿曲面有精度限制。完整三维液体、永久水文、城市及世界连续区域、资源返还和最终美术继续待办；不得将外流记账作为范围外仍在运行的真实水体。

## 生成主体收束与原资源回能

`verify_recovery.script` 核对支付份额、真实接触与交锋消耗、水量流失、收束体积、反射、分支和分件守恒、预测回退、时间改造及创作，并保存独立世界。`verify_recovery_resources.script` 经正式支付与能力入口检查真元、斗气、法力、体力的实际入账，以及容量不足、超容量法力和同编号目录变更；它不会用攻击力量兑换资源。`verify_recovery_boundaries.script` 检查真实反射后的原支付者、实际死亡、收束前最后路径以及提交事件中清理旧执行。`verify_recovery_parallel.script` 比较128及512次刚性、水层、交接收束的完整状态、预算和回退，水层使用每边16格，不代表整场帧率验收。

`verify_recovery_editor_ui.script` 经普通表单增删、编辑收束开始、历时及比例，核对撤销、正式保存、中英文和小分辨率。真正退出再重启后运行 `verify_recovery_restart.script`；依据位于 `artifacts/skill_rebuild/recovery/`，不要在冷启动检查前覆盖原保存依据。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/悬锋收束回能.json --view native --duration 4 --zoom 14 --keep-frames --output artifacts/skill_rebuild/recovery-sword-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/水层收束回能.json --terrain-slope 2 --view native --duration 4 --zoom 14 --keep-frames --output artifacts/skill_rebuild/recovery-water-current/native
```

将视角及输出目录改为 `worldfall`、`firstperson` 可对照三个视角。纯回收输入必须实际向原支付者入账且不超过本次支付，不能用存在模型代替功能验收；这类测试人物在开始前留出真实资源容量。录像记录 `recoveryLog` 中每次请求、入账、原支付者及转化体积，水层逐帧另记 `recoveredVolume`。回能是转化本次生成主体，不代表永久世界材料、库存或跨施放残留已经完成。规则见 `Doc/技能系统全面重构/生成主体收束与原资源回能.md`。

## 独立世界材料与跨施放回收

`verify_world_materials_math.script` 检查真实转交、预测回退、部分水格的水量和支付份额，以及独立数值维护的串行/工作池一致性。`verify_world_materials.script` 经正式能力入口留置器物和水层，再由另一次法术选择并回收；检查原支付者、来源死亡、额度不足和实际入账，并保存回收中途的独立测试世界。

`verify_world_materials_editor_ui.script` 经普通参数页设置交出时点、独立时长和世界材料回收选项，覆盖撤销、保存重开、中文、英文和1024分辨率。真正退出游戏再启动后执行 `verify_world_materials_restart.script`：恢复稳定材料、水深动量、原归属和已确认累计，只继续尚未完成的份额；另检查单条损坏的局部丢弃，以及真实表单保存。冷启动依据和报告在 `artifacts/skill_rebuild/world-materials/`，冷启动检查前不要重跑保存脚本。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/器物独立留置.json --view native --duration 4.2 --zoom 12 --keep-frames --output artifacts/skill_rebuild/world-material-sword-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/水层独立留置.json --view native --duration 4.2 --zoom 14 --keep-frames --output artifacts/skill_rebuild/world-material-water-current/native
```

更换`--view worldfall`或`--view firstperson`并更换输出目录验证另两个视角。留置短片以同次正式施放的材料接受记录、支付和原施放结束后仍存续作为功能依据，不要求无攻击材料对目标造成伤害。跨施放选择和实际回能由独立实机脚本验证，录像没有另配固定连招或整招专用执行器。

## 连续世界水层与来源输运

`verify_world_water_math.script` 检查跨区静水、通量、逐格支付份额、两来源交汇、容量回压和128/512区的工作池对照；同时检查三种表面分区不会在共同水面留孔或重复覆盖。`verify_world_water.script` 让两位真实修士通过正式能力入口留水，验证水离开原计算范围、实际交汇及只回收本人的水，并保存共同水流与跨区回收中途的世界。

真正退出再重启后运行`verify_world_water_restart.script`，恢复共同格网和逐格来源，检查八点原法力跨多个内部区域仍合计返还六点；另读取上一批保留的真实版本1世界验证连续升级。保存依据位于`artifacts/skill_rebuild/world-water/`，冷启动前不要重新运行保存脚本；旧格式依据为`artifacts/skill_rebuild/world-materials/save`。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/跨区留水.json --view native --duration 4.8 --zoom 24 --keep-frames --output artifacts/skill_rebuild/world-water-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/跨区留水.json --opponent-construction Doc/技能系统全面重构/构造输入/迎流留水.json --target-offset 16 0 --view native --duration 4.8 --zoom 26 --keep-frames --output artifacts/skill_rebuild/world-water-mixed-current/native
```

更换视角与输出目录验证Worldfall及附身。双向水流是两位修士独立施放；录像检查双方正式支付、正式转交和真实交汇格，不要求无攻击的世界材料制造伤害或法术交锋。来源数与交汇格来自已经发布的实际共同水面。

### 世界材料的选择、冻结与解冻

`verify_world_matter_math.script`验证一次请求、预测回退、局部体积和支付份额、刚性金属熔化、容量入口及128/512份串并行完整状态。`verify_world_matter.script`通过正式入口完成双来源留水、部分冻结、他人解冻、归属筛选、容量耗尽保留、冰层对流水的真实承托和修士自主创作，并将当前冰、流水及熔融金属正式保存到`artifacts/skill_rebuild/world-matter/save`。真正退出游戏、换进程后运行`verify_world_matter_restart.script`；不能把同进程重读当作冷启动。表单检查使用`verify_world_matter_editor_ui.script`，保存重开并保留中英文、小分辨率截图。旧版兼容仍使用此前保留的正式v1/v2存档及`verify_world_water_restart.script`，不要先重写这些旧夹具。

普通材料转换可选`WorldMatter`：按部位真实空间形状选择已生成世界材料，`FromMatter`/`FromState`筛选来源，`OwnedOnly`限制原支付归属，`MaximumVolume`限制本次实际转换体积。冻结和解冻不退款，也不改变原支付者。保持态停止流动并保留基底，解冻后继续共同水流。世界输出目前仅开放已登记的液态和保持态；其他输出明确编译失败，后续完善对应物态规则。原版地形、建筑通行及完整热量传播仍在后续任务中。

录像工具现在统计同一次正式施放中`matter`事件的`convertedVolume`，输出实际材料与物态；存在独立对手时还要求转换的原支付者正是对手。转换前已有材料不会被当成本次转入量。三个视角仍读取同一真实材料，不以截图颜色判断功能通过。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/留水凝冻验证.json --opponent-construction Doc/技能系统全面重构/构造输入/迎流留水.json --target-offset 16 0 --view native --duration 5 --zoom 20 --keep-frames --output artifacts/skill_rebuild/world-freeze-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/掌心熔金验证.json --opponent-construction Doc/技能系统全面重构/构造输入/迎面留剑验证.json --target-offset 16 0 --view native --duration 5 --zoom 16 --keep-frames --output artifacts/skill_rebuild/world-melt-current/native
```

把`native`分别换成`worldfall`和`firstperson`，同时更换输出子目录。明确输入、实际支付与转换、当前内容指纹和逐帧报告共同保留；这些是物态交互验证片段，不代替最终城域战斗、美术和性能验收。

### 生成水域导电与真实电击

`Conduct`是普通操作，可由定时或真实条件事件触发；普通参数提供水路距离、最低水深、每格和每个人物的消耗，以及总格数和人数上限。当前接入共同格网中的生成液态水，冻结、干隙及不相接的高度会阻断传播。传播扣除原施放余力及支付份额，实际电击使用已有雷电伤害、抗性与派生反应规则，不伪造主体直接接触。水面短暂增强明暗的位置来自已到达水格，不新增线条、投掷物或可重复攻击的装饰实体。

`verify_conduction_math.script`检查水路、循环、边界、预算、请求回退及实际工作池；`verify_conduction.script`通过正式入口检查多人物实际扣血、干地与空中不受击、冻结阻断、后续回收只返还未耗份额，以及三份修士创作，并正式保存到`artifacts/skill_rebuild/conduction/save`。真正退出并重启后运行`verify_conduction_restart.script`，检查构造及材料恢复、没有重放伤害，以及第二种枝体入口的真实导电。普通表单检查使用`verify_conduction_editor_ui.script`，保留中英文和1024分辨率截图。旧档读取仍由此前保留的v1/v2正式夹具验证，不能先重存旧夹具。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/引水行雷.json --opponent-construction Doc/技能系统全面重构/构造输入/导电验证水流.json --target-offset 16 0 --view native --duration 4.8 --zoom 18 --keep-frames --output artifacts/skill_rebuild/conduction-hand-current/native
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/枝引雷流.json --opponent-construction Doc/技能系统全面重构/构造输入/导电验证水流.json --target-offset 16 0 --view native --duration 4.8 --zoom 18 --keep-frames --output artifacts/skill_rebuild/conduction-wood-current/native
```

改用`worldfall`、`firstperson`时同步更换输出子目录。`conductionLog`记录原来源、当前控制者、真实到达水路、人物生命值前后、消耗和剩余；录像通过条件要求同一次正式传播到达实际目标并按伤害规则扣血，不能只靠水面变亮通过。原版海洋、封闭独立水层、电流维持及完整元素和性能继续待办。


## 原版水域及独立水层导电验证

`verify_conduction_domains_math.script`核对不同格宽、窄干隙、上下分层、原版零厚水面和工作池一致性。`verify_conduction_domains.script`从独立测试世界开始，经过普通已学能力和真实支付验证原版海面、冰／岩浆断路、封闭水层以及受击回调中冻结水路，随后保存本批正式夹具。实际退出并重新启动测试游戏后，运行`verify_conduction_domains_restart.script`，严格检查不同进程恢复、不重放旧电击，以及第二种形体在恢复后的真实导电。保存位于`artifacts/skill_rebuild/conduction-domains/`，不覆盖之前的共同水域或v1／v2旧档夹具。

封闭水域使用普通构造`Doc/技能系统全面重构/构造输入/封闭导电验证水流.json`。三视角演示继续使用统一入口，例如：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/引水行雷.json --opponent-construction Doc/技能系统全面重构/构造输入/封闭导电验证水流.json --view native --duration 4 --output artifacts/skill_rebuild/conduction-closed-hand/native
```

另两种视角为`worldfall`及`firstperson`，需要本机可选Worldfall已加载。原版海面目前有真实传播与人物受击，尚无海面专用放电显示；封闭和共同生成水层的明暗增强都只标记实际已到达的原水格。


## 活动水体与技能选敌验证

`verify_active_water_math.script`检查活动水格、零攻击余力、结束／取消、冻结／解冻、收束、预测回退、不可变表面以及实际外流／蒸散的支付份额。`verify_active_water.script`从独立测试世界验证持剑施法、水中和空中选敌、自主能力候选、目标保护、供水者死亡中断，以及同一水体自身导电；完成后保存至`artifacts/skill_rebuild/active-water/save`。真正退出并重启测试游戏后执行`verify_active_water_restart.script`，检查活动水体和旧电击不重放，普通构造参数及重新施放仍有效。

外部供水演示使用`活动导电验证水流.json`作为`--opponent-construction`；同一部位自身导电可直接运行：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/流水载雷.json --view native --duration 4 --output artifacts/skill_rebuild/active-water-self/native
```

另两种视角为`worldfall`和`firstperson`。这些输入使用普通流散和导电操作，没有额外整招执行代码；海面放电显示、持续电流、活动水体转交时的短暂反馈连续性及最终美术继续推进。


## 有限连续放电验证

`verify_conduction_pulses_math.script`检查每波时点、有限计数、条件只启动一次、预测回退、批量数值推进、历时／尺寸改造及32／128次工作池一致性。`verify_conduction_pulses.script`验证原版和活动水体四波真实扣血、冻结缺失第二波后继续第三／四波、加速不合并时点、余力耗尽、中断及收束退款；完成后保存至`artifacts/skill_rebuild/conduction-pulses/save`。真正重启后运行`verify_conduction_pulses_restart.script`，普通表单用`verify_conduction_pulses_ui.script`验证。

`循水连雷.json`和`流水连雷.json`使用普通`Conduct`操作的`Pulses=4`、`Interval=0.4`。前者可配`--opponent-construction .../活动导电验证水流.json`，后者为水体自身连续放电。例：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/流水连雷.json --view native --duration 4 --output artifacts/skill_rebuild/conduction-pulses-self/native
```

结果的`operation`和`pulse`分别记录原资料的操作和本轮波序号；同一波人物去重，跨波允许再次作用，不能用整段命中数大于零替代逐波验证。对手自身供水的编号必须和真实水路来源匹配。原版海面专用显示、移交后的短暂反馈接续及最终美术仍需单独验收。


固态留置使用 `verify_solid_layers_math.script` 检查真实定形及数量、`verify_solid_layers.script` 检查正式支付和共同层部分转换，真正重启后运行 `verify_solid_layers_restart.script` 检查第四版保存及历史第一、二、三版升级。普通演示输入为“流水凝铁留置”和“流水凝木留置”，继续使用同一个 `--construction` 入口；没有增加整招执行器。世界材料明确保存定形状态，旧档逐版本补齐并保留原档。


流散中的定时和条件投送用 `verify_flow_flight_math.script` 检查准确时点、实际动量及回退，用 `verify_flow_flight.script` 检查正式命中、接触停止后再启动和留置；真正重启后运行 `verify_flow_flight_restart.script`。普通演示输入为“迟发流涌”和“复流迟发”，沿用同一个 `--construction` 入口。浅层流散之后的投送支持水平速度、加速度及追踪；不能接收三维路径后静默忽略。


自然创法请求使用`verify_creation_requests.script`验证真实活动入口、固定输入、有限处理、正式授予、等待和保存；`verify_creation_timed.script`补查按月功法，`verify_creation_comprehension.script`补查实际功法领悟和同月重复学习。真正退出并重启后运行`verify_creation_requests_restart.script`验证待处理输入恢复及只授予一次。复现输入可调用`ConstructedAbilityService.Generate(actor,seed)`获得独立草稿，或调用`TryQueueCreation(actor,seed)`走实际有限队列。结果与原版世界夹具位于`artifacts/skill_rebuild/creation-requests/`。


创法倾向用`verify_inspiration.script`检查真实灵根、功法、已学、性格和使用经历的影响；真正退出重启后用`verify_inspiration_restart.script`检查受理时输入及使用记录。请求第二版保存完整数值输入，首版没有输入的未完成请求局部取消并保留原档与说明，已完成法术保持。开发复现可先用`ReadInspiration(actor)`读取输入，再调用`Generate(actor,seed,input)`；显式输入不因角色后来改变而重抽。

原术衍生用`verify_derivation.script`检查保留、替换、扩展、自然选源、独立版本及真实第二版升级；真正退出重启后用`verify_derivation_restart.script`检查第三版完整原术快照及单次授予。`Generate(actor,seed,input,source)`可复现独立衍生草稿，`TryQueueCreation(actor,seed,sourceId)`可指定此人真正已学的原术；不指定时沿用自然选择。排队预期必须使用受理状态中的`Inspiration`和`Source`，不能在排队后重新读取人物猜测。完成后临时快照释放，正式构造与直接来源保存；第二版待处理请求继续独立创作。

创法重复控制用`verify_similarity_math.script`检查真实结构与参数边界，`verify_similarity.script`检查正式登记、重复说明和目录更新，真正退出重启后运行`verify_similarity_restart.script`检查索引重建及旧资料保持。`benchmark_creation.script`测量512名修士24个月机会及512份草稿：使用相同受理和处理入口，测试场地固定为陆地，冻结行动并按月补足营养以保持样本人数。记录目录、缓存、实际世界速度及托管内存，不当作完整战斗性能验收。结果在`artifacts/skill_rebuild/similarity/`。

正式保存测试中新建人物后应先推进实际逻辑步，让原版单位列表完成接入；保存时检查返回的`SavedMap.actors_data`确有该人物。只看文件存在或模组侧人物引用，不能证明原版人物已经写入世界。

### 构造命名、说明与书籍传承

`verify_construction_text.script`检查实际材料、过程、费用和中英文说明，确认染色不冒充换材、读取不改写正式名称、编辑后不残留旧说明。名称在本地按实际构造生成；旧的正式名称仍保存，未完成请求在完成时取得当前规则名。

`verify_transmission.script`建立真实城市、语言、文化和原版图书馆，经既有写书、附体写作、自主宗门贡献及原版阅读行为验证确定版本；同时检查实际功法编写、改进、主修和领悟进度，保存到`artifacts/skill_rebuild/transmission/save`。真正退出重启后运行`verify_transmission_restart.script`，不能先重跑保存脚本覆盖依据。书只引用确定能力版本，作者改造不改写旧书或其他学习者的资料。

`verify_transmission_ui.script`从上述独立存档检查编辑器固定摘要、原版书籍和功法提示，保留中英文及1024×768截图。会切换测试世界和显示分辨率；结束后恢复原分辨率和中文。说明放在可换行正文，避免原版数值栏的长度截断；新增功法传承仍列在既有法术池内。详细规则见[构造命名说明与书籍传承](../../Doc/技能系统全面重构/构造命名说明与书籍传承.md)。

## 构造法术参与城市工作

`verify_city_constructions.script` 在独立世界检查纯世界构造的准确地块、资源费用、副作用选择，以及城市自然发布、认领、寻位和验收。它会保存测试世界到 `artifacts/skill_rebuild/city-constructions/save`。退出并重新启动游戏后，再执行 `verify_city_constructions_restart.script`；同一进程不能代替冷启动验证。后者同时录制自主工作的截图，结果写入同目录。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_city_constructions.script 180000
# 真正退出并重新启动游戏，等待加载完成后再执行下一条。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_city_constructions_restart.script 180000
```

这两项验证复用实际城市工程。除独立夹具准备和前段对照施放外，自主阶段没有逐帧指定任务或代替人物使用能力。画面验证的是功能流程，不代表最终流派美术。边界合同、施放中归属变化和新农田保护仍使用 `verify_world_scope.script`，不要同时执行两个游戏脚本。


## 持续天气与原版雨云

先执行 `verify_rain_process.script`，检查新构造原理和旧已学雨云的真实支付、持续参数、实际雨滴、暂停及对象池清理，保存结果到 `artifacts/skill_rebuild/rain-process/save`。真正退出并重启游戏后执行 `verify_rain_process_restart.script`，比较实际云龄、位置和行动进度，继续降雨、检查来源死亡、再次保存和逐条恢复，同时录制原版功能过程。

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_rain_process.script 180000
# 退出并重启游戏，等待加载完成。
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_rain_process_restart.script 180000
```

云已经形成后独立于原施放存在。恢复不会重放原法术、重复扣费或重等三秒成云；其实际结果使用原版雨滴。此验证只覆盖原版功能和保存，不代表最终流派美术或Worldfall天气验收。

### 实际资源通道与材料本金

`verify_construction_resources.script`在独立夹具里检查真元、斗气、法力、体力、妖力和自定义通道的正式支付及收束；保留真实旧容器的任选、同时支付检查。先运行：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction_resources.script 180000
```

真正退出并重启游戏后，再运行：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_construction_resources_restart.script 180000
```

结果在`artifacts/skill_rebuild/resource-channels/`。恢复脚本先登记同一个测试资源，再读取自己的保存；新报价可以变化，原有材料本金不重新报价。测试暂时隔离原版法力、体力的自然增量，并在结束时恢复；不是正常玩家设置。此批不代表旧学习、词条与轨迹已经全部迁移。

### 旧功能法术进入共同执行

`verify_utility_migration.script`通过真实已学能力入口验证八种功能法术、原收费、境界范围、词条、多次发射和轨迹改造，结果及录像帧在`artifacts/skill_rebuild/utility-migration/`。真正退出重启后运行`verify_utility_migration_restart.script`核对原已学资料与再次使用。两者均用已有DebugToolbox的`run`命令执行。

原来的`demo_spell.py`和`demo_all.py`现在把共同开始计入实际施放次数，并输出`worldContactsLog`，区分真正改变世界与只有施放开始。脚本方式显式标记为`Script`；每段边界失效后不再接受上一段世界修改，测试施法者的独立雨云也在清理时移除。被演示占用的临时容器仍可观测，结束后正常回收。

当前覆盖旧功能模板的默认形态及支持的范围、熟练、显式多发和显现等改造；未映射的状态词条与轨迹继续走原路径。旧容器、全局蓝图及书籍的正式资料转换尚未完成。完整范围见[共同执行接入](../../Doc/技能系统全面重构/旧功能法术的共同执行接入.md)。

## 固定基线世界

`baseline_scenarios.py`为全面重构的1.02提供独立配置、固定人数/种子和正式世界快照，区别于逐法术录制。每轮先重载同一份已准备世界，再运行无技能、普通交战、顶尖交锋或长期天气过程；具体参数、状态及复现边界见[可重复基线场景](../../Doc/技能系统全面重构/可重复基线场景.md)。

```bash
python3 Scripts/SpellDemos/baseline_scenarios.py prepare --launch
python3 Scripts/SpellDemos/baseline_scenarios.py run --scenario all --repeat 2
python3 Scripts/SpellDemos/baseline_scenarios.py quit
```

游戏与客户端使用`artifacts/skill_rebuild/baseline/`下的独立配置和临时会话；普通演示仍沿用原入口，不会自动连接这份专用会话。准备时记录代码/资源及真实输入内容，改变内容需重新准备；仅提交号变化可继续使用。基线场景的准备不等于最终美术或性能通过。

## 操作输入输出约定

操作输入输出约定的开发检查：

```bash
python3 Scripts/SpellDemos/verify_operation_contracts.py
```

使用同一私有游戏编译`Content/Skills/`和构造输入目录中的当前能力，检查专用参数归属、实际资源需求、计划输入不可改写和既有执行数值，再核对正式首版编号。证据保存在`artifacts/skill_rebuild/operation-contracts/`。输出之间的绑定按3.04继续实现。

## 操作输出绑定

命名输出驱动后续操作的检查：

```bash
python3 Scripts/SpellDemos/verify_output_bindings.py
python3 Scripts/SpellDemos/verify_collection_bindings.py
```

验证生长和骨架完成、冻结暂停、整体结构与单独部位、真实父关系、停止、中断、预测回退及正式构造部分保存恢复；再运行现有操作和正式资料回归。证据保存在`artifacts/skill_rebuild/output-bindings/`。3.04仍继续世界输出接入，不把未实现的输出当成已确认事实。

集合入口进一步验证真实分件、扣减后的余力与支付守恒、逐片化流及投送、全部等待、回退重放和明确的展开预算，再运行上述回归。证据保存在`artifacts/skill_rebuild/collection-bindings/`。世界确认与原有部位操作输出继续在3.04接入。

## 基础动作与握持资源专项

基础动作与握持资源的专项入口：

```bash
python3 Scripts/SpellDemos/verify_form_grips.py --record
python3 Scripts/SpellDemos/verify_form_actions.py --record
```

两者复用视角验证的私有游戏。前者检查剑、镜、双柄炉和真实持剑；后者检查五份基础动作的尺寸、速度、转向、实际支撑、双方接触阶段与非人尾鳍，并录制两种显示。结果分别保存在`artifacts/skill_rebuild/form-grips/`和`form-actions/`。它们验证资源和几何配对，完整人物抓投、受力与自然战斗继续按后续任务接入。

## 原版与模组性能基线

```bash
python3 Scripts/SpellDemos/benchmark_baselines.py
```

先结束已有WorldBox。入口使用私有游戏/加载器配置，依次测量Cultiway四种场景和只加载调试工具的原版对照，每项预热4秒、采样20秒、重复2次。`--seconds`、`--warmup`、`--repeat`可调整时长与次数。每轮重载同一正式世界，实际尺寸为1280×720，解除帧上限；数值和逐帧原始数据保存在`artifacts/skill_rebuild/performance-baseline/runs/`。计数、内存和结论边界见[性能基线](../../Doc/技能系统全面重构/性能基线.md)。

## 视角切换与共同遮挡

`verify_native_projection.py`检查纯原版显示的真实深度、透明及位置，缺少固定世界时自动准备。退出该独立游戏后，再运行三视角检查：

```bash
python3 Scripts/SpellDemos/verify_view_switch.py --worldfall /data/Downloads/S3COND2_WORLDFALL
python3 Scripts/SpellDemos/verify_view_switch.py --quit
```

使用`artifacts/skill_rebuild/view-switch/environment/`的私有游戏、配置和调试连接，只加载Cultiway、DebugToolbox及给定的Worldfall原DLL。下载包附带的译文包装源码会重入补丁，测试副本直接采用原DLL的NML入口；下载目录和玩家安装均不修改。当前下载包声明0.9.8，实际程序集版本1.0.0.0。

同一暂停世界重复往返原版、三维与附身第一人称，检查发布姿态、地面高度、实际像素位置、前后遮挡、透明、近裁剪、画面渐变及退出清理。结果、实际来源和12帧/秒的技术录像放在`artifacts/skill_rebuild/view-switch/views/`。会显式关闭测试世界残留的加载画面与控制台，等待原版近景渐变和真实人物、树木显示就绪，再开始原版录制。这是显示正确性检查，不能替代战斗、美术或规模性能验收；范围见[视角切换与共同遮挡](../../Doc/技能系统全面重构/视角切换与共同遮挡.md)。

## 构造编译的按范围验证

统一入口直接执行选定专项，各脚本每轮只运行一次：

```bash
python3 Scripts/SpellDemos/verify_compilation.py --list
python3 Scripts/SpellDemos/verify_compilation.py --suite compile
python3 Scripts/SpellDemos/verify_compilation.py --suite layers missing
python3 Scripts/SpellDemos/verify_compilation.py --suite versions
python3 Scripts/SpellDemos/verify_compilation.py
```

四类分别覆盖构造编译及预算、改造优先次序、实际缺项和版本重建；缺省运行四类。结果按日期建立独立目录，保留实际输入副本、执行脚本、内容指纹、进程和逐项结果。第一项失败就停止，未执行项不计通过；期间代码、资源或输入变化也不会报告同版本通过。

版本场景只修改测试目录中的形体说明副本，调用实际资源重载，检查旧施放保留、真实作用高度变化、同编号定义更新和失败回退；正式资源文件保持原样。需要自己的结果目录时使用`--output artifacts/skill_rebuild/compilation-checks/新的目录名`，已有目录会被拒绝。

复用既有Worldfall私有游戏，结束后用`python3 Scripts/SpellDemos/verify_view_switch.py --quit`退出该配置。具体覆盖及固定输入见[编译验证与固定输入](../../Doc/技能系统全面重构/编译验证与固定输入.md)。

## 启动支付与真实本金

`python3 Scripts/SpellDemos/verify_cast_payments.py`验证报价只冻结一次、实际扣费、任选与全部、共享余额、启动失败撤销和正式开始后的结束规则；随后顺序运行支付回收、世界输出和旧功能来源检查。复用上面的独立游戏配置，结果在`artifacts/skill_rebuild/cast-payments/`，结束后仍用`verify_view_switch.py --quit`退出。范围及未完成内容见[启动支付与真实本金](../../Doc/技能系统全面重构/启动支付与真实本金.md)。

多资源与存档使用两个阶段，真正退出后再恢复：

```bash
python3 Scripts/SpellDemos/verify_multiresource.py prepare
python3 Scripts/SpellDemos/verify_view_switch.py --quit
python3 Scripts/SpellDemos/verify_multiresource.py restart
python3 Scripts/SpellDemos/verify_view_switch.py --quit
```

第一阶段从仓库内固定历史压缩包恢复原资源世界，再检查当前支付和多资源材料；第二阶段恢复原通道世界、部分收束的多资源材料，以及不同资源配比的共同水层。逐项报告在`artifacts/skill_rebuild/multiresource/`，具体金额及升级规则见[多资源本金与存档转换](../../Doc/技能系统全面重构/多资源本金与存档转换.md)。

## 实际序列受理与封存物品

`python3 Scripts/SpellDemos/verify_sequence_admission.py`检查开始拒绝、预付和逐发支付、等待和跳过、取消、明确拒绝、真实初始化异常、完成通知顺序、符箓及卷轴消费、排队输入和正常游戏推进，再运行相关支付、构造及功能法术检查。结果在`artifacts/skill_rebuild/sequence-admission/`，结束后使用上面的`verify_view_switch.py --quit`退出独立游戏。覆盖边界见[实际发射与序列受理](../../Doc/技能系统全面重构/实际发射与序列受理.md)。

## 身体关系的并行占用专项

复用现有独立测试安装和角色，检查另一招被拒绝后原关系是否保留。结果放在各场景的 `parallel-occupancy/`，不会覆盖普通关系结果。

```bash
# 持盾与另一只手；包含原关系结束后的再次施放
python3 Scripts/SpellDemos/verify_body_footwork.py --native --held-connections --held-cases shield.face --parallel-occupancy

# 当前头、足、角三类声明；不展开身体和装备样本
python3 Scripts/SpellDemos/verify_body_footwork.py --native --connection-parts --source-cases head foot.right organ:yao.horn.thunder --parallel-occupancy

# 已支持的双手并行：一手结束不能抹掉另一手；Worldfall省略 --native
python3 Scripts/SpellDemos/verify_body_footwork.py --native --acting-surfaces --acting-cases pair
```

当前头、腿、器官及持盾样板占用站姿，因此拒绝另一独立手部动作。狼形还没有人形手部绑定，结果单独记录这一限制；拒绝不表示更细的并行能力已经实现。全面部位占用推导继续全程 TODO 4.28。

## 身体接触的连续录像

复用上述已经通过的关系场景和独立游戏。先退出另一套游戏，再录制当前环境，避免同时占用显存：

```bash
# 原版五段；缺省覆盖徒手、剑、角、飞行目标及游泳目标
python3 Scripts/SpellDemos/record_body_connections.py --native

# 退出独立原版，再运行Worldfall五段
CULTIWAY_BASELINE_ROOT="$PWD/artifacts/skill_rebuild/performance-baseline/cultiway" CULTIWAY_BASELINE_GAME="$PWD/artifacts/skill_rebuild/performance-baseline/cultiway/game/worldbox" python3 Scripts/SpellDemos/baseline_scenarios.py quit
python3 Scripts/SpellDemos/record_body_connections.py
python3 Scripts/SpellDemos/verify_view_switch.py --quit

# 只补录受影响的一段，或只重新编码已有真实帧
python3 Scripts/SpellDemos/record_body_connections.py --case flying
python3 Scripts/SpellDemos/record_body_connections.py --native --encode-only
python3 Scripts/SpellDemos/record_body_connections.py --encode-only
```

输出在`artifacts/skill_rebuild/body-connection-films/native/`及`worldfall/`，每段有同名录像和证据JSON；完整五段运行还生成`manifest.json`及顺序合集`all.mp4`。实际输入、关系事件、控制归还结果、逐帧时间和原始图片放在对应场景的`film/`子目录，不覆盖普通专项结果。指定场景运行另写`manifest-场景名.json`；之后可用完整`--encode-only`更新总目录。

按实际游戏世界时间编码，暂停不计入运动时长，不补离线动作。原版临时隐藏状态提示、粒子和顶层装饰，再还原显示；Worldfall在结算边界等待异步画面更新后读取实际最终纹理，再继续原场景。采集时间长于片长，不作为实时帧率证明。飞行及游泳沿两种环境各自已经验证的构造，差异和验收边界见[连续录像记录](../../Doc/技能系统全面重构/接触关系连续录像.md)。这些片段用于接触功能验收，不是最终美术或负载性能证明。

## 抓持后的带动专项

复用同一关系世界和两个人物，检查真实抓住以后随手部移动、环境入口截停及原动作正常归还：

```bash
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying
# 退出另一套独立游戏后，在Worldfall核对受影响的正常身体显示
python3 Scripts/SpellDemos/verify_body_footwork.py --carrying --carry-cases move
```

结果在`artifacts/skill_rebuild/body-carrying-native/`或`body-carrying/`；`--carry-cases move blocked`可限定范围，完整入口另核对实际带动中的换世界清理。默认依次检查移动、旋转、数值模型阻挡及地面截停，持物旋转和实际房屋按需指定，不扩大装备样本。

```bash
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases rotate
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases rotate-item
python3 Scripts/SpellDemos/verify_body_footwork.py --carrying --carry-cases rotate
```

正常带动样板复用已有腾起支撑，先上提再进入横移或旋转；站位只选择指定早阶段可达的真实接触，避免在下落途中才抓住。`blocked`使用上提路径中的数值顶面，`ground`保持原下压场景；实际房屋仍由`model`单独验收。

`rotate`核对真实抓点、完整旋转及中间身体尺度，独立原版还检查实际相机批次与网格顶点；`rotate-item`使用同一人物和已有铁剑，额外核对真实持物，结束后检查网格归还。原版输出`rotated-native.png`为完整相机画面，`rotated-target-diagnostic.png`临时隐藏出招者，仅用于区分人物重叠与目标自身的显示缺陷，不算双方交锋证据。Worldfall沿已有双视角入口输出同一时点画面。

`blocked`在实际取得目标后添加数值三角面，核对共同时点截停、一次解除和恢复，不将它冒充真实建筑；`ground`检查身体真实表面下压时的地面截停；`model`复用Worldfall原有房屋，按实际地面与房屋几何选站位，仍经正式抓取和带动，要求确实移动后才碰到该房屋。后两种结果包含环境身份、环境接触点及共同时间。原有脚底重叠可以向外离开，继续压深会停止，不扩大接触容差。

```bash
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases blocked ground
python3 Scripts/SpellDemos/verify_body_footwork.py --carrying --carry-cases model
```

通过DebugToolbox单独运行`Scripts/SpellDemos/verify_body_environment.script`可快速检查刚性转动中途落地、原有重叠、双线性地面隆起、薄面及反向边查询；输出`artifacts/skill_rebuild/body-environment/`。它不创建游戏人物，也不算实机障碍验收或最终录像。

Worldfall活动人形及实际持物已接通平面共同模型投影，`--carrying --carry-cases rotate rotate-item`还能复核同一人物连续换装，不重建测试环境。检查包含出招者继续推进后的当前表面与实际网格、布局不变时的网格复用。已有动物、龙和器官沿同一入口提交面色或顶点色，资源快速核对可通过DebugToolbox运行`Scripts/SpellDemos/verify_body_surface_colors.script`，不创建战斗样本；实机继续复用已有`--target-parts --target-cases wolf.organ:yao.horn.thunder`场景。真实环境接触与释放摔投的交付状态、正式保存及整条过程录像见4.24的后两份交付，见[固定范围](../../Doc/技能系统全面重构/抓持带动与摔投.md)。


## 离手后的身体运动

复用带动专项的同一对人物与现成世界，在真实倾斜、转动途中松手。`throw`保留来源施放，核对离手连续性、重力和角速度、环境接触、同一步归还查询及最终控制；`throw-detached`在离手后结束原施放，核对目标独立继续；`throw-external`核对外部重新施力后旧控制退出、外部速度保留及一次取消报告。

```bash
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases throw throw-detached throw-external
# 先退出原版独立游戏，再核对Worldfall受影响的显示及归还路径
python3 Scripts/SpellDemos/verify_body_footwork.py --carrying --carry-cases throw
```

结果沿`body-carrying-native/cases-.../`或`body-carrying/cases-.../`保存，包含实际输入、确认事件与检查。`released-body.png`为实际原版相机近景；Worldfall另有`movies/released/native/`与`worldfall/`同一时点画面。多场景共用目录时事件文件和图片以最后对应场景为准，需要逐段保留时分别指定一个场景；完整检查记录仍包含全部指定场景。本入口是功能检查，正式保存及连续录像继续C3。

撞伤和清理继续复用同一抓投，不另建环境：

```bash
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases ground slam impact
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases impact-zero impact-external impact-world
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases impact-death
# 退出原版后，仅核对Worldfall受影响的真实撞击和恢复
python3 Scripts/SpellDemos/verify_body_footwork.py --carrying --carry-cases impact
```

`ground`核对原地贴地受阻不凭空撞伤，`slam`沿同一动作先抬起再压下，核对仍抓着的真实撞伤；`impact`核对离手后的真实接触点速度、冻结来源、标准受击、暂停/倍速和同一候选重入去重；`impact-zero`核对零倍率不伤害；`impact-external`在伤害回调施加新运动，要求旧归还不覆盖它；`impact-world`在实际离手后加载已有世界，要求旧运动和控制退出，不补结算撞伤；`impact-death`单独核对飞行中实际死亡后旧运动退出。死亡和换世界场景分别运行，避免对已销毁人物继续准备下个动作。撞击记录为`body-impacts.json`；这些场景省略重复相机取图，沿用C1已经通过的显示证据。脚本改变来源层次只用于核对冻结归属，结束时恢复；临时受击观察随场景退出移除。

### 抓投的编辑、正式恢复与连续录像

万法阁的双方关系在选择“抓持”后提供“抓住后带动目标”；开启后可选择随抓点旋转，以及实际撞伤倍率。关闭带动或改为抵住会清除旋转。零倍率可搬动而不撞伤，非法倍率仍由正式编译阻止保存，撤销恢复上次合法草稿。

```bash
python3 Scripts/SpellDemos/verify_body_footwork.py --carrying --carry-cases save
python3 Scripts/SpellDemos/record_body_connections.py --case throw throw-item
# 退出Worldfall独立游戏后，复用没有Worldfall的安装
python3 Scripts/SpellDemos/verify_body_footwork.py --native --carrying --carry-cases save
python3 Scripts/SpellDemos/record_body_connections.py --native --case throw throw-item
```

`save`只运行一次指定保存场景，输出`cases-save/save-held`、`save-flight`及恢复记录，分别保存抓持中和离手中的正式世界。它同时核对费用、结构比较、人物掌握和读档后再次抓投。真正退出并重启对应独立游戏后，通过现有DebugToolbox执行`verify_body_carrying_restart.script`核对跨进程恢复；它要求进程号确实变化，不会只清空字典冒充重启。

真实表单检查使用`verify_body_connection_editor_ui.script`，图像和结果在`body-carrying-editor/ui/`，核对开关联动、非法倍率、撤销、保存重开及中英文界面。它操作自己的夹具人物，不写玩家世界。

两段抓投录像输出到`body-connection-films/native/`或`worldfall/`的`throw.mp4`、`throw-item.mp4`；后者是同一目标实际持有铁剑，不声称出招者持剑抓人。工具自动复用本安装已通过`save`的世界和人物，并先准备实际镜头再选站位；必须先在同一安装运行上面的保存场景。完整输入、帧、事件和正式结果保存在`body-carrying[-native]/cases-throw[-item]/film/`。常速片长来自递增的真实世界时间，包含起势、抓持、带动旋转、离手、环境接触、撞伤和归还；不把暂停等待画面算作片长。功能录像不替代最终动作美术和负载性能验收。

当前四段均通过，统一交付清单为`artifacts/skill_rebuild/body-carrying-delivery/video-manifest.json`。旧的未完成尝试保存在`worldfall/prior-attempts/`，不混入交付清单。取景先准备原版位置和缩放再锁定Worldfall，避免相机移动触发解锁；仍按真实世界时间编码。


## 兵器架挡、拨压与脱手的连续录像

沿用4.25已有双人铁剑场景，不再遍历身体和装备；每段包含正式施放、真实器物接触、解除和双方控制归还。脱手另外记录同一原物品的地面状态、落地静止和实际拾取。

```bash
# 当前Worldfall独立游戏，四段完整录像
python3 Scripts/SpellDemos/record_weapon_connections.py
# 原版独立游戏；先退出Worldfall，避免同时占用显存
python3 Scripts/SpellDemos/record_weapon_connections.py --native
# 指定行为：brace、deflect、passive、disarm
python3 Scripts/SpellDemos/record_weapon_connections.py --case disarm
# 汇总已有实际帧，不重新运行场景
python3 Scripts/SpellDemos/record_weapon_connections.py --encode-only
```

录像及证据索引位于`artifacts/skill_rebuild/weapon-connection-films/native/`或`worldfall/`；完整运行额外生成`all.mp4`和`manifest.json`。输入、逐帧真实世界时间、控制状态、原物品归属及正式关系事实保存在`weapon-connections[-native]/cases-场景/film/`，不覆盖原专项结果。底层入口`verify_weapon_connections.py --case normal --film`只接受这四种正常行为的单场景。

Worldfall的主动拨压沿实际臂链的可达方向上拨；普通持物录像复用共用拨动动作和已有的真实目标部位适配，起势朝向固定。原版沿既有下压手路。两者均须正式重新确认真实器物接触，没有增加接触范围或骨长。开拍前只冻结自主行为，让新世界的人物完成一次正常发布，再等待三维画面跟上。Worldfall编码明确排除一张施放前可能残留旧世界纹理的技术初帧，记录裁去帧数和实际世界时间起止；接触及恢复过程连续。录像按世界时间常速编码，暂停等待三维绘制不计入片长，功能录像不作为最终美术或负载性能证明。存档和异常释放复用本项已有证据。


## 真实结果驱动的身体收势

```bash
# 4.26 A：命中、实际双剑架挡、挥空和恢复后再次出招
python3 Scripts/SpellDemos/verify_body_responses.py --native
# 同一专项的真实原版三时点近景
python3 Scripts/SpellDemos/verify_body_responses.py --native --case contact blocked --pictures
```

输出在`artifacts/skill_rebuild/body-responses-native/`，指定场景放入`cases-.../`。`actions.json`记录实际世界时间、动作阶段和`BodyResult`；`checks.json`及`result.json`保留功能结果。`--pictures`直接读取原版相机近景，不以屏幕缩放后的远景判断手路；不加该参数只执行功能检查。

命中场景使用允许间隔重复的规则，恢复时把原目标移回掌面，核对本身体实际停攻；兵器场景沿已有铁剑与正式架挡。数值检查共用同一身体，不遍历身体或装备。当前只验原地动作的命中与被挡恢复；带步法、失衡、中断及落地继续B，共用生成与编辑、正式保存升级和双视图连续录像集中C，见[固定范围](../../Doc/技能系统全面重构/结果驱动动作过程.md)。

## 非人身体的真实结构

```bash
# 4.29 C2：沿已有独立安装采集同一狼形的真实关节和候选支撑
python3 Scripts/SpellDemos/verify_body_structure.py
# 先退出原版独立游戏，再复用Worldfall安装
python3 Scripts/SpellDemos/verify_body_structure.py --worldfall
```

输出位于`artifacts/skill_rebuild/body-structure/native/`或`worldfall/`，保留完整输入、检查、`joints.json`及`supports.json`。它检查真实四足、缺失部位不给替代及原版制作表清理后重新采集；查询不取得身体控制，不是爪击或接地求解演示。原版资料现已覆盖既有八种有足兽形的已有行走帧，各帧只取实际可见足面；蛇形、游泳帧没有地面足部替身。Worldfall沿原模型读取真实骨段，不给单段兽腿补出膝关节。最低接触可以是面、线或点；完整录像集中D。

```bash
# 对照原版图片快速检查兽足区域、实际关节和承接；需要 Pillow
/home/inmny/.venvs/cultiway/bin/python Scripts/SpellDemos/check_creature_regions.py
# 同一真实狼形沿原动画切帧，检查各帧自己的足面和关节
python3 Scripts/SpellDemos/verify_body_structure.py --frames
# 器官额外预留全身：正常接触、冲突不支付、无伪造人形足底及结束归还
python3 Scripts/SpellDemos/verify_organ_control.py --stance
python3 Scripts/SpellDemos/verify_organ_control.py --stance --worldfall
```

切帧事实位于`artifacts/skill_rebuild/body-data/native/`；资源检查加`--preview 输出路径`可导出开发叠图。器官预留事实位于`artifacts/skill_rebuild/organ-stance/native/`或`worldfall/`，复用现有狐形及尾翼，未扩充身体装备样本。

```bash
# 只读取同一既有兽形清单的Worldfall原模型，用实际共用裁面核对足部声明
python3 Scripts/SpellDemos/check_worldfall_feet.py
# 按需导出原模型与足部区域并排预览，需要Pillow
/home/inmny/.venvs/cultiway/bin/python Scripts/SpellDemos/check_worldfall_feet.py --preview artifacts/skill_rebuild/body-data/worldfall/foot-regions.png
```

该入口复用现有独立三维安装，不创建一批动物。`body-data/worldfall/models.json`保存原模型与实际裁面输出，各自足部低于原关节，蛇形不提供伪造足面；区域预览不代替实际动作或最终模型验收。

原地兽足动作沿同一已有身体，读取原像素足部或原模型明确足垫。其他足底按自己的骨长贴地，真实支撑不使用人形双足。它检查缺失部位不支付、实际足面命中、另一足保持贴地及正常归还；`--worldfall`另核对实际绘制足尖。

```bash
python3 Scripts/SpellDemos/verify_body_joint.py
# 先退出原版，再核对同一三维斜地场景
python3 Scripts/SpellDemos/verify_body_joint.py --worldfall
```

输入、定义、接触、归还、检查及截图保存在`artifacts/skill_rebuild/body-joint/native/`或`worldfall/`。这是原地框架验证，非人步法、自然站姿交接、创法编辑和保存及集中影片仍按4.29后续交付，不算最终动作美术。
