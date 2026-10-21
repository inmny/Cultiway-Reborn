# 法术实机演示工具

两个入口共用一套临时游戏内工具。读取运行中模组的实际清单，通过正式技能序列、主动能力与装备入口施放，按游戏世界时间录制。新增法术正常注册后，会自动进入清单，不需要再维护录像名单。

## 使用前

- 本机需要 Python 3.10 以上、ffmpeg，以及 WorldBox/NML 和已启用的 DebugToolbox。
- 客户端缺失时，在 `Mods/DebugToolbox` 执行 `dotnet build Client/DebugToolbox.Client.csproj -c Debug`。
- 演示会读取仓库中的独立测试世界，替换当前内存中的世界；请在测试游戏中运行。不会保存或覆盖玩家存档，也不会修改测试夹具。
- 默认连接已经启动的游戏。Linux 下可加 `--launch`，在游戏未运行时使用 `artifacts/spell_demo/profile` 独立配置启动。第一次启动仍需让 NML 正常加载 Cultiway 和 DebugToolbox。
- 一次运行一个入口；`Ctrl+C` 会请求结束录制、暂停游戏并恢复界面。运行时不必开着额外的调试脚本。

以下命令在项目根目录执行，两个入口也支持从其他工作目录调用。

## 遍历所有法术

```bash
python3 Scripts/SpellDemos/demo_all.py --output artifacts/spell_demo/latest
```

默认每段三世界秒，最多每世界秒截二十帧；分别录制原始效果、添加词条、改变轨迹。默认词条从当前兼容项中选取巨大、齐射、急速，最多两个；巨大使用 1.8 倍。替代轨迹优先展示曲线、螺旋、群落坠击或追踪等与原始轨迹不同的效果。不兼容的变化列入 `manifest.json` 的 `skipped`，不会通过绕过检查制造录像。

默认录像为普通观察视角的无声实机画面；`--view worldfall` 切换为已加载 Worldfall 的三维观察，并固定日光以便对比动作和材质；设置只写入演示使用的游戏配置。缺少该模组或镜头未就绪时会明确失败。一次正式施放可以包含技能原有的连发或被动追击。每招前还原地形、原版及模组状态，隔离背景单位；施肥、排水、净土等分别准备未成熟麦田、水域或火焰，保证展示实际作用条件。

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

# 独立形体、材质和共同姿态的接入录像；没有战斗验收含义
python3 Scripts/SpellDemos/demo_all.py --resource-probe --view native --duration 6 --keep-frames
python3 Scripts/SpellDemos/demo_all.py --resource-probe --view worldfall --duration 6 --keep-frames
```

盘点导出 `inventory.json` 与 `能力迁移清单.md`，包括未列入140项演示的内部技能、被动法宝、器官及状态。登记条目之间有引用，不能把总数当成独立法术数量。人物载入后生成的丹药状态也会进入登记；本工具临时冻结状态排除在外。尚未展开的来源会列入 `pending`。

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

它比较身体、形体及混合构造在16、128、1024、4096次独立施放下的姿态、旋转、生长、脱离、追踪、接触形状、余量和进度，输出 `construction-math-benchmark.json`。计时不包含输入采集、前态复制、候选筛选、实际确认和绘制，不能当作整场战斗性能。`verify_clashes.script` 另有超过计算门槛的实际相撞及同时攻盾，并通过临时追踪确认现有工作线程参与；追踪在用例结束时解除，不用于性能计时。

完整显示交接可单独检查：

```bash
dotnet ../DebugToolbox/Client/bin/Debug/net10.0/DebugToolbox.Client.dll run Scripts/SpellDemos/verify_presentation.script 60000
```

此项对正式受理的施放逐步推进积压世界时间，检查完整轮次前后身体、部位、持物及形体一起切换，以及多次读取、换器和接触回调清理。结果写入 `presentation-checks.json`。玩法中的器物控制检查使用 `HasWeaponControl`；绘制中的持物隐藏读取 `IsWeaponBorrowed`，受理尚未发布时仍显示上一完整轮。
