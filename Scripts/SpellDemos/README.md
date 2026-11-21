# 法术实机演示工具


无主煞风劫使用 `python3 Scripts/SpellDemos/verify_baleful_wind.py`：复用现成世界，验证固定环境风柱、九波真实承伤、指定目标、中途正式存读后只续接未结算劫风、失败归还及换世界清理；只回归一条受影响的人物来源。结果、事件、正常画面及中途存档写入 `artifacts/skill_rebuild/trajectories/baleful-wind/`。加 `--section tail` 只补人物来源及换世界尾段，已通过的九波与存读不重跑；交付索引明确保留原整段失败和已通过分段。完整连续录像仍集中在5.05 C交付点。

两个入口共用一套临时游戏内工具。读取运行中模组的实际清单，通过正式技能序列、主动能力与装备入口施放，按游戏世界时间录制。新增法术正常注册后，会自动进入清单，不需要再维护录像名单。

命名步骤与同部位混合操作的修改窗口使用 `python3 Scripts/SpellDemos/verify_step_editor.py`：复用既有测试世界，通过真实行和选择菜单编辑名称、输出引用、阶段及飞行曲线，检查非法循环、撤销恢复、正式保存再打开及依赖的实际执行。结果与中英窗口图写入 `artifacts/skill_rebuild/trajectories/step-editor/`。加 `--section runtime` 只补由表单已保存资料的实际运行与结束，不重跑编辑流程；`--section capture`只补已保存资料的正常输入画面。此入口不重跑身体装备或旧材料矩阵，正式世界冷启动与集中录像在5.05 C交付点执行。

近距光束外观用 `dev.py exec --timeout 315 -- python3 Scripts/SpellDemos/verify_beams.py --section appearance` 复验。会检查旧资料半径、实机区内外、新版束体自然收束，并输出原版俯视的近、中、远景图。直接运行 `verify_beams.py` 仍进入原光束来源规则检查。

连锁雷体外观用 `dev.py exec --timeout 210 -- python3 Scripts/SpellDemos/verify_chain.py --section appearance` 复验。检查一次实际短链、下一跳的触发与无伤旁观者，保存原版近远图；直接运行 `verify_chain.py` 仍执行原完整来源检查。

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

原土墙迁移与旧世界阻挡使用 `python3 Scripts/SpellDemos/verify_wall_migration.py`：复用独立测试游戏和现成世界，检查旧飞行法术、原版弹丸、地面走动/步法/受力、实际容量、结束恢复与失败退款。结果写入 `artifacts/skill_rebuild/trajectories/wall-migration/`。加 `--section solid-walls` 仅检查新增冰墙与木壁的独立形体、来源接管、真实阻挡及失败恢复，证据独立写入 `artifacts/skill_rebuild/trajectories/solid-walls/`，不覆盖土墙结果。完整编辑、自主创作、保存冷启动与集中录像仍在5.05的C交付点进行。

土墙打磨只需 `dev.py exec --timeout 245 -- python3 Scripts/SpellDemos/verify_wall_migration.py --section earth-appearance`。沿用 B21 原已学来源和固定世界，检查人物正面、墙端内外与撤墙通行，输出近远、Worldfall 斜视与撤墙画面。已经拍过同一版资源时，使用 `--section earth-walk` 只复验普通寻路步行和撤墙。 土墙成形和收势使用 `--section earth-motion`，只重验墙身未成形、临到期、碎块仍可见和撤完时的正面通行，保留阶段图和实际时间。

任意曲线路径使用 `verify_flight_path.script` 检查实际位置、解析速度、切向朝向、循环、延迟、速度继承、反射及预测回退；`verify_flight_contacts.script` 检查高弧线越过中间人物后实际命中后方，以及带非零横向速度的曲线反射。曲线资料、旧轨迹逐项接入范围及两份可复现输入见[曲线说明](../../Doc/技能系统全面重构/可编辑曲线与轨迹接入.md)。仍使用同一 `--construction` 录制入口，不增加单招专用工具。

构造录像的每张 `materialFrames` 同时记录共同状态的地图位置 `position`，便于将路径、截图和实际接触对应。`absoluteHeight` 为真时，高度已包含共同地表；`groundHeight` 是同一快照中的局部地面高度，Worldfall 不再重复抬高。未接入共同地表的旧表现仍保留原来的相对高度。

## 遍历所有法术

```bash
python3 Scripts/SpellDemos/demo_all.py --output artifacts/spell_demo/latest
```

默认每段三世界秒，最多每世界秒截二十帧；分别录制原始效果、添加词条、改变轨迹。默认词条从当前兼容项中选取巨大、齐射、急速，最多两个；巨大使用 1.8 倍。替代轨迹优先展示曲线、螺旋、群落坠击或追踪等与原始轨迹不同的效果。不兼容的变化列入 `manifest.json` 的 `skipped`，不会通过绕过检查制造录像。

默认录像为普通观察视角的无声实机画面；`--view worldfall` 切换为已加载 Worldfall 的三维观察，并固定日光以便对比动作和材质；设置只写入演示使用的游戏配置。缺少该模组或镜头未就绪时会明确失败。一次正式施放可以包含技能原有的连发或被动追击。每招前还原地形、原版及模组状态，隔离背景单位；施肥、排水、净土等分别准备未成熟麦田、水域或火焰，保证展示实际作用条件。

`--lighting night` 可配合 `--view worldfall` 或 `firstperson` 固定夜景，默认 `day` 保持日光对照。结果与清单记录请求条件、实际外部昼夜模式及主光／环境光颜色；原版视角不接受此夜景选项。

`--view firstperson` 使用 Worldfall 真实附身镜头，单招报告的 `viewState` 记录是否实际附身到施法者。原地掌击用 `--view worldfall --zoom 2.2` 可锁定施法者录近景；原版近景仅覆盖本次开发测试相机。附身掌击开启 `--motion-trace` 时额外记录 `palmView`，可用它定位接触帧上的掌面。沿坡构造的附身录制固定向实际目标取景，退出时恢复鼠标转向。

普通录像和 `--motion-trace` 的原地掌击使用同一人形对靶。`palm-entry.json` 记录调用正式入口前的实际视角、距离、高度和出手条件；`result.json` 只有记录到一次真实掌面接触、对应扣血以及自然收势才算通过。建议至少录两世界秒，同时看出手和归还控制。

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

4.31的延伸接触复用现成人物和独立测试安装：

```bash
python3 Scripts/SpellDemos/verify_body_extension.py
# 同一入口在已有Worldfall独立安装执行；先退出上一个独立游戏
python3 Scripts/SpellDemos/verify_body_extension.py --worldfall
# 同一入口验证真实法宝承载、独占、原物归还及独立投送
python3 Scripts/SpellDemos/verify_body_extension.py --carrier
```

检查实际身体窗口、阶段作用及限额、收势和中断回收、非法远程抓持、正式远近接触与去重、一次支付、占用拒绝和归还后的再次使用，并通过实际编辑按钮保存学习。结果与中英文截图位于`artifacts/skill_rebuild/body-extension/{native,worldfall}/`。没有增加身体和装备样本，正式世界恢复及连续影片集中到4.31C。

`--carrier`复用同一人形并正式制作、装备一件飞剑，检查原物处的真实出手、支付与独占、旧主动抢占拒绝、离手后归还法宝且作用继续、实际命中、再次使用和卸装后的中断。实际编辑按钮可以保存学习承载规则，结果及中英文截图位于`artifacts/skill_rebuild/body-carrier/{native,worldfall}/`，可与`--worldfall`合用。正式世界恢复、换世界及连续影片集中4.31C，不按法宝种类重复共用生命周期。

4.31C1共用正式恢复沿同一入口执行：

```bash
python3 Scripts/SpellDemos/verify_body_extension.py --save-stage all
# 可分开执行；restore发现仍是准备进程时，会正常退出并真正重启
python3 Scripts/SpellDemos/verify_body_extension.py --save-stage prepare
python3 Scripts/SpellDemos/verify_body_extension.py --save-stage restore
```

准备阶段读取旧缺省资料，在真实身体及原法宝仍借用时写出正式世界。恢复阶段先验证文件和临时控制未复活，再登记测试状态，实际使用旧离体、延伸与法宝承载，最后检查换世界和旧凭据延迟归还。资料在`artifacts/skill_rebuild/body-transform-save/native/`；共用格式只做一次原版冷启动，Worldfall关注新增位置和连续画面，不重复相同存档流程。失败保留原进程及冻结脚本，不因观察超时自动重开场景。

4.31C2连续画面复用同一人物、形体及一件原法宝：

```bash
python3 Scripts/SpellDemos/record_body_transformations.py --native
# 先正常退出上一个独立游戏，再录Worldfall
python3 Scripts/SpellDemos/record_body_transformations.py
# 指定一种改造，或仅重新编码已有实际帧
python3 Scripts/SpellDemos/record_body_transformations.py --native --case carrier
python3 Scripts/SpellDemos/record_body_transformations.py --native --encode-only
```

两种显示各提供`extension`与`carrier`两段及`all.mp4`，位于`artifacts/skill_rebuild/body-transform-films/{native,worldfall}/`。各段保留正式构造、实际接触、费用、带时间的帧与事件，法宝片段额外保存每帧原物借用状态。镜头覆盖实际作用路径，Worldfall不作中央裁切；身体控制真实归还后才解除测试附体。片长沿世界时间编码，不插入离线动作；功能影片不替代后续流派美术。

4.32A的实际地块接触条件使用`python3 Scripts/SpellDemos/verify_world_contact_events.py`。复用已有足底及普通形体，检查真正触地后才在接触处启动分支、悬空落空、出手前中断和控制归还；不附带伪造的地形修改。冻结输入、构造和实际事件保存在`artifacts/skill_rebuild/world-contact-events/`。本入口只验证传播起点，宏观传播与双视图连续影片集中后续批次。

4.32B1的地表过程使用`python3 Scripts/SpellDemos/verify_ground_propagation.py`。沿同一足底触地启动普通`Propagate`操作，实际检查到达后的远近人物伤害、净化、共同波厚、预测回退、极短窄波面、动态水隙及取消清理；复用同一身体和世界，不增加装备样本。冻结输入、构造、动作、接触、实际地块结果及结束记录保存在`artifacts/skill_rebuild/ground-propagation/`。B2的城市/世界尺度、Worldfall实绘及C的正式恢复和连续影片仍单独交付。

4.32B2复用已有平坦演示世界，城域通过原版入口建立单个实际城市，全域使用地图实际宽高；两类场景使用相同的触地与传播过程。运行`python3 Scripts/SpellDemos/verify_ground_scale.py --case city world`检查原版，正常退出该独立游戏后，加`--worldfall`检查另一套显示；也可只指定一种尺度。输出在`artifacts/skill_rebuild/ground-scale/{native,worldfall}/{city,world}/`，包括实际路线、身体动作、接触与世界结果，及来源蓄势近景、传播中途、外沿和结束画面。范围、速度、厚度及入口安静区均为普通构造资料，显示和作用共用。入口同时核对向后合并到达的时间与路程精度，原版城域附带一次重新进入和真实交锋检查；不重开身体装备矩阵。当前两种尺度、两套显示均已交付，造型和密集性能仍有后项；正式保存及集中录像继续C。

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

#### 逐波放电画面（6.12）

在已加载 Worldfall 的独立游戏运行下面两段。`native` 对同一波对照原版及三维画面，再冻结一格宽的横断面；`transferred` 用同一测试世界检查活动水层转交、下一波和换世界。两段分别保存真实水路、截图及结束时的绘制错误检查。

```bash
python3 .agents/skills/develop/scripts/dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_conduction_visuals.py --section native
python3 .agents/skills/develop/scripts/dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_conduction_visuals.py --section transferred
```

如需准备兼容副本，使用先前已通过三维验收的 Worldfall 原 DLL、`mod.json` 和图标，放在独立游戏的 `worldbox_Data/StreamingAssets/mods/Worldfall` 下；玩家安装无需改动。查阅 [逐波放电记录](../../Doc/技能系统全面重构/生成水域导电与实际电击.md#612-收尾按真实水路显示放电)。

#### 交锋去重与终止（6.16）

在独立游戏中分段运行。`water` 核对两波实际到达、逐人扣血、取消和重施；`contact` 对照主体、追加、分送及同一原人物的多映像传伤。两段在真实受击回调内记录主动伤害与后续反应的分类，并保存扣血和交锋记录。规则见[交锋去重与终止](../../Doc/技能系统全面重构/交锋去重与终止.md)。

```bash
python3 .agents/skills/develop/scripts/dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_clash_termination.py --section water
python3 .agents/skills/develop/scripts/dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_clash_termination.py --section contact
```

#### 非接触结果（6.17）

用现有世界复验指定传讯。只在正式送达后启动依赖结果和到达条件的后续；收件人提前越界、同源刷新和读档不产生新送达，也没有命中。运行后在本次输出的 `direct-signal` 目录查看结果、实际收件记录与正式测试存档。

```bash
python3 .agents/skills/develop/scripts/dev.py exec --timeout 177 -- python3 Scripts/SpellDemos/verify_direct_signal.py
```

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

```bash
# 4.29 C3：器官出招与本身体真实命名足共同移动；包含缺失拒绝和结束行走恢复
python3 Scripts/SpellDemos/verify_named_steps.py --organ
python3 Scripts/SpellDemos/verify_named_steps.py --organ --worldfall
```

复用现有狼形与单尾，不与`--ground`等其他场景混用。输入与事实位于`artifacts/skill_rebuild/organ-steps/native/`或`worldfall/`：`preflight.json`保留共同脚下及真实作用段，`checks.json`核对真实安装、身体底面无静态重影及器官作用；接触、归还与实绘图分别保存。完整连续录像仍集中4.29的D交付。

原地兽足动作沿同一已有身体，读取原像素足部或原模型明确足垫。其他足底按自己的骨长贴地，真实支撑不使用人形双足。它检查缺失部位不支付、实际足面命中、另一足保持贴地及正常归还；`--worldfall`另核对实际绘制足尖。

```bash
python3 Scripts/SpellDemos/verify_body_joint.py
# 先退出原版，再核对同一三维斜地场景
python3 Scripts/SpellDemos/verify_body_joint.py --worldfall
```

输入、定义、接触、归还、检查及截图保存在`artifacts/skill_rebuild/body-joint/native/`或`worldfall/`。这是原地框架验证，不算最终动作美术；步法与自然站姿交接使用各自入口，创法编辑、正式保存及集中影片继续4.29的D交付。

```bash
# 4.29 C4：正式受击接手、真实骨段起身、起身中再受击和控制归还
python3 Scripts/SpellDemos/verify_body_joint.py --recovery
# 先退出原版，再复用同一狼形的三维身体
python3 Scripts/SpellDemos/verify_body_joint.py --recovery --worldfall
```

复用同一实际狼形、既有正式接触入口与地表，不扩展身体装备样本。非法承力输入在编译时拒绝；三维入口另暂时移除本狼形的足部声明，核对缺失资料时不接管，再原样恢复声明。受击从运行中的真实关节接手，起身中再次受击沿当刻表面继续，最后核对足面接地、骨长与父子连接、一次归还及普通行走。

输入与结果位于`artifacts/skill_rebuild/joint-recovery/{native,worldfall}/`。`force.json`与`feet.json`对应真实承力出口；`recovery.json`保存真实骨段误差及交接事件；`ground.json`保存完成状态；`actual-rise.png`仅为功能截图，完整连续影片仍集中D。

```bash
# 4.29 C5：真实兽足抓持、跨步维持、目标占用拒绝及双方普通行走恢复
python3 Scripts/SpellDemos/verify_body_joint.py --connection
# 先退出原版，再核对同一三维实际身体
python3 Scripts/SpellDemos/verify_body_joint.py --connection --worldfall
```

复用原有狼形爪击和目标，不扩展身体装备清单。关系只在实际足面与目标所选真实表面接触后建立；数值检查另核对双向有限面接触及包围球内真实空隙不接触。`--connection`与`--recovery`分别执行，完整输入、关系建立与解除、拒绝原因及功能图保存在`artifacts/skill_rebuild/joint-connections/{native,worldfall}/`。完整存档、换世界和连续影片继续D，共用关系规则不按身体重复整套验证。

```bash
# 4.29 D1：共用骨段生成、真实部件与命名足表单、学习保存后正式施放
python3 Scripts/SpellDemos/verify_body_joint.py --authoring
# 先退出原版，再核对同一三维身体及编辑结果
python3 Scripts/SpellDemos/verify_body_joint.py --authoring --worldfall
```

复用现有狼形、单尾及原目标。实际表单选择当前身体提供的原骨段或器官部件，编辑各足偏移、身体离地与移动曲线；新增姿态独立复制落脚资料，非法收势阻止保存，撤销恢复输入。另创并学习后正式施放，缺少实际足部时不支付，结束后恢复普通行走。原输入、正式编辑结果、接触与中文、英文窗口截图位于`artifacts/skill_rebuild/body-authoring/{native,worldfall}/`。该入口验证共用生成及编辑；修士自然创法请求、正式版本转换和冷启动恢复归D2，完整影片归D3。

```bash
# 4.29 D2：实际休整创法、自主使用、旧请求转换，退出后用新进程恢复
python3 Scripts/SpellDemos/verify_body_creation.py
# 原版进程退出后，复用现有 Worldfall 安装及同一身体输入
python3 Scripts/SpellDemos/verify_body_creation.py --worldfall
# 准备已通过时只恢复该世界，不重复自然创法及旧请求流程
python3 Scripts/SpellDemos/verify_body_creation.py --stage restore
```

复用既有狼形、单尾、目标和测试世界，不扩展身体装备样本。自然机会来自真正恢复妖力的休整行为；自主施放只观察游戏 AI 的正式事件。排队后改变器官集合，结果仍读取已保存的完整输入。旧人物请求和旧单轴构造通过正式区段转换；损坏身体输入只跳过该人物请求。保存时保留一项运行中的真实骨段与命名足过程，真正退出后由另一个游戏进程读档，核对固定输入、完整学习关系、只完成一次和临时控制不恢复，最后检查换世界清理。

`--stage prepare`只写出冷启动准备，`--stage restore`使用同一准备档；若仍是原进程，会先正常退出再重启。失败保留原进程和诊断，不会因等待时间长而自动重开场景。资料位于`artifacts/skill_rebuild/body-creation/{native,worldfall}/`：`autonomous.json`为实际自主使用，`old-request.json`为未改写的旧输入，`restart-expected.json`与`restored-input.json`为保存前后输入，`restore-checks.json`为恢复与世界清理结论。跨进程比较先统一为同一种 JSON 数字形式，不增加数值误差容限。此入口不录制，4.29连续近景影片仍在D3。

```bash
# 4.29 D3：复用既有角、尾、爪、组合身体及命名尾部件，录制完整近景
python3 Scripts/SpellDemos/record_nonhuman_body.py --native
# 先退出原版，再录制同一组Worldfall画面
python3 Scripts/SpellDemos/record_nonhuman_body.py
# 只录指定段，或只重新编码已有实际帧
python3 Scripts/SpellDemos/record_nonhuman_body.py --case horn tail
python3 Scripts/SpellDemos/record_nonhuman_body.py --native --encode-only
```

五段分别为`horn`角带动真实头部、`tail`尾扫配合命名四足跃进、`claw`真实兽足绕原关节出手、`together`既有狐形尾翼分别同时出手，`parts`沿B2已有三尾狐的`tail.0`与`tail.2`分别运动：原版第二条拍下后横扫，三维使用实际模型的平扫，各自完整输入分别保存，第三尾保留自然显示。复用原角色、器官容量及测试世界，不展开新身体装备清单。为单独展示本次身体过程，原器官法术预置有限冷却，站立目标沿既有冻结及攻击恢复状态；本次动作继续原体力支付、真实几何、接触与归还规则。原版读取实际游戏相机，Worldfall读取原模组最终纹理，等三维实际发布后才取样，按世界时间编码，不用暂停凑出运动时长。

产物在`artifacts/skill_rebuild/nonhuman-body-films/{native,worldfall}/`。各段目录保留完整`definitions.json`、带时间的帧与正式事件`recording.json`及结果；五段影片和`all.mp4`位于上级，`manifest.json`为完整清单。每个动作均须发生正式接触，全部部位正常收势、归还控制后才接受本段。此处是4.29框架功能影片；完整流派动作美术仍归9.49及10.09。

```bash
# 4.32 C1：普通触地传播创作、派生、真实编辑和运行中正式保存
python3 Scripts/SpellDemos/verify_ground_authoring.py
# 正常退出上述原版独立进程后，以新进程读回同一正式世界
python3 Scripts/SpellDemos/verify_ground_authoring.py --restart
```

复用已有原版独立安装、世界和一名创造者；旧规则请求复用同一目标，不增加身体装备样本。准备阶段从当前规则独立创作普通传播，通过实际窗口生成独立修订并学习、检查无效速度禁止保存，再由正式入口支付和触地。保存时身体和波面仍在运行，同时保留规则7、规则8的等待请求。`--restart`要求进程编号确实变化，核对完整构造、人物掌握及两项确定输入，确认临时过程不复活，再检查恢复后使用、接触去重、控制归还和换世界清理。

产物在`artifacts/skill_rebuild/ground-authoring/`：中英文表单截图、创作及编辑的完整资料、正式`save/`、`expected.json`、两阶段冻结脚本和各自的结果与`*-checks.json`。失败保留现场。已控制同一人物时不重复解除附体，避免原版解除附体附带的随机推力干扰场景；不修改实际中断规则。宏观双视图连续影片集中C2。

```bash
# 4.32 C2：复用城域和全域场景，录制正式触地、传播、到达和结束
python3 Scripts/SpellDemos/record_ground_scale.py --native
# 正常退出原版独立进程后，录制相同过程的Worldfall画面
python3 Scripts/SpellDemos/record_ground_scale.py
# 指定一段，或只重新编码已有真实帧
python3 Scripts/SpellDemos/record_ground_scale.py --case city
python3 Scripts/SpellDemos/record_ground_scale.py --native --encode-only
```

复用B2的实际城市与256×256全陆地世界、同一足部动作和普通传播。近景记录真实蓄势与触地，0.5秒后切到同一次施放全景，不重放来源动作；后续连续记录实际到达、地块结果和自然结束。原版取景隐藏头顶图标，三维读取Worldfall最终纹理并等待正式发布；取景暂停不计入运动时长。仍保留本场景的支付、作用和归还检查，不重复额外的重新进入及双方交锋回归。

四段影片、两个`all.mp4`合集及清单位于`artifacts/skill_rebuild/ground-scale-films/{native,worldfall}/`，各段保留冻结输入、完整定义、真实路线、世界结果、逐帧时点及正式事件。影片按实际世界时间编码，不能据此推断负载帧率。块状波面和来源动作仍是本项功能画面，最终流派造型及性能继续第9工作包和13.17。


```bash
# 4.34：复用掌击与铁剑，核对真实接触后的局部表面反馈
python3 Scripts/SpellDemos/verify_surface_feedback.py
# 先退出原版独立进程，再核对Worldfall
python3 Scripts/SpellDemos/verify_surface_feedback.py --worldfall
# 只复用已有固定GPU小场景，核对受影响的骨段、表面和合批通道
python3 Scripts/SpellDemos/verify_surface_feedback.py --worldfall --batch
```

行为入口检查实际接触、落空、发力部位随动、世界时间衰减、暂停保持、自然结束、取消及运行中换世界；铁剑实际交锋双方同帧发布。输入、结果与原表面/单件/合批图片位于`artifacts/skill_rebuild/body-surface-feedback/{native,worldfall}/`。`--batch`只执行本次新增资料影响的固定GPU检查，输出到同级`batch/`，不重跑规模、资源生命周期或身体装备矩阵。

```bash
# 4.34集中影片：关闭粒子、拖尾及装饰线条，连续观察掌击与铁剑
python3 Scripts/SpellDemos/record_body_responses.py --native --surface-feedback
# 正常退出原版后，复用原Worldfall安装
python3 Scripts/SpellDemos/record_body_responses.py --surface-feedback
# 只录一段，或只重新编码已经记录的实际帧
python3 Scripts/SpellDemos/record_body_responses.py --surface-feedback --case blocked
python3 Scripts/SpellDemos/record_body_responses.py --surface-feedback --encode-only
```

该开关仅复用`contact`掌击和`blocked`铁剑架挡，输入以无伤触碰避免原版伤害图遮挡表面，真实接触与收势规则保留。逐帧记录反馈位置、强度及装饰启用数，结束后恢复原显示。产物为`artifacts/skill_rebuild/body-surface-films/{native,worldfall}/`中的两段影片、`all.mp4`、`manifest.json`和各段完整输入、正式事件与实际帧。两端均按真实世界时间编码，Worldfall沿已有近景裁切；这是框架功能影片，最终动作美术仍归对应后项。


```bash
# 5.02 A：挂点并集与明确点，共用起始、间隔及最多份数规则
python3 Scripts/SpellDemos/verify_distribution.py
```

复用已有原版独立安装、测试世界、枝体、剑体和同一剑形子结构，不扩展身体装备样本。检查稳定去重及疏密、整棵子树的声明与实例身份、位置方向和实际接触形状、费用随真实展开变化，以及定义往返和非法选择拒绝。随后由同一人物正式施放两种父主体上的相同分布，确认两个子结构分别命中、只支付一次、自然结束后全部清理并再次使用。

输入、原版功能截图、两种完整定义、逐次实际作用与结果保存在`artifacts/skill_rebuild/distribution/collections/`。这只交付5.02 A的集合规则；实际表面、路径及空间生成继续B，修士创作、玩家编辑、正式冷启动和双端集中影片继续C，不能将本入口的定义往返当作完整存档验收。


```bash
# 5.02 B1：只核对实际部位、逐顶点表面绑定及真实子结构作用
python3 Scripts/SpellDemos/verify_distribution.py --surfaces
```

复用同一原版独立安装和现成世界。普通部位来源在每个选中部件的最大实际三角面中心放一份，表面来源按实际面积建立有限候选；都继续共同起始、间隔和数量规则。部件选择支持稳定名称、末尾前缀及`role:`用途，交集按资源顺序去重。通过原发布网格独立核对三个顶点各自的控制、成形、面内位置和法向，实际作用段读取同一变换；原网格转成水层后停止依赖它的分支。

资料在`artifacts/skill_rebuild/distribution/surfaces/`：`checks.json`包含数值与真实运行结果，`facts.json`记录两种已有主体的正式接触及实际生命损失，`sword.definition.json`、`water_whale.definition.json`保留完整输入，同名图片为原版功能画面。正常流程沿同一人物及已有剑体、水鲸分别正式施放，检查一次支付、真实命中、自然结束和再次使用。成形资源仅核对共用数值过程，不扩展身体装备清单。路径及空间来源继续B2，正式冷启动与双端集中影片仍在C。

```bash
# 5.02 B2：只核对曲线路径与有限空间区域
python3 Scripts/SpellDemos/verify_distribution.py --spatial
```

该入口与`--surfaces`互斥，复用同一原版独立安装、现成世界和人物。路径按已有局部三次曲线参数生成位置及切线方向，闭合路径不重复终点；区域按中心、尺寸、旋转及三轴格数生成各格中心，可选方盒或椭球，朝向支持区域前向、中心或背离中心。两者继续共同起始、间隔和最多份数；取点在编译时完成，不增加逐帧曲线选点或区域查询。

输出为`artifacts/skill_rebuild/distribution/spatial/`中的冻结输入、版本、检查及结果、路径/空间两份完整定义和同名原版功能图。检查曲线首尾、父体旋转、真实作用段、闭合边界、区域方向与边界、非法区域和定义往返；实际施放各只支付一次，两个子结构各接触一次，自然结束后由同一人物继续正式使用。真正飞行的曲率预算单独保留，不为静态排布放宽飞行规则。正式冷启动及双端集中影片仍在C。


```bash
# 5.02 C1：固定输入创作、自然请求及真实修改窗口
python3 Scripts/SpellDemos/verify_distribution.py --authoring
```

该入口与其他分布批次互斥，复用原版独立安装和现成世界。只检查第九版创作覆盖六种分布、实际展开预算、固定输入重放、旧规则保持、分布派生和自然修炼请求，以及现有改造窗口的来源选择、非法间隔、撤销、独立学习、正式支付和结束。中英文来源及三轴参数、曲线控制的实机截图同批保存，不重开身体装备清单。

输出在`artifacts/skill_rebuild/distribution/authoring/`：`checks.json`与`result.json`记录真实流程，`pending.json`固定自然等待输入，`natural.json`、`derived.json`、`edited.json`分别为完整结果，PNG为实际窗口。`SynthesisForm.Parts`及旧请求相邻转换已接入；正式世界保存、另一进程恢复与双端连续影片仍分别留在C2、C3，不能把当前请求复制与转换检查算作完整存档交付。


```bash
# 5.02 C2：正式保存后退出，另一进程恢复新旧等待请求与全部分布
python3 Scripts/SpellDemos/verify_distribution_save.py
```

复用现成世界和两个人物，保存六种分布、学习顺序、等待请求及尚在运行的施放；在独立验收文件写入上版请求区段，实际检验相邻转换及原档备份。准备成功后正常退出原进程再重启，恢复完整定义、旧输入与新输入；随后正式使用、自然结束、再次使用及换世界清理。不复测已通过的身体装备矩阵。本批无生产代码改动。

资料在`artifacts/skill_rebuild/distribution/save/`，`prepare-*.json`、`restore-*.json`及同名脚本保留实际两阶段结果，`restart-expected.json`为保存前完整预期，`save/map.wbox`为包含真实旧区段的世界文件。支持`--stage prepare`和`--stage restore`，后者拒绝保存时的同一进程。C2已交付，双端连续影片仍留C3。


```bash
# 5.02 C3：依次在两套独立游戏录制六种来源；切换前正常退出上一套
python3 Scripts/SpellDemos/record_distribution.py
python3 Scripts/SpellDemos/record_distribution.py --worldfall
# 只重编码现有实际帧，不重新运行场景
python3 Scripts/SpellDemos/record_distribution.py --worldfall --encode-only
```

可用`--case anchors points parts surface path space`指定一段或一组；缺省生成完整合集。复用A、B已交付定义、枝体、水鲸和剑形子结构，正式受理后记录实际接触、随动和自然结束。原版读取真实相机，三维读取Worldfall最终纹理，按真实世界时间编码；等待显示不计入运动时长。水鲸、路径和区域保留更宽裁切，裁切资料随同名JSON记录。初次使用须先运行`verify_distribution.py`及`--surfaces`、`--spatial`，生成被复用的完整定义；C2的保存入口另需先运行`--authoring`生成修改副本。

资料在`artifacts/skill_rebuild/distribution/films/{native,worldfall}/`。各来源目录包含完整定义、冻结输入、逐帧时间、实际事件与结果，同名MP4为单段，`all.mp4`为合集，`manifest.json`连接最终交付。两套显示和C2正式恢复均已交付，5.02关闭；这些功能画面不替代后续流派美术及宏大战斗验收。


```bash
# 5.04 A1：有限立体射线、真实厚度、末端随动及正式支付结束
python3 Scripts/SpellDemos/verify_ray.py
```

复用现成世界和同一人物，核对发布网格、胶囊端点与末端子结构使用同一控制，不因长度放大横向半径；更新目标按实际角速度转向，固定输入保持，未启动不提前显现，定时和真实事件启动、停止、回退及无效模型拒绝沿共同入口。实体与冰材料各正式支付一次，线上两人各受伤一次，横向范围外无伤，来源移动仍随动，结束后继续使用。不重开身体装备矩阵。

输出在`artifacts/skill_rebuild/ray/`，包含冻结脚本、版本、实际结果、完整定义、人物生命变化及两张原版功能图。模型`energy_ray`由现有制作工具输出，代码和资源只增加实际伸展能力；当前接触行为为明确穿透，A2继续前方阻挡和截断，B继续创作修改，C继续正式冷启动与双端连续画面，不能将A1算作5.04完整交付。


```bash
# 5.05 B23：火墙、风墙、剑幕的原来源、同形体防御与人物作用
python3 Scripts/SpellDemos/verify_wall_migration.py --section contact-walls
```

复用墙体场景、正式已学入口和支付准备，火墙及剑幕检查实际0.35秒伤害、真实容量损耗后伤害不下降；风墙检查真实水平推力且无伤害。共同覆盖墙外无作用、关闭人物作用仍防御、一个资料失败、一次受理失败退款与结束恢复，不重跑身体装备矩阵。主体正侧和斜向截图随同三种普通及带词条JSON保存在`artifacts/skill_rebuild/trajectories/contact-walls/`，原行为结果与单独补拍记录分别保留。此处是基础来源交付，最终流派美术、完整编辑及创作、正式冷启动和集中录像仍归对应后项。


```bash
# 5.05 B24：五种原护盾的随身壳面、真实防御及来源失效结束
python3 Scripts/SpellDemos/verify_wall_migration.py --section shields
```

复用墙体场景、人物和正式学习及支付入口，各来源只核对自身普通/强化资料、实际已学自用受理、一次石锥拦截及无隐藏人物伤害。随位置和真实腾起高度跟随、同来源替换、自然结束、一次受理失败退款和来源实际死亡结束共用灵盾验证，不按身体或装备重跑。完整输入、结果、五种普通及强化JSON和主体图在`artifacts/skill_rebuild/trajectories/shields/`。开发构建、NML实机编译及Linux原版运行通过；三平台仅资源构建，最终美术、Worldfall、完整编辑与自主创作、正式冷启动和集中录像仍归对应后项。


```bash
# 5.05 B25：原水幕的液态厚面防御、正式来源及结束恢复
python3 Scripts/SpellDemos/verify_wall_migration.py --section water-curtain
```

该入口与`--section solid-walls`共用`verify_passive_walls.script`，分别传入本批实际来源及预期材料，不重复搭建场景。水幕核对液态资料、普通及强化已学来源、逐发支付、真实石锥拦截和容量、墙外通过、同来源替换、一个失败退款及结束恢复；未映射附加作用仍留原入口。完整输入、结果、普通及强化JSON和主体图写入`artifacts/skill_rebuild/trajectories/water-curtain/`，不覆盖既有墙体证据。完整编辑、创作、正式冷启动及集中连续录像继续5.05 C，最终水法美术及Worldfall继续对应交付点。


```bash
# 5.05 B26：七种原贯穿投射的真实贯穿、伤害与逐发支付
python3 Scripts/SpellDemos/verify_piercing.py
```

复用飞剑与墙体的独立游戏、世界、人物和拍摄入口，各来源只核对自身普通/强化资料、独立主体、穿过两名敌人且每人一次、原0.85伤害倍率及一次收费。强化合击和齐射后的两发支付、真正出手后改目标位置的追踪、实际受理失败退款及恢复走共用金针检查；原飞剑只做一次正常命中对照。完整输入、普通及强化JSON、结果和七张主体图在`artifacts/skill_rebuild/trajectories/piercing/`。本体交锋量与人物伤害倍率分离，未映射作用继续原来源。正式保存冷启动、完整编辑、自主创作及集中录像继续5.05 C。加`--section common`只重验共享段，结果单独保存到`common/`；初次来源通过与后来共享段的分段交付依据见`delivery.json`，保留初次失败记录。


```bash
# 5.05 B27：七种普通投射的默认轨迹、真实首击结束、费用和土材料
python3 Scripts/SpellDemos/verify_projectiles.py
```

七种原已学来源分别检查自身普通/强化资料、实际主体、默认追踪/直射/抛弧、首次100伤害后结束及一次支付。强化连发、受灵材料修正影响的一次原灵光束对照、一次直射改追踪和受理失败退款恢复用共享段验证，不重新跑飞剑所有轨迹。另仅核对地刺与土墙的土材料转换及一次地刺真实土系伤害。加`--section sources`、`--section common`或`--section soil`分别只运行对应段，结果独立写入`artifacts/skill_rebuild/trajectories/projectiles/<分段>/`；不带参数写入该目录根。完整编辑、自主创作、正式冷启动和集中连续录像继续5.05 C。

`--section polarity-visuals`仅补拍阴弹、阳弹的模型朝向，不重跑已通过的伤害、词条或费用流程；结果和两张图保存在`projectiles/polarity-visuals/`，原行为证据保留在根目录。


## 五种原刃波的接入验证

`python3 Scripts/SpellDemos/verify_blades.py`复用现成世界和原已学入口，验证剑气、风刃、水刃、火刃、阴刃的独立主体、真实前后扫敌、每人一次、0.9人物倍率与本体量分离、逐发费用和结束。共用部分只验一次强化两发、改追踪、失败退款与恢复，以及受影响的原飞剑对照。

`--section sources`只运行五种来源，`--section common`只运行共用部分，`--section visuals`只补拍五种主体及结束，不重复整套伤害检查。输入、转换资料、实际发射、主体图及结果写入`artifacts/skill_rebuild/trajectories/blades/`，分段运行写入同名子目录。完整连续录像仍集中到5.05 C，不以本入口替代。


```bash
# 5.05 B29：投射与坠落在真实结束位置交接余力爆发
python3 Scripts/SpellDemos/verify_impacts.py
```

八种原已学来源分别验证自身普通/强化资料、飞行与爆发主体、实际接触后范围伤害、一次费用及结束清理；本体强度与人物倍率分开，首次接触者仍可接受一次爆发。共享部分只运行强化两发、出手后追踪移动目标、部分交锋后的余力、寿命耗尽、取消、受理失败退款恢复以及默认接触组规则。编辑器仅验证本次新增的新接触组选项、关闭继承时清理、撤销及保存重开，并保留中英界面图。

加`--section sources`、`--section common`、`--section lifecycle`、`--section ui`分别只运行对应段，`--section visuals`只补拍飞行和爆发主体。证据在`artifacts/skill_rebuild/trajectories/impacts/`，分段运行写入同名子目录；失败原始输入另留，不用后一次通过覆盖失败事实。正式存档冷启动、完整编辑、自主创作和集中连续录像继续5.05 C。


```bash
# 5.05 B30：土脊、潮浪和灵藤的原来源、真实贴地及越过目标扫敌
python3 Scripts/SpellDemos/verify_ground_waves.py
```

复用现有独立游戏、世界和人物。三种来源分别核对普通/强化资料、实际材料、前后动作、显示与接触的同一姿态、实际底部贴地、前后和偏轴敌人每人一次、侧外安全及一次费用。共享部分只检查强化费用与原地面波单发预算、出手后追踪移动目标、关闭人物作用，以及一个受理失败退款和恢复。加`--section sources`、`--section common`或`--section visuals`只运行对应段；证据独立写入`artifacts/skill_rebuild/trajectories/ground-waves/`及其分段子目录。最终质感继续9.02；正式冷启动、完整编辑创作和集中连续录像继续5.05 C。

B30的后方与侧外人物按原地面摇摆的真实离开方向放置，分别位于前方2格/偏轴0.35格、前方2格/偏轴3.5格；不假定摇摆后仍沿原始直线前进。`common`同时覆盖共同直达后继续的无效混用拒绝及现有中英字段开关。最终证据索引为`artifacts/skill_rebuild/trajectories/ground-waves/delivery.json`。


```bash
# 5.05 B31：龙卷与玄阴的原领域、间隔伤害与独立本体量
python3 Scripts/SpellDemos/verify_remaining_fields.py
```

复用既有独立游戏、世界、人物和拍摄入口，两种来源分别核对普通/强化资料、原已学受理、各自形体、真实推进或固定、同一姿态的显示与接触、原间隔伤害、侧外安全、逐发费用及结束清理。共享段只核对一个既有毒潭、强化龙卷、玄阴改跟随、关闭人物作用及一次受理失败退款恢复，不扩充身体装备样本。

加`--section sources/common/visuals`可分别只验证来源、共用路径或补拍主体。输入、实际发射、资料、前后主体图及结果保存到`artifacts/skill_rebuild/trajectories/remaining-fields/`，分段结果保存到同名子目录；失败输入与结果保留。完整编辑、自主创作、正式保存冷启动及集中连续录像继续5.05 C，最终流派质感继续9.02。

9.02 D 用 `dev.py exec --timeout 245 -- python3 Scripts/SpellDemos/verify_remaining_fields.py --section appearance` 在独立游戏对龙卷和玄阴拍近、中、远景，并核对实际边内命中、紧邻外侧无接触、原费用与自然结束。可加 `--form wind` 或 `--form yin` 只补对应一术，导出的原始结果位于独立环境的 `output/fields-appearance-*`。


```bash
# 5.05 B32：减速、冰冻、硬直随新主体真实接触
python3 Scripts/SpellDemos/verify_contact_statuses.py
```

复用现有独立游戏、世界和人物。资料段核对七条攻击转换及爆发作用面的声明保存、倍率和回调映射；实机只用一个灵弹核对三个词条的真实命中、叠加/刷新、原费用、抗性时长及到期恢复，再用一个玄阴领域核对间隔接触。关闭人物作用及一次真实受理失败退款恢复走共同路径，不扩大身体、装备和全部来源矩阵。

`--section definitions/runtime`可只运行资料段或实机段。完整输入、来源定义、实际发射和结果写入`artifacts/skill_rebuild/trajectories/contact-statuses/`，分段写入同名子目录。完整修改表单、自主创作、正式存档冷启动及集中连续录像继续5.05 C。


```bash
# 5.05 B33：虚弱、破甲、禁言随真实接触按目标攻防计算
python3 Scripts/SpellDemos/verify_contact_reductions.py
```

复用既有独立游戏、世界和人物。资料段快速核对七条攻击转换、爆发及零伤害推力面的保存编译；实机只用一个已学灵弹核对三状态的真实削弱、禁止施法、再次接触刷新、侧外安全、原费用、抗性及到期恢复，一个玄阴领域核对间隔刷新，并走一次边界失败退款恢复。不扩大身体装备及全部来源实机矩阵。

`--section definitions/runtime`可单跑受影响段。输入、来源定义、实际发射及结果保存到`artifacts/skill_rebuild/trajectories/contact-reductions/`，分段存入同名子目录。完整编辑、创作、正式存档冷启动和集中录像继续5.05 C。


```bash
# 5.05 B34：燃烧、毒伤、同源限层及真实周期掉血
python3 Scripts/SpellDemos/verify_contact_ticks.py
```

复用现有世界和人物。资料段检查七条攻击转换、爆发和零伤害推力面，核对效果及人物倍率只换算一次。实机只用一个灵弹验证周期掉血、燃烧刷新、同源毒层上限、不同来源分别计数、原费用及到期停伤，再用一个领域验证间隔接触。收尾检查失败退款恢复、抗性时长，并定量消耗本体后核对每秒量随余力变化，不扩大身体装备矩阵。

`--section definitions/runtime/recovery`可只跑受影响段；`recovery`含边界失败恢复、抗性和余力。证据存入`artifacts/skill_rebuild/trajectories/contact-ticks/`及同名分段目录。完整编辑、创作、正式冷启动与集中录像继续5.05 C。


```bash
# 5.05 B35：永恒诅咒的周期与属性组合、刷新及自然恢复
python3 Scripts/SpellDemos/verify_compound_curse.py
```

沿用独立游戏、既有世界和人物。资料段快速核对七条攻击转换、爆发和零伤害推力面的声明，原九种接触状态一起保存编译，以及十六项的有限上限。实机只用一个已学灵弹检查周期与削弱同时生效、切换单类作用时清除旧值、原费用、侧外安全和自然恢复；同一人物的真实零移动倍率与普通组合只核对周期不被吞掉。一个领域验证间隔刷新，共用一次边界失败退款恢复及抗性时长，不扩大身体装备矩阵。

`--section definitions/runtime/edge/common`可只跑受影响段。输入、来源定义、实际发射及结果存入`artifacts/skill_rebuild/trajectories/compound-curse/`及相应分段目录。完整编辑、创作、正式冷启动和集中录像继续5.05 C。


```bash
# 5.05 B36：慈悲的本体减强、命中自愈、余力及失败恢复
python3 Scripts/SpellDemos/verify_contact_mercy.py
```

`--section definitions/runtime/edge/common/carrier`分别检查资料、正常命中、关闭接触和零值、余力与领域及退款恢复、合法命魂的共享恢复服务。复用既有世界、人物和调试通道，不展开全部来源、身体和装备矩阵。恢复观察只记录实际服务输入及同次生命变化，不把人物自身回血算作法术治疗。命魂段通过正式神游建立载体，只验证共享服务恢复完整度且不治疗原主，不冒充原主与载体分离的旧施法已转换。

输入、来源定义、发射及实际恢复记录位于`artifacts/skill_rebuild/trajectories/contact-mercy/`；交付以`delivery.json`引用的已通过正常前段及分段结果为准，初次脚本编译错误和人物自身回血干扰的记录分别保存在`initial-script-compile/`及`initial-background-heal/`。完整表单、创作、正式冷启动及集中录像继续5.05 C。


```bash
# 5.05 B37：混沌逐发强度、初始偏角与速度波动
python3 Scripts/SpellDemos/verify_chaos_setup.py
```

复用现有世界、人物和独立游戏。资料段核对七条攻击转换、保存及重复编译不重抽、慈悲组合和原强度/速度下限；实机只用一个已学投射来源检查各发独立、本体与初始运动、真实命中、费用及结束，再用一个推进领域检查实际三维运动，并走一次边界失败退款及恢复。实际发数读取原人物预算，不把连发词条数值当作固定发数，不扩大身体装备或全部轨迹矩阵。

`--section definitions/runtime/common`可只运行相应段。输入、检查、展开资料、真实发射和结果保存在`artifacts/skill_rebuild/trajectories/chaos-setup/`及对应分段目录，最终依据见`delivery.json`；失败记录保留，已有通过段不重复执行。完整编辑、自主创作、正式冷启动及集中连续录像继续5.05 C，最终美术继续9.02。


```bash
# 5.05 B38：接触击退距离、腾起高度及共同落地恢复
python3 Scripts/SpellDemos/verify_contact_knockback.py
```

复用现有世界、人物和独立游戏。资料段检查七条转换、倍率、保存编译、爆发时点、纯防御面、已有推力与状态及治疗的共同保留、独立复制和非法参数。实机只用一个已学灵弹测量世界速度、实际最高点、落地距离、原费用及来源结束后的恢复；共同段检查零高度、纯竖直、零幅度和一次边界失败退款及恢复，不扩展身体装备矩阵。普通正高度参数按共同世界运动计算，避免旧力度单位重复换算造成实际高度严重偏大。

`--section definitions/runtime/common`可只运行对应段。输入、发射、实际受力和结果保存到`artifacts/skill_rebuild/trajectories/contact-knockback/`及对应分段目录；最终依据见`delivery.json`。首次失败与旧公式证据保留，最终编译构造器的非法派生速度检查单独记录，不重复已通过的运动。完整字段表单、创作、正式冷启动与集中连续录像继续5.05 C，最终美术继续9.02。


```bash
# 5.05 B39：持续范围牵引、世界时间及结束恢复
python3 Scripts/SpellDemos/verify_radial_motion.py
```

`--section definitions/runtime/common`分别运行资料、实际范围与移动中心、零值及失败退款恢复。资料段合并核对七条攻击转换的保存编译、范围与一次强度倍率、独立复制及未知回调；实机复用现成世界，只用一个固定领域和一个移动领域检查真实沿地牵引、距离衰减、范围内外、敌我及离地/不能移动边界、原费用、固定世界整点、暂停/倍速、取消和自然结束。共同段走一次真实拒绝退款及恢复，不重复身体装备或全部来源实机。

输入、展开资料、发射、实际世界速度、共同时间及结果保存在`artifacts/skill_rebuild/trajectories/radial-motion/`及相应分段目录；最终依据见`delivery.json`。失败证据保留，通过段不重复执行。新字段完整表单、自主创作、正式冷启动及集中连续录像继续5.05 C，范围提示与最终美术继续对应后项。


```bash
# 5.05 B40：真实接触换位、身体控制归还及同次击退
python3 Scripts/SpellDemos/verify_contact_exchange.py
```

`--section definitions/runtime/recovery`分别运行资料、已学灵弹正常换位及零概率/离地/真实掌击归还。`definitions-zero`、`recovery-edge`、`recovery-body`仅补对应未通过段；不重复已通过的前段。入口复用既有世界和人物，核对连续坐标、原费用、精确位置保存标记、旧身体释放、换位先于同次击退、中间空隙不形成攻击路径，以及旧动作寿命结束后的控制归还。准备时清除退出手动控制自带的随机弹飞，不修改换位地面限制；真实腾起用于离地拒绝，掌击使用合法的近身地点选择。

完整输入、展开资料、实际接触限制、双方放置坐标及控制归还记录在`artifacts/skill_rebuild/trajectories/contact-exchange/`；最终依据见`delivery.json`。失败记录保留，仅采用索引明确指出的通过前段和补齐结果。完整编辑、自主创作、正式存档冷启动与集中连续录像继续5.05 C；最终美术继续对应后项。


```bash
# 5.05 B41：追加伤害、随机接触候选与原燃尽
python3 Scripts/SpellDemos/verify_additional_contact.py
```

`--section definitions/runtime/recovery`分别运行资料转换与保存编译、一个实际已学灵弹的真实随机选择和燃尽、零概率零作用及首份追加伤害致死后停止。观察只记录实际选择与共同伤害，不替换随机数或结算。使用已选中的实际候选验证对应作用，不遍历身体装备或重复已有状态受力矩阵。原逐发费用、伤害归属、侧外安全和结束后停效在同一场景确认。

完整输入、展开资料、实际选择、伤害及结果保存在`artifacts/skill_rebuild/trajectories/additional-contact/`及对应分段目录；最终依据见`delivery.json`。新字段完整表单、自主创作、正式冷启动与集中连续录像继续5.05 C。


```bash
# 5.05 B42：生命阈值、实际击杀返生与未杀反噬
python3 Scripts/SpellDemos/verify_conditional_contact.py
```

`--section definitions/sentence/trial/common`分别运行保存编译与来源转换、同一已学终焉灵弹的高低生命分支、同一已学轮回灵弹在真实减伤后未杀反噬及实际击杀返生、零量与结束停效。观察仅记录标准伤害输入及结算前后生命，不改变伤害或分支；减伤通过现有护甲状态提供。最后一次致死后不继续复用该死亡目标准备下一项。资料入口不采样世界，不重复身体装备及费用退款矩阵。

输入、展开资料、伤害归属、真实生命变化和结果保存在`artifacts/skill_rebuild/trajectories/conditional-contact/`及对应分段目录；最终依据见`delivery.json`。完整编辑、自主创作、正式冷启动和集中连续录像继续5.05 C。

`--section sentence-edge`仅补低生命执行和死亡收尾；原版死亡标记可晚于生命归零，入口在真实世界继续推进后确认死亡，不改变伤害规则。首次高生命前段与失败低生命观察保留于`initial-death-flag/`，交付索引只采用其中已通过的高生命前段。

```bash
# 5.05 B43：真实命中后的球形范围分送与原爆炸转换
python3 Scripts/SpellDemos/verify_contact_spread.py
# 只复现本批的一段，已有通过段无须重复
python3 Scripts/SpellDemos/verify_contact_spread.py --section definitions
python3 Scripts/SpellDemos/verify_contact_spread.py --section runtime
python3 Scripts/SpellDemos/verify_contact_spread.py --section common
```

`definitions`核对七条攻击转换、爆发后段、纯防御面、原范围与一次倍率、普通结果隔离、保存编译、非法资料及资源重载版本。`runtime`复用已学灵弹，核对主伤害致死后仍分送、真实近邻、范围外/友方/高空边界、建筑去重及共同球形显示。`common`只覆盖普通间隔的重复分送、取消停发、反馈到期与换世界清理。证据位于`artifacts/skill_rebuild/trajectories/contact-spread/`；完整编辑、自主创作、正式冷启动及集中录像继续5.05 C。

若仅需补建筑及显示尾段，可用`--section runtime-edge`；交付索引保留原已通过人物段，不要求重跑共用规则。

```bash
# 5.05 B44：指定性质的实际材料、显示和已学来源
python3 Scripts/SpellDemos/verify_nature_selection.py
python3 Scripts/SpellDemos/verify_nature_selection.py --section definitions
python3 Scripts/SpellDemos/verify_nature_selection.py --section runtime
```

资料段核对十二种性质及八条转换分支：明确材料、物态、实际表面、保存编译、形状轨迹、一次强度倍率和独立附加元素；同材料保留原物态，不扩大原资产的性质限制。实机段复用一枚已学水性质灵弹，核对真实水伤害、表面、原费用、侧外安全、关闭接触、取消停效和同来源恢复，不扩身体装备样本。指定性质优先于本次动态元素；可导电水域及无指定性质的动态覆盖继续原入口，留给后续独立迁移。

输入、展开资料、实际伤害与画面保存在`artifacts/skill_rebuild/trajectories/nature-selection/`及相应分段目录；最终依据见`delivery.json`。完整编辑、自主创作、正式冷启动及集中连续录像继续5.05 C。


## 自主飞行创作与新旧请求保存

`python3 Scripts/SpellDemos/verify_flight_creation.py`复用独立安装、现成世界及人物辅助，检查规则11的普通曲线和目标弧线、真实距离触发返回、命名步骤派生、固定输入、新旧请求正式升级及存读、实际收费使用与成品保存。主流程只选择当前人物能承担费用的曲线结果，不修改收费或资源容量规则；上一正式规则10的实际输入与既有预期保存在`fixtures/flight-creation-rule10.json`，读取旧预期时使用普通资料缺省字段。

`--section cast`只补一份可支付的实际创作、正式施放、退出及成品保存；`--section flow`只核对原操作／命名步骤的真实水流排除和其他普通部位继续可选。初次交付的分段补验还支持`tail`、`requests`与`save`，分别跳过已经通过的运动前段、全部运动段、运动及命名派生段；运行范围以实际输入及`checks.json`为准，不把这些结果视为一次全段成功。

证据索引为`artifacts/skill_rebuild/trajectories/flight-creation/delivery.json`。若系统硬件图形上下文不可用，本机可用`env __GLX_VENDOR_LIBRARY_NAME=mesa LIBGL_ALWAYS_SOFTWARE=1`作为命令前缀完成定向功能验证，不能据此验收性能或最终美术。统一演示批量输入、其余作用字段、正式冷启动及集中连续录像继续5.05 C，不扩大身体装备样本。

## 5.05 C3：目录遍历与单份构造演示

`--construction`现在接受单个构造JSON或一个目录。目录递归读取全部`*.json`，按相对文件路径排序；每份必须是独立的`AbilityDefinition`对象，不能混入清单或结果文件。文件缺少编号、编号重复、目录为空时，在发送游戏请求之前拒绝。完整的形体、作用和资源规则仍由正式编译器检查。

```bash
# 列出目录，不连接游戏
python3 Scripts/SpellDemos/demo_all.py --construction Scripts/SpellDemos/fixtures/construction-batch --list

# 逐份正式学习、施放、录制，生成各段录像与合并总览
python3 Scripts/SpellDemos/demo_all.py --construction /绝对路径/构造目录 --output /绝对路径/录像目录

# 在目录中指定一份；支持中文名、完整编号或编号末段
python3 Scripts/SpellDemos/demo_spell.py 批量曲线 --construction Scripts/SpellDemos/fixtures/construction-batch --target-offset 8 0 --output /绝对路径/单份录像

# 单文件入口继续可用
python3 Scripts/SpellDemos/demo_spell.py --construction /绝对路径/法术.json --output /绝对路径/单份录像
```

目录遍历支持`--filter`与`--resume`；续录比较每份资料的完整内容，修改输入须用新目录或重新录制。每段的`definition.json`、`result.json`和`demo.mp4`分别保存输入、正式施放事实和录像，批次的`manifest.json`包含完整选择与参数。构造的变化写入各份资料，旧法术的词条、轨迹和动画变体参数不能用于构造演示。构造仍需适合选定目标及场景；需要环境条件的招式应准备对应条件，不能把施放被拒绝或没有实际作用当作通过。

每份构造开始时载入同一个现成测试世界，避免人物职业、冷却、持续材料与前一招串场；结束、取消或失败后再次恢复夹具，清除演示人物与临时构造，归还控制并恢复界面。`status.json`的`sceneRestored`标明这一步是否实际完成。失败时保留当前输入、已完成片段与错误，确认收尾后再运行下一批，不需要重启游戏。

定向验证入口是`python3 Scripts/SpellDemos/verify_construction_batch.py`：只使用两份固定的普通资料，覆盖斗气直行→真元曲线的遍历、名称指定、一次非法模型拒绝与后续恢复，以及相同输入续录。使用独立安装、现成世界和同一进程；图形环境不可用时，可加`env __GLX_VENDOR_LIBRARY_NAME=mesa LIBGL_ALWAYS_SOFTWARE=1`完成此功能验证。证据在`artifacts/skill_rebuild/trajectories/construction-batch/`。这些是演示工具的功能录像，流派最终美术与性能不在本批验收范围。

## 5.05 C4：接触状态、恢复和伤害性质的编辑与创作

```bash
python3 Scripts/SpellDemos/verify_contact_creation.py
# 只补受影响的一段，已通过段不重跑
python3 Scripts/SpellDemos/verify_contact_creation.py --section ui
python3 Scripts/SpellDemos/verify_contact_creation.py --section cast
python3 Scripts/SpellDemos/verify_contact_creation.py --section creation
python3 Scripts/SpellDemos/verify_contact_creation.py --section derivation
python3 Scripts/SpellDemos/verify_contact_creation.py --section save
python3 Scripts/SpellDemos/verify_contact_creation.py --section capture
```

入口复用独立安装、现成世界及普通飞行部位，不建立身体或装备矩阵。`ui`通过真实窗口修改整招及部位的目标限制、伤害依据和元素、恢复及净化、状态合并和层数、固定属性、目标属性比例和周期伤害；检查非法输入、撤销、正式保存再打开；`cast`只复用已保存结果检查实际支付、命中、作用和结束恢复。`creation`以有限固定输入检查第十二版独立创作及原术派生，保留原运动、命名和引用，新生成费用明确包含状态及恢复。`save`复用创作段的实际原术和种子，检查正式第十三版人物区段、新旧等待请求存读、各授予一次、真实使用和成品保存。

`fixtures/contact-generation-rule11.json`来自此前C2的真实正式存档及其既有结果，保留完整第十二版区段和规则11的原预期，验证时不重新制造旧期望。证据写入`artifacts/skill_rebuild/trajectories/contact-creation/`，最终以`delivery.json`列出的实际通过段为准。图形环境不可用时，本机可加`env __GLX_VENDOR_LIBRARY_NAME=mesa LIBGL_ALWAYS_SOFTWARE=1`执行功能验证；最终美术、性能、正式冷启动和集中连续录像仍在对应交付点执行。

C4补段中，`derivation`只复用已经选定的种子14补接触派生，不重跑独立创作；`capture`只补已保存资料的中英状态名称画面。首次其他派生产生非法命名引用的拒绝如实保留并归后续C处理；测试不会放宽引用规则。

## 5.05 C5：接触击退和换位的编辑与创作

```bash
python3 Scripts/SpellDemos/verify_contact_displacement.py
# 已通过部分不用重跑，直接补受影响的一段
python3 Scripts/SpellDemos/verify_contact_displacement.py --section ui
python3 Scripts/SpellDemos/verify_contact_displacement.py --section cast
python3 Scripts/SpellDemos/verify_contact_displacement.py --section creation
python3 Scripts/SpellDemos/verify_contact_displacement.py --section save
```

`ui`沿真实作用页修改击退距离、高度、换位概率及停步，验证非法值拒绝、撤销、部位独立覆盖、正式保存再打开，并留下中英截图。`cast`直接施放窗口保存的成品，只观察正式换位及击退调用、实际支付命中和结束后的再次施放；不替换受力、概率或控制规则。`creation`检查第十三版独立生成、保留命名引用的接触位移派生及对应需求。`save`复用创作段的实际原术和种子，验证新旧等待请求正式世界存读、分别完成一次及成品使用保存。

`fixtures/contact-displacement-rule12.json`截取自C4真实正式人物区段13及当时固定的预期，保留规则12的完整输入和原费用。旧期望不重新生成。证据位于`artifacts/skill_rebuild/trajectories/contact-displacement/`；重新执行失败段前会将原输入、结果及截图保留到`failures/`。入口复用独立安装和现成世界，不扩大身体装备样本。图形环境不可用时可加前述软件渲染环境变量完成本批功能验证；最终美术、性能及集中录像仍归对应交付点。

## 5.05 C6：附加伤害、随机与条件接触结果

```bash
python3 Scripts/SpellDemos/verify_contact_results.py
python3 Scripts/SpellDemos/verify_contact_results.py --section ui
python3 Scripts/SpellDemos/verify_contact_results.py --section cast
python3 Scripts/SpellDemos/verify_contact_results.py --section creation
python3 Scripts/SpellDemos/verify_contact_results.py --section save
```

窗口按“普通作用与附加伤害／随机接触结果／条件接触结果”展示，结果中的状态、恢复、伤害和击退使用同一套普通参数。`ui`从真实窗口操作，分别经正式入口保存直接、随机、条件成品及完整嵌套资料，验证非法值、撤销、增删、部位独立覆盖和重新打开，并留中英截图。`cast`直接使用这些成品，观察标准受击、恢复及真实候选选择；检查生命比例伤害、候选配套结果、生命条件两分支，以及伤害真正致死或存活后的对应作用和结束恢复。

`creation`以固定有限输入检查规则14的附加伤害、随机与条件生成及接触结果派生，完整运动、命名和引用继续保留。需求使用真实接触组和重复预算；候选与互斥分支按较大结果计，不将候选数量当作多次命中，整数资源仍按真实取整。`save`检查正式人物区段15、新旧等待请求正式世界存读、各完成一次，以及新成品使用和保存。

旧样例`fixtures/contact-results-rule13.json`直接截取C5正式区段14和当时的完整预期，验证旧规则13结果及费用未改。证据保存在`artifacts/skill_rebuild/trajectories/contact-results/`，重跑失败段前自动保留旧输入和结果。入口复用现成世界、同一个普通部位和独立安装，不扩大身体装备矩阵；软件渲染只用于功能和窗口验证。正式冷启动、集中录像及最终美术与性能仍归对应交付点。

C6实际验收采用分段续接，没有重跑已经通过的部分：`ui-tail`从直接伤害成品继续随机及条件编辑；`cast-tail`从随机开始，`cast-conditions`从生命条件开始，`cast-kill`从真实击杀开始并核对再次施放。原始失败、各前段通过项及剩余补段由`delivery.json`明确列出，不将前段通过改写成整段成功。施放准备先重新接管再归位，避开原版解除操控施加的随机推力；每次记录真实位置及形状，漏命中时保留现场。

## 5.05 C7：范围与介质分送的编辑及自主创作

```bash
python3 Scripts/SpellDemos/verify_contact_spreads.py
python3 Scripts/SpellDemos/verify_contact_spreads.py --section ui
python3 Scripts/SpellDemos/verify_contact_spreads.py --section cast
python3 Scripts/SpellDemos/verify_contact_spreads.py --section creation
python3 Scripts/SpellDemos/verify_contact_spreads.py --section save
```

同一接触页新增“范围与介质分送”，复用普通伤害、状态、恢复与击退结果。`ui`从真实窗口编辑球形半径、建筑比例和显示时间，再编辑介质通道、实际材料物态、接收者上限、同通道一次、继承原作用及连接反馈，检查非法值不能保存、撤销、部位深拷贝、正式替换和再打开，并留下中英截图。部位与性质页可声明实际介质通道。

`cast`使用窗口正式保存的成品，核对球形范围内外及每人一份结果，再复用既有薄水域验证真实介质连通、两人上限、结束清理及移除介质后再次施放。原始火弹与水体会实际交锋并消耗水体，保留其失败输入和观测；用于介质分送的成品经真实窗口改为雷法，不修改碰撞或材料规则。`ui-tail`只补中英文和正式保存，`ui-medium`可从已存成品通过真实表单调整材料，`cast-tail`只补未完成介质段。完整输入、分段结果、原始失败及输入快照位于`artifacts/skill_rebuild/trajectories/contact-spreads/`。

`creation`验证规则15独立创作与接触分送派生、冻结输入重现、运动及命名引用保留、新费用及范围/重复次数预算；`save`验证正式人物区段16、新旧等待请求各完成一次、真实收费施放及成品再次存读。旧样例`fixtures/contact-spreads-rule14.json`取自C6正式世界的区段15与当时固定预期；薄水域`fixtures/contact-spreads-medium.json`复用B45已通过资料，只替换测试编号与通道。不重跑已通过的身体装备或介质形状矩阵，正式冷启动和连续录像仍集中在C交付点。


### 5.05 C8：放置改造的命名运动引用

`python3 Scripts/SpellDemos/verify_placement_references.py --section behavior`复用固定世界，检查随身、直接/两点放置和环绕在移除根部飞行或脱离时保留后续命名步骤与时点，验证明确不能转换的完成依赖，以及正式付费、实际随动旋转和结束恢复。`--section save`承接behavior保存的原术及选中种子，验证新规则16/人物区段17与C7正式规则15/区段16请求的迁移、各完成一次和成品再次保存；旧样例在`fixtures/placement-references-rule15.json`。

`all`按顺序只执行behavior和save。首轮已通过部分不重跑：`recovery`仅续接behavior结束后的同一人物，在实际冷却到期后再次施放；`save-tail`仅续接save完成请求后的同一成品，补退出与再次保存。两个续接入口需要对应前段及同一游戏进程，正常整套运行无需调用。原始失败及完整输入保留于证据目录，索引为`artifacts/skill_rebuild/trajectories/placement-references/delivery.json`。软件渲染只用于功能验证。


### 5.05 C9：世界接触作用编辑与创作

`python3 Scripts/SpellDemos/verify_world_contact_creation.py --section ui|cast|creation|save|all`。共用既有窗口、世界和接地形体，覆盖地块操作的真实表单、范围和次数、独立创作及派生、新规则17与正式区段18存读。旧样例`fixtures/world-contact-creation-rule16.json`直接提取C8正式等待存档并带来源校验值，不用当前生成结果重造旧预期。

`placement|cast-tail`只用于同一进程中保留失败后的续接；正常all不加入这些段。C9首次演示的沿地表剑形体以模型最低处贴地，刃面实际悬空；placement经真实运动表单改明确高度，cast-tail补地块范围和结束恢复。原失败和观察记录保留，窗口、自主创作及存档已通过部分没有重跑。交付索引为`artifacts/skill_rebuild/trajectories/world-contact-creation/delivery.json`；本批实际为分段通过，不声称一次all整体通过。中英窗口截图在ui，真实作用结果在cast-tail，正式新旧请求及成品存档在save。软件渲染只验证功能和窗口。


### 5.05 C10：人物接触频率、共用组和命中收势

`python3 Scripts/SpellDemos/verify_actor_contact_creation.py --section ui|cast|creation|save|all`。复用既有窗口、世界和共享剑形体，验证身体/部位接触规则、同组同步、非法候选拒绝、撤销和保存再打开，中英窗口、真实共享次数及间隔、实际伤害、结束后再次施放，以及规则18创作与正式人物区段19存读。旧样例`fixtures/actor-contact-creation-rule17.json`直接来自C9正式等待存档和当时完整预期，保留来源校验值。

`--section cast-tail`只在同一人物的原施放段失败后补伤害及恢复，不加入正常all。交付索引`artifacts/skill_rebuild/trajectories/actor-contact-creation/delivery.json`保留根路径缺陷、日志记录、初次伤害和续接脚本的原失败，以及分别通过的结果。本次实际交付为分段通过，不声称一次all整体通过。截图位于ui，初次已通过的组次数在cast，伤害与恢复在cast-tail，正式新旧存档在save。正式冷启动与集中录像仍在5.05 C交付点完成，软件渲染仅用于功能及窗口。


### 5.05 C11：范围推拉编辑与自主创作

`python3 Scripts/SpellDemos/verify_radial_motion_creation.py --section ui|cast|creation|save|all`。复用既有窗口、世界和一个剑形部位，验证世界半径、推拉速度、重新选人间隔及人物关系的真实表单，零半径拒绝、撤销和保存再打开，中英窗口、范围内外实际移动、结束归还及再次施放，规则19独立创作与派生、正式人物区段20新旧请求存读。旧样例`fixtures/radial-motion-creation-rule18.json`直接来自C10正式等待存档与当时完整预期，保留来源校验值。

证据索引为`artifacts/skill_rebuild/trajectories/radial-motion-creation/delivery.json`。本批窗口、施放、创作、存读四段在同一进程分别通过，没有执行一次完整all；没有原始失败。中英窗口图位于ui，实际位移在cast和save的motion.json，正式等待及完成存档在save。正式冷启动与集中录像仍在C交付点完成，软件渲染仅用于功能和窗口。


### 5.05 C12：交锋用途及屏障规则编辑与自主创作

`python3 Scripts/SpellDemos/verify_clash_creation.py --section ui|cast|creation|save|all`。复用现成世界和剑形主体，覆盖攻击/屏障/装饰用途、吸收/反射、地面人物阻挡与反射次数的真实表单、非法候选拒绝、撤销和保存再打开，中英窗口、表单成品真实反射及归属交接、到期退出及再次施放，规则20独立创作与派生、正式人物区段21新旧请求存读。旧样例`fixtures/clash-creation-rule19.json`来自C11正式等待存档和当时完整预期，保留来源校验值。

`--section save-tail`只续接保存段已完成请求及实际施放后的同一人物，不重建世界，不加入正常all。证据索引`artifacts/skill_rebuild/trajectories/clash-creation/delivery.json`保留首次投射方向、内部计划读取和续接脚本的原失败，明确分段通过。中英图在ui，反射记录在cast，正式等待存档及存读前段在save，实际成品及完成存档在save-tail。已有未受影响的身体、装备及反射角度矩阵没有重跑；正式冷启动及集中录像仍归C交付点，软件渲染仅用于功能及窗口。


### 5.05 C13：连接、来源存续和同招并存的编辑与自主创作

`python3 Scripts/SpellDemos/verify_persistence_creation.py --section ui|binding|instances|survival|creation|save|all`。`ui`操作真实窗口并保存两份连接成品、中英截图，检查非法比例和并存值、完整候选拒绝、撤销、覆盖和再打开；`binding`使用表单成品检查真实两点位置、新位置有伤而转移空隙无伤，以及仍依赖来源的跟随结束；`instances`检查最老同源替换、其他来源保持、逐次支付、到期及再次施放；`survival`检查固定独立主体在来源真实死亡后继续、原归属及自然结束。上述共用已通过的身体、装备和来源失效矩阵不重复。

`creation`验证规则21独立创作与存续/两点连接派生、冻结输入重现及原资料保持；`save`验证正式人物区段22、新旧等待请求各授予一次、旧结果和费用保持，新成品实际支付、结束及完成后再次正式存读。旧样例`fixtures/persistence-creation-rule20.json`直接来自C12真实等待存档及当时完整预期，附来源校验值。

证据索引为`artifacts/skill_rebuild/trajectories/persistence-creation/delivery.json`。本批六段在同一进程分别通过，没有执行一次完整all；首次存读脚本的内部资料访问编译失败保留于`failures/save-1`，修正后只重跑未执行的存读段。正式冷启动和连续录像仍集中于C交付点，软件渲染仅用于功能及窗口。


### 5.05 C14：真实旧轨迹构造升级与局部失败恢复

`python3 Scripts/SpellDemos/verify_legacy_trajectory_save.py`复用现成世界，注入第一版真实曲线及一份故意缺失模型的独立定义，经正式加载核对完整曲线、费用、原档自动副本和局部报告；再恢复第六版真实命名步骤，核对命名输入与未取整基准，按真实目标规则、范围与冷却使用两份旧成品，验证实际作用、结束、再次使用及成品和已学关系再次正式存读。样例`fixtures/legacy-trajectory-save.json`附真实历史来源校验值；历史文件存在时会核对，固定样例可独立用于复现。

`--section runtime`仅用于读取前段已完成、首次正式施放尚未受理后的同一进程续接，不重新建立世界，不加入正常all。索引`artifacts/skill_rebuild/trajectories/legacy-trajectory-save/delivery.json`明确保留纯地点目标未受理及已有冷却的两次失败，以及分别通过的读取前段、运行和保存后段，不声称一次all完整通过。本批不重跑已有身体、装备或等待创作请求矩阵；正式冷启动与连续录像仍集中于C交付点。


### 5.05 C15：放置改造的真实开始条件

`python3 Scripts/SpellDemos/verify_placement_starts.py --section ui|behavior|save|all`。`ui`通过真实随身命令保留同名就绪步骤，编辑和撤销事件延迟，覆盖再打开并保存中英窗口；`behavior`复用一剑形主体，核对四种放置的共同转换、前序与事件延迟、早事件晚执行、未触发等待、预测回退、原部位命名输出、无法等价完成及行程条件的明确失败、新规则22自主派生、实际付费接触和结束恢复；`save`验证世界构造区段12、人物请求区段23、新旧等待请求各完成一次、完整旧结果及费用保持，以及当前成品使用和再次正式存读。

原术样例为`fixtures/placement-starts.json`，旧请求`fixtures/placement-starts-rule21.json`直接来自C13真实等待世界与当时预期。索引`artifacts/skill_rebuild/trajectories/placement-starts/delivery.json`记录同一进程三段分别通过，未运行一次完整all，没有实机段失败。正式冷启动和连续录像仍集中于C交付点，不重跑身体装备矩阵。新增就绪操作复用共同数值推进，不新增特效资源或游戏设置。


### 5.05 C16　基础来源的轨迹覆盖

`python3 Scripts/SpellDemos/verify_trajectory_coverage.py`读取真实轨迹登记、基础来源默认以及当前编辑器允许且能编译的改轨，逐份转换、保存读回及共同编译。它只建立临时容器，不逐份发射。清单为`artifacts/skill_rebuild/trajectories/trajectory-coverage/coverage.json`，休眠近身登记明确单列。修炼专属、法宝和器官完整来源迁移归第10包。

`python3 Scripts/SpellDemos/verify_trajectory_edges.py`只检查新增辅助路径、水平涡旋领域和灵藤显现的实际已学入口、原支付、作用及结束恢复。`--section continuation`只用于同进程原人物的费用观察及未完成后段，不重建世界。`--section vine`只验证灵藤分支。交付索引`trajectory-coverage/delivery.json`保留原失败及分别通过的结果，没有一次完整all通过的声明。截图用于功能观察，最终质感归9.02，正式冷启动和集中录像随后集中完成。


### 5.05 C17　正式冷启动和集中交付

`python3 Scripts/SpellDemos/verify_trajectory_delivery.py`依次执行两次真正重启及集中录像。`--section pending`使用C15真实正式等待世界，核对新旧请求完整输入、原结果与费用、各授予一次，以及恢复成品的实际支付、接触、后续步骤和结束，再保存完成世界。`--section completed`真正退出并重启后读取该完成世界，核对完整定义、原费用、命名引用、已学关系及请求完成状态，不重新授予。

`--section videos`复用统一演示入口集中录制不同原理，按已完成的同版本片段续录。正式存读结果在`artifacts/skill_rebuild/trajectories/trajectory-delivery/pending`和`completed`，画面在`videos`各分组目录。单段`demo.mp4`为连续原片，各组保留实际结果和总览。此入口只操作已有独立测试游戏，重启前确认原进程结束。软件渲染用于轨迹和功能观察，最终流派美术及性能分别按后续工作包交付。

`--section pending-tail`仅续接第一次冷启动已恢复的同一进程和世界，保留加载期间正常完成的请求，不重造等待状态，也不加入正常all。加载完成时请求可能仍在等待，也可能已经正式授予，两个状态分别核对完整输入或原完整成品。集中录像的总目录为`videos/index.html`，总览为`videos/全部法术-总览.mp4`，原版和Worldfall各有合集。

`--section completed-tail`仅续接第二次冷启动的同一完成世界，核对正式保存的完成状态及剩余机会时间。保存按剩余时间恢复，因此不把已经过期的绝对时刻作为应逐字相等的资料。两个tail均用于原失败后的分段补齐，不加入正常all，也不冒充完整all通过。

`--video-groups curve,shield,palm,worldfall`可只补所列未完成组，已完成原片保留，各组原始版本分别记录。普通法术演示按实际施放清理、界面和控制恢复核对收尾，构造演示的整世界恢复标志不用于普通演示。

迁移后的墙和护盾使用正式构造交锋记录核对吸收量与余量，普通演示结果保留`clashesLog`。自然结束不算作容量被消耗。仍走旧入口的法宝等来源继续读取自身真实耐久。

此交付入口默认使用普通独立安装，Worldfall组会正常退出当前游戏，再复用已有`artifacts/skill_rebuild/view-switch/environment/cultiway`兼容安装。`--installation`可明确指定独立安装根目录，续接时选择原记录的同一安装。原版掌击近景使用普通安装，三维组使用兼容安装，各组保留实际版本。


### 5.06　逐段成形的限定复验

通过develop的独立环境执行`python3 .agents/skills/develop/scripts/dev.py exec -- python3 Scripts/SpellDemos/verify_progressive_formation.py`。默认all按顺序执行普通分段。日常只执行受本次修改影响的分段，已经通过的内容保留原结果。

- `--section creation`检查实际成形输入、分枝与曲面独立创作、原术衍生、固定输入重现及尺寸。
- `--section legacy`检查真实规则22等待请求升级后的完整结果和费用。
- `--section ui`通过原窗口控件选择模型、动作与历时，编辑完成依赖，验证拒绝、撤销及保存再打开，并保存中英文截图。
- `--section branch`和`--section surface`分别检查真实施放中的未来区域、形成后命中、末端等待、完成换材、支付及结束恢复。
- `--section stopped`检查真实接触使半成形分枝停止、后续中断、末端继续等待，以及结束后再次施放。
- `--section save`生成正式新旧等待世界，验证保存读取、各授予一次、新成品实际使用，再保存完成世界。
- `--section cold --state <上次save输出目录>`在同一环境停止再启动后单独执行，核对磁盘等待输入与跨进程恢复。载入帧可以已完成请求，仍须保持完整结果和各授予一次，并核对完成世界去重。

入口显式将现成世界和真实旧样例传到本次独立输出，结果位于`CULTIWAY_DEV_OUTPUT/progressive-formation/<section>`，开发工具同时保留命令、实际输入与原始响应。cold会复制上次保存资料并提取实际地图中的等待区段。

集中录像通过同一开发入口执行`python3 Scripts/SpellDemos/record_progressive_formation.py --state <已通过的复验输出或交付归档>`，录制分枝、曲面和中途停止。`--case branch|surface|stopped`可指定一段。兼容环境加载Worldfall后加`--worldfall`，入口先核对实际加载模式。兼容模组放在私有游戏目录，开发代码或资源快照重建后需重新准备。

录像复用`record_delivery.script`及已有按世界时间编码入口，保留正式支付、命中、成形或中断、自然结束和原始帧。结果位于`CULTIWAY_DEV_OUTPUT/formation-films/native`或`worldfall`。`--encode-only --output <已有录像目录>`只编码已有帧，三维同时提供`--worldfall`。5.06交付索引位于`/home/inmny/.local/state/cultiway-dev/evidence/skill-growth-films/delivery.json`。

### 5.09　结构形变的执行复验

通过develop执行`python3 Scripts/SpellDemos/verify_deformation.py`。`--section core`检查控制收拢、展开、命名完成、连续替换、预测恢复、停止、半成形保持、局部曲面及刚性边界。`--section inheritance`检查变化中挂点脱离和定型材料。`--section cast`使用一位来源及目标，确认收拢后原位置安全、展开后实际命中、支付、自然结束及再次使用。

`--section pricing`用于单独复验共同需求。`--section ui`通过原控件检查历时、间距、指定控制的偏移旋转、完成引用、错误拒绝、撤销及正式保存再打开，并保存中英文截图。`--section creation`检查冻结控制输入、独立创作、其他模型原术衍生、成形完成衔接和原资料保持。`--section legacy`恢复真实规则23等待请求并核对原完整结果与费用。

`--section save`读取本进程已通过的creation结果，保存正式新旧等待世界、各授予一次、实际支付使用和完成世界。可用`--creation <已有creation目录>`指定已通过的原输入。默认all按顺序执行普通分段，core内包含费用检查。日常仅补受影响段，每段复制现成世界到所属开发输出，实际输入、检查和结果位于`CULTIWAY_DEV_OUTPUT/deformation/<section>`。跨进程恢复通过`--section cold --state <上次save输出目录>`在同一环境正常停止再启动后执行，核对真实磁盘等待输入、完整结果与费用、各授予一次以及完成世界去重。


集中录像通过同一开发入口执行`python3 Scripts/SpellDemos/record_deformation.py --state <已通过的cast目录>`，录制枝体收展、曲面弯折及接触中止。`--case branch|surface|stopped`可只录受影响的一段，Worldfall环境加`--worldfall`。入口读取正式成品，曲面只更换已有模型、真实控制参数及适当尺寸，停止样本使用共同接触停止规则。目标从同次真实作用形状选点，收拢段另确认原位置无伤，三段均保留真实支付、接触和自然结束。

录像保留真实世界时刻，暂停仅等待发布，沿既有编码入口按世界时间差生成常速片段。`--encode-only --output <已有deformation-films/native或worldfall目录>`只编码已有原始帧，Worldfall同时加`--worldfall`。输出为`CULTIWAY_DEV_OUTPUT/deformation-films/<视角>`，最终证据索引位于`/home/inmny/.local/state/cultiway-dev/evidence/deformation-films/delivery.json`。失败与各次分段结果分别归档，软件渲染画面用于功能观察。


### 5.11 A　结束去向的当前自主创作

通过develop执行`python3 Scripts/SpellDemos/verify_ending.py`。限定分段为`creation|independent|runtime|legacy|save|all`，日常只补本次受影响分段，旧身体及装备矩阵保留原依据。

`creation`验证规则25独立生成及余量续形、液态流散、收束回能和世界留置四类衍生、固定输入重现、原术与命名依赖保持，以及没有实际生成材料的身体输入。`independent`让无已学原术的人物通过正式独立队列完成新成品，并核对完整冻结结果及一次授予。

`runtime`读取已通过的创作成品，通过正常入口支付，确认来源结束才守恒交接、真实后段接触、水层与有限体积、实际回能、原施放结束后独立留置、余额不足拒绝及恢复后再次使用。`legacy`恢复真实旧规则24等待请求并核对完整结果和费用。`save`正式保存新旧等待世界、读取后各授予一次、新成品实际使用及完成世界。`runtime`和`save`可用`--creation <已有creation目录>`复用已通过输入。

每段将现成世界和真实旧样例显式传入开发环境。旧样例`fixtures/ending-rule24.json`来自5.09正式等待世界与当时完整预期。结果位于`CULTIWAY_DEV_OUTPUT/ending/<分段>`。本批索引为`/home/inmny/.local/state/cultiway-dev/evidence/ending-creation/delivery.json`，原失败及各次分别通过的事实保留。真正冷启动和连续双端录像集中于5.11最终交付。


### 5.11 B1　真实库存取用及步骤消耗

通过develop执行`python3 Scripts/SpellDemos/verify_inventory.py --section runtime|save|world|all`。入口显式传入现成世界，沿原物品工厂建立木料和骨料，通过正常已学能力入口受理，结果位于`CULTIWAY_DEV_OUTPUT/inventory/<分段>`。

`runtime`确认完整借用、原库存关系、消耗前等待、同一批材料拒绝重复受理、一件原木料的真实消耗回执及后续生长、重复提交去重、余量不足中断、未用材料归还、取消及转移，以及扣费通知中材料失效时撤销费用、释放借用和再次正常受理。编译边界同时检查无效输入引用，旧构造保持没有额外材料要求。

`save`在部分使用且其余材料仍借用时写出正式世界。核对已用物件退出、短时占用排除及原有炼制占用保留，读取后完整恢复物件身份、来源、品阶、数量、归属、构造和已学关系，旧施放及延迟消耗不重放，再保存完成世界。`world`在实际借用中加载另一世界，确认旧借用清理、新世界取用及取消恢复，旧操作没有接管新物件。

三段分别通过，索引位于`/home/inmny/.local/state/cultiway-dev/evidence/inventory-inputs/delivery.json`，实际输入、命令、正式世界和结果一并归档。B2继续原窗口及自主创作接入，5.11最终冷启动及连续录像集中在C完成。

### 5.11 B2　库存材料编辑与自主创作

通过develop执行`python3 Scripts/SpellDemos/verify_inventory_creation.py --section ui|creation|independent|runtime|legacy|save|all`。日常只执行受影响分段。入口复用现成世界及真实旧规则25样例，各段资料位于`CULTIWAY_DEV_OUTPUT/inventory-creation/<分段>`。

`ui`通过原控件编辑两种材料的名称、形状和件数，建立真实消耗与完成依赖，检查错误拒绝、撤销、正式替换再打开和中英文窗口。`creation`检查冻结材料输入、独立创作及库存衍生、完整重现和原资料保持。`independent`通过正式人物队列创作，等待期间移走材料，核对结果及费用保持且只授予一次。

`runtime`读取已通过窗口成品，由另一位修士真实支付及扣料，确认完成后生长、缺料不扣费、补料恢复及自然结束。可用`--ui <已通过ui目录>`复用输入。`legacy`检查真实旧规则25完整结果与费用。`save`使用已通过的创作输入，正式保存和读取新旧等待请求，各授予一次，新成品实际使用后再次保存。可用`--creation <已通过creation目录>`复用输入。

六段分别通过，证据索引为`/home/inmny/.local/state/cultiway-dev/evidence/inventory-creation/delivery.json`。B关闭，5.11 C继续永久世界结果、真正冷启动和集中连续双端录像。

### 5.11 C1　生成材料交给原版永久结果

通过develop执行`python3 Scripts/SpellDemos/verify_material_transfer.py --section ui|runtime|save|precision|all`。`ui`从原窗口设置每次成功改变地块所需的体积，检查非法值、撤销、中英文及正式保存再打开。`runtime`读取窗口成品，验证真实抬地、有限水格填入原版水地块、实际扣除及余量留置，以及世界用途预估、体积不足、原版确认失败和再次使用。`save`正式读取已经成功保存的原版结果和剩余材料，核对身份、来源、体积、未耗支付份额及去重，再保存恢复世界。`precision`核对实体和水格的极小份额拒绝、正常扣除及预测回退，以及真实身体的材料限制。

`runtime --ui <已通过ui目录>`和`save --state <已通过runtime目录>`可复用原输入。各段输出在`CULTIWAY_DEV_OUTPUT/material-transfer/<分段>`。同段失败的原运行保留，仅补尚未关闭的缺口。真正冷启动及双端连续录像集中于C3。

`precision`同样支持`--ui`。C1各分段通过，证据索引为`/home/inmny/.local/state/cultiway-dev/evidence/material-transfer/delivery.json`。旧世界收益清空整份本金与新按体积扣除的冲突已修正，所有准备错误及实际分段结果保留。C2继续自主创作，5.11保持未完成。


### 5.11 C2　生成材料交出的修士自主创作

通过develop执行`python3 Scripts/SpellDemos/verify_material_creation.py --section creation|independent|runtime|legacy|save|all`。`creation`需通过`--basis <C1已通过窗口保存的正式成品>`传入真实原术，检查规则27独立生成、材料交出衍生、完整输入重现、原资料和步骤保持及中英文说明。`independent`通过无原术人物的正式队列完成独立创作并核对一次授予。

`runtime`和`save`可用`--creation <已通过creation目录>`复用原输入。前者验证真实支付、原版地形、材料与支付守恒、余量留置、失败及恢复后再次使用。`legacy`使用真实规则26等待样例核对原完整结果与费用。`save`正式保存和读取新旧请求，各授予一次，新成品实际使用并保存完成世界，同时恢复原术与新成品的已学关系。

结果位于`CULTIWAY_DEV_OUTPUT/material-creation/<分段>`。五段分别首次通过，索引为`/home/inmny/.local/state/cultiway-dev/evidence/material-creation/delivery.json`。C2关闭，真正冷启动和集中双端连续影片继续C3。

### 5.11 C3a　结束去向的集中冷启动

通过develop执行`python3 Scripts/SpellDemos/verify_ending_delivery.py --section pending`，依次读取A、B2、C2已有正式等待世界，核对不同进程、完整结果和原费用、各授予一次及完成世界。`--family ending|inventory|material`只补指定一组，已通过组保留原依据。`--evidence <已有证据根目录>`可更换正式输入位置。

`--section inventory`读取B1真实部分消耗世界，恢复原物件身份、来源、品阶及库存归属，保留原炼制占用并归还短时借用，旧施放和延迟消耗不重放。`--section permanent`读取C1真实永久地块及长期材料，按实际形状选择余量，保存选中部分的回收进度、已确认入账及未选中余量。

正常退出后再次启动，执行`--section completed --state <上次ending-cold输出或合并归档>`。入口拒绝同一进程依据，核对三个完成世界各成品及原术关系、重复授予去重，再读取中途回收世界，检查原进度和已确认入账，仅继续剩余份额，保留未选中材料和原版永久地块。

分段输出在`CULTIWAY_DEV_OUTPUT/ending-cold/<分段>/<组>`。分次通过时将同名通过组复制到一个归档根目录，再作为`--state`传入，原运行和失败记录继续保留。坐标与浮点资料按保存时的实际类型或相同JSON形式比较。最终连续双端录像继续C3b。

C3a已交付，证据索引为`/home/inmny/.local/state/cultiway-dev/evidence/ending-cold/delivery.json`。原失败、各次分段通过以及正常退出后封存的日志一并保留，原版与Worldfall连续画面继续C3b。

### 5.11 C3b　材料交出与独立余量连续录像

通过develop执行`python3 Scripts/SpellDemos/record_ending.py --state <C2已通过的creation目录>`，录制实体交出与余量留置、水层交出与余量回收。兼容环境加载Worldfall后加`--worldfall`，入口先核对实际加载模式。`--case solid|water`只补对应组，原运行及失败保留。

录像读取真实衍生成品的副本，延长存在窗口以看清过程，实体组提高交出比例，使用目标随动和只在新位置接触来跟随地块更新。水层组改用已有浪体及有限流散，回收由现有真实选择入口执行。完整修改后定义和原输入一并保存，按实际参数重新报价及支付。各帧保留真实世界时刻、部位位置和阶段，事件保留实际地块改变、交出体积、支付份额、留置、回收与入账。

每组沿共同相机和按世界时间编码入口产生连续原片。`--encode-only --output <已有ending-films/native或worldfall目录>`只编码已有原始帧，三维同时加`--worldfall`。不同运行的通过组可复制到同一个对应视角目录再统一编码，保留原运行索引。输出位于`CULTIWAY_DEV_OUTPUT/ending-films/<视角>`。软件渲染画面用于功能与对应显示观察，流派最终美术及性能继续对应工作包。

C3b已交付，索引为`/home/inmny/.local/state/cultiway-dev/evidence/ending-films/delivery.json`，对应视角的`all.mp4`为两组常速合集。地点连接高度修正后仅补受影响实体组，水层原通过段及所有失败记录继续保留。5.11关闭，最终流派美术和遮挡继续第9工作包。


### 5.13 A1　生成实体侵蚀及实际余量

通过develop执行`python3 Scripts/SpellDemos/verify_erosion.py --section core|runtime|all`。`core`检查木铁耐受、实际阶段、共同份额、替换、材料转换、收束及主体结束、完全耗尽、预测回退、非法参数、零体积与动态流散入口限制。`runtime`复用同一现成世界和来源，通过正常学习、支付和施放，比较枝体及剑体的实际缩小、作用量及本金减少，等待真实完成后留置，并验证不适用材料的失败结束及再次使用。

本批最终数值段通过，实机行为完成。首次地点演示漏写选取声明，修正测试输入后补齐实机段；末尾日志包含结束坐标，序列化失败。导出已改为纯数值材料事件，实际留置事件从原落盘检查恢复并与原材料记录核对。各段按各自原调用状态交付，原始失败保留，索引位于`/home/inmny/.local/state/cultiway-dev/evidence/erosion-generated/delivery.json`。A1关闭，独立材料过程与保存、原物件及生灵、窗口与创作和集中双端影片继续5.13后批。


### 5.13 A2a　独立世界材料侵蚀及正式保存

通过develop执行`python3 Scripts/SpellDemos/verify_world_erosion.py --section material|world|save|all`。`material`检查真实阶段交接、独立几何及本金扣除、数值恢复、材料转换、局部分割后的份额与进度、非法资料。`world`复用同一现成世界及来源，正常学习、支付和施放，验证普通留置继承损耗、独立过程、失败结束、真实范围收束中断、再次使用及来源死亡后继续，并保存中途世界。`save`读取该中途世界，验证原身份、阶段和进度、剩余过程、完成后的再次存读、坏条目局部报告与保留其他材料、旧6区段升级及换世界。

单独运行`save`可通过`--state`指定已通过的`world`输出目录。旧6资料固定在`fixtures/erosion-world-v6.json`，由既有正式世界提取，含来源文件及校验值。复验将该原区段嵌入现成世界，另从本批实际中途存档生成非法进度副本。各段分别通过，证据索引为`/home/inmny/.local/state/cultiway-dev/evidence/erosion-world-materials/delivery.json`。A2a关闭，范围侵蚀、局部选择及完成衔接继续A2b，整体5.13继续推进。


### 5.13 A2b1　完整实体范围侵蚀与真实完成

通过develop执行`python3 Scripts/SpellDemos/verify_world_erosion_requests.py --section core|world|all`。`core`检查共同请求、实际完成等待、选择主体保持、预测回退、来源结束和命名材料输入。`world`正常学习、支付和施放，验证实际选择、完成后续、容量与明确空输入、原支付者限制、替换与收束中断、结束释放及再次使用。可用`--part normal|boundaries|receipts|all`分别验证正常过程、边界和数量回执。`receipts`使用8份侵蚀6份、输出剩余2份，并检查替换累计，覆盖实际损耗和余量的区别。

原材料分割入口另以`verify_world_erosion.py --section material`补验新分段计数。正常及数量、边界、其他控制者限制分别验收，原始测试资料、材料输出形式和理想数值断言的失败保留，索引位于`/home/inmny/.local/state/cultiway-dev/evidence/erosion-world-requests/delivery.json`。A2b1关闭，曲面局部选择与共同显示、正式区段8的新旧存读继续A2b2。


### 5.13 A2b2　定形曲面局部侵蚀、共同显示及正式存读

通过develop执行`python3 Scripts/SpellDemos/verify_partial_erosion.py --section core|world|save|all`。`core`只检查单格可表示比例、实际容量及极小份额。`world`复用现成世界、一个来源及真实转为木质并留置的曲面，通过正常已学入口验证局部施放、部分厚度、容量、未选中格子、共同表面、完成后明确材料转换，以及部分替换与原余量继续。截图和中途正式世界随输入、事件及检查保存。

`save`读取已通过的world中途世界，验证完整身份、冻结底面、历史及各自过程、仅续接剩余过程、完成后去重、坏过程编号局部恢复、实际旧7升级和换世界清理。可用`--state <已通过world目录>`复用原输入。旧7资料位于`fixtures/erosion-world-v7.json`，取自A2a的真实正式世界，保留原路径、校验值和实现提交。

各段输出位于`CULTIWAY_DEV_OUTPUT/partial-erosion/<分段>`。三段各自首次通过，证据索引位于`/home/inmny/.local/state/cultiway-dev/evidence/erosion-partial/delivery.json`。A2关闭，真正冷启动和连续双端画面集中5.13 C。


### 5.13 B1　生灵接触老化与原版生命结果

通过develop执行`python3 Scripts/SpellDemos/verify_aging.py --section core|runtime|save|all`。`core`核对零参数的旧序列化与结构、编译副本、共同年数需求、非法年数以及格挡和偏转入口。`runtime`复用现成世界、一个来源和一个目标，通过正常学习与支付施放，验证1岁至21岁的实际成长、作用面外人物、共同频率和次数、原版寿终法则及不老特性、真实寿终和结束后再次使用。

`save`读取已通过实机输出，核对原人物身份、年龄和年龄增量、完整构造、已学关系、完成世界再次存读及换世界清理。可用`--state <已通过runtime目录>`复用原世界。输出位于`CULTIWAY_DEV_OUTPUT/aging/<分段>`，实际事件记录前后年龄、真实接触时点及原版寿终结果。

各段独立验收，首次脚本名称及数值比较失败保留，仅补对应段。索引位于`/home/inmny/.local/state/cultiway-dev/evidence/aging-actors/delivery.json`。原物件继续B2，窗口与自主创作、真正冷启动和集中双端画面继续C。


### 5.13 B2a　明确装备栏腐蚀与原耐久保存

通过develop执行`python3 Scripts/SpellDemos/verify_aging.py --family equipment --section core|runtime|save|all`。默认family仍为actor，保留B1入口原行为。`core`核对明确栏位、份额边界、编译副本、共同需求及格挡和偏转入口。`runtime`复用同一现成世界、来源和目标，仅一件铁甲及木剑，检查真实支付、指定栏位、材料差异、不老、微量与范围外、真实间隔、耐久耗尽后的属性和原物品归属，以及结束后再次使用。

`runtime`保存`worn-save`和`broken-save`两份真实世界。`save`恢复部分及完全磨损的原物品身份、耐久、持有人和装备栏，完整腐蚀构造、已学关系及破损属性，继续推进及再次存读保持一次结果，换世界清理旧施放。可通过`--state <已通过runtime目录>`复用输入。输出位于`CULTIWAY_DEV_OUTPUT/equipment-erosion/<分段>`，事件中保留前后耐久和真实时点。

本批all中三段依次首次通过，索引位于`/home/inmny/.local/state/cultiway-dev/evidence/erosion-equipment/delivery.json`。B2b继续模组库存物件的已登记过程，真正冷启动及双端影片继续C。


### 5.13 B2b　库存物件寿命与到期解除借用

通过develop执行`python3 Scripts/SpellDemos/verify_aging.py --family inventory --section core|runtime|save|all`。`core`检查有限进度、件数及形状、正式登记的未知形状、编译副本、共同需求、格挡和偏转入口，以及可表示精度和原寿命边界。`runtime`复用现成世界、一个来源与一个目标，有限木料、骨料、永久及暂停对照组成同一库存，验证真实支付、实际筛选及件数、原资料和未选中保持、作用面外、借用中到期、后续消耗中断及其他材料归还，以及结束后再次使用。

`runtime`保存`aged-save`及`expired-save`，事件保留原物件稳定身份、真实前后进度、原寿命、到期交接和世界时点。`save`在正式世界恢复回调取原资料，随后按实际世界时间核对额外进度，验证部分进度及原身份、来源、品阶、归属、完整构造和已学关系，到期世界再次存读保持一次结果，旧施放和借用不重放，换世界清理。可用`--state <已通过runtime目录>`复用原输入。

输出位于`CULTIWAY_DEV_OUTPUT/inventory-aging/<分段>`。首轮core及runtime通过，save脚本包装错误与真实暂停缺陷分别定位，源头修正后仅补受影响实机和保存。原all失败、字段差异及临时只读调用记录保留，正式脚本只保留恢复边界和实际时间对照。证据索引位于`/home/inmny/.local/state/cultiway-dev/evidence/aging-inventory/delivery.json`。B关闭，窗口与创作、旧请求保持、真正冷启动及双端画面继续C。


### 5.13 C1　侵蚀老化原窗口及当前目标自主用途

通过develop执行`python3 Scripts/SpellDemos/verify_aging_editor.py --section ui|pricing|runtime|ai|all`。`ui`使用原控件编辑人物年龄、装备腐蚀、有限库存寿命、世界及原主体侵蚀、留置独立侵蚀和真实命名完成依赖，保留错误拒绝、撤销、正式替换、再开及中英截图。`pricing`对照实际作用覆盖、原部位与命名步骤计费、原名称别名去重、明确世界容量、旧规则27和新规则28及完整说明。作者填写的费用沿原入口。

`runtime`通过窗口成品的正常学习与支付入口观察原年龄、原装备耐久与原库存寿命，验证作用面外保持及结束再用。`ai`从未被接管的场景人物开始，观察当前目标是否有真实损耗用途、原混合用途、普通候选及实际游戏AI自主释放。停止接续后确认每个实际施放均已结束。原版解除接管会添加眩晕和困惑，自主验证不引入这一额外场景。

后段可用`--ui`指定已通过窗口目录，输出在`CULTIWAY_DEV_OUTPUT/aging-editor/<分段>`。`ui-save`是首次窗口已覆盖后的准确比对与再开续验，允许从该次未通过的尾段输出继续；`ui-world`补世界筛选的正式保存再开，需要已通过窗口输出。两者不加入正常all。原all、命名测试输入和接管准备失败分别保留，实际按分段交付。证据索引为`/home/inmny/.local/state/cultiway-dev/evidence/aging-editor/delivery.json`。C1关闭，新旧等待创作及正式存读继续C2，集中冷启动和原版及Worldfall连续画面继续C3。


### 5.13 C2　侵蚀老化创作及新旧正式请求

通过develop执行`python3 Scripts/SpellDemos/verify_erosion_creation.py --section creation|independent|runtime|save|all`。`creation`需要`--basis`指向C1已通过窗口的`saved-definition.json`，有限种子覆盖原主体、世界范围及留置独立侵蚀、人物老化、装备腐蚀和库存寿命的独立生成及原术衍生，核对冻结输入、原术和实际引用。`independent`使用无已学原术人物的正常队列，确认完整成品和一次授予。`runtime`使用实际衍生成品，观察原报价支付、真实年龄及结束恢复。

`save`恢复真实规则27旧请求，人物区段28原样升级29，旧作者、名称、完整结果及费用保持，与新规则28请求一起正式保存和读取，各授予一次。新成品实际使用，完成世界再次读取保持原年龄和完整新旧结果。旧样例`fixtures/erosion-creation-rule27.json`来自5.11 C2真实等待世界及当时完整预期。保存前沿用现成入口等待三帧，使新人物进入原版保存枚举。

后段通过`--creation`复用已通过的生成目录，输出在`CULTIWAY_DEV_OUTPUT/erosion-creation/<分段>`。首轮前三段通过，首轮存读因场景人物尚未进入保存列表失败，修正准备后仅补存读段，按分段验收。索引为`/home/inmny/.local/state/cultiway-dev/evidence/erosion-creation/delivery.json`。C2关闭，真正冷启动及原版、Worldfall连续影片继续C3。


### 5.13 C3a　侵蚀老化的集中真正冷启动

通过develop执行`python3 Scripts/SpellDemos/verify_erosion_delivery.py --section resume`，依次读取C2新旧等待及使用后、A2b2局部材料、B2a磨损与破损、B2b部分及到期库存的真实交付世界。各组拒绝原进程，核对完整结果、作者及费用、真实年龄、原物件和关系、有限寿命及到期、局部材料的剩余侵蚀与体积和支付守恒。每组保存本进程的完成世界及当时完整预期。

正常停止并确认进程退出，再次启动后执行`--section completed --state <上次erosion-cold/resume目录>`。逐组读取前次完成世界，核对完整原结果、原关系及一次授予，额外损耗和旧施放不重放。原人物仍按出生时刻及原年龄增量计算，原库存只按真实世界时间推进。

可用`--family requests|materials|equipment|inventory`只补受影响组，默认all依次运行四组。`--evidence`切换既有证据根目录，输出位于`CULTIWAY_DEV_OUTPUT/erosion-cold/<分段>/<组>`。两条集中命令分别首次通过，输入、响应、完成世界及正常退出日志均在`/home/inmny/.local/state/cultiway-dev/evidence/erosion-cold/delivery.json`索引。C3a关闭，原版及Worldfall连续影片继续C3b。


### 5.13 C3b　侵蚀老化的双端连续画面

通过develop执行`python3 Scripts/SpellDemos/record_erosion.py --state <C2已通过的creation目录>`，连续录制主体损耗、余量独立侵蚀及另一正常施放选择世界材料，另录连续真实接触推进人物年龄、原铁甲破损及有限库存部分推进至到期。输入复用C2实际衍生成品，采用已有模型与可编辑参数，并沿规则28重新报价，原输入及实际定义保留。

已加载Worldfall的独立环境加`--worldfall`，入口核对实际模式。`--case material|aging`只补对应场景。每帧保留实际世界时间、位置、接触与结果，暂停只等待当前时点绘制，沿实际时间差编码常速原片及视角合集。`--encode-only --output <已有视角目录>`可重编已有帧，兼容画面同时加`--worldfall`。

输出位于`CULTIWAY_DEV_OUTPUT/erosion-films/native|worldfall`，每组包含输入世界、原始及实际定义、实际脚本、逐帧画面、时刻和事件、结果与影片。原版原片沿首次通过结果。Worldfall先前取景中的旧建筑遮挡，通过将原清理移至原版枚举初始化后、调整斜向近景解决，仅补兼容两段。取景原片和诊断脚本失败均保留，按分段验收。索引为`/home/inmny/.local/state/cultiway-dev/evidence/erosion-films/delivery.json`。软件渲染用于功能与双端对应观察，最终美术与性能继续对应工作包。


### 5.14 A　映像对应与真实交锋传伤

通过develop执行`python3 Scripts/SpellDemos/verify_correspondence.py --section core|runtime|save|all`。边界段检查普通部位的明确原人物、具名对应和传伤份额、完整编译复制与非法声明拒绝。实机段复用现成世界、一个来源和两个目标，沿普通学习与付费使用，观察近处映像受击、远处原人物真实受伤、原攻击归属和有限交锋消耗。多个独立映像和直接人物接触共用攻击次数，补充自身资格拒绝、端点真实死亡、缺少原人物不收费及结束再用。

保存段用`--state <已通过的runtime目录>`复用正式完成世界，`--legacy <真实旧12世界>`可指定旧来源。默认旧样例来自5.13 C2已交付的真实完成世界，入口核对正式区段12并保存校验值及原资料。完整旧构造原样升级，新13对应、原人物和生命结果及已学关系保持，重复读取及换世界不重放。

输出位于`CULTIWAY_DEV_OUTPUT/correspondence/<分段>`。原all的边界段通过，实机事件坐标直接序列化失败，改为纯数值后仅补实机及存读，按分段验收。完整输入、事件、世界和退出记录位于`/home/inmny/.local/state/cultiway-dev/evidence/correspondence-damage/delivery.json`。A关闭，其他作用、时序、身体与持物接触、编辑及自主创作、集中双端影片继续5.14后批。


### 5.14 B1　真实身体及持物接触映像

通过develop执行`python3 Scripts/SpellDemos/verify_body_correspondence.py --section normal|frequency|control|all`。`normal`复用原身体与同一铁剑，检查真实徒手及持物接触、原近战传伤、实际支付、映像承住与破碎后的连续收势、落空和控制归还。原目标首次近战受击在当前接触边界确认，嵌套反应及一次领取用同一段检查。

`frequency`检查持续接触的间隔、共同脉冲和次数上限，以及持续贴住有效映像的去重。重入沿普通飞行曲线把同一映像真实移出并移回，保留离开位置和实际传伤时点。可用`--frequency-case timed|reentry|all`只补对应部分，单独输出`frequency-timed`或`frequency-reentry`。`control`验证真实器物替换中断、新器物再次使用和换世界控制归还。

输出位于`CULTIWAY_DEV_OUTPUT/body-correspondence/<分段>`，保留完整实际定义、脚本、费用、原人物及器物身份、传伤事实和生命结果。正常段及间隔、脉冲在修正后的all中通过，重入及控制后续分别通过，受影响的A实机段单独补验通过。原失败和推进诊断日志保留，按分段验收。索引位于`/home/inmny/.local/state/cultiway-dev/evidence/body-correspondence/delivery.json`。正式声明与区段仍为13，其他作用、编辑与自主创作、集中恢复及连续影片继续5.14后批。


### 5.14 B2 映像附带作用许可

通过独立环境运行 `python3 Scripts/SpellDemos/verify_correspondence.py --section effects`，复用现成世界核对实际恢复、状态、推力、未许可作用和到期。脚本在输出目录保存新14正式完成世界。保存检查用 `--section save --state <effects目录> --legacy <真实旧13世界>`，核对完整新旧资料、实际生命结果及重复读取和换世界去重。证据索引位于 `/home/inmny/.local/state/cultiway-dev/evidence/correspondence-effects/delivery.json`。


### 5.14 B2 映像有效时段

通过独立环境运行 `python3 Scripts/SpellDemos/verify_correspondence.py --section timing`，核对实际开启时点、到期、实体自然结束、撤销、再次使用及等待中换世界。用 `verify_body_correspondence.py --section timing` 核对实际掌面在窗口内接触及结束归还控制。保存检查使用 `--section save --state <timing目录> --legacy <真实旧14世界>`。实际事件和分段结果位于 `/home/inmny/.local/state/cultiway-dev/evidence/correspondence-timing/delivery.json`。

### 5.14 映像编辑及自主创作

通过独立环境运行 `python3 Scripts/SpellDemos/verify_correspondence_creation.py --section ui|creation|all`。窗口段验证字段、中英说明、非法候选保留、撤销、再开和独立需求；创作段验证规则29的冻结输入、独立生成、已学原术衍生、实际付费传伤和完成。真实等待世界保存在 `correspondence-creation/creation/pending-save`，两段及首次失败的索引位于 `/home/inmny/.local/state/cultiway-dev/evidence/correspondence-creation/delivery.json`。

5.14集中交付：`verify_correspondence_delivery.py --section resume|frozen --state <B3创作目录>` 用真实旧新等待档及已有结果检查正式冷启动。再次启动后，`--section completed --state <第一次冷读目录>`核对已授予和结算不会重放。`record_correspondence.py --state <B3创作目录>`连续录制真实映像，Worldfall独立环境加`--worldfall`；`--encode-only <视角目录>`可只按已保存的原始帧编码。双端原片、事件、核对和退出记录见 `/home/inmny/.local/state/cultiway-dev/evidence/correspondence-delivery/delivery.json`。

5.15 双门转移可用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_spatial_mapping.py` 复验。`--section core` 检查两个出口方向、实际完成和无效入口；`--section runtime` 只走正式施放、门间无扫伤和出口实际扣血。脚本复用现有世界，记录真正的出门坐标、碰撞形状和伤害归属。人物搬运用 `--section actor`，只补成功抵达和再次施放可用 `--section actor-clear`。阻挡拒绝与安全抵达分段验收，原失败及实测位置见 `~/.local/state/cultiway-dev/evidence/spatial-mapping-b1/delivery.json`。原法宝搬运用 `--section artifact`，检查真实借用、受阻保留、到达转向、归还和同一物件复用；`--section artifact-clear` 仅补安全落点和复用。完整单次结果与早期准备失败见 `~/.local/state/cultiway-dev/evidence/spatial-mapping-b2/delivery.json`。通行容量用 `--section capacity`，覆盖阻挡后恢复、实际抵达计数、重复接触和出口提前关闭，证据见 `~/.local/state/cultiway-dev/evidence/spatial-mapping-b3a/delivery.json`。短时暂存用 `--section staging`，验证实际接触、延迟开门后原人物抵达、受阻关门放行及实际换世界清理，证据见 `~/.local/state/cultiway-dev/evidence/spatial-mapping-b3b/delivery.json`。万法阁用 `--section editor` 复验三种双门步骤的选择、参数、无效组合拦截、撤销和保存重开，中英文截图与证据见 `~/.local/state/cultiway-dev/evidence/spatial-mapping-c1/delivery.json`。自主创作用 `--section creation`，退出并启动新进程后用 `--section restart --pending <creation 输出目录>`，检查正式新旧请求授予、实际付费施放和重存；分段证据见 `~/.local/state/cultiway-dev/evidence/spatial-mapping-c2/delivery.json`。双端录像用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 540 -- python3 Scripts/SpellDemos/record_spatial_mapping.py`；兼容安装加 `--worldfall`，单段用 `--case shape|actor|artifact`，从已验证画面重编码用 `--encode-only <视角目录>`。六段原片及合集见[空间映射与转移](../../Doc/技能系统全面重构/空间映射与转移.md)。

5.16 A 局部变速用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_erosion.py --section time`。在同一次正式施放中验证两块实际材料、不同速度和真实完成依赖。复验数据见 `~/.local/state/cultiway-dev/evidence/process-time-a/delivery.json`。

5.16 B1 世界材料变速用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_process_time_world.py --section pending`：复用同一种木料验证范围侵蚀完成衔接，并留存暂停中的正式世界。退出测试游戏后启动新进程，将末尾参数换成 `--section restart --state <上次的 process-time-world/pending 目录>`。新进程检查余下暂停、实际加速、完成存读、坏记录局部丢弃和真实旧第 8 版的常速接续。证据见 `~/.local/state/cultiway-dev/evidence/process-time-b1/delivery.json`。

5.16 B2 使用同一入口的 `--section retiming`：在同一份活动木料上暂停、替换为两倍、等待原结果真正完成，验证已完成时拒绝、下一次再用与清除变速，并正式保存读取第 10 版及真实旧第 9 版。证据见 `~/.local/state/cultiway-dev/evidence/process-time-b2/delivery.json`。

5.16 C1 使用同一入口的 `--section editor`：复用木料与现成世界，在实际万法阁检查四处时间窗、重叠拒绝、撤销、规则 31 推荐费用及原规则费用、中英文说明和正式保存重开。证据见 `~/.local/state/cultiway-dev/evidence/process-time-c1/delivery.json`。

5.16 C2 使用 `--section creation` 进行独立创作、原术衍生与正式保存。退出并启动新进程后，用 `--section creation-restart --state <creation 输出目录>` 冷读规则 30、31 的真实等待请求，检查一次授予、实际付费、暂停、恢复和重存；`creation-use` 仅用于前段冷读已通过之后补后半段。分段结果和原失败见 `~/.local/state/cultiway-dev/evidence/process-time-c2/delivery.json`。

5.16 C3 用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 360 -- python3 Scripts/SpellDemos/record_process_time.py --state <C2实际学会并重存的输出目录>`，录制同场两份世界木料的独立节奏；另起兼容环境后加 `--worldfall`。`--encode-only <视角目录>` 从已录原帧重核并编码。两端影片、暂停体积对照、两倍进度及实际结束见 `~/.local/state/cultiway-dev/evidence/process-time-c3/delivery.json`。

5.17 A 使用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_status_removal.py`，复用现成世界验证真实状态施加、按层和来源解除、非匹配保留、恢复、再次支付施放及成品存读。可用 `--legacy <旧构造区段15的正式世界目录>` 核对旧构造和费用。当前结果、实际输入、保存资料及进程退出记录见 `~/.local/state/cultiway-dev/evidence/status-removal-a/delivery.json`。

5.17 B 使用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_grab_release.py`，复用现成世界和一个真实抓持动作，验证主动放手的归属、解救力度、提前归还、拒绝及再次抓持、正式存读。记录和初次脚本编译失败见 `~/.local/state/cultiway-dev/evidence/grab-release-b/delivery.json`。

5.17 C 使用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_equipment_repair.py`，用同一件原版铁甲验证真实材料扣除、封顶、重复接触、无效时归还及原物品、耐久、材料和完整法术存读。结果和存档见 `~/.local/state/cultiway-dev/evidence/equipment-repair-c/delivery.json`。

5.17 D 使用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_contact_signal.py`，用同一收件人核对未送达、真实登记、重复接触、作用分支、解除、到期及正式存读。可用 `--saved-world <原输出目录>` 仅复验已生成的世界。分段证据、初次失败及读回后续见 `~/.local/state/cultiway-dev/evidence/contact-signal-d/delivery.json`。

5.17 E1 使用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_application_editor.py`，在原窗口验证具名解除、解救、装备修复、讯号及状态条件的编辑；检查材料引用、报价、撤销与保存重开。中英截图、说明和实际成品见 `~/.local/state/cultiway-dev/evidence/application-editor-e1/delivery.json`。

5.17 E2 用独立环境执行 `verify_application_creation.py creation`，然后完全退出游戏、重新启动，以 `verify_application_creation.py restart --state <creation 输出>` 读取正式待授予档。两步均通过 `dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_application_creation.py ...`。旧规则资料取自 5.16 的真实等待档和当时的预期；新旧各授予一次，再实际支付、消耗原材料和修复原装备并重存。分段首次结果及通过记录见 `~/.local/state/cultiway-dev/evidence/application-creation-e2/delivery.json`。

5.17 E3 通过 `dev.py exec --timeout 330 -- python3 Scripts/SpellDemos/record_application.py --state <E2 已重存的输出> --signal-definitions <D 的 saved-definitions.json>` 录原版；加载同一兼容环境并加 `--worldfall` 录对照。录像使用同一组实际术式，逐帧核对修复、无效接触、真实收件、条件恢复和解除。已保存的视角目录可通过 `--encode-only <视角目录>` 重新编码。两端原片及事实索引见 `~/.local/state/cultiway-dev/evidence/application-films/delivery.json`。

### 5.18 A 地波实际到达

通过 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_ground_route.py` 复用已有地面和同一个足底入口。`--section arrival` 只查近远到达、同部位操作和预测回退；`--section interruption` 只查途中断路、接回后同招再用和取消。正式施放与事件位置、结束、输入和初次登记失败见 `~/.local/state/cultiway-dev/evidence/ground-route-a/delivery.json`。

### 5.18 B 水路与人物实际送达

通过 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_network_arrival.py`。`--section water` 复用循水连雷，核对实际水路、空水、首末两轮和接续位置；`--section signal` 复用告警入口，核对落空、真实收件、同源刷新及到期后重发。输入、真实脉冲和收件时点、初次准备失败见 `~/.local/state/cultiway-dev/evidence/network-arrival-b/delivery.json`。

### 5.18 C 多段条件的顺序与终止

通过 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_ground_route.py --section chain` 复用 A 的地面及足底，验证真实地波到达、延迟独立成形、同刻换材和人物实际恢复。正常结束、波源先结束和主动取消使用同一组合，其他地波检查仍由 `all` 执行。实际输入、事件、恢复及退出证据见 `~/.local/state/cultiway-dev/evidence/propagation-chain-c/delivery.json`。

### 5.18 D1 在万法阁编辑传播条件

在已启动的独立游戏运行 `dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_propagation_editor.py`。默认从本地开发证据目录取 A 的地波和 B 的两种到达构造；可用 `--evidence` 指定证据根目录。复验通过实际窗口编辑两处条件、撤销不可保存的路程，并核对替换和重开。输出含中英截图、最终构造和说明。

### 5.18 D2 创作与正式冷读

在原版独立安装运行 `dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_propagation_creation.py creation`。退出该游戏并另起新进程，用同一入口的 `restart --state <creation输出目录>` 验证已冻结的新规则 33 与历史规则 32 请求。输出保留正式等待档、完整成品、付费水路接续和再次保存的结果。

### 5.18 D3 地波与水路连续录像

在原版独立游戏运行 `dev.py exec --timeout 390 -- python3 Scripts/SpellDemos/record_propagation.py --state <D2重存目录>`；兼容安装加 `--worldfall`。两端复用 C 的正式地波连锁资料和 D2 已重存的循水成品，保留逐帧位置及实际触地、水路到达和收束记录。`--case ground|water` 只录对应片段，`--encode-only <视角目录>` 只核对和编码已有原帧。

### 5.19 通用运动操作的实机复验

在独立游戏中运行 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_operation_acceptance.py --section motion`。同一剑形分别等待真实生长和定时就绪，随后反向旋转，在飞行中制动再起或加速；输出原定义、绘制姿态、正式存档和人物编号。另起游戏后用 `--section reload --state <motion输出目录>` 只复验正式恢复和再次施放。`--section turn` 单独验证「折向续行」的反向弯点和真实接触；若动作段已通过，可用 `--section turn-save` 只补正式保存比较。`verify_spatial_mapping.py --section artifact-varied` 复用同一原飞剑，核对不同出口角度、归还、正式存读及过门不重放。完整操作表及复用依据见[逐项验收](../../Doc/技能系统全面重构/通用操作逐项验收.md)。

### 5.20 嵌套、换材与实际结果的接续

在独立游戏中运行 `dev.py exec --timeout 195 -- python3 Scripts/SpellDemos/verify_composite_change.py`。使用[聚剑凝冰](../../Doc/技能系统全面重构/构造输入/聚剑凝冰.json)核对三级部件的聚合、换材、使用真实父连接离体、以新材料命中和世界留置。输出包含绘制姿态、步骤回执、实际伤害和正式存档。另起游戏后加 `--state <正式保存输出目录>`，读回同一构造及材料并重新施放；中断已经通过时可用 `--skip-interruption` 只补正常施放和保存。

### 6.01 人物受击范围与高度

在独立游戏中运行 `dev.py exec --timeout 195 -- python3 Scripts/SpellDemos/verify_actor_hit_boundary.py`；加载 Worldfall 的独立游戏加 `--worldfall`。两端复用同一存档和受击者，比较旧执行的实际碰撞回调与新构造的实际伤害，覆盖边界内外、离地错开和再次命中。用 `--section legacy|legacy-height|construction` 补测相应段。输入世界、实际脚本、打击前后血量和判定明细保存在 `CULTIWAY_DEV_OUTPUT/actor-hit-boundary/<视角>/<分段>`。

### 6.02 A　薄板、厚块与实际部位

在独立游戏中运行 `dev.py exec --timeout 195 -- python3 Scripts/SpellDemos/verify_part_contact_shapes.py`。用同一个受击者检查真实边内外、斜放空角、离地、厚块内部、部位自己移动和薄板拦截。`--section attack|controlled|barrier` 可只验证改变的段。输出在 `CULTIWAY_DEV_OUTPUT/part-contact-shapes/<分段>`，保留试件资料、实际脚本、血量及拦截位置。

### 6.02 B　扇形、路径带和形成空隙

在独立游戏中运行 `dev.py exec --timeout 195 -- python3 Scripts/SpellDemos/verify_part_contact_regions.py`。同一个目标验证扇形尺寸、开角和厚度，以及路径后段出现前后、弯道内侧和结束。`--section sector|path` 可只重验受影响的段。输出在 `CULTIWAY_DEV_OUTPUT/part-contact-regions/<分段>`，保留载入的形体声明、实际执行脚本、阶段和伤害。

### 6.02 C　持续复合区域与正式剑气

在原版独立游戏中运行 `dev.py exec --timeout 215 -- python3 Scripts/SpellDemos/verify_sustained_contact_regions.py`。完整段验证重叠去重、离开重入、间隔、到期及保存；可用 `--section reentry|interval|save|original` 补受影响的段，`original` 验证原来已学剑气的实际费用与命中。

分别冷启动原版和装有 Worldfall 的独立游戏，用 `--section restore --input-save <上一轮输出的 completed-save 绝对路径>` 检查读回两项已学构造并再次施放。Worldfall 端加 `--worldfall`，另外比对实际绘制与作用位置。每端保留自己的运行输出，不重跑保存。输入脚本、结果及世界在 `CULTIWAY_DEV_OUTPUT/sustained-contact-regions/<分段>`。

### 6.03 A　高速直飞的剑刃与飞掌

在独立原版游戏运行 `dev.py exec --timeout 195 -- python3 Scripts/SpellDemos/verify_high_speed_contacts.py`。使用真实施放和已发布形体，检查一步两端、实际付款、落在步内的扣血时点和自然结束。`--section blade|palm` 用于分别复验；飞掌段包含偏开无伤及同招再放。结果在 `CULTIWAY_DEV_OUTPUT/high-speed-contacts/<分段>`。

### 6.03 B　连续转向与断点

用 `dev.py run Scripts/SpellDemos/verify_rotating_contact_math.script --timeout 120` 检查六度转角的真实外弧、内弦空隙、候选范围和首次接触时间。实机沿用上面的 Python 入口，`--section turn` 检查高速旋转的实际扣血与内环无伤，`--section breaks` 检查间隔转移和立即改向各自的空隙与真实落点。旧直移和空间筛选共用回归入口为 `dev.py run Scripts/SpellDemos/verify_pair_sweep.script --timeout 120`。

### 6.05　真实接触结果

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_contact_facts.py --section opposition` 核对两种真实交锋：有余力时反射并交接归属，容量不足时破盾而保留攻击余量。`--section ground` 复用触地分支，核对实际位置、方向、不改地形、悬空落空与归还身体。用 `--section damage` 复用同一对人物检查普通扣血、盾的实际消耗、受击即回血和免疫；`--section body` 检查掌击接触与原队列的实际扣血通过同一编号关联，且收势归还身体控制。结果分别保存在 `CULTIWAY_DEV_OUTPUT/contact-facts/<分段>`。

### 6.09　身体与器物格挡

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_body_guard.py --section palm` 核对实际掌面、反面、收势后和破防余力。`--section weapon` 沿用同一路径，换为实际持有的一把铁剑。`--section melee` 从万法阁让人物学会架掌，核对近身直掌全挡、破防后实际扣血、收势归还控制和再次使用。`--section melee-remainder` 只复验近身破防和原版实际扣血。`--section lost` 在格挡期间取下实际铁剑，验证同一路来招不再被旧剑拦住。`--section initial-force` 核对即使来招选择按初始力度计伤，仍不能补回已被身体格挡的损耗。各段结果位于 `CULTIWAY_DEV_OUTPUT/body-guard/<分段>`。

### 6.18　正式模型与实战边界

在独立游戏运行 `dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_visual_boundary.py --section circle`。可分别选择 `circle`（圆内外）、`height`（同位置改高度）、`blade`（细长刃边）、`wave`（潮浪经过）、`shield`（盾面边缘）。脚本加载现成测试世界，用正式模型施放；同一次运行的画面、几何坐标和实际扣血或反射回执保存在 `CULTIWAY_DEV_OUTPUT/visual-boundary/<分段>`。结论及样本说明见[画面与实际作用边界](../../Doc/技能系统全面重构/画面与实际作用边界.md)。

### 6.19　实际判定误差与有效区调试

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_contact_accuracy.py --section accuracy`。`accuracy` 核对三档扇形半径的内缩和实际扣血；`outline` 核对两种视角共用轮廓，以及关调试、自然结束和换世界清理；`isolated` 核对只看有效区及显示恢复；`water` 比较实际湿格箱体与水面的四侧。每段使用同一份现成世界，实际输入、数值和截图保存在 `CULTIWAY_DEV_OUTPUT/contact-accuracy/<分段>`。容差及开发调用见[作用形状精度与调试轮廓](../../Doc/技能系统全面重构/作用形状精度与调试轮廓.md)。

### 7.11A　出行途中使用本人双门

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_travel_construction.py`。复用现成世界，给已有出行任务安排有效的友城目的地，由游戏自己选择已学能力。核对实际落点和费用、落地后继续步行、预计出口危险时跳过、出行任务取消时中止及下一次出行可重新使用。日志在 `CULTIWAY_DEV_OUTPUT/travel-construction`，见[出行实施与证据](../../Doc/技能系统全面重构/构造法术的日常活动.md#出行实施与证据)。

### 7.11B　打坐时自主凝神

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_meditation_construction.py`。复用现成世界，用原打坐任务核对已学术的自主支付与实际接触、人物吸收与地块扣除、低供给跳过以及到期恢复。日志在 `CULTIWAY_DEV_OUTPUT/meditation-construction`，见[凝神实施与证据](../../Doc/技能系统全面重构/构造法术的日常活动.md#凝神实施与证据)。

### 7.11C　原装备维修任务用修复术

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_maintenance_construction.py --section normal`，核对原任务自主选术、正式支付、实际材料消耗、原铁甲修复与缺料／满耐久跳过。`--section cancel` 核对接触前脱下、归还原库存材料以及重新穿回后可继续维修。复用同一现成世界和铁甲样本；记录保存在 `CULTIWAY_DEV_OUTPUT/maintenance-construction/<分段>`，见[维护实施与证据](../../Doc/技能系统全面重构/构造法术的日常活动.md#维护实施与证据)。

### 7.11D　自然领悟可用的日用术

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_daily_use_creation.py creation`。先核对固定创法资料能重现工作、救灾、出行、修炼及维护成品，再让原打坐任务产生自然灵感、正式领悟并自行付费使用凝神术。记录和正式待办档在 `CULTIWAY_DEV_OUTPUT/daily-use-creation/creation`。

用 `dev.py stop` 退出该游戏，再用 `dev.py start` 启动新进程。运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_daily_use_creation.py restart --state <creation输出目录>`。旧档默认读取 `~/.local/state/cultiway-dev/evidence/propagation-creation-d2/creation`，可用 `--legacy` 指定该历史证据的副本；它必须包含规则 33 的真实等待世界和当时记录的成品。新旧资料分别恢复并授予一次，新学修复术再由原维修任务付费使用和保存。见[自然领悟与实测](../../Doc/技能系统全面重构/构造法术的日常活动.md#自然领悟与实测)。

### 7.20　实际身体、器物与步法生成近身战技

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_martial_study.py prepare`。入口复用一个原版世界与同一把铁剑，核对空手、剑刃和剑尖生成不同结构，真实命名足参与创作，以及正式学习、出手费用、位移、接触、收势与缺少器物时的拒绝。输出正式等待档在 `CULTIWAY_DEV_OUTPUT/martial-study/prepare`。

用 `dev.py stop` 确认退出后再启动，以 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_martial_study.py restore --state <prepare输出目录>` 核对新旧冻结资料和一次授予。旧档默认读取 7.11 留下的实际规则 34 等待世界，可用 `--legacy` 指定该证据目录副本。

### 7.21　确定版本的战技传授、操练与自主出手

在独立游戏运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_martial_practice.py practice`。默认取 7.20 留下的正式世界和原剑尖战技，也可用 `--source <7.20实战输出目录>`。复用一个城市，走原版写书、入库和读书，再检查原训练任务的出手、付费、收势、中途退出和实际使用经历。输出位于 `CULTIWAY_DEV_OUTPUT/martial-practice/practice`。

用 `dev.py stop` 退出后重新启动，在新进程运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_martial_practice.py restore --state <practice输出目录>`。恢复书本、已学关系、动作和使用经历后，让学习者在真实交战中自主决定是否用该战技。

### 8.06 从地波到留置、正式保存与回收

在独立游戏中通过 `dev.py exec --timeout 235 -- python3 Scripts/SpellDemos/verify_propagation_residue.py ui` 从 5.18 的实际地波输入开始，在万法阁编辑两招。默认沿用当时通过的 `ground-route-a/passed/definition.json`；可用 `ui --ground <definition.json>` 指定通过 `verify_ground_route.py --section arrival` 导出的输入。

接着运行 `verify_propagation_residue.py world --state <ui输出目录>`，用窗口成品验证断路没有留置、接回地面后同招在实际远处留下独立材料，并保留正式档。退出并另起游戏进程，运行 `verify_propagation_residue.py restart --state <world输出目录>`，检查成品、留下的材料和原支付份额，实际回收后重新保存。三段分别通过 `dev.py exec` 运行；结果目录名为 `propagation-residue`，一段失败时只补对应段。

### 8.07 材料改造、实际伤害与独立染色

通过独立游戏运行 `dev.py exec --timeout 235 -- python3 Scripts/SpellDemos/verify_matter_edit.py ui`，在万法阁保存只染色和真实换材两份成品。接着用 `world --state <ui输出目录>` 验证原术和仅染色成品的火伤、换材成品冻结前后的水伤与冰伤、实际表面与留置。退出并重新启动独立游戏，再用 `restart --state <world输出目录>` 打开正式档。输出目录位于每次启动的 `output/matter-edit` 下，各段结果单独保留。


### 8.08 多项改造与冲突定位

启动独立游戏后运行 `dev.py exec --timeout 210 -- python3 Scripts/SpellDemos/verify_process_conflicts.py ui`，验证三层部位上的冲突、两个跳转入口、撤销和重新打开。接着把返回的 `output` 传给 `world --state <输出目录>`，验证同一成品的扣费、普通声明和实际依赖次序及正常结束。`save` 可单独补跑保存与重开。首次实际验证保留了点错页签的失败记录，保存与施放由随后两段分别补齐。


### 8.09 同一份构造预览与施放

启动独立游戏后，使用 `dev.py exec --timeout 210 -- python3 Scripts/SpellDemos/verify_compiled_use.py --section values` 验证报价与正式选点。`--section preview` 复用同一世界和水剑，另验窗口的暂停、重播、冻结画面、实际接触轮廓、无世界副作用和关闭后的正常施放。两段分别输出证据。窗口播放采用选定时的地形及人物姿态；由命中、格挡或世界写入触发的后续需要实战确认。

### 8.10 改造窗口的资源与输入提示

用上面的同一入口改传 `--section diagnostics`，复用现成世界和水剑，检查必选人物提示、公共搜索、缺失形体定位、中英文显示，以及换回原形体后的恢复。输出至独立游戏当次的 `output/editor-inputs-810`，含复验结果和实际窗口截图。

### 8.11 构造收藏、分享和修订

在独立游戏中运行 `dev.py exec --timeout 210 -- python3 Scripts/SpellDemos/verify_construction_collection.py`。使用水剑验证万法阁收藏、剪贴板导入导出、编号冲突、独立副本、替换、重开编辑，并输出正式世界档。记下输出里的 `output` 目录；用同一环境先 `stop` 再 `start`，将该目录传给同一命令的 `--reload-from`，核对共同编号、修订、掌握、冷却和本机收藏。冷启动输出在 `output/construction-collection-reload`，同时从普通世界视图拍摄中文收藏清单和英文导入确认，并记下读取报告。冷启动段完成后会从测试收藏移出第二个样例，世界法术仍保留。

### 8.12 万法阁的列表边界和窗口关闭

通过独立游戏运行 `dev.py exec --timeout 210 -- python3 Scripts/SpellDemos/verify_wanfa_window_edges.py --source <8.11保存段的输出目录>`。复用该世界中的水剑，查看空收藏、长目录末项、长名搜索、960×540 下的六个编辑页，以及反复打开关闭、释放预览与切世界。全部结果位于当次 `output/wanfa-window-edges/all`，可加 `--section catalog` 或 `--section lifecycle` 只复验受影响段。

### 8.13 战技部位、握持与过程改造

部位与握持批次：在独立游戏中运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_martial_binding.py`。复用原世界和一把铁剑，走万法阁的徒手左右换手、持械握点与接触时段编辑、非法区间拒绝、撤销保存和重开。验证同时记录实际握持位置、剑尖生效区间和动作控制归还。如果只需补施放与恢复，传入 `--cast <窗口输出的 edited.json>` 即可，其余验证不会重跑。阶段、伤害和正式世界存读集中到 8.13 整体验收。

步法与发力批次：在同一独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_martial_footwork.py`。可加 `--basis <部位与握持批次的 edited.json>` 使用上一批实际保存的成品。检查接地足滑动被拒绝、左右足逐点抬脚、撤销重做、平面曲线与发力参数保存重开，再核对换步、单次攻击接触和收势归还。失败时用 `--cast <本批输出的 edited.json>` 单独复验施放，保留前段的中英文画面与结果。

阶段与接触条件批次：用 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_martial_process.py --section editor --basis <步法批次的 edited.json>` 核对姿态改名、接触和收势引用、阶段以及保存重开。`--section cast --basis <阶段批次的 edited.json>` 只复用窗口成品，确认费用不足与扣费、不可移动目标拒绝、真实抓握跨过攻击间隔并归还控制。后续分段沿用正式付费修订：

- `--section strike --basis <cast 输出的 paid.json>`：确认剑尖在后段的实际伤害，并在下一次动作用着身体时保存。
- 在同一独立环境退出重启后，`--section restore --restore <strike 输出目录>`：核对完整修订、原剑、剩余冷却，验证再次支付和归还身体。
- `--section film --basis <cast 输出的 paid.json>`：复用正式动作的连续取景，记录画面与事实并编码视频。铁剑格挡仍使用 `verify_body_guard.py --section weapon`。

### 8.14 身体、器物提示与跨形态改造

在独立游戏中运行 `dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/verify_body_form_inputs.py --section inputs --source <8.13 剑尖段目录>`，确认万法阁用中英文说明缺剑，并在装回同一把剑后恢复预演。`--section transform` 复用同一个 `--source`，验证形体缺失接触面时拒绝写入、保存独立风刃成品、再次打开。`--section cast --basis <transform 输出的 saved.json>`（仍要传 `--source`）从窗口成品直接复验实际握持、扣费、风刃离手命中和身体控制归还。每段的 `result.json`、`checks.json` 及窗口截图在当次 `output/body-form-<分段名>-814`，施放段可以独立重跑。

### 9.02 主体与视距

在独立游戏运行 `dev.py exec --timeout 245 -- python3 Scripts/SpellDemos/verify_form_detail.py --appearance all`，复用同一世界和修士，从正式已学入口拍摄灵盾、水幕、剑气、雷枪、土脊和炎陨的近、中、远景。`--appearance meteor` 只补验炎陨，另核对真实落点仅结算一次、火体按原时限撤去。截图、输入和检查结果保存在当次 `output/form-visibility-*`；画面要对照落点人物查看。

炎陨的连续动作使用 `dev.py exec --timeout 245 -- python3 Scripts/SpellDemos/verify_form_detail.py --appearance meteor-motion`。同一落点依次核对正常命中和再次施放落空，保存下坠、触地、火肩展开、余焰四张图以及真实作用范围和时间记录。

### 9.03 连续动作与收势

`demo_spell.py` 和 `demo_all.py` 的 `--motion-trace` 按实际录像帧额外记录共同姿态、朝向、局部动作、阻挡与身体接管，也记录近身伤害真正出队后的扣血。演示读取仓库现成的 `world-format-1` 夹具；缺少地图时直接停止，避免给随机世界贴上相同的标签。复验仍从 `dev.py exec` 进入独立游戏。以下是录像入口示例；`--output` 可改为本次验收目录。

```bash
python3 Scripts/SpellDemos/demo_all.py --filter FlameMeteor,EarthWall --cases base --view native --speed 1 --duration 7 --fps 40 --zoom 10 --motion-trace --keep-frames --output artifacts/skill_rebuild/motion/field
python3 Scripts/SpellDemos/demo_spell.py PalmStrike --cases base --view native --speed 1 --duration 1.4 --fps 50 --zoom 2.2 --motion-trace --keep-frames --output artifacts/skill_rebuild/motion/palm
python3 Scripts/SpellDemos/demo_spell.py SwordQi --cases trajectory --trajectory SineWave --view native --speed 1 --duration 2.5 --fps 40 --zoom 10 --motion-trace --keep-frames --output artifacts/skill_rebuild/motion/sword
python3 Scripts/SpellDemos/demo_spell.py TidalWave --cases base --view native --speed 1 --duration 1.8 --fps 50 --zoom 10 --motion-trace --keep-frames --output artifacts/skill_rebuild/motion/tide
```

`result.json` 的 `start` 是首帧世界时间，`motionFrames` 的时间相对首帧，实际接触和伤害仍以原世界时间记录。时间线用于对齐实拍，施放成功不能替代画面验收。

### 9.04 线状效果与表面接缝

在独立游戏运行 `dev.py exec --timeout 260 -- python3 Scripts/SpellDemos/verify_visual_cleanup.py --section materials`，复用正式水幕、岩脊及雷矛，保存近远和三维斜视。用 `--section seams` 比较水面泡沫及岩面在展开坐标两侧的真实输出。复用 `verify_chain.py --section appearance` 核对连锁雷击。近战御器随 10.04 改用实际兵器外形和下方的新复验入口。

短片复验：`dev.py exec --timeout 145 -- python3 Scripts/SpellDemos/demo_spell.py WaterCurtain --cases base --view native --zoom 22 --fps 15 --duration 2.2 --output <本次验收目录>`。

9.05 的施放与命中检查使用 `dev.py exec --timeout 225 -- python3 Scripts/SpellDemos/verify_material_feedback.py --section weapon|spell|steam`（每次选一个段）。`weapon` 复用木剑御器，观察真实生命变化、局部碎屑、原版攻击对照和退场；`spell` 走仍保留的旧执行入口，在混沌弹上区分主体薄雾与通用覆盖；`steam` 对照隔开的水火与实际减水的接触。每段保存原版及 Worldfall 画面、脚本输入和检查结果。

9.06 粒子复验沿用同一入口，每次用 `--section` 选择一段：`travel`（混沌弹没有掠地粒子，冰枪仍冻结途经水面）、`weapon`（木剑碎屑的位置、方向与真实扣血）、`surface`（守护状态粒子的暂停、换视角、结束与换世界）、`terrain`（正式排水只在实际变化的地块生成下落水珠）。命令格式：`dev.py exec --timeout 225 -- python3 Scripts/SpellDemos/verify_material_feedback.py --section travel`。

9.07 声音与节奏复验：在独立游戏运行 `dev.py exec --timeout 230 -- python3 Scripts/SpellDemos/verify_combat_feedback_907.py --section weapon`，比较同一御器的木、铁受击声、原版普攻和免疫；必要时用 `--material wood` 或 `--material iron` 单独补验。`--section landing` 对照同一坠石在空中结束和真实落地。`--section clash` 对照同一木障挡住与打穿，核对实际声音实例、两视角共用的短停碎面、震动和恢复。只补破防可加 `--part break`。每段保留脚本输入及结果；交锋另留两视角的接触帧。

9.08 主体及残留去向：`dev.py exec --timeout 230 -- python3 Scripts/SpellDemos/verify_visual_endings_908.py --section motion`。复用固定与追随路线对照移动目标；另选 `block` 核对攻体从共同画面移除、剩余木障正常退场，`interrupt` 核对来源失效后延迟攻体不会出现，或 `material` 核对流水凝冰和有限冰体的世界交接。各段留正式施放结果和代表帧。

9.09 A 共用形体换材：`dev.py exec --timeout 230 -- python3 Scripts/SpellDemos/verify_form_combinations_909.py --section catalog` 读游戏实际加载的形体全集，核对两端表面并保留部件和变化资料；用 `--section surfaces` 检查同一正式施放原材、真正换材、换回及正常退场，保存实际画面。独立冰体交接复用 9.08 的 `--section material`。

9.09 B 共用形体布局和轨迹：同一入口使用 `--section layouts`，在复用世界中正式施放剑列和水鲸列，比较转弯前后的部位间距、实际作用区和自然退场，记录原版与 Worldfall 画面。

9.09 C 共用形体视距：同一入口使用 `--section scale-meshes` 核对游戏实际加载的全部形体、边界和顶点资料；`--section scale-frames` 按结构保存每件的原版三档静帧；`--section scale-worldfall` 保存有风险及各类代表的斜视静帧。后两段可用 `--forms warhammer,water_wave` 限定取景；原版可再加 `--full-reference` 补拍同距离的完整网格作对照。报告记录实际绘制使用的等级及网格，投影面积预警须结合画面判断。

### 9.12 金系剑气与剑幕

在独立游戏的 `dev.py exec --timeout 225 --` 后运行 `python3 Scripts/SpellDemos/verify_blades.py --section sword`，核对剑气锋口内外命中、支付和结束。`python3 Scripts/SpellDemos/verify_sword_curtain.py` 核对十五把剑的画面与拦截面、阵内外来弹及原时点退场。补测交锋及退场可加 `--section contact`，仅补退场用 `--section exit`。

常速录像沿用单招入口：

```bash
python3 Scripts/SpellDemos/demo_spell.py SwordQi --cases base --scene single --view native --realm 2 --speed 1 --zoom 9.5 --duration 1.55 --fps 22 --motion-trace --keep-frames --output <剑气原版输出目录>
python3 Scripts/SpellDemos/demo_spell.py SwordCurtain --cases base --scene shield --view native --realm 2 --speed 1 --zoom 12.5 --duration 5 --fps 22 --motion-trace --keep-frames --output <剑幕原版输出目录>
```

Worldfall 使用已加载其模组的独立游戏，改用 `--view worldfall` 和独立输出目录，剑气缩放为 `10.5`。剑幕迎击场景实际发射石弹；判断以拦截记录及剩余容量为准。分别保留四段影片、实际时点、接触及阶段原帧。


### 9.14 水系潮浪与贴地后流

在独立游戏用 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_water_current.py` 核对后流分流、画面干湿边界、正式施放命中和自然退场。潮浪单独复验使用 `verify_ground_waves.py --section tidal`。

贴地后流的常速录像走原有构造入口，沿用演示缓坡，不改技能参数：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/鲸散守恒后流.json --target-offset 16 0 --terrain-slope 2 --view native --speed 1 --duration 5.7 --zoom 16 --fps 18 --keep-frames --output <后流原版输出目录>
```

Worldfall 改用 `--view worldfall` 和独立输出目录。检查 `result.json` 的扣费、真实接触、水面出现后的水量及末帧退出；画面看水鲸接成后流、沿坡铺开和薄边淡去。原版初稿和正式两视角原片分别保存。

### 9.15 火系移动火体与附着燃烧

在独立游戏运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_fire_presentation.py --section travel`，验证原火身接触面、正式支付、正面命中、侧外无伤与到期退出。改为 `--section burning`，验证真实点燃、烧损、冷却后剩余木料，以及熄灭和退场。两段可独立复验。

常速录像复用构造演示入口，每段通过 `dev.py exec` 分开运行：

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/离体炎流.json --isolate-target --target-offset 16 0 --view native --speed 1 --duration 3.5 --zoom 22 --fps 20 --keep-frames --output <移动火体原版目录>
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/点木流火.json --opponent-construction Doc/技能系统全面重构/构造输入/可燃分枝.json --isolate-target --target-offset 20 0 --view native --speed 1 --duration 7 --zoom 16 --fps 18 --keep-frames --output <附着燃烧原版目录>
```

Worldfall 使用 `--view worldfall` 和另一输出目录。附着燃烧看 `result.json` 中实际的传热、烧损和末帧火体记录；这两段拍的是自然燃尽，水冷却由独立复验覆盖。

### 9.16 土系地脉与土墙

在独立游戏用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_ground_waves.py --section earth` 复验原已学「地脉」。检查岩片是否随自身接触面抬升和落稳，扣费、正面命中和侧外无伤是否维持原规则，以及停止推进后是否只剩无作用的碎落。`verify_wall_migration.py --section earth-motion` 复验「土墙」成形期阻挡、原时限、碎块阶段恢复通行与自然撤除。两段分别运行。

常速录像各通过 `dev.py exec` 分开运行。下面是原版视角；Worldfall 改成 `--view worldfall`，并使用另一个输出目录。

```bash
python3 Scripts/SpellDemos/demo_spell.py EarthRidge --cases base --scene single --view native --realm 1 --target-offset 6 0 --speed 1 --duration 1.65 --zoom 16 --fps 30 --motion-trace --keep-frames --output <地脉原版目录>
python3 Scripts/SpellDemos/demo_spell.py EarthWall --cases base --scene single --view native --realm 2 --target-offset 8 6 --speed 1 --duration 4.75 --zoom 17 --fps 23 --terrain-slope 2 --motion-trace --keep-frames --output <土墙原版目录>
```

`motionFrames` 中的形体和地面阻挡记录用来定位落稳、断裂和撤除。固定镜头原片供画面验收，命中与步行通行分别以上面的实机复验为依据。

### 9.17 冰壳与厚浪冻融

在独立游戏里用 `dev.py exec` 分别运行 `python3 Scripts/SpellDemos/verify_ice_presentation.py --section shell` 和 `--section thaw`。前一段对照投送落空、未合拢时的空隙、合拢后阻挡及受击后的通行与退场；后一段对照同一浪体的实际物态、形状时钟、位置和体量。输出 `checks.json` 与 `timeline.json`。

常速录像也通过 `dev.py exec` 分开运行。Worldfall 改成 `--view worldfall`，并使用另一个输出目录。

```bash
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/触生冰壳.json --opponent-construction Doc/技能系统全面重构/构造输入/破冰验证来袭.json --isolate-target --target-offset 6 0 --view native --speed 1 --duration 4.45 --zoom 14 --fps 22 --keep-frames --output <冰壳原版目录>
python3 Scripts/SpellDemos/demo_all.py --construction Doc/技能系统全面重构/构造输入/厚浪凝冰与解冻.json --isolate-target --target-offset 8 0 --view native --speed 1 --duration 4.7 --zoom 17 --fps 22 --keep-frames --output <冻融原版目录>
```

查看每段的 `result.json`，确认实际命中、对手支付与交锋时点，或同一浪体的水、冰、水三个阶段。通行与形状是否锁定仍以对应实机复验为准。

### 9.18 落雷与水面传导

用独立游戏运行 `dev.py exec -- python3 Scripts/SpellDemos/verify_thunder_strike.py`，核对正式付费、实体雷瓣收束、下落、人物接触后的余电，以及走出落点后的空落和退场。保留 `checks.json` 与 `timeline.json`。

用 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_conduction_visuals.py --section native` 复用实测水道，核对确认后的水格和三批有厚度的雷脊、原版与 Worldfall 共用网格、冰面断路与回收。

聚雷录像用 `demo_all.py --construction` 选择 `聚雷下击.json`（建议目标偏移 `5 0`、缩放 `12`），分别使用 `--view native` 和 `--view worldfall`。水道录像在上述水路复验命令上加 `--record native` 或 `--record worldfall`，原速对照结冰前后的两段水路。活动水体另用 `流水载雷.json`（偏移 `7 2`、缩放 `17`）演示。

### 9.19 风与煞风

在独立游戏中，用 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_wind_combat.py --section carry` 核对空地、本来就在地面的同一件物品卷起落地、范围内外的人物受力和恢复拾取。`--section erosion` 核对实际护甲分次受损、离开有效区后停止、余痕退出和再次施放。`--section trial` 从原劫程入口核对无主风柱、指定人物承伤与清理，复用既有天劫保存验证结果。

两类常速录像分别给 `carry` 和 `erosion` 加 `--record native` 或 `--record worldfall`。输出目录记录实际成功事件、姿势截图和 `demo.mp4`；相同材料的空场不生成装饰性携物。

### 9.20 毒息与毒潭

在独立游戏中运行 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_poison_combat.py --section breath`，核对毒息推进、直接接触和范围传播的区别、真实毒伤、解毒以及自然消退。`--section pool` 核对分次腐蚀、离开作用区、无甲命中和再次施放。

两段各可加 `--record native` 或 `--record worldfall`，从同一个正式学习和施放入口录制常速画面，并记录付出的法力、传播结果或原甲的耐久变化。

### 9.21 阴与影

在独立游戏中运行 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_yin_combat.py --section intake` 查看实际接触、两边灵气变化、离场与来源失效。`--section shadow` 查看原对应人物的牵引、短限制和立影回落，也核对相邻人物不会承接联系。每段可加 `--record native` 或 `--record worldfall` 录制同一正式施放入口的常速原片。

### 9.22 阳与生命

在独立游戏中运行 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_sun_life.py --section solar`，核对原曜阳光束、阳罡护盾、箭与阴术撞击。`--section life` 施放可编辑的护生修复，比较同场受伤人物、活木和不适用对象，核对中断后的实际数据。两段可加 `--record native` 或 `--record worldfall` 录制常速实机原片。

`--section life-world` 用原复苏场的正式已学入口检查法力、伤员、同一活树和换世界后的清理。此段无需录制。

### 9.23 两仪渡气

在独立游戏中运行 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_polarity.py --section balance`，核对两端实际扣收、反向、比例持平、还有下一次作用机会时离场和再用。该段可加 `--record native` 或 `--record worldfall` 录制常速过程。`--section editor` 核对两种语言、无效额度拒绝及覆盖重开；`--section legacy` 用旧玄阴汲流抽查单向扣收。

### 9.24 混沌解构

在独立游戏中运行 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_entropy.py --section legacy`，用原已学入口核对侵蚀场的收费、伤害间隔、范围及转移空隙。`--section clash` 让「散相解构」与「解构验证联盾」实际对撞，核对被触部位、无害碎片、仍有作用的支座及本方剩余材料。两段均可加 `--record native` 或 `--record worldfall` 录制常速实机过程。

### 9.25 五相周流

在独立游戏中运行 `dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_five_phases.py --section main --record native`。复用同一块场地；用一招真实来刃打开岩壳，随后对照各相的施放时点、扣血、实际材料和水面边界。可将视角改为 `--record worldfall`，也可改为 `--section reordered` 检查水相之后先金后火。

`--section interrupt` 检查未受拦截、岩壳被打尽和木相打空；`--section quench` 让外来火命中实际水层，核对后续收到的余量；`--section restore` 检查正式写读档；`--section editor` 核对表单添加交接、纠正来源和中英文重开。各段单独运行即可。

### 9.26 折景渡物

在已启动的独立游戏中，用 `dev.py exec --timeout 145 -- python3 Scripts/SpellDemos/verify_fold_gate.py --section view` 核对两端实际取景、地块更新、同一飞剑穿门与关门。改用 `--section blocked` 复用既有人物暂存场景，只查真实受阻、归还控制及对端画面撤去。`--section visual --view native` 或 `--section visual --view worldfall` 只对照实际画面；截图使用已安装 Worldfall 的同一环境，前者先退出三维观察。每次证据单独输出。可编辑资料见 [折景渡物](../../Doc/技能系统全面重构/构造输入/折景渡物.json)。

### 9.26 借时催枯

在已启动的独立游戏中，用 `dev.py exec --timeout 140 -- python3 Scripts/SpellDemos/record_process_time.py --imprint-check --state "$HOME/.local/state/cultiway-dev/evidence/process-time-c2/use"` 复用时序创作的正式已学成品。对照两份实际木料，检查只在选中材料暂停、加速时显示印片，恢复常速撤去，扣除的材料与灵力保留。可编辑资料见 [借时催枯](../../Doc/技能系统全面重构/构造输入/借时催枯.json)。

### 9.26 魂魄替负

在已启动的独立游戏中，用 `dev.py exec --timeout 140 -- python3 Scripts/SpellDemos/verify_soul_pact.py` 检查真实双方、免伤与额度实际扣除、耗尽与死亡后的收束，以及正式保存与重开。可编辑资料见 [魂魄替负](../../Doc/技能系统全面重构/构造输入/魂魄替负.json)。

### 9.26 D 照影传讯

在已启动的独立游戏中，用 `dev.py exec --timeout 145 -- python3 Scripts/SpellDemos/verify_observed_signal.py` 验证真实交战、目标离场后仍传旧位置、收件者自己较新的所见优先，以及过期后的失效和撤去。旧告警复用 `verify_direct_signal.py`。可编辑资料见 [照影传讯](../../Doc/技能系统全面重构/构造输入/照影传讯.json)。

### 9.26 原速录像

使用已安装 Worldfall 的独立游戏，各段通过 `dev.py exec` 分开运行。

- `dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/record_relations_926.py --case gate --view native`：`--case` 也可用 `pact`、`signal`；三维观察用 `--view worldfall`。各次都有自己的原帧、实机结果和影片。
- `dev.py exec --timeout 325 -- python3 Scripts/SpellDemos/record_process_time.py --imprint-film --state "$HOME/.local/state/cultiway-dev/evidence/process-time-c2/use"`：三维观察额外加 `--worldfall`；通过成片原帧核对被选木料及未选木料。

八段成片及复验证据总索引：`~/.local/state/cultiway-dev/evidence/world-relations-926/delivery.json`。

### 寸掌葬岳（9.27）

在已启动的独立游戏中运行 `dev.py exec --timeout 145 -- python3 Scripts/SpellDemos/verify_bury_mountain_palm.py`。复用原掌击场地，核对真实前后两处命中、岩片和作用角点、遇水隙断开，以及落稳后再次施放。可编辑资料见 [寸掌葬岳](../../Doc/技能系统全面重构/构造输入/寸掌葬岳.json)。

同一入口加 `--section save` 核对「横断」的侧脊、两份正式构造保存及重开编辑。`--section native` 或 `--section worldfall` 录制相应视角下的原招；两次均确认实际掌击、后排命中和回落后才编码。


### 剑冢开天（9.28）

在已启动的独立游戏中运行 `dev.py exec --timeout 145 -- python3 Scripts/SpellDemos/verify_sword_grave.py`。读取[剑冢开天构造](../../Doc/技能系统全面重构/构造输入/剑冢开天.json)，检查残兵起出、接缝扣合、合拢期无伤、正式落斩和收尾重放。`--section finish` 单独复验落斩至重放；`--section visual` 只采集原版的起兵、扣合、落斩和下沉截图。

- `--section fracture` 核对实际碎刃如何继承运动、交接余力、命中侧靶及落稳。`--section blocked` 验证强盾耗尽主剑后仍连接的刃不会攻击；`--section interrupted` 核对中断时两块外刃实际下落、无伤、落稳和清理。
- `--section save` 核对原招与[逆锋](../../Doc/技能系统全面重构/构造输入/剑冢开天·逆锋.json)的扣合次序、材质和脱刃方向，保存再打开，确认两份结构和人物的学习记录都保留。
- `--section native` 和 `--section worldfall` 分别录制同一招实战。每段先核对真实重斩、碎刃命中、回落和结束，再按实际经过的世界时间编码为 `demo.mp4`。

### 天炉倾海（9.29）

在已启动的独立游戏中运行 `dev.py exec --timeout 115 -- python3 Scripts/SpellDemos/verify_sky_furnace.py`。使用[天炉倾海构造](../../Doc/技能系统全面重构/构造输入/天炉倾海.json)，核对实际炉口、离口速度、三次触地、分流、总量和自然结束。`--section launch` 只验证出流至落地，`--section upright` 只验证保持直立及再施放，`--section visual` 采集原版开发截图。

- `--section cooling` 复验落地后先结壳、后到热流接触破壳、热量与材料去向，以及冷透后撤掉伤害面。`--section cutoff` 验证施术者死亡后只保留已离炉的熔料。`--section damage` 分别验证空中的流束和触地后的液层确有伤害。
- `--section save` 比较原招和[偏口](../../Doc/技能系统全面重构/构造输入/天炉倾海·偏口.json)的实际落点与铁壳位置，通过正式存档重开，再改口复放。`--section native` 和 `--section worldfall` 分别录制真实抬炉、出流、结壳、破裂和熄灭，按游戏内经过的时间输出 `demo.mp4`。录像使用 `dev.py exec --timeout 180`。

### 天鲸覆城（9.30）

在已启动的独立游戏中运行 `dev.py exec --timeout 115 -- python3 Scripts/SpellDemos/verify_sky_whale.py`。使用[天鲸覆城构造](../../Doc/技能系统全面重构/构造输入/天鲸覆城.json)，核对起跃姿态与实际接触、正式扣费、目标离地和侧边未命中、落地恢复与再次施放。输出原版开发截图 `breach-native.png` 以及实际接触和高度 `trace.json`。默认只运行起跃托举。

`--section spill` 验证整身翻转、腹部挂点朝下放水、实际触地与水量；`--section cutoff` 验证来源消失后只有已离体的水继续；`--section stones` 记录地面接触、三块碎石的升空和落地。分段可独立运行。

- `--section impact` 检查上升托举、下压伤害、触地后只传递剩余水量、实际绕流和自然退场。接触高度以命中的地块为准，水面分流仍使用沿地面插值得到的高度。
- `--section save` 通过正式存档比对原招与[逆卷](../../Doc/技能系统全面重构/构造输入/天鲸覆城·逆卷.json)的实际落点、派生关系、可编辑数据及学习记录，再改回前腹角度复放。完整存读用 `dev.py exec --timeout 255`；已有正式世界和落点记录时可加 `--resume-save <上次输出目录>`，只核对重开与改角后的复放。
- `--section native` 和 `--section worldfall` 各录一段连续实机录像，包含起跃、卷石、翻身、垂流、撞散、奔流和收尾，按游戏时间输出 `demo.mp4`。录像用 `dev.py exec --timeout 180`。

### 森罗倒生（9.31）

在已启动的独立游戏中运行 `dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_hanging_tree.py --section growth --view worldfall`。使用[森罗倒生构造](../../Doc/技能系统全面重构/构造输入/森罗倒生.json)，观察树冠先落定、主根和侧根各自生长，对照实际根尖在高低地面先后钉住、保持原位并清场。`--section short --view native` 只检查缩短侧根后确实够不到地面，另两条根与自然收尾照常。`trace.json` 保留实际接触，`growth` 另保存两张所选视角的截图。

抓持阶段用 `--section grip --view worldfall` 检查两侧根枝先接地再实际抓持、推挤、扣血及正常结束复放。`--section cut`、`escape`、`source-loss`、`ungrounded` 各用同一场地核对断根、被外力拉开、来源死亡及短根不伤人；这些入口可用 `--view native`。`grips.json` 记录真实几何、接触、控制和解除时点。

- `--section save` 对照原招和[偏庭](../../Doc/技能系统全面重构/构造输入/森罗倒生·偏庭.json)的接地顺序与抓持，正式存档重开，再改回西根长度复放。用 `dev.py exec --timeout 250`。
- `--section native` 和 `--section worldfall` 各录一段从倒悬到完全回收的常速实机录像，输出 `demo.mp4`、预览及实际过程记录。用 `dev.py exec --timeout 175`。Worldfall 必须实际启用正常绘制路径，进了后备绘制会提示验证未开始。

### 山河叩首（9.32）

在已启动的独立游戏中运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_bowing_mountains.py --section ground`。复用固定世界，正式施放[山河叩首](../../Doc/技能系统全面重构/构造输入/山河叩首.json)，对照两片岩体错时抬起、绕底边翻落、真实接地、翻起褶皱和断块落稳；还验证扫到的目标扣血、片间安全和术后通行。保存 `checks.json` 及抬起、俯压、落稳的原版截图。`--view worldfall` 可切换观察角度。

- `--section raised`：只抬高同一构造，验证悬空翻落不会触发地面结果。
- `--section interrupted`：近片拆开后令施术者死亡，验证远片停止，近片碎块仍独立落稳。
- `--section save`：核对无许可、显式拒绝和许可一格的真实地貌结果与岩量收支。正式存档后复核原招、[偏脊派生](../../Doc/技能系统全面重构/构造输入/山河叩首·偏脊.json)、人物学习状态和地形。输出 `material.json` 与实际存档。用 `dev.py exec --timeout 225`。
- `--section native` 和 `--section worldfall`：分别录制原版和三维旁观的常速同招全过程。输出 `demo.mp4`、预览图和 `timeline.json`。三维录像同时核对正常绘制路径和实际岩体提交。
- `--section delivery`：在同一游戏进程依次运行接地、中断、存读和双视角录像。用 `dev.py exec --timeout 480`；每段结果独立保留。

### 霜花葬形（9.33）

独立游戏启动后，运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/verify_frost_flower.py`。地面段读取[霜花葬形构造](../../Doc/技能系统全面重构/构造输入/霜花葬形.json)，复用固定世界。核对正式出手与支付、薄霜先后抵达、水道断路、实际霜厚、人物不冻结和自然消退。输出 `checks.json`、`result.json` 和俯视图 `ground-frost.png`。其余分段见[交付条件](../../Doc/技能系统全面重构/霜花葬形实战表现.md)。

`--section coating --view worldfall` 使用同一场地的一名持剑着甲人物。先检查正式招式在实际接触后留霜、到期消退，再以同一附着规则限定甲片和持物，核对擦空、移动和换装。输出 `frost-cast.png`、`frost-equipment.png`、`frost-moved.png`、`frost-replaced.png` 及检查记录。

`--section enclosure --view worldfall` 复用同一场地和同一受术者，依次检查开口、闭合冻结、提前走出、闭环外侧和战力抵抗。它还检查同一轮围合不会不断补上冻结、施放结束后实体屏障会消失。输出 `petals-open.png`、`petals-closed.png`、`petals-escaped.png` 及检查记录。 只复验后三组对照时，追加 `--enclosure-part contrast`。

`--section bloom --view worldfall` 检查同一冰瓣从闭合姿态盛放、真实裂成主瓣与瓣尖、碎片命中与擦空、落稳和恢复。输出 `bloom-open.png`、`bloom-fragments.png`、`bloom-settled.png`。

`--section save` 验证原招与[狭瓣派生](../../Doc/技能系统全面重构/构造输入/霜花葬形·狭瓣.json)的正式存档、还原和两种实际冻结边界，使用 `dev.py exec --timeout 185`。`--section film --view native` 和 `--section film --view worldfall` 录制从蓄霜、闭环冻结到盛放碎裂及清场的一次常速施放，输出 `demo.mp4`、`preview.png` 和 `timeline.json`。

### 雷神凿世（9.34）

在独立游戏里运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_thunder_earthbreaker.py`。用同一名受术者核对[雷神凿世](../../Doc/技能系统全面重构/构造输入/雷神凿世.json)从云腹显形、举锤到实际接地与命中，再对照侧边擦空、悬空无地面后续和退场。输出五张阶段图及实际作用记录。`--section contrast` 只补跑侧边和悬空；`--view native` 切换原版取景。

`--section eruption` 核对真实落锤、凹痕、地层抬升、岩片和雷柱各自的实际伤害及范围外对照，输出 `thunder-crater.png`、`thunder-eruption.png` 和 `thunder-rockfall.png`。`--section guard --view native` 只复验架挡及解除后重放；`--section permit --view native` 复验许可单格。

`--section save --view native` 核对原术与[偏雷派生](../../Doc/技能系统全面重构/构造输入/雷神凿世·偏雷.json)正式存读，比较两次真实撞点、压痕和雷柱，使用 `dev.py exec --timeout 215`。`--section film --view native` 与 `--section film --view worldfall` 分别录一次常速过程，输出 `demo.mp4` 与阶段时间记录。

### 影中葬身（9.35）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 150 -- python3 Scripts/SpellDemos/verify_shadow_burial.py --section grip`，确认[影中葬身](../../Doc/技能系统全面重构/构造输入/影中葬身.json)的影身从原身脚下反向站起，左右真实接触后才取得抱持。此段关闭拖拽，也对照了原身提前离开的情况。

`--section drag` 对照合法拖入和受阻停下。`--section release` 复验真实救援、挣脱、死亡和换世界。`--case rescue|escape|death|world` 可以只补跑对应分支。

`--section save` 在抱持期间正式存档，核对原术和[深折派生](../../Doc/技能系统全面重构/构造输入/影中葬身·深折.json)的学习、来历和实放。`--section variant --saved <输出目录>` 用同一档单独复验深折。重启后用 `--section cold --saved <输出目录>` 核对冷启动。

`--section film --view native` 和 `--section film --view worldfall` 各录一段原速完整过程，输出 `demo.mp4` 与 `timeline.json`。

### 镜中碎身（9.36）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 150 -- python3 Scripts/SpellDemos/verify_mirror_shatter.py --section image`。用同一人物验证[镜中碎身](../../Doc/技能系统全面重构/构造输入/镜中碎身.json)的镜框、实时反照、装备变化和映像退出。该段只开启映照。

`--section travel` 验证实碰镜面、两个许可部位按时受伤、打偏、落片和回收。`--section interrupt` 复验护盾的实际吸收以及提前击碎镜面。

`--section save` 在实际扣血后保存，读回原术与[裂冠派生](../../Doc/技能系统全面重构/构造输入/镜中碎身·裂冠.json)，等保存的冷却走完再实放。`--section restored --saved <输出目录>` 用同一档补跑读回后的施放。重启游戏后用 `--section cold --saved <输出目录>` 核对冷启动。

`--section film --view native` 与 `--section film --view worldfall` 各录完整原速过程，输出 `demo.mp4`、`timeline.json` 和真实结算记录。

### 折界千重（9.37）

在独立游戏中运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 150 -- python3 Scripts/SpellDemos/verify_thousand_fold.py --section image`。它复用同一片地图，检查三条带里的实际景物、错层、依次归位和撤销。换成 `--section contact`，核对开启时无伤、闭合边沿与实际作用同形、范围外无伤及退场。

`--section save` 在首层已造成伤害时保存，读回[原术](../../Doc/技能系统全面重构/构造输入/折界千重.json)与[折径](../../Doc/技能系统全面重构/构造输入/折界千重·折径.json)，等冷却后分别实放。`--section restored --saved <输出目录>` 可对同一档补跑读回后的施放。退出并重新启动游戏后，运行 `--section cold --saved <输出目录>`。

用 `--section film --view native` 和 `--section film --view worldfall` 分别录制同一场战斗。输出附有世界时间、每次实际切割及视频。

### 刹那千秋（9.38）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 150 -- python3 Scripts/SpellDemos/verify_moment_of_ages.py --section mechanics`，观察[原术](../../Doc/技能系统全面重构/构造输入/刹那千秋.json)的护持、铁甲、人物老化、两轮枯荣和石屋风化。`--section interrupt` 在第一轮已结算后撤销，检查后续停止及已实现的效果保留。

`--section save` 在同一次施放中保存进行中和完成后的两份正式世界。完整读回后使用[停荣](../../Doc/技能系统全面重构/构造输入/刹那千秋·停荣.json)。重启游戏后运行 `--section cold --saved <输出目录>`，核对两份档的恢复和未完成阶段不重放。

`--section film --view native` 和 `--section film --view worldfall` 录制同一场景的两种视角，并导出已经发生的世界、生命及装备变化。

### 归墟开口（9.39）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 150 -- python3 Scripts/SpellDemos/verify_returning_abyss.py --section materials`。同一场地用正式施放放置生成石块、剑和敌方材料，随后核对[归墟开口](../../Doc/技能系统全面重构/构造输入/归墟开口.json)的实际选取、拉伸、消解及原支付份额。`--section cancel` 验证撤销后仍由世界维护的散件。

`--section combat` 加入一个真实敌方术式和一件原版落地铁剑，核对交锋、牵引及落地。`--section film --view native` 与 `--section film --view worldfall` 用同一场景录制完整过程。

`--section save` 留下卷动中和退场后的两份正式档，读回后实放[狭喉](../../Doc/技能系统全面重构/构造输入/归墟开口·狭喉.json)。重启游戏后运行 `--section cold --saved <输出目录>`，检查两份档及各材料的实际去向。

### 地口行风（9.40 风）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_groundmouth_wind.py --section behavior`。复用现有地块和铁剑，核对[地口行风](../../Doc/技能系统全面重构/构造输入/地口行风.json)的槽内外受力、原物越过实际出口、落地拾取及卸压。 `--section variant` 验证[短槽斜口](../../Doc/技能系统全面重构/构造输入/地口行风_斜口.json)的朝向、尺寸、非法参数和退场。

`--section film --view native` 和 `--section film --view worldfall` 同时记录实际行为和按世界时间编码的录像。两段均使用现有地貌，实测人物和原物的放置写在脚本里。

### 明壳承阳（9.40 阳）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_solar_reservoir.py --section behavior`。验证[明壳承阳](../../Doc/技能系统全面重构/构造输入/明壳承阳.json)的种核成形、两次来袭实际入壳、延迟蓄力、裂口出光、原壳退场和护生。 `--section miss` 让来势走壳外，核对空壳无反击，并验证反射屏障不能同时重复计入吸收量。

`--section film --view native` 和 `--section film --view worldfall` 使用同一人物和地块输出原速录像。裂口、光束起点和回击力量均以实机交锋记录核对。 `--section editor` 检查开关、撤销、正式替换和中英文重开。

### 五相换城（9.40 五相）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_five_phase_district.py --section district`。验证[五相换城](../../Doc/技能系统全面重构/构造输入/五相换城.json)的岩壳宽度、壳外来势、五次实际承力、位置与余量接替、角色受击及世界材料。 `--section carrier` 让同一构造从实际原法宝的位置开始。 `--section reordered` 保留器承，调换金火两相。两种改造资料均写入各自结果目录的 `definition.json`。

对应场景可加 `--record native` 或 `--record worldfall` 输出真实来势和换相过程。 `--section editor` 验证沿用来源位置开关、撤销、保存及中英文重开。

### 鸣铗纳锋（9.40 法宝）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 162 -- python3 Scripts/SpellDemos/verify_weapon_chamber.py --section release`。用真法宝和原版落地的兵器验证[鸣铗纳锋](../../Doc/技能系统全面重构/构造输入/鸣铗纳锋.json)。输出器口与原物的位置、高度、随动和抛出记录，并检查实际护口交锋、口外无作用和拾回原物。

`--section interrupt` 在暂存时中止，核对最后位置落地和原法宝归还。 `--section editor` 核对尺寸编辑、无效输入、撤销、中英文重开。释放流程可加 `--record native` 或 `--record worldfall`。

### 连印走符（9.40 符箓）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/verify_seal_relay.py --section relay`。通过正式物品入口消耗一张真实符箓，核对[构造资料](../../Content/Skills/Attack/SealRelay.json)的三次实际命中、留印位置、余力衰减和退场。 `--section miss` 让首张纸符飞空，检查原物消费和失效请求保留，以及后续形体是否停在源头。

正常传递可加 `--record native` 或 `--record worldfall`。输出同时记录每次作用面的准确位置与伤害。

### 狐火吐息、伏首雷角刺与伏地扑（9.40 妖兽器官）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 265 -- python3 Scripts/SpellDemos/verify_beast_attacks.py --section attacks`。在同一狼形上复验吻端出火、真实雷角接触和四足起落后以前爪命中。结果记录每招的支付、命中位置、动作和身体控制归还。加 `--part breath`、`--part horn` 或 `--part pounce` 可只复验一招。

`--record native` 或 `--record worldfall` 输出对应视角的常速录像。`--section bounds` 检查火息范围外无伤、撤下雷角后角刺拒绝且不扣体力。`--section save` 将同一狼形的器官和两招学习记录写入正式世界，供整项冷启动复验。

### 瘴潮凝潭（9.40 毒术）

在独立游戏运行 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_poison_combat.py --section condense`。验证[瘴潮凝潭](../../Doc/技能系统全面重构/构造输入/瘴潮凝潭.json)的实际接触、传播、凝潭、甲衣损耗、离开液面及消退。`--miss` 复验飞空，`--record native` 或 `--record worldfall` 记录常速画面。`--save` 保留实际退场后的世界，供冷启动读回。

### 9.40 正式存读

用 `verify_weapon_chamber.py --section save` 保存器口确实暂存原剑时的世界。`verify_five_phase_district.py --save`、`verify_beast_attacks.py --section save`、`verify_poison_combat.py --section condense --save` 和 `verify_seal_relay.py --save` 分别保存落地材料、器官、铁甲和符箓结果。

重启独立游戏后，用 `dev.py exec --timeout 248 -- python3 Scripts/SpellDemos/verify_supplemental_attacks_archive.py`，通过 `--weapon`、`--district`、`--organs`、`--poison` 和 `--seal` 传入对应的输出目录。可以只复验受影响的一份档。各目录应包含 `archive/map.wbox` 和原记录 `expected.json`。

### 行根出锋、树脊留刃与坠锋留材（9.41）

在独立游戏用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/verify_reused_parts.py --section blade`，检查真正交锋后的余力和独立飞刃命中。`--section screen` 检查原地接替、第二击、地面阻挡与旁路通行。`--section drop` 检查实际落地与原材料留置。`--section miss` 验证来势未触到根段时不会启动飞刃。

三份实战构造可分别加 `--record native` 或 `--record worldfall`。`--section editor` 检查既有编辑入口、撤销及重开。`--section drop --save` 留下三份已学构造与铁刃材料，重启后用 `verify_supplemental_attacks_archive.py --parts <输出目录>` 集中读回。

### 徒手战技（9.42）

在独立游戏用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 155 -- python3 Scripts/SpellDemos/verify_unarmed.py --section upper`。同一个人形和目标复验三招发力、实际接触、支付与收势，再把直拳目标移到侧外验证落空。用 `--section palm`、`--section punch`、`--section elbow` 或 `--section miss` 可单独复验。`--section hand-edit` 在真实窗口切换伸掌和握拳，核对接触形状、撤销与正式改造重开。

`--section lower` 复验托山提膝、贴地扫足、倚身靠肩和扫足落空。可用 `--section knee`、`--section sweep`、`--section shoulder` 或 `--section sweep-miss` 只复验受影响的一段。记录包含实际支撑足、发力部位、扣血、身体位移和控制归还。

`--section archive` 从编辑器调整原肩撞的两个作用阶段、推力和短进步，保存该改造与六份原战技。重启独立游戏后，用 `--section cold --archive <输出目录>` 从正式世界恢复原人物、构造和掌握关系，并施放改造后的肩撞。

`--section palm-project` 用万法阁把原地掌击显化为追踪飞掌，检查撤销、重新打开、离手转向、命中和收费，保存正式世界。重启后用 `--section palm-project-cold --archive <输出目录>` 重新实际施展已学飞掌。

`--section film --view native` 和 `--section film --view worldfall` 分别录制六招，按实际世界时间编码成一段连续视频。`--move` 可以只补拍指定招式。拍摄用正式施放入口并核对支付、命中与收势。

### 短兵战技（9.43）

在独立游戏用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/verify_short_weapons.py`。验证真实铁剑、石剑的实际握点、刃尖位置、错装备拒绝、命中和原物归还。`--item iron` 或 `--item stone` 只复验对应器形。

`--move cut` 验证斜切选择实际前刃，`--move sweep` 验证横扫中段及转体收势。`--move miss --item iron` 验证侧移避开斜切，`--move deflect --item iron` 验证真实剑身拨开斜切，双方各自收势。

`--section archive` 使用万法阁身体页选择石剑外形和靠护手的握法，检查撤销、重开和正式写档。重启独立游戏后，用 `--section cold --archive <输出目录>` 读回原石剑与改造，再实际施放。`--section film --view native` 和 `--section film --view worldfall` 分别录制三招，可叠加 `--move thrust`、`--move cut` 或 `--move sweep` 补拍。

`--section knight --move committed` 使用原决斗家入口，在蓄势期间移动目标友方，检查按接触时位置决定重击倍率。`--move flurry` 检查同一次动作中的三段刃面接触和原倍率，`--move flurry-swap` 在首段命中后换剑，检查剩余段停止并归还身体。

同一骑士入口下，`--move guard` 和 `--move guard-hammer` 分别核对持斧和持锤的格挡，`--move counter` 核对有效受击后由实际剑刃回锋，`--move stance-loss` 核对换掉同款剑后的状态解除。

`--move repulse` 用原版铁锤核对震退的地面范围、实际扣血后的推力和硬直。`--move repulse-swap` 在斧扫起势时换掉原斧，核对中断及身体归还。

`--move bulwark` 用同一场地核对守护壁垒的友方顺序、实际减伤和到期。`--move bulwark-swap` 在壁垒展开前换掉同款斧，核对中断后的状态和身体归还。

### 长兵战技（9.44）

在独立游戏用 `python3 .agents/skills/develop/scripts/dev.py exec --timeout 165 -- python3 Scripts/SpellDemos/verify_long_weapons.py`。检查正式铁枪刺的滑握、实际枪尖命中、近身落空、两手握杆横扫及原枪归还。用 `--case strike`、`--case near`、`--case sweep` 或 `--case interrupt` 单独复验。横扫检查左手占用拒绝、两手握点和实际臂长，换器检查同款新枪也会解除旧动作。

同一入口加 `--case knight` 可走原枪骑学习与使用流程，核对一笔斗气、枪尖扣血后破甲、免伤、落空和换枪归还。需要单独补验时可用 `knight-immune`、`knight-miss`、`knight-swap`。使用 `--case knight-building` 核对原入口点名的建筑经实际枪尖接触受击。

`--case knight-study` 从破甲突刺的正式施放触发骑士自主创作，核对真实铁枪、独立报价、不同转腕姿态和实战扣血。输出待处理的正式世界档及完整预期，供新进程读回核对。

`--case knight-study-archive` 只准备同一自然变式的等待档，确认实际铁枪已进入原版物品保存列表。用 `verify_knight_archives.py restore-study --archive <输出目录>` 在新进程核对冻结资料、一次授予及实战施展。旧执行编号可在迁移前版本用同一脚本的 `prepare-legacy` 正式保存，再用当前版本的 `restore-legacy --archive <输出目录>` 读回并出手。

`--case charge` 复验破阵冲锋先命中沿途敌人、收势以及实际扣血后的侧向推开。`charge-wall`、`charge-swap` 和 `charge-building` 分别检查墙体、换枪和原入口允许的建筑。

`--case skyfall` 复验苍穹坠击的实际高度、落地时点和主次伤害。`skyfall-evade` 核对点名对象移出中心后的范围伤害，`skyfall-swap` 核对换枪后下落及归还，`skyfall-building` 核对原入口点名建筑的落点伤害。

`verify_migrated_martial_film.py --view native` 和 `--view worldfall` 拍摄 10.03 的原地掌击和九项骑士战技。在独立游戏中用 `dev.py exec --timeout 640` 启动。录像从起势前拍到收势后，以实际世界时间编码，随片记录费用、命中、格挡承力或护持。可重复传入 `--move` 补拍指定招式。

`--case press --archive <输出目录>` 用正式对练档中的人物运行架杆拨锋与持剑刺击。检查接触点相合、杆身带动持剑手、收枪解除关系、双方原物归还，以及对方没有兵器时保留体力。

`--case edit` 检查万法阁的实际握点、滑握比例、器物尺寸和普通接触面，并保存中英文画面和 `edited-definition.json`。使用 `--case range --edited <edited-definition.json 路径>` 实战核对该改造的选取距离、尖端接触以及原物归还。

`--case obstacle --part tip` 和 `--case obstacle --part shaft` 分别检查原版城墙实际挡住哪一枪段。均核对畅通时的命中、受阻后的收势与原物归还，以及拆墙后再次命中。

`--case archive` 在实际编辑器创建改造并写入原版世界档。停掉测试游戏并重新启动后，使用 `--case cold --archive <输出目录>` 读回原人物、铁枪和武技，再从人物已掌握的招式中施放改造。

`--case film --view native --archive <输出目录>` 拍摄原版常速录像，`--view worldfall` 切换到 Worldfall。每招从同一个正式对练档重新加载，连续记录完整动作后拼接。使用 `--move strike`、`--move sweep` 或 `--move press` 单独补拍。三招一起拍时，给 `dev.py exec` 预留 750 秒。

### 重兵战技（9.45）

在独立游戏通过 `dev.py exec` 运行 `python3 Scripts/SpellDemos/verify_heavy_weapons.py --case drop --archive <长兵对练档目录>`。取样铁锤、双手握点、脚底和重心，核对真实锤面命中、向下受力、回弹和归还。用 `--case miss` 在蓄势途中将同一目标移出落点，检查空挥和正常收势。

`--case guard` 用既有测试盾验证真实盾面能接住铁锤。用 `--case interrupt` 检查蓄势中换上同款铁锤会解除原动作，原人物随后能持新锤重施。

`--case archive` 在正式万法阁改造托握、器物尺寸与锤面，并写入同一对人物的正式世界档。停掉并重开独立游戏后，用 `--case cold --archive <新输出目录>` 读回改造与原铁锤并重施。

`--case film --archive <新输出目录> --view native --move drop` 录制正式命中。用 `--view worldfall` 切换到 Worldfall，用 `--move miss` 拍摄空挥触地，用 `--move guard` 拍摄盾面接住重锤。每段都从同一个正式世界档开场。

### 防御战技（9.46）

在独立游戏通过 `dev.py exec` 运行 `python3 Scripts/SpellDemos/verify_defense.py --case front --archive <重兵对练档目录>`。用同一原铁锤验证正式盾面的迎面架挡、回弹和归还，`--case rear`、`--case late` 和 `--case overflow` 分别检查背面、过晚和破防后余力。

`--case craft` 走万法阁学习、城市打造和玩家方向选取，核对真实耗材与原铁盾。测试从同一对练档里的原人物开始。

`--case yield` 用同一记重锤验证转肩沉掌，取样左右脚的抬落、实际承托点、侧向卸力和推退后的控制归还。

`--case archive` 在万法阁改造盾架，保存原人物、实际兵器和两份已学招式。重开独立游戏后，用 `--case cold --archive <新输出目录>` 从正式存档读回并施展改造副本。

`--case film --archive <新输出目录> --view native --move front` 常速录制迎面架挡。用 `--view worldfall` 切换画面，用 `--move overflow` 或 `--move yield` 拍摄破防余力或转肩卸力。每段录像都在实际命中、受力和收势符合预期后输出。

### 步法战技（9.47）

通过 `dev.py exec` 运行 `python3 Scripts/SpellDemos/verify_footwork_actions.py --case retreat --archive <防御对练正式档>`。沿用同一对人物，核对后撤、侧移、承重脚和收势方向。改为 `--case charge` 检查真实冲撞和接触收势，改为 `--case wall` 或 `--case vault` 分别检查墙前止步和跨过低坎后落地。

`--case archive` 在原窗口修改抬脚高度和侧移路线，学会副本后保存世界。新进程使用 `--case cold --archive <新输出目录>` 从原版世界档恢复两份招式并施展副本。

`--case interrupt` 用档中的原铁锤打断实际空中的跃落，核对中断发生的高度和后续落地。常速录制时，在四个步法场景任选其一并附 `--view native` 或 `--view worldfall`。录像中对练者收起铁盾，以便看清脚步与肩面。

### 抓投战技（9.48）

通过 `dev.py exec` 运行 `python3 Scripts/SpellDemos/verify_grappling_actions.py --case throw --archive <步法正式对练档>`。沿用同一对人物，核对抓持、上提转动、带惯性离手、实际撞伤及翻滚起身。`--case press` 检查握持时实际触地，`--case miss` 在接触前移开目标，`--case interrupt` 在抓持成立后用原版外力打断来源。

`--case archive` 修改压摔撞伤倍率并保存副本。新进程用 `--case cold --archive <新输出目录>` 读回并施展改造版，核对实际碰撞的倍率。录制两招时，给 `--case throw` 或 `--case press` 附加 `--view native` 或 `--view worldfall`。

`--case archive-view --archive <新输出目录>` 可重新打开已保存的副本并截取中英文窗口。每次复验的记录、截图或录像路径由脚本返回。9.48 的验收文件汇总于 `~/.local/state/cultiway-dev/evidence/grappling-948/delivery.json`。

### 非人身体战技（9.49）

在独立游戏中通过 `dev.py exec` 运行 `python3 Scripts/SpellDemos/verify_nonhuman_actions.py --case tail` 验证卷尾横扫。`--case pair` 在同一狐形上施放尾扫和翼切，观察实际接触、分别付费以及不同收势时刻。`--case loss` 在翼切蓄势中撤下凶翼，核对接触消失、尾部仍可施展，以及恢复凶翼后的再施展。

`--case archive` 在游戏内选择两条真实命名尾并保存完整副本。换一个游戏进程后使用 `--case cold --archive <上一段输出目录>` 读回原体和招式，验证两条尾各自出手。

`tail` 和 `pair` 可附 `--view native` 或 `--view worldfall` 录制常速画面。雷角和前爪的录像复用 9.40 的 `verify_beast_attacks.py --part horn|pounce --record native|worldfall`。

正式世界、八段录像及各场景的结果路径汇总于 `~/.local/state/cultiway-dev/evidence/nonhuman-949/delivery.json`。

### 火刃旧变体（10.01）

在独立游戏中运行 `dev.py exec --timeout 185 -- python3 Scripts/SpellDemos/verify_fire_blade_variants.py`。脚本通过原学习和付费入口逐个使用四个索引，核对锋口、双锋间隙、成形时点和收束位置，并用索引 3 复验折线路线。可重复传入 `--variant` 复验指定索引。每轮输出实际检查和施放记录。

### 火球旧变体与火系存档（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py fireball` 验证全部十八个索引。可重复指定 `--variant`，或者使用 `--case miss`、`--case tracking --variant 11`、`--case gravity` 单独复验落空、改追踪和成形期间的范围作用。

用同一入口的 `fire-family --case archive` 保存火刃和火球的完整学习资料。退出并重新启动独立游戏后运行 `fire-family --case cold --archive <保存输出目录>`，核对全部索引、追踪轨迹和两个读回招式的真实施放。

录像使用 `demo_all.py --filter Cultiway.FireBlade,Cultiway.Fireball --variants all --cases base --realm 0 --target-offset 7 0 --zoom 9 --duration 1.4 --fps 30`。分别指定 `--view native` 或 `--view worldfall` 和各自的输出目录。总览包含四个火刃、十八个火球各自的一段连续施放。

### 水系飞行旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py water-family` 逐个验证水箭、水球和水刃。改用 `water-arrow`、`water-orb` 或 `water-blade` 后，可重复传入 `--variant` 复验指定索引。水球可加 `--case miss` 或 `--case tracking` 检查落空收势和改轨迹后追踪移动目标。

用 `water-family --case archive` 保存全部十三份学习资料。退出并重启后用 `water-family --case cold --archive <保存输出目录>` 读回资料，施放三种代表索引。

两种视角分别使用 `demo_all.py` 的 `--view native` 或 `--view worldfall`。水箭和水刃用 `--filter Cultiway.WaterArrow,Cultiway.WaterBlade --zoom 23` 保留飞行和收势，水球用 `--filter Cultiway.WaterOrb --zoom 9` 看转动的内外水层。两组都使用 `--variants all --cases base --realm 0 --target-offset 7 0 --duration 1.55 --fps 30`。

### 金系与风系飞行旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py metal-wind` 逐个验证飞剑、剑气、金针、风弹和风刃。可选择 `flying-sword`、`sword-qi`、`golden-needle`、`wind-bullet` 或 `wind-blade` 并传入 `--variant` 复验原索引。金针用 `--case miss` 核对落空，风弹用 `--case tracking` 核对改轨迹后追踪移动目标。

用 `metal-wind --case archive` 保存九份学习资料。退出并重启后用 `metal-wind --case cold --archive <保存输出目录>` 读回资料，经正式入口施放五个代表索引。

录像共用 `demo_all.py --variants all --cases base --realm 0 --target-offset 7 0 --duration 1.55 --fps 30`。剑气、金针和风刃用 `--zoom 23`，飞剑和风弹用 `--zoom 12`。两组均分别录制 `--view native` 和 `--view worldfall`。

### 雷珠、雷枪旧变体（10.01）

用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py thunder-projectiles` 验证全部四种雷珠及雷枪。用 `thunder-orb --case landing --variant 0` 验证无直接接触的指定点爆发，用 `--case miss --variant 0` 核对提前到期，用 `--case tracking` 核对改追踪后命中移动目标。批次同一入口支持 `--case archive`，重启游戏后用 `--case cold --archive <保存输出目录>` 读回并付费施放。

两种视角均用 `demo_all.py --filter Cultiway.ThunderOrb,Cultiway.LightningSpear --variants all --cases base --realm 0 --target-offset 7 0 --zoom 20 --duration 1.55 --fps 30`。分别选 `--view native` 或 `--view worldfall` 和对应输出目录。

### 落雷旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py lightning-strike` 逐个通过已学入口施放十一个旧索引。用 `--case landing` 核对索引 0 加速后先着地、成形结束才在指定点爆发。用 `--case miss --variant 1` 核对提前到期，用 `--case cancel` 核对空中取消。

用同一入口的 `--case archive` 保存十一份学习资料，关闭并重启游戏后用 `--case cold --archive <保存输出目录>` 重新施放索引 0 和 9。录像用 `demo_all.py --filter Cultiway.LightningStrike --variants all --cases base --realm 0 --target-offset 7 0 --zoom 16 --duration 1.2 --fps 30`，分别指定 `--view native` 和 `--view worldfall`。

### 冰棱、冰枪、冰雨旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py ice-projectiles` 实测首击、贯穿和实际落点爆发。用 `icefall --case scatter` 核对同一人物两次施放的散落，用 `icefall --case miss` 核对提前到期。批次入口的 `--case archive` 保存全部三个索引，另起进程后用 `--case cold --archive <保存输出目录>` 重新读回及施放。

两种视角录像使用 `demo_all.py --filter Cultiway.IceShard,Cultiway.IceLance,Cultiway.Icefall --variants all --cases base --realm 0 --target-offset 7 0 --zoom 19 --duration 1.55 --fps 30`。

### 飞石、石锥、落石旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py earth-projectiles` 逐个施放五个旧索引。`rock-projectile --case miss` 检查抛弧落空后的收势，`falling-rock --case landing` 检查实际着地，`falling-rock --case miss --variant 1` 检查提前到期。`earth-projectiles --case archive` 保存五份学习资料，重启后用 `earth-projectiles --case cold --archive <保存输出目录>` 读回并重放三种法术。

原版与 Worldfall 分别使用 `demo_all.py --filter Cultiway.RockProjectile,Cultiway.StoneCone,Cultiway.FallingRock --variants all --cases base --realm 0 --target-offset 7 0 --zoom 19 --duration 1.55 --fps 30`。

### 木枪、落木旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py wood-projectiles` 逐个验证四个旧索引。`wood-spear --case tracking --variant 1` 核对改轨迹后仍用原木桩并命中移动目标，`falling-timber --case landing` 检查真正着地，`falling-timber --case miss --variant 1` 检查提前到期。用 `wood-projectiles --case archive` 保存，重启后用同批次的 `--case cold --archive <保存输出目录>` 读回并重放。

录像用 `demo_all.py --filter Cultiway.WoodSpear,Cultiway.FallingTimber --variants all --cases base --realm 0 --target-offset 7 0 --zoom 19 --duration 1.55 --fps 30`，分别选择 `--view native` 和 `--view worldfall`。

### 灵弹、阴煞弹、纯阳弹、混沌珠旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py energy-bolts` 验证四个旧索引。`yin-bolt --case tracking` 核对改追踪后保留形体，`chaos-orb --case landing` 检查指定点爆发，`chaos-orb --case miss` 检查提前到期。批次支持 `--case archive`，重启后用 `--case cold --archive <保存输出目录>` 读回并重放四种法术。

录像用 `demo_all.py --filter Cultiway.SpiritBolt,Cultiway.YinBolt,Cultiway.YangBolt,Cultiway.ChaosOrb --variants all --cases base --realm 0 --target-offset 7 0 --zoom 19 --duration 1.55 --fps 30`，分别选择 `--view native` 和 `--view worldfall`。

### 毒针、阴刃、炎陨旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py needle-blade-meteor` 验证三个索引。`poison-needle --case tracking` 检查改追踪，`flame-meteor --case landing` 检查指定地面，`flame-meteor --case miss` 检查提前到期。批次支持 `--case archive`，重启后用 `--case cold --archive <保存输出目录>` 读回并重放。

录像用 `demo_all.py --filter Cultiway.PoisonNeedle,Cultiway.YinBlade,Cultiway.FlameMeteor --variants all --cases base --realm 0 --target-offset 7 0 --zoom 19 --duration 1.55 --fps 30`，分别选择 `--view native` 和 `--view worldfall`。

### 地刺、地脊、沧浪、灵藤旧变体（10.01）

用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py ground-spike --case legacy` 记录旧地刺的结算时点、范围和接触对象。用 `ground-forms` 一次检查七个索引，必要时可指定 `ground-spike --variant 0`、`earth-ridge`、`tidal-wave` 或 `spirit-vine`。批次的 `--case archive` 保存全部索引，重启后用 `--case cold --archive <保存输出目录>` 读回并重放。

原版和 Worldfall 分别用 `demo_all.py --filter Cultiway.GroundSpike,Cultiway.EarthRidge,Cultiway.TidalWave,Cultiway.SpiritVine --variants all --cases base --realm 0 --target-offset 7 0 --zoom 19 --duration 1.65 --fps 30` 录制。

地刺边界用 `ground-spike --case legacy-edge --variant 0` 和 `ground-spike --case edge` 对照。在同一岩床的中心及距中心 1.38、1.42 格处放靶。

地脊放大后的岩床接触可用 `demo_all.py --filter Cultiway.EarthRidge --variants all --cases base --realm 0 --caster-position 64 72 --target-offset 5 0 --aim-offset 7 0 --zoom 15 --duration 1.65 --fps 30 --motion-trace` 补拍。原地面对练的人物仍在同一张图上。分别选 `--view native` 和 `--view worldfall`，在输出的 `result.json` 核对实际接触和受伤人数。

### 土墙、冰墙、木壁旧变体（10.01）

用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py solid-walls --case archive` 写入三份旧学习资料。换游戏进程后用同一入口的 `--case cold --archive <保存输出目录>` 读回并重放。默认的 `--case hit` 使用现成对练检查落位、实际行走、费用和结束恢复，可指定 `earth-wall`、`ice-wall` 或 `wood-wall`。

补拍冰墙迎击用 `demo_all.py --filter Cultiway.IceWall --variants all --cases base --scene shield --realm 2 --target-offset 8 -5 --zoom 12.5 --duration 2.8 --fps 30 --motion-trace`，分别选择 `--view native` 和 `--view worldfall`。输出记录墙实际承受的力量。

### 火墙、风墙、剑幕、水幕旧变体（10.01）

用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py contact-walls --case archive` 保存四个旧索引。重启后用 `contact-walls --case cold --archive <保存输出目录>` 读回并施放。默认的 `--case hit` 可单独指定 `fire-wall`、`wind-wall`、`sword-curtain` 或 `water-curtain`。

迎击录像用 `demo_all.py --filter Cultiway.FireWall,Cultiway.WindWall,Cultiway.SwordCurtain,Cultiway.WaterCurtain --variants all --cases base --scene shield --realm 2 --target-offset 8 -5 --duration 2.8 --fps 30 --motion-trace`。原版配 `--view native --zoom 12.5`，Worldfall 配 `--view worldfall --zoom 16.5`。输出记录实际拦截和墙面容量变化。

### 随身护盾旧变体（10.01）

在独立游戏中用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py shields --case archive` 保存八个索引。重启后使用 `shields --case cold --archive <保存输出目录>` 读回，核对每个索引的成形、有效防御、拦截、原价和收势。也可选择单个护盾并指定 `--variant`。

用 `demo_all.py --filter Cultiway.SpiritShield,Cultiway.FlameShield,Cultiway.WindShield,Cultiway.LightningShield,Cultiway.YangShield --variants all --cases base --scene shield --realm 0 --target-offset 7 0 --zoom 17 --duration 3.6 --fps 24 --motion-trace` 录制，分别选择 `--view native` 和 `--view worldfall`。

### 链雷、灵光、曜阳光旧变体（10.01）

用 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py beams-and-chain --case archive` 保存三份旧学习资料。重启后使用 `beams-and-chain --case cold --archive <保存输出目录>` 读回并施放。复验核对成形时无伤、真实命中、固定端点退光、链雷首击余光和正常结束。

录像用 `demo_all.py --filter Cultiway.SpiritBeam,Cultiway.SolarBeam,Cultiway.ChainLightning --variants all --cases base --realm 0 --caster-position 64 72 --target-offset 5 0 --zoom 16.5 --duration 1.45 --fps 30 --motion-trace`，分别选择 `--view native` 和 `--view worldfall`。

### 五种持续领域的旧索引

在独立游戏运行 `dev.py exec --timeout 220 -- python3 Scripts/SpellDemos/verify_legacy_variants.py persistent-fields --case archive`，将风卷四个索引和玄阴域、毒潭、毒雾、乱熵域各自的旧索引写入同一正式世界。重启后用同批次的 `--case cold --archive <保存输出目录>` 核对旧资料并逐个重放。单个来源可用 `tornado`、`yin-field`、`poison-pool`、`poison-mist`、`entropy-field`，风卷可用 `--variant` 指定索引。

复用同一对练核对成形无伤、推进或跟随、间隔伤害、固定点收势及随后清理。同一毒潭的第三次受理需只替换最老实例，三次均按原价。组合录像用 `demo_all.py --filter Cultiway.Tornado,Cultiway.YinField,Cultiway.PoisonPool,Cultiway.PoisonMist,Cultiway.EntropyField --cases base --variants all`，分别指定原版与 Worldfall 视角。旧地块作用随 10.01 的世界作用验收。

### 墟隙的旧索引

在独立游戏中用 `dev.py exec -- python3 Scripts/SpellDemos/verify_legacy_variants.py rift --case legacy` 对照原执行的爆发时点、半径、费用及地块和建筑记录。用 `--case archive` 取得正式世界，重启后以 `--case cold --archive <保存输出目录>` 读回并重放。用 `--case miss` 验证空地张合。放大、强化、抗性、失败退款及恢复仍可用 `verify_rift.py` 分别核对。

原版与 Worldfall 的画面均由 `demo_spell.py Rift --cases base --duration 1.1 --motion-trace` 录制。成形段只有裂口，爆发时冲面覆盖真实的圆形范围，随后两部分各自收势。

### 六种友方术的旧索引

用 `dev.py exec --timeout 240 -- python3 Scripts/SpellDemos/verify_support_variants.py --case archive` 将六个零号索引保存进正式世界。换进程后，传入 `--archive <保存输出目录>`，依次用 `--case legacy` 对照旧行为、用 `--case cold` 重放当前实现。只补单体或范围时可选 `--section single` 或 `--section area`，读档仍校验全部学习资料。

录像中可以从报告的 `friendStatusesBefore` 和 `friendStatusesAfter` 核对友方的状态变化，并读取生命前后值。录制回春领域时，时长要覆盖完整有效段和收势。

### 四种瞬时改地术的旧索引

在独立游戏中，用 `dev.py exec -- python3 Scripts/SpellDemos/verify_terrain_variants.py --case legacy --archive <旧档输出目录>` 记录旧执行。改后用 `--case cold --archive <同一旧档输出目录> --baseline <旧执行的 result.json>` 逐格对照并检查收势。两者都核对八份归档学习资料，可用 `--spell RaiseTerrain` 等参数只重放受影响法术。水和邻格可能在施法以后继续由游戏更新，作用范围以实际结算记录为准。

合辑用 `demo_all.py --scope base --filter Cultiway.RaiseTerrain,Cultiway.LowerTerrain,Cultiway.FillWater,Cultiway.DrainWater --cases base --variants all --realm 0 --caster-position 64 72 --aim-offset 6 0 --motion-trace`。分别传入 `--view native` 和 `--view worldfall`，从 `worldContactsLog` 核对真实变更的地块。

### 植被催生、净土、召雨与催熟的旧索引

在独立游戏中运行 `dev.py exec -- python3 Scripts/SpellDemos/verify_world_utility.py --case legacy --archive <旧档输出目录>`。用其 `result.json` 作为 `--baseline`，配合 `--case cold --archive <同一旧档输出目录>` 重放当前实现。两者均读取归档的八份世界法术；`--spell CleanLandField` 等选项可只重放对应法术。

演示用 `demo_all.py --scope base --filter Cultiway.NatureGrowthField,Cultiway.CleanLandField,Cultiway.SummonRainCloud,Cultiway.Fertilize --cases base --variants all --duration 4.9 --motion-trace`。分别指定原版或 Worldfall，取实际地块操作查看前后状态。召雨段保留成云时间，完整天气存读仍通过原有 `verify_rain_process.script` 和 `verify_rain_process_restart.script` 复验。

### 旧攻击的范围爆发（10.01）

在独立游戏中用 `dev.py exec -- python3 Scripts/SpellDemos/verify_legacy_area_impacts.py --case archive` 保存学习资料。用 `--case legacy --archive <归档输出目录>` 记录各术的原执行。重新启动独立游戏后用 `--case cold --archive <归档输出目录> --baseline <原执行的 result.json>` 对照。两种执行都可以用 `--spell Rift` 等参数只复验指定法术。对照落石、落雷和墟隙的代表场景时，两端都加 `--objects`，可分别检查固定沙地、人物总伤害和敌方完工建筑。

两种视角的录像用 `demo_all.py --scope base --filter Cultiway.GroundSpike,Cultiway.Rift,Cultiway.LightningStrike,Cultiway.Icefall --cases base --variants first --realm 0 --caster-position 64 72 --target-offset 6 0 --aim-offset 6 0 --duration 1.8 --fps 26 --motion-trace`，原版配 `--view native --zoom 10`，Worldfall 配 `--view worldfall --zoom 12`。地形变化以复验的实际地块记录为准。

### 旧攻击沿途掠地（10.01）

在独立游戏中运行 `dev.py exec -- python3 Scripts/SpellDemos/verify_legacy_ground_travel.py --case legacy`。随后用 `--case current --baseline <原执行的 result.json>` 复验当前实现，查看沿途格子状态及取消后的结果。可用 `--spell PoisonNeedle` 等参数只复验一个样本。

### 旧攻击命中人物后的地块作用（10.01）

在独立游戏中运行 `dev.py exec -- python3 Scripts/SpellDemos/verify_legacy_ground_contact.py --case legacy`。随后用 `--case current --baseline <原执行的 result.json>` 对照每次接触、脚下地块和实扣费用。使用 `--spell ChainLightning` 可单独对照跳跃及衰减。报告的 `damage` 分别记录带来源的命中和原版环境伤害，用来查明总掉血的组成。

### 飞行攻击实际接触建筑（10.01）

在独立游戏中运行 `dev.py exec -- python3 Scripts/SpellDemos/verify_legacy_building_flight.py --case legacy`。再用 `--case current --baseline <原执行的 result.json>` 对照。可用 `--spell WaterArrow`、`--spell Fireball` 或 `--spell RockProjectile` 单独安排样本。报告包含水矢直击和穿透、火球拦截和拆屋后的复放，以及飞石越过房屋时的真实结果。

以相同入口加 `--group sustained` 可对照曜阳光、毒潭、火墙与风墙对同一类完工建筑的作用。报告保留单次实际扣血、共同接触的年龄、实扣费用及结束观察。

用 `--group chain` 对照从人物经建筑再跳回人物、点名建筑起跳和拆屋后改选三种实际路径。调试某一段时可用 `--chain-case actor-relay`、`--chain-case building-first` 或 `--chain-case open`。

### 持续领域的受力、液体与地形（10.01）

在独立游戏中运行 `dev.py exec -- python3 Scripts/SpellDemos/verify_legacy_field_world.py --case legacy`，再用同一入口的 `--case current --baseline <原执行的 result.json>` 对照。可以用 `--spell Tornado` 或 `--spell YinField` 限定样本。报告记录角色的实际位移、行进及接触产生的水滴、改变的地块与结束后的观测。

### 基础法术整项交付（10.01）

在独立游戏中运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_base_spell_archive.py --case archive`。退出并重新启动后，用同一入口的 `--case cold --archive <保存输出目录>` 核对全部 127 个旧索引及冻结的学习资料。

用 `python3 Scripts/SpellDemos/build_base_spell_reels.py <分批证据目录> <交付目录>` 合成原版和 Worldfall 录像。脚本按制作清单核对各索引都有已验证的原片，并生成章节及逐索引时间表。

### 自身法相的主动入口（10.02）

在独立游戏中运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_formation_self.py`。脚本沿用现成对练，核对剑胎显锋、灵台显神、真身镇劫及五相轮转的实扣费用、生效阶段、持续时长和自然结束。状态和冷却按原游戏速度等待。

同一入口加 `--case carrier` 检查真实命魂出窍、肉身要求、出手位置、状态归属和归返后的再次使用。录像可用 `demo_spell.py` 指定对应能力；演示会从修炼者实际拥有的主动入口施放。

### 范围法相的主动入口（10.02）

在独立游戏中运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_formation_area.py`。同一组人物依次观察龙相震击的真实伤害与震退、纯阳净域的敌我分流及玄阴寒域的冻结、沉默和实际灵气收支。另检查实际落点、旧地块作用、圈外及结束。可用 `--spell DragonAspectBurst`、`--spell PureYangDomain` 或 `--spell MysteriousYinDomain` 单独复验受影响的能力。

### 修炼提示和龙威（10.02）

在独立游戏中运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_formation_derived.py`。同一张对练地图用于观察真实攻防、正式付费施法和致命受击。可用 `--section ward` 查看护盾与适应，`--section casting` 查看凝元和回响，`--section survival` 查看幻身与保命，`--section dragon` 查看第五层龙威的眩晕和震退。提示结束时检查没有新增接触和伤害，龙威另核对敌我、圈外及支付。

同一入口可用 `--section strikes` 复验混沌回响、归墟回响和五相追加攻击。核对点名对象、原伤害、相位切换、同秒限制和接触结束。

同一入口可用 `--section status` 复验破甲、中毒、霜缚和灼烧。检查同源条件、实际状态与周期伤害、同位置旁人及收势。单项可用 `--section armor`、`poison`、`frost` 或 `burn`。

用 `--section support` 连续复验木行回补与两种阳行净化。可用 `--section life-return` 对比实际普通击杀与木毒击杀，也可用 `--section cleanse` 单独核对正式付费施法后的净化、生命恢复和收势。

用 `--section blasts` 复验焚烬与凝元实际爆发，核对圈内、紧贴圈外、空中敌人及圆面尺寸。用 `--section blast-lost` 在单独一轮核对点名目标实际死亡后不会展开。

用 `--section counters` 复验炼体反打与真身反打。核对近身武器受击、远处及非武器拒绝、实际土行扣血、沿地推退和原冷却。

用 `--section tracking` 验证两种剑道追击与两种阴行汲灵。两组可通过 `--section sword-flight`、`--section yin-flight` 单独检查。用 `--section flight-lost` 检查真实飞行途中失去对象是否停止。

### 修炼组合的正式存读（10.02）

在独立游戏中运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_formation_archive.py archive`，记录返回的输出目录。正常退出并重新启动独立游戏后，运行 `dev.py exec --timeout 180 -- python3 Scripts/SpellDemos/verify_formation_archive.py cold --archive <前段输出目录>`。脚本核对跨进程恢复三个代表境界、状态及冷却、原条件再次触发和撤去来源后的清除。

### 七种修炼主动能力实录（10.02）

独立游戏就绪后运行 `dev.py exec --timeout 900 -- python3 Scripts/SpellDemos/record_formation_reels.py --output <实录目录>`。脚本依次使用七个真实主动入口，在两种视角分别生成连续片段、含章节的合辑与原片索引。用 `--view native` 或 `--view worldfall` 限定视角，已有原片可用 `--encode-only` 重新合成。

### 御器的真实器物与原版弹丸（10.04）

在独立游戏用 `dev.py exec --timeout 175 -- python3 Scripts/SpellDemos/verify_native_projectile_flight.py` 复验远程御器的原版弹丸。核对真实逐发支付、原版轨道与三维显示一致、换武器停止续射、弹丸复用及换世界清理。

在同一入口加 `--section bow` 检查原弓离手、逐发从实际弓位放箭、随人物移动回手和首发前换同款弓。用 `--section bow-world` 单独检查升弓期间换世界，确认旧弓飞行和施放会话都已清理。默认入口也检查有在飞的代理箭矢时换世界。

用 `--section melee` 检查原铁枪离手后的枪线接触、连续按次付费、移步后回手和同款换装中断。用 `--section melee-sweep` 与 `--section melee-crush` 分别核对实际铁剑横扫和铁锤重砸；两段复用同一套人物和世界。

用 `--section artifact-sword` 从正式合成和装备入口检查原飞剑。记录原位起飞、两个视角共用姿态、刃段实际扣血、失去目标后巡航、召回过程中停伤、回到移动后的持有者，以及重新出剑后的控制失效清理。

用 `--section artifact-array` 检查正式分光剑阵的一次付费、成阵、实际单刃接触、同一刃归阵和自然收束。用 `--section artifact-array-recall` 单独检查主动收阵立即停伤、全部剑影回到原物位置、再次施放及运行资格撤除后的清理。

用 `dev.py exec --timeout 170 -- python3 Scripts/SpellDemos/verify_weapon_flight_archive.py --section prepare`，让原弓、飞剑和分光剑阵同时运行，正式保存并在同一进程换世界。退出并重新启动后，用同一脚本的 `--section cold --archive <prepare 输出目录>`，核对原件、完整参数、冷却、临时对象清理和再次施放。`--section legacy --archive <5.15 留下的 variant-save 目录>` 从原档本身提取预期，再用原法宝命中并召回。

连续实录使用 `dev.py exec --timeout 600 -- python3 Scripts/SpellDemos/record_weapon_flight.py --view native|worldfall --output <录像目录>`。原版用未安装 Worldfall 的独立游戏，三维用已安装 Worldfall 的独立游戏。可重复传入 `--move arrow-rain|sky-volley|sweep|thrust|crush|flying-sword|sword-array` 只补指定招式，同一视角可续写到同一目录。七段齐备后生成 `all.mp4`，每段和合辑均按实际世界时间播放。`--encode-only <原始画面目录>` 可直接从已通过的实机画面再编码。

### 法钟的范围与净化（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_artifact_chime.py` 检查原法钟的共同范围。`--section shake` 核对真实扣血、眩晕、沿地外推、圈外落空和再次施放。`--section cleanse` 核对原数量上限、敌我状态解除、圈外状态保留与恢复。双视角使用已安装 Worldfall 的独立游戏。调材质时用 `--section views` 只补拍两个视角及范围；仅查共同范围与游戏行为时可加 `--skip-views`。

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_golden_bell_activation.py` 核对金钟护界的一次付费、原盾容量和时长、共用钟体的实际位置、自然到期，以及再次启动后切回待命的收束。只补近景用 `--views-only`，在同一暂停时点采集原版与 Worldfall。

### 山岳印落（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_mountain_seal.py` 核对原印面的垂直下落、触地半径、实际伤害和震退，以及到期或切回待命后原件回手。用 `--skip-views` 只查行为，用 `--views-only` 复验落印和震裂边界在两种视角的实机画面。

### 禁法敕令与封脉镇身（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_seal_effects.py` 让同一件正式法印使用两招。检查原费用、禁法敕令的圈内外和停止维持、封脉镇身的点名、随动与冷却后再用。用 `--section edict` 或 `--section meridian` 补指定一招，用 `--skip-views` 只核对行为和实际形体。

### 万魂幡阵与号令旗域（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_banner_fields.py` 让同一件原幡依次部署两种法域，核对原费用、实际边界、敌方受损、友方增益和无力维持后的归还。可用 `--section myriad` 或 `--section command` 单独复验，用 `--skip-views` 只查行为。

### 阴兵显化（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_spirit_host.py` 核对击杀储魂、两笔原费用、实际鬼火生物、到期清理和再次施展后的正式召回。用 `--skip-views` 只核对行为和真实幡门位置。

### 破妄照影与摄魂定魄（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_mirror_effects.py` 在同一场地正式炼制两件原镜。破妄照影检查实际部署点、付费、圈内外显隐和净化，以及维持费不足时收镜。摄魂定魄检查拒绝友方、点名控制、按实际抽取返还和冷却后再用。用 `--section truth` 或 `--section stasis` 只补其中一招，用 `--skip-views` 只核对游戏行为和形体位置。

### 吞天葫芦（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_gourd_swallowing.py` 正式炼制并部署葫芦，核对实际葫芦口、圈内伤害和牵引、真元抽取与入库，以及付不起维持费时的收束。用 `--skip-views` 只核对行为和形体边界。

### 五灵倾泻（10.05）

用 `dev.py exec --timeout 190 -- python3 Scripts/SpellDemos/verify_gourd_outpouring.py` 让正式炼制的同一葫芦取得实际吞摄储量，再比较有储量与空储量的两次施放。核对原费用、火材附带状态、扇形开角、原作用距离与同一实体扇面。用 `--skip-views` 复验行为，用 `--views-only` 由同一原配方补拍两个视角和扇面边界。
