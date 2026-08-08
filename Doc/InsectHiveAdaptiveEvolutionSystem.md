# 虫群自适应进化系统设计文档

**版本**：v1.0  
**状态**：设计基线  
**最后更新**：2026 年 8 月 8 日  
**当前范围**：自主巢群、AI 自适应、巢群基因库与个体差异  
**明确不包含**：存档、读档恢复、完整文明、玩家手动编辑基因

---

## 1. 文档目的

本文定义 Cultiway 虫族的完整玩法与技术方案。虫族不是一组固定单位，也不是依靠击杀逐级升级的生物，而是一套能够观察环境、尝试变异、比较适应度并将有效变化遗传给后代的自主巢群系统。

本文需要解决以下问题：

- 虫族如何在没有玩家指挥的情况下扩张、生产和战斗。
- “自适应进化”如何超越属性加成，改变形态、运动方式、技能、AI、职虫比例和巢穴结构。
- 环境压力如何来自真实事件，而不是读取全世界隐藏数据。
- 个体变异、巢群遗传和快速行为调整如何分层，避免所有变化混成一个随机系统。
- 如何限制无限叠加，使每次适应都带来代价、替换和可被针对的弱点。
- 如何使用项目现有 ECS、Ability、Skill、Status、语义和战斗规则体系实现。
- 如何在不使用 `ActorData.custom_data_*` 和 `BuildingData.custom_data_*` 的前提下管理全部运行时状态。

本文是实现依据。未在本文中声明的核心状态，不应在实现时临时塞入 Actor、Building、静态字典或 UI 对象。

---

## 2. 已确定的产品方向

### 2.1 阵营形态

虫族首先实现为自主巢群，而不是第五文明。

- 母巢是生产、基因记忆和群体决策中心。
- 虫群不建立 `City`、`Culture`、`Clan` 或普通建筑队列。
- 虫族使用一个技术性的非文明 `KingdomAsset` 接入原版敌我、伤害和目标搜索。
- 不同母巢拥有独立基因库，可以在不同环境中形成不同生态型。
- 第一阶段不同母巢彼此友好，不处理巢群内战和领地外交。

### 2.2 进化控制

进化完全由 AI 决定。

- 玩家通过制造环境、投放敌人和观察结果间接影响进化。
- UI 只展示压力、试验、基因和历史，不提供器官编辑器。
- 调试工具可以强制增加压力或开始试验，但不属于正式玩法。

### 2.3 遗传层级

适应以母巢为共同记忆，以个体为表达和试验载体。

- 母巢保存已固化的基因模板、遗传记忆和当前试验。
- 新生虫从母巢模板生成表现型，而不是从父母 Actor 继承。
- 个体可以出现一次有界的体细胞适应，但不会自动写回母巢。
- 试验个体的真实表现决定候选变异是否进入后续新生代。
- 子巢建立时深拷贝母巢基因，之后独立演化。

### 2.4 数据所有权

所有虫族玩法状态都属于 Mod ECS 与世界会话。

- Actor ECS 实体持有个体状态。
- 独立 Hive ECS 实体持有母巢状态。
- ECS LinkRelation 表达个体归属、试验分组和能力来源。
- WorldBox Actor 与 Building 只是宿主、渲染和物理对象，通过 Binder 解析。
- 服务中的字典只允许作为可重建索引，不是权威数据。
- 当前版本不实现任何持久化，也不设计临时兼容格式。

---

## 3. 设计目标与非目标

### 3.1 设计目标

1. **变化可观察**：玩家能够从贴图、移动方式、攻击动作、队伍组成和巢穴行为中看出进化结果。
2. **原因可解释**：每次候选、固化和淘汰都能指出主要压力和真实证据。
3. **适应有延迟**：受到一次攻击不会立即全族免疫；变化必须经过积累、试验和新生代传播。
4. **适应有代价**：每个模块消耗槽位、复杂度、生物质或职虫效率，不存在无条件最优解。
5. **巢群会分化**：相同初始基因的母巢在不同地区能够走向不同形态。
6. **系统可扩充**：增加新器官、职虫、巢器官或行为策略时，不修改进化算法主流程。
7. **运行成本有界**：伤害事件 O(1) 记录，进化按低频预算运行，不逐帧扫描全世界。
8. **状态单一写入**：每类状态只有一个领域服务能够修改。

### 3.2 非目标

- 不复制任意敌人 Trait、Skill、ActorAsset 或代码行为。
- 不使用机器学习、神经网络或不可解释的遗传算法黑盒。
- 不为每种器官组合动态创建 ActorAsset。
- 不让每只虫独立维护完整压力模型。
- 不建立完整的城市、外交、贸易、文化和科技系统。
- 不在第一阶段实现地下移动；现有路径系统没有稳定的地下层语义。
- 不在当前版本保存 ECS 虫族状态。
- 不为存档缺失增加 ActorData、BuildingData 或全局文件回退。

---

## 4. 核心体验

玩家看到的完整循环如下：

1. 玩家放置一座原始母巢。
2. 母巢消耗初始生物质，孵化幼虫并分化为工虫和兵虫。
3. 虫群捕猎、搬运尸体、巡逻和保护母巢。
4. 战斗、饥饿、水域和母巢受袭形成可衰减的环境压力。
5. 虫群首先通过行为和生产策略快速应对，例如增加兵虫、集中防空或停止远征。
6. 长期无法解决的压力触发遗传候选，例如翼化、酸液囊或层叠甲壳。
7. 一部分新生虫成为试验组，另一部分同代同职虫作为基准组。
8. 系统比较两组在目标压力下的表现和一般生存能力。
9. 成功变异固化到相应职虫模板；失败变异进入抑制记忆。
10. 新一代逐步替换旧一代，虫群的外观、能力和行为发生可见变化。
11. 母巢成熟后产生建巢母虫；子巢继承当时的基因快照并独立演化。

进化不是唯一反应速度。虫群同时具有快速行为调整、个体生理适应和慢速遗传变化。

---

## 5. 术语

| 术语 | 含义 |
| --- | --- |
| 母巢 `Hive` | 虫群的 ECS 聚合根，对应一座 WorldBox 母巢建筑 |
| 巢核 `Hive Core` | WorldBox Building 宿主，负责碰撞、受击、贴图和世界位置 |
| 基因模板 `Genome Template` | 母巢针对每类职虫保存的已固化模块组合 |
| 表现型 `Phenotype` | 某个个体实际表达的形态和模块集合 |
| 压力 `Pressure` | 由真实事件结算得到的近期适应需求 |
| 证据 `Evidence` | 支撑某类压力和候选有效性的事件数量与强度 |
| 能力样本 `Sample` | 从已经交互的敌人或环境提取的有界语义信息 |
| 候选 `Candidate` | AI 提出的一个具体替换或新增方案 |
| 试验 `Trial` | 同职虫、同时期的基准组与变异组对照过程 |
| 固化 `Commit` | 候选进入母巢基因模板，影响后续新生虫 |
| 抑制 `Suppress` | 候选失败后短期内降低再次提出概率 |
| 遗传记忆 `Genetic Memory` | 母巢对曾经表达过的模块保留的熟悉度 |
| 体细胞适应 `Somatic Adaptation` | 仅影响当前个体、不会直接遗传的终身变化 |
| 职虫 `Caste` | 幼虫、工虫、兵虫、喷吐虫等稳定功能形态 |
| 巢器官 `Hive Organ` | 作用于母巢本体、孵化或群体感知的结构变化 |
| 神经策略 `Doctrine` | 改变目标优先级、编队和撤退规则的群体行为模块 |

---

## 6. 三种反应速度

虫族不能把所有应对都称为“进化”。系统明确区分三种时间尺度。

### 6.1 行为可塑性

时间尺度为数秒到数月，不修改基因。

- 调整巡逻、守巢、捕猎和撤退优先级。
- 调整下一批幼虫的职虫比例。
- 遇到飞行敌人时优先集火或躲入巢核范围。
- 生物质不足时减少高成本职虫，增加工虫。
- 母巢受袭时停止远征并召回附近单位。

行为可塑性可以快速撤销，不需要试验。它只能使用当前基因已经允许生产和执行的能力。

### 6.2 个体生理适应

时间尺度为数月到一个生命周期，只影响个体。

- 重复承受同类伤害后形成一次应激硬化。
- 长期饥饿后进入低耗代谢。
- 多次中毒幸存后获得弱化的滤毒能力。
- 濒死幸存后形成瘢痕甲壳或再生倾向。

每个个体同时最多拥有一个体细胞适应。它强度有限，并承担明确代价。体细胞适应的成功事件只会增加母巢对应候选的证据，不会直接成为全巢能力。

### 6.3 遗传进化

时间尺度为多个世代，改变新生虫。

- 增加、替换或抑制器官模块。
- 解锁新的固定职虫形态。
- 改变母巢器官。
- 固化群体神经策略。
- 让子巢继承一个已经分化的基因快照。

遗传进化必须经过候选、试验和固化。一个母巢同一时间只允许进行一个遗传试验。

---

## 7. 世界对象与生命周期

### 7.1 母巢创建

正式玩法只提供“放置原始母巢”的力量按钮，不直接暴露各类职虫生成按钮。

创建顺序：

1. WorldBox 创建母巢 Building。
2. `InsectWorldboxAdapter` 收到建筑创建事实，向 Ingress 队列写入 `HiveCoreCreatedInput`。
3. `HiveLifecycleService` 在 CommandExecution 阶段创建 Hive ECS 实体。
4. Hive 实体获得 `HiveTag`、`HiveCoreBinder`、`HiveIdentity`、资源、压力、基因和生产组件。
5. `HiveRepository` 建立 `BuildingId -> Hive Entity` 的可重建索引。
6. 初始基因模板只允许幼虫、工虫和兵虫。
7. 母巢获得初始生物质并开始第一批孵化。

WorldBox Building 不保存 Hive Entity ID。反向关联只存在于 Mod 世界会话的 Repository 和 ECS Binder 中。

### 7.2 个体出生

个体必须由 `HiveBroodService` 创建：

1. 生产队列冻结一份卵期表现型方案。
2. 服务消耗生物质并生成幼虫 Actor。
3. `ActorExtendManager.Get(actor)` 取得或创建对应 Actor ECS 实体。
4. `InsectPhenotypeService` 添加 `InsectIndividual`、`InsectPhenotype` 和 `InsectFitnessState`。
5. Actor ECS 实体通过 `HiveMembershipRelation` 指向母巢实体。
6. 若属于当前试验，则通过 `TrialCohortRelation` 指向试验实体。
7. 幼虫进入孵化状态，到期后提交 `BeginMetamorphosisCommand`。

个体数据只存在于 Actor ECS 实体。ActorData 只保留 WorldBox 自身字段，虫族系统不写入其自定义容器。

### 7.3 个体死亡

死亡处理顺序：

1. WorldBox 死亡回调进入适配器，只写入稳定句柄和死亡上下文。
2. Fitness 服务结算该个体的最终适应度贡献。
3. Trial 服务把结果计入基准组或变异组。
4. Membership 服务移除归巢关系并更新母巢人口计数。
5. Biomass 服务按尸体价值创建一个有时限的 `BiomassDeposit` ECS 实体。
6. Actor ECS 实体按项目现有回收流程销毁。

死亡不能直接触发固化。Trial 服务只在低频评估阶段判断试验结果。

### 7.4 母巢摧毁

1. Building 摧毁事实进入 Ingress。
2. Hive 状态切换为 `Collapsing`，停止新增孵化和新试验。
3. 未完成幼虫和储存生物质按规则损失。
4. 成员移除 `HiveMembershipRelation`，获得 `OrphanedInsect`。
5. 已存在的建巢母虫可以携带冻结基因建立新巢。
6. 没有建巢能力的孤虫尝试加入兼容母巢；失败后进入野化并逐渐死亡。
7. Hive 实体在全部关系解除后进入回收阶段。

### 7.5 世界结束

虫族实体全部属于当前世界代次。世界结束时由 WorldSession 统一删除，不允许功能自行维护永久静态状态。

当前不支持从 WorldBox 存档恢复虫族 ECS。读取包含虫族 Actor 或 Building 的世界不属于本版本支持范围，也不建立临时重建规则。

---

## 8. 初始职虫体系

职虫使用固定 ActorAsset，组合器官不创建新的动态资产。

| 职虫 | 资产建议 | 基础职责 | 解锁条件 | 主要度量 |
| --- | --- | --- | --- | --- |
| 幼虫 | `Cultiway.InsectLarva` | 等待分化，无战斗能力 | 初始 | 成功孵化率、孵化时间 |
| 工虫 | `Cultiway.InsectWorker` | 采集、搬运、修复、照料幼虫 | 初始 | 生物质运回量、存活、任务完成 |
| 兵虫 | `Cultiway.InsectWarrior` | 近战、护巢、捕猎 | 初始 | 承伤、击杀、保护距离 |
| 喷吐虫 | `Cultiway.InsectSpitter` | 酸液、毒素、远程压制 | 武器腺模块 | 有效伤害、射程利用、反装甲效果 |
| 重甲虫 | `Cultiway.InsectBulwark` | 阻挡、吸收火力、护卫 | 重甲形态模块 | 吸收伤害、队友存活、移动代价 |
| 翼侦虫 | `Cultiway.InsectWingedScout` | 侦察、防空、跨水域行动 | 翼化模块 | 发现目标、追击成功、远征存活 |
| 攻城虫 | `Cultiway.InsectRavager` | 破坏建筑和固定目标 | 攻城形态模块 | 建筑伤害、成本、护送需求 |
| 建巢母虫 | `Cultiway.InsectFounder` | 携带基因快照并建立子巢 | 分巢囊巢器官 | 建巢成功率、迁移距离 |

### 8.1 形态与器官的关系

- 形态决定 ActorAsset 级别的图形、飞行、水陆属性、基础攻击方式和体型。
- 器官决定可组合的属性贡献、Ability、Status 反应、元素适应和 AI 标签。
- 同一种形态可以表达不同器官，例如两只兵虫可以分别拥有耐火膜和层叠甲壳。
- 只有真正需要 ActorAsset 字段的变化才建立新形态，例如飞行和水生。
- 不建立“飞行重甲酸液再生兵虫”之类的资产笛卡尔积。

### 8.2 固定槽位

每个职虫基因模板具有以下槽位：

| 槽位 | 数量 | 说明 |
| --- | --- | --- |
| 甲壳 | 1 | 防护和外表面结构 |
| 运动 | 1 | 移动器官与姿态；部分形态会锁定该槽 |
| 武器腺 | 1 | 主动攻击或攻击附加机制 |
| 代谢 | 1 | 生物质、恢复、饥饿与环境耐受 |
| 神经 | 1 | 个体战术倾向和感知 |

槽位是硬互斥，复杂度预算是软上限。高阶模块可以占用两个逻辑槽位或显著增加生物质成本。

---

## 9. 生物质经济

### 9.1 资源定位

生物质是虫群的唯一生产资源，用于：

- 孵化幼虫。
- 分化高阶职虫。
- 表达高复杂度器官。
- 修复母巢。
- 建造巢器官。
- 产生建巢母虫和建立子巢。

生物质不是 WorldBox 城市资源，不写入 BuildingData。它是 Hive ECS 实体上的 `HiveBiomass` 组件。

### 9.2 生物质来源

1. 敌方或中立生物死亡后生成 `BiomassDeposit`。
2. 工虫到达尸体位置，执行采集并携带有限生物质。
3. 工虫返回母巢范围后提交 `DeliverBiomassCommand`。
4. 母巢接收后增加存量，存量不超过容量。
5. 虫族尸体只能回收基础价值的 30%，避免死亡循环凭空增殖。
6. 植物和小动物可作为低价值来源，但不会提供高级能力样本。

### 9.3 尸体存款

`BiomassDeposit` 是有位置和寿命的临时 ECS 实体：

```text
SourceObjectHandle
WorldPosition
RemainingAmount
CapabilitySamples
CreatedAt
ExpiresAt
ReservedByActor
```

尸体对象消失不影响已经创建的 Deposit。Deposit 到期、耗尽或世界结束时回收。

### 9.4 初始成本建议

以下只是首轮平衡基线，不属于稳定 API：

| 项目 | 生物质 | 孵化时间 |
| --- | ---: | ---: |
| 工虫 | 8 | 1 个月 |
| 兵虫 | 12 | 2 个月 |
| 喷吐虫 | 16 | 3 个月 |
| 翼侦虫 | 18 | 3 个月 |
| 重甲虫 | 24 | 4 个月 |
| 攻城虫 | 32 | 5 个月 |
| 建巢母虫 | 80 | 12 个月 |
| 子巢巢核 | 120 | 建巢时一次支付 |

模块可给最终成本增加 `0%..50%` 的表达附加值。试验个体的附加成本必须计入适应度，防止昂贵变异只凭战斗结果获胜。

---

## 10. 环境压力系统

### 10.1 压力通道

压力通道使用固定枚举，便于 O(1) 更新和有界数组存储。

| 组 | 通道 |
| --- | --- |
| 元素 | 金、木、水、火、土、阴、阳、混沌 |
| 战斗方式 | 近战武器、远程武器、酸液、毒素、疾病、爆炸、重力/击退 |
| 敌人能力 | 飞行威胁、高装甲目标、建筑目标、高机动目标 |
| 环境 | 液体/溺水、饥饿、移动失败 |
| 群体 | 母巢受损、成员伤亡、生产不足 |

首版预计 24 个以内的通道。新增通道需要同时定义事件来源、归一化尺度、衰减时间和至少一个可响应模块。

### 10.2 事件来源

| 事实 | 记录内容 |
| --- | --- |
| `DamageResolved` | 最终实际伤害、最大生命、ElementComposition、AttackType、攻击者形态 |
| `ActorDied` | 死亡个体、存活时间、最近有效攻击者、死亡类型、所属母巢 |
| `DamageDealt` | 虫族攻击者、伤害、目标装甲与形态语义 |
| `EnemyKilled` | 目标价值、实际使用过的能力语义、可回收样本 |
| `HiveDamaged` | 巢核实际伤害、伤害来源、当前守军情况 |
| `ActivityFailed` | 无法到达、追击失败、采集失败、目标逃脱 |
| `MonthlyEcologySample` | 饥饿比例、液体区域暴露、人口结构、生物质缺口 |

### 10.3 伤害严重度

单次个体伤害的基础严重度：

```text
HealthSeverity = clamp(ActualDamage / MaxHealth, 0, 1)
PowerSeverity  = clamp(AttackerPower / max(DefenderPower, 1), 0.5, 3)
RawSeverity    = HealthSeverity * sqrt(PowerSeverity)
```

元素通道按 `ElementComposition` 的归一化权重分配。AttackType 和攻击者已表现出的形态语义可以同时贡献战斗方式通道。

死亡额外贡献：

```text
DeathSeverity = 3 + clamp(RecentDamageFraction, 0, 1)
```

死亡信号绕过普通受伤月度上限，但同一尸体只结算一次。

### 10.4 个体月度贡献上限

每个虫族个体具有轻量的 `InsectObservationBudget`：

```text
WorldMonth
DamagePressureUsed
EnvironmentalPressureUsed
```

同一个体每月由非致死伤害贡献的总严重度有上限。这样不会因为一个高恢复单位持续承伤，使整个巢群错误地认定单一威胁无限增长。

### 10.5 月度归一化

事件首先进入 `HivePressureAccumulator`。每个世界月结算一次：

```text
Observed[k] = AccumulatedSeverity[k] / max(MeanActivePopulation, 1)
Decay       = exp(-ln(2) * DeltaYears / HalfLife[k])
Pressure[k] = Pressure[k] * Decay + Observed[k]
Evidence[k] = Evidence[k] * Decay + AccumulatedEvidence[k]
```

结算后清空月度累计，不清空衰减后的长期压力。

### 10.6 建议衰减时间

| 压力 | 半衰期 |
| --- | ---: |
| 普通伤害方式 | 3 年 |
| 元素伤害 | 4 年 |
| 饥饿和生产不足 | 2 年 |
| 飞行、装甲和建筑目标 | 5 年 |
| 母巢严重受损 | 6 年 |
| 大规模伤亡 | 5 年 |

行为策略读取当前压力，可以快速变化；遗传候选读取压力、证据和历史峰值，变化更慢。

### 10.7 反信息泄露

虫群只能适应已经发生的交互：

- 敌人飞行只有在被发现、攻击或追击后才成为证据。
- 敌人的 Skill 只有实际施放后才贡献该次 Skill 的语义。
- 击杀敌人可以采集其固有形态样本，但不读取从未使用过的全部已学技能。
- 不扫描附近所有敌人的 Trait 和装备来提前生成克制方案。
- 世界中不存在“全知母巢”共享数据；每座母巢只接收成员和巢核观察到的事实。

---

## 11. 能力样本与语义同化

### 11.1 样本用途

压力表示“需要解决什么”，样本表示“虫群是否见过可借鉴的生物结构”。

示例：

- 长期无法追上飞行敌人产生飞行压力。
- 击杀飞行生物提供 `Flight` 形态样本。
- 两者共同满足时，翼化候选获得高分。
- 如果没有飞行样本，翼化仍可在极高压力下自然出现，但证据要求和试验成本更高。

### 11.2 样本来源

- 击杀目标的固有形态：飞行、水生、甲壳、再生、爆炸体。
- 实际命中的攻击：酸液、毒素、远程投射、范围爆炸、元素组成。
- 工虫成功运回的特殊尸体样本。
- 虫族个体成功形成的体细胞适应。

### 11.3 语义系统复用

样本使用项目现有 Semantic 系统表达，不保存任意敌方对象副本。

- WorldBox Adapter 为实际事件提供能力或形态的语义来源。
- `SemanticProfile` 只在事件发生时构建或读取相关切片。
- `HiveSampleBank` 保存稳定 Semantic ID、强度、来源数量和最后观察时间。
- 只允许白名单语义进入遗传候选，例如 `Flight`、`Ranged`、`Acid`、`Poison`、`Armor`、`Regeneration`。
- 样本不会直接授予 Trait 或 Skill，只解锁预定义适应资产。

### 11.4 样本衰减与容量

- 母巢最多保留 32 类样本条目。
- 样本强度半衰期默认 10 年，比普通压力更慢。
- 超出容量时移除强度最低且最久未使用的条目。
- 已固化模块依赖的样本不会因样本衰减而失效。
- 遗传记忆可以降低重新表达旧模块时的样本需求。

---

## 12. 适应定义体系

不建立一个包含所有行为的巨型“Mutation”类型。不同作用域保留独立定义，通过共同的评分描述参与候选选择。

### 12.1 公共评分描述

所有可进化定义都包含 `AdaptationProfile`：

```text
StableId
PressureResponses[]
EvidenceRequirements[]
SemanticSampleRequirements[]
Prerequisites[]
Conflicts[]
ComplexityCost
BiomassCostMultiplier
ReplacementCost
MinimumGeneration
TrialPolicy
EvaluationWeights
```

`PressureResponses` 可以为正或负。正值表示缓解该压力，负值表示在该压力下形成新弱点。

### 12.2 基因器官定义

`InsectGeneModuleAsset` 描述个体或职虫模板中的器官：

```text
Slot
AllowedCastes
StatContributions
AbilityGrantIds
StatusReactionIds
DamageRuleIds
VisualProfileId
RequiredMorphId
```

### 12.3 固定形态定义

`InsectMorphAsset` 描述需要 ActorAsset 支持的生物形态：

```text
ActorAssetId
AllowedCastes
LockedSlots
BaseBiomassCost
MetamorphosisDuration
MetamorphosisVisualId
PathingCapabilities
```

### 12.4 巢器官定义

`HiveOrganAsset` 描述母巢本体结构：

```text
HiveSlot
ConstructionBiomass
ConstructionDuration
CoreStatContributions
BroodModifiers
SensorModifiers
GrantedHiveAbilities
VisualStageId
```

巢器官不是 Actor Trait，也不修改 BuildingData。它由 Hive ECS 组件表达，再通过 Stat Contributor 或适配器影响 WorldBox Building。

### 12.5 神经策略定义

`HiveDoctrineAsset` 描述群体行为偏好：

```text
TargetPriorities
FormationProfile
RetreatPolicy
SupportPolicy
ActivityUtilityModifiers
RequiredCasteCapabilities
```

神经策略可快速切换的部分属于行为可塑性；需要长期固化的高阶协同能力才进入遗传槽位。

---

## 13. 初始适应内容目录

### 13.1 甲壳模块

| 模块 | 主要压力 | 非数值变化 | 主要收益 | 代价 |
| --- | --- | --- | --- | --- |
| 层叠几丁质 | 近战、高装甲敌人 | 外观变厚，偏向重甲形态 | 武器伤害通过率降低 | 移速下降，孵化成本提高 |
| 蜂窝甲壳 | 爆炸、范围攻击 | 将一次冲击分散为多段吸收 | 降低爆发伤害 | 持续伤害更有效 |
| 脱落甲片 | 高单次伤害 | 首次重创后脱壳并短暂加速 | 有限次数的伤害上限 | 脱壳后甲壳槽暂时失效 |
| 滤毒表皮 | 酸液、毒素、疾病 | 降低 Status 持续和层数 | 抗毒、抗疫 | 恢复效率降低 |
| 元素折射膜 | 单一主元素 | 甲壳按元素着色 | 对一种元素适应 | 对次要克制元素产生弱点 |

元素折射膜包含八个数据变体，但共用一套规则实现。甲壳槽只能表达其中一个，不允许同时获得八元素抗性。

### 13.2 运动模块

| 模块 | 主要压力 | 行为变化 | 代价 |
| --- | --- | --- | --- |
| 翼化结构 | 飞行威胁、隔水目标 | 解锁翼侦虫和空中追击 | 甲壳上限降低，成本提高 |
| 两栖气囊 | 液体、溺水、岛屿环境 | 解锁两栖路径和水域采集 | 陆地加速能力降低 |
| 弹射肌腱 | 远程压制、追击失败 | 获得短距跃击 Ability | 高耐力消耗，落地恢复期 |
| 锚定爪 | 重力、击退、守巢 | 抵抗位移，优先固守位置 | 主动追击速度降低 |
| 疾走附肢 | 高机动敌人、捕猎失败 | 提升包抄和目标保持能力 | 甲壳与携带容量降低 |

地下掘行不进入初始目录，直到路径系统拥有明确的地下空间和可攻击语义。

### 13.3 武器腺模块

| 模块 | 主要压力 | 能力 | 代价 |
| --- | --- | --- | --- |
| 酸液囊 | 高装甲、建筑目标 | 远程酸液，叠加腐蚀 | 射速低，生物质成本高 |
| 骨刺列腺 | 飞行和远程敌人 | 高速直线骨刺，可对空 | 对重甲效率低 |
| 毒针腺 | 高生命猎物、持久战 | 近战注毒和持续伤害 | 即时伤害下降 |
| 破城颚 | 建筑、固定目标 | 近战破甲和建筑加成 | 对小型高速目标命中差 |
| 破裂囊 | 大规模伤亡、被包围 | 死亡时产生定向爆裂 | 个体不可回收，成本高 |

### 13.4 代谢模块

| 模块 | 主要压力 | 机制 | 代价 |
| --- | --- | --- | --- |
| 腐食胃 | 生物质不足 | 提升低质量尸体回收率 | 特殊样本提取效率降低 |
| 储能囊 | 饥饿、长途行动 | 延长无补给活动时间 | 速度和闪避下降 |
| 再生组织 | 长期消耗战 | 脱战后消耗携带生物质恢复 | 持续消耗巢群资源 |
| 共生滤器 | 疾病环境 | 将部分疾病压力转为样本 | 火元素伤害更危险 |
| 高效孵化代谢 | 生产不足 | 降低普通职虫孵化时间 | 个体寿命和上限下降 |

### 13.5 神经模块

| 模块 | 主要压力 | 行为变化 | 代价 |
| --- | --- | --- | --- |
| 猎空反射 | 飞行威胁 | 优先保留对空目标，喷吐虫集火 | 地面近敌权重下降 |
| 焦点神经节 | 高威胁单体 | 小队共享目标并集中攻击 | 面对大量分散敌人反应变慢 |
| 分布式神经 | 队长死亡、控制效果 | 队长失效后仍保持基础协同 | 高级复杂指令执行效率下降 |
| 伤亡撤退反射 | 高伤亡 | 达到阈值后撤回巢核重组 | 可能放弃即将击杀的目标 |
| 攻城协同 | 建筑压力 | 重甲虫护送攻城虫，工虫跟随回收 | 野外捕猎效率下降 |

### 13.6 巢器官

| 巢器官 | 主要压力 | 作用 | 代价 |
| --- | --- | --- | --- |
| 厚壁巢核 | 母巢受损 | 改变 Building 防护和受击反应 | 孵化速度下降 |
| 多室孵化腔 | 生产不足 | 增加并行孵化槽 | 持续占用更多生物质 |
| 生物质储囊 | 资源溢出、远征需求 | 增加容量并减少腐败 | 巢核受火焰伤害提高 |
| 感知触须 | 突袭、目标发现不足 | 扩大巢群威胁感知与召回范围 | 占用巢器官槽 |
| 孢子炮台 | 长期远程围攻 | 母巢获得防御 Ability | 孵化和修复共享冷却资源 |
| 分巢囊 | 过度拥挤、资源充足 | 允许产生建巢母虫 | 极高一次性成本 |

---

## 14. 候选生成与评分

### 14.1 评估频率

- 每座母巢每年最多进行一次候选评估。
- 每帧最多评估两座母巢，超出部分排队。
- 已有活动试验、母巢濒毁、人口过低或资源不足时不开始新试验。
- 同一基因槽固化后默认三年内不允许再次替换。

### 14.2 候选集合

候选生成器只枚举以下定义：

1. 与当前前三个主要压力至少有一个正响应。
2. 满足世代、职虫、样本和前置模块要求。
3. 不与不可替换结构冲突。
4. 与当前模板不同。
5. 母巢能够负担最低试验成本。
6. 当前不存在同 ID 的抑制期，或压力已经超过重新尝试阈值。

候选先按稳定 ID 排序，再评分，禁止依赖资产库遍历顺序产生随机差异。

### 14.3 压力需求值

```text
Need[k] = 1 - exp(-Pressure[k] / Scale[k])
Confidence[k] = 1 - exp(-Evidence[k] / EvidenceScale[k])
```

`Need` 表示该问题有多严重，`Confidence` 表示是否已有足够事实。一次极端事件可以产生高 Need，但证据不足会限制候选得分。

### 14.4 候选得分

```text
PressureBenefit = sum(Need[k] * Confidence[k] * Response[candidate, k])
SampleBonus     = MatchedSampleStrength * SampleAffinity
MemoryBonus     = Familiarity * ReactivationWeight
SynergyBonus    = SumSynergyWithCommittedModules
RiskPenalty     = Sum(Need[k] * NegativeResponse[candidate, k])
CostPenalty     = ComplexityCost + BiomassCost + UpkeepCost
ReplacePenalty  = CurrentModuleValue * ReplacementCost
SuppressPenalty = CurrentSuppression

FinalScore = PressureBenefit
           + SampleBonus
           + MemoryBonus
           + SynergyBonus
           - RiskPenalty
           - CostPenalty
           - ReplacePenalty
           - SuppressPenalty
```

### 14.5 选择规则

- `FinalScore` 低于最低阈值时不产生候选。
- 取最高三项进入最终选择池。
- 使用母巢 Seed、基因世代和评估序号生成确定性微扰，幅度不超过总分的 5%。
- 最高分候选通常获选，微扰只用于让压力近似的不同母巢产生有限分化。
- 日志记录前三名、主要收益、主要代价和落选原因。

### 14.6 重新表达旧模块

被替换模块不会从遗传记忆中立即消失。

- 旧模块进入潜伏状态，熟悉度初始为 1。
- 熟悉度半衰期默认 20 年。
- 重新表达旧模块降低样本、复杂度和试验规模要求。
- 仍然需要重新试验，因为环境和其他基因组合可能已经改变。

---

## 15. 对照试验与适应度

### 15.1 试验实体

每次遗传试验创建一个独立 ECS Trial 实体：

```text
TrialIdentity
TrialCandidate
TrialSchedule
TrialBaselineMetrics
TrialVariantMetrics
TrialDecisionState
```

Hive 通过 `HiveActiveTrialRelation` 指向 Trial。参与的个体通过 `TrialCohortRelation` 指向 Trial，并标记 `Baseline` 或 `Variant`。

### 15.2 分组原则

- 只比较同一母巢、同一职虫、相近出生时间的个体。
- 默认下一批目标职虫中 50% 为基准组，50% 为变异组。
- 变异表达率不会超过母巢全部新生个体的 25%，避免一次试验拖垮整个巢群。
- 分组使用稳定轮转，不按个体能力挑选，避免选择偏差。
- 两组都冻结出生时的基因快照，试验期间不会被后续模板变化改写。

### 15.3 最小样本

默认要求：

- 基准组至少 8 个已结算个体。
- 变异组至少 8 个已结算个体。
- 至少发生 12 次与目标压力相关的有效交互。
- 最短运行 2 年，最长运行 5 年。

“已结算”包括死亡、达到最大观察时间或完成足够的角色任务。长期存活个体不需要为等待自然死亡而阻塞试验。

### 15.4 个体适应度维度

适应度不是一个通用击杀分。每种候选通过 `EvaluationWeights` 选择相关维度：

| 维度 | 计算依据 |
| --- | --- |
| 目标压力缓解 | 对目标伤害的实际通过率、状态持续、追击或任务成功 |
| 一般生存 | 观察窗口内存活比例、非目标死亡率 |
| 战斗产出 | 实际伤害、有效击杀、控制和保护贡献 |
| 职责产出 | 工虫搬运量、侦察发现、护卫成功、建筑伤害 |
| 资源效率 | 产出除以孵化成本、维护消耗和回收损失 |
| 行动稳定 | 卡死、不可达、技能失败和撤退失败次数 |

### 15.5 归一化适应度

```text
TargetBenefit = weighted mean of target-specific metrics
GeneralFitness = weighted mean of survival, role output and stability
ResourceFitness = UsefulOutput / max(BiomassCost + Upkeep, 1)

CompositeFitness = TargetWeight * TargetBenefit
                 + GeneralWeight * GeneralFitness
                 + ResourceWeight * ResourceFitness
```

连续指标先按候选定义的合理上限压缩到 `0..1`。极端值使用截尾均值，避免一只异常强个体支配整个试验。

### 15.6 固化条件

同时满足以下条件才固化：

1. 样本量和目标交互量达到最低要求。
2. 变异组目标收益至少比基准组高 15%。
3. 变异组一般生存不低于基准组的 90%。
4. 资源效率没有低于候选定义的底线。
5. 当前主要压力仍存在，没有在试验期间完全消失。

固化后：

- 候选写入对应职虫基因模板或形态槽。
- 原模块进入遗传记忆。
- 基因世代加一。
- 后续新生虫默认表达新模板。
- 现存基准组不会被强制改造。
- 发布 `AdaptationCommittedEvent` 和世界日志。

### 15.7 淘汰与无结论

候选出现以下结果之一：

- `Rejected`：目标收益不足或一般生存过低，进入 5 年抑制期。
- `Inconclusive`：缺少目标交互或两组差异过小，进入 2 年冷却但不记为失败。
- `Interrupted`：母巢摧毁、人口崩溃或世界结束，不产生遗传结论。
- `Committed`：满足全部固化条件。

试验结束后先复制一条有界摘要到母巢历史，再解除关系并回收 Trial 实体。

---

## 16. 个体体细胞适应

### 16.1 定位

体细胞适应用于表现“同一只虫在生存压力下发生有限变化”，但不能替代遗传试验。

- 每个个体同时最多一个体细胞适应。
- 只从当前表现型允许的体细胞候选中选择。
- 强度不超过对应完整遗传模块的 40%。
- 体细胞适应通常伴随寿命、恢复、速度或生物质消耗代价。
- 它不会修改母巢基因模板，也不会传播给其他现存个体。

### 16.2 触发条件

个体的 `InsectSomaticStress` 记录有界压力：

```text
DominantPressure
AccumulatedSeverity
SurvivedCriticalEvents
LastEventAt
```

满足以下条件时可以提出一次个体适应：

1. 个体没有现存体细胞适应。
2. 在当前生命周期内至少三次承受同类有效压力。
3. 至少一次在生命低于 25% 后存活并脱离战斗。
4. 对应体细胞定义与当前形态兼容。
5. 个体没有处于遗传试验的变异组，避免混淆试验结果。

### 16.3 体细胞结果

初始支持：

| 体细胞变化 | 触发 | 效果 | 代价 |
| --- | --- | --- | --- |
| 应激硬化 | 重复武器伤害 | 小幅降低同类伤害 | 速度下降 |
| 瘢痕脱壳 | 单次濒死 | 下一次重创触发脱壳 | 最大生命下降 |
| 弱滤毒 | 多次中毒幸存 | 缩短毒素状态 | 恢复变慢 |
| 低耗代谢 | 长期饥饿 | 降低营养消耗 | 攻击频率下降 |
| 应激再生 | 多次非致死重伤 | 脱战后缓慢恢复 | 消耗携带生物质 |

### 16.4 对母巢的影响

体细胞适应本身不遗传，但会产生证据：

- 个体形成适应时，母巢获得少量对应样本。
- 该个体后续在目标压力下成功存活，会增加候选的正证据。
- 形成适应后快速死亡，会增加风险证据。
- 同一种体细胞适应不会因大量低价值个体重复触发而无限增加样本，母巢每月有接收上限。

---

## 17. 表现型生成与同步

### 17.1 出生时冻结

`InsectPhenotypeService` 在卵进入生产队列时生成 `BroodPhenotypePlan`：

```text
CasteId
MorphId
GenomeGeneration
GeneModuleIdsBySlot
DoctrineVersion
TrialEntity
TrialGroup
BiomassCost
Signature
```

计划创建后保持不变。母巢在孵化期间完成新的固化，不会改变已经支付成本的卵。

### 17.2 表现型签名

签名由以下内容按稳定顺序组成：

```text
CasteId
MorphId
甲壳模块 ID
运动模块 ID
武器腺模块 ID
代谢模块 ID
神经模块 ID
TrialCandidateId
```

签名用于：

- 快速判断是否需要重新同步。
- 对照试验分组。
- 日志和 UI 展示。
- 避免同一来源重复授予 Ability 或 Status。

签名不包含 Actor ID、名字、出生时间和运行时冷却。

### 17.3 表达顺序

个体完成分化后按固定顺序表达：

1. 使用 `ActorTransformationService.TransformInPlace` 切换固定形态。
2. 更新 `InsectIndividual` 的职虫和形态。
3. 写入 `InsectPhenotype` 的模块快照。
4. 由属性贡献器重新计算静态属性。
5. 由 Ability Provider 按模块动态公开能力。
6. 由 Status Service 创建确实需要实例状态的器官效果。
7. 更新战斗标签和活动候选能力。
8. 标记 Actor 属性、技能缓存和图形为脏。
9. 发布 `PhenotypeExpressedEvent`。

### 17.4 转换清理

现有 `TransformInPlace` 会保留旧 Trait、ECS 组件、物品和技能。虫族必须遵守以下规则：

- ActorAsset 的出生 Trait 只放所有形态都允许保留的虫族公共 Trait。
- 可替换器官不能直接实现为永久 ActorTrait。
- 模块能力使用来源可识别的动态 AbilityGrant，不写入 LearnedSkill。
- 模块 Status 使用“个体 + 模块 ID”的来源键，重新表达前由 PhenotypeService 对账。
- 形态转换后调用一次 `SynchronizePhenotype`，移除旧模块来源、添加新来源。
- ECS 中的 Fitness、Membership 和 Trial 关系保持不变。

### 17.5 现存成虫

遗传固化只影响后续 BroodPhenotypePlan。

- 现存成虫保持出生时表现型。
- 只有具备“成虫再蜕变”能力的模块可以主动进入蛹期并重新表达。
- 再蜕变消耗生物质、占用时间，并使个体在蛹期无法行动。
- 第一阶段不开放全体成虫再蜕变，避免固化后瞬间全巢换装。

---

## 18. 职虫生产策略

### 18.1 目标

生产策略属于行为可塑性，比遗传变化快。它根据当前人口、资源和威胁决定下一只幼虫分化为什么，但不能生产尚未被基因解锁的职虫。

### 18.2 人口容量

基础母巢人口上限建议为 64：

| 职虫 | 初始目标比例 |
| --- | ---: |
| 工虫 | 40% |
| 兵虫 | 50% |
| 其他 | 10% |

解锁新职虫后不直接增加总人口，而是在总容量内改变构成。巢器官可以有限提高孵化并行数或人口容量。

### 18.3 职虫效用

```text
Utility(caste) = PopulationShortage
               + PressureResponse
               + ResourceDemand
               + StrategicDemand
               - BiomassScarcity
               - IncubationOpportunityCost
               - CurrentQueueSaturation
```

示例：

- 生物质低于 25% 容量时，提高工虫效用。
- 飞行压力升高且已解锁翼侦虫时，提高翼侦虫和骨刺喷吐虫效用。
- 母巢受损时，提高兵虫和重甲虫效用，降低远征型职虫。
- 建筑目标长期存在时，提高攻城虫效用，但必须保留最低护巢人口。
- 当前试验需要目标职虫时，在安全范围内提高该职虫生产权重。

### 18.4 生产队列

`HiveBroodQueue` 是固定容量队列，默认最多 8 个条目。

- 每次入队冻结表现型和成本。
- 资源在入队时预留，出生成功后正式消耗。
- 巢核被摧毁时，未完成条目全部取消，预留资源只返还一部分。
- 队列只能由 `HiveBroodService` 修改。
- UI 读取不可变队列快照，不能重排。

### 18.5 幼虫分化

幼虫是实际 Actor，不是纯计时器。

- 幼虫停留在母巢安全半径内。
- 幼虫可以被攻击和杀死。
- 幼虫死亡会造成生产损失和伤亡压力。
- 到达分化时间后，命令进入 StructuralCommit 阶段再执行形态转换。
- 幼虫不能在 ECS Query 内直接调用 TransformInPlace。

---

## 19. 虫群 AI 与群体协同

### 19.1 活动类别

| 活动 | 主要参与者 | 触发条件 |
| --- | --- | --- |
| 照料幼虫 | 工虫 | 孵化队列非空 |
| 搬运生物质 | 工虫 | 存在未预留 Deposit |
| 修复巢核 | 工虫 | 巢核受损且有生物质 |
| 巢边警戒 | 兵虫、重甲虫 | 常态最低防御需求 |
| 区域巡逻 | 兵虫、翼侦虫 | 威胁较低且人口充足 |
| 捕猎 | 兵虫、喷吐虫 | 生物质不足且存在目标 |
| 召回防御 | 全战斗职虫 | 母巢威胁达到阈值 |
| 攻击建筑 | 攻城虫及护卫 | 建筑压力和战略需求足够高 |
| 建立子巢 | 建巢母虫及护卫 | 分巢条件满足 |
| 孤虫归巢 | 孤立个体 | 原母巢失效 |

### 19.2 活动候选

实现目标遵循项目架构文档中的 `IActivityCandidateProvider`：

```text
JobId
Category
Utility
Urgency
EstimatedDuration
Cooldown
ExclusiveGroup
Reason
CanStart
```

虫族模块只提交候选，不修改通用选择器。若该接口实现时尚未落地，先建立通用候选合同，不把虫族判断继续塞入 `PatchActor`。

### 19.3 巢群编组

虫群按母巢和战术角色建立稳定小队：

- 每组 6 到 12 个单位。
- 一个组只属于一座母巢。
- 组长优先由神经等级、职虫适配和稳定 Actor ID 决定。
- 重甲虫可以承担前排，喷吐虫保持后排，工虫不进入普通攻击组。
- `ICombatGroupProvider` 向现有战术战斗层公开成员、组长和默认 Directive。
- 成员出生、死亡、归巢变化时增量维护，不重新扫描全体虫族。

### 19.4 行为可塑性策略

`HiveTacticalPolicyService` 每月或威胁变化时重算：

```text
GuardRatio
PatrolRatio
HuntRatio
RetreatHealthThreshold
MaxPursuitDistance
FocusFireEnabled
AntiAirPriority
ProtectWorkersPriority
ProtectHivePriority
```

这些值是当前策略，不进入基因历史。已固化神经模块只改变策略允许范围和效用权重。

### 19.5 敌人信息共享

- 仅同一母巢成员共享近期威胁。
- 共享内容为目标稳定句柄、最后位置、时间、威胁语义和置信度。
- 信息随时间衰减，目标失效后移除。
- 子巢建立后不实时共享母巢战术信息。
- 分布式神经模块可以降低组长死亡造成的信息丢失，但不能跨巢心灵感应。

---

## 20. 战斗系统接入

### 20.1 防御规则

虫族防御模块接入 `FinalDamageStage.Adaptation`：

- Rule 只读取已经提交的 `InsectPhenotype` 和静态模块定义。
- Rule 只修改当前伤害上下文，不创建状态、实体或特效。
- 脱壳、反应性再生等后续行为写入事务 Outbox，由 Consequence 阶段处理。
- 伤害最低值、境界压制和其他阶段仍按现有固定顺序执行。

### 20.2 主动能力

酸液、骨刺、跃击和孢子炮台通过统一 ActiveAbility Provider 暴露：

- `Collect` 根据表现型模块和巢器官返回能力句柄。
- `CanPrepare` 检查形态、资源、冷却和目标关系。
- `ResolveAiWeight` 使用职虫职责和当前 Hive 战术策略。
- `ResolveTacticalProfile` 声明射程、用途、移动意图和目标类型。
- `TryUse` 提交 Ability Request，不直接生成投射物。
- SkillLibV3 只负责轨迹、碰撞、命中和动画。

### 20.3 能力来源

模块能力使用以下来源身份：

```text
SourceKind = InsectPhenotype
SourceHandle = ActorHandle
InstanceKey = PhenotypeSignature + ModuleId
```

巢器官能力使用 Hive Entity 和 Hive Organ Slot 作为来源。模块被替换后，旧来源失效，冷却和持续状态由 Ability Runtime 清理。

### 20.4 属性贡献

虫族模块通过专用 Contributor 构建属性，不直接永久写入 Actor 基础数据。

- Contributor 读取 `InsectPhenotype`。
- 每个模块的属性贡献从静态定义解析。
- 贡献按模块 ID 稳定排序。
- 表现型变化后只标记该 Actor 属性缓存为脏。
- 巢核属性通过 Hive Organ Contributor 作用于绑定 Building 的运行时 Stats。

### 20.5 Status 使用边界

Status 只表示有持续时间或层数的运行时效果：

- 腐蚀。
- 中毒。
- 脱壳后的脆弱期。
- 再蜕变蛹期。
- 个体应激状态。

永久器官本身不是 Status。器官的存在由 `InsectPhenotype` 表达。

### 20.6 目标关系

- 虫族使用统一技术 Kingdom 接入普通文明敌对。
- 同一技术 Kingdom 的不同母巢在第一阶段互不攻击。
- 捕猎中立动物由虫族捕食活动显式选择，不把所有自然生物全局改成敌国。
- Ability 的友军判断除 Kingdom 外，还检查 HiveMembership，避免未来多虫族阵营扩展时混淆。

---

## 21. 分巢、继承与生态型

### 21.1 分巢条件

母巢同时满足以下条件时可以生产建巢母虫：

- 已建造分巢囊。
- 人口达到容量的 85%。
- 生物质达到容量的 75%。
- 当前没有高等级母巢威胁。
- 至少完成一次遗传固化。
- 距离上次分巢超过 10 年。

### 21.2 建巢母虫载荷

建巢母虫拥有 `FounderGenomePayload`：

```text
ParentHiveSerial
GenomeGeneration
DeepClonedCasteGenomes
DeepClonedHiveOrgans
DoctrineIds
GeneticMemorySubset
FounderSeed
ReservedBiomass
```

Payload 是冻结快照，不通过关系引用母巢可变数组。母巢后续进化不会改变已经离巢的母虫。

### 21.3 选址

选址效用考虑：

```text
DistanceFromParent
Reachability
NearbyBiomassPotential
NearbyEnemyThreat
ExistingHiveDensity
LiquidRatio
TerrainCompatibility
EscapeRoute
```

建巢母虫不会直接读取整个世界最优点。它在有限探索半径内评估已经到达或被翼侦虫发现的候选地块。

### 21.4 子巢创建

1. 母虫到达目标并进入固定建巢时间。
2. 消耗 Payload 中预留生物质。
3. WorldBox Adapter 创建母巢 Building。
4. HiveLifecycleService 创建新 Hive Entity。
5. 深拷贝 Payload 中的模板和器官。
6. 对一个非核心槽施加小概率初始偏移，作为子巢差异来源。
7. ParentHiveSerial 仅用于展示，不建立持续共享状态。
8. 建巢母虫被新巢吸收并回收。

### 21.5 生态型形成

生态型不是静态 Asset，而是对当前基因组合的展示分类。

示例分类：

- 熔火甲群：火元素折射膜、厚壁巢核、低翼化比例。
- 沼泽疫群：两栖气囊、共生滤器、毒针腺。
- 天猎群：翼侦虫、骨刺列腺、猎空反射。
- 攻城群：层叠甲壳、破城颚、攻城协同。

分类由 Presentation 层根据语义权重解析，不作为逻辑分支条件。

---

## 22. ECS 数据模型

### 22.1 总体原则

- 所有运行时组件位于当前统一 EntityStore。
- Hive、Trial、BiomassDeposit 都是独立 ECS 实体。
- Actor 的虫族状态附着在 ActorExtend 的 ECS Entity 上。
- WorldBox 对象只通过 Binder 解析。
- 组件使用有界数组和稳定 ID，不在每个 Actor 上放 Dictionary。
- Query 只读期间不进行结构修改。
- 所有数组对外查询时复制为 ReadModel，不把可变引用交给 UI。

### 22.2 Hive 实体组件

```csharp
public struct HiveTag : IComponent;

public struct HiveCoreBinder : IComponent
{
    public long BuildingId;
}

public struct HiveIdentity : IComponent
{
    public long Serial;
    public long ParentSerial;
    public int GenomeGeneration;
    public uint RandomSeed;
    public double FoundedAt;
    public HiveLifecycleState State;
}

public struct HiveBiomass : IComponent
{
    public float Stored;
    public float Reserved;
    public float Capacity;
}

public struct HivePopulationState : IComponent
{
    public int Total;
    public int Larvae;
    public int[] ByCaste;
    public float MeanActivePopulationThisMonth;
}

public struct HivePressureAccumulator : IComponent
{
    public float[] Severity;
    public float[] Evidence;
    public int WorldMonth;
}

public struct HivePressureState : IComponent
{
    public float[] Pressure;
    public float[] Evidence;
    public float[] HistoricalPeak;
    public int LastSettledMonth;
}

public struct HiveSampleBank : IComponent
{
    public HiveSemanticSample[] Entries;
    public int Count;
}

public struct HiveGenome : IComponent
{
    public CasteGenomeTemplate[] Castes;
    public HiveOrganSlotState[] HiveOrgans;
    public string[] DoctrineIds;
    public GeneticMemoryEntry[] Memory;
    public int Version;
}

public struct HiveBroodQueue : IComponent
{
    public BroodQueueEntry[] Entries;
    public int Head;
    public int Count;
}

public struct HiveTacticalPolicy : IComponent
{
    public float GuardRatio;
    public float PatrolRatio;
    public float HuntRatio;
    public float RetreatHealthRatio;
    public float MaxPursuitDistance;
    public int Version;
}

public struct HiveEvolutionSchedule : IComponent
{
    public double NextCandidateAt;
    public int CandidateSequence;
    public double LastCommitAt;
}
```

数组在实体创建时按固定上限分配。只有所属服务可以修改数组内容；不得把组件副本中的数组替换为共享临时数组。

### 22.3 Actor 虫族组件

```csharp
public struct InsectIndividual : IComponent
{
    public string CasteId;
    public string MorphId;
    public int BirthGenomeGeneration;
    public double BornAt;
    public InsectLifeStage Stage;
}

public struct InsectPhenotype : IComponent
{
    public string Signature;
    public string[] ModuleIdsBySlot;
    public int Version;
}

public struct InsectFitnessState : IComponent
{
    public float DamageDealt;
    public float DamageTaken;
    public float RelevantDamageTaken;
    public float KillValue;
    public float RoleOutput;
    public float BiomassConsumed;
    public float BiomassDelivered;
    public int ActionFailures;
    public double EvaluationStartedAt;
}

public struct InsectObservationBudget : IComponent
{
    public int WorldMonth;
    public float DamagePressureUsed;
    public float EnvironmentPressureUsed;
}

public struct InsectSomaticStress : IComponent
{
    public InsectPressureChannel DominantPressure;
    public float Severity;
    public int CriticalSurvivals;
    public double LastEventAt;
}

public struct InsectSomaticAdaptation : IComponent
{
    public string AdaptationId;
    public float Potency;
}

public struct InsectMetamorphosis : IComponent
{
    public string TargetCasteId;
    public string TargetMorphId;
    public double CompleteAt;
    public string TargetPhenotypeSignature;
}
```

`InsectPhenotype.ModuleIdsBySlot` 长度固定为槽位数量。字符串引用静态资产的共享 ID，不为每个个体复制字符串内容。

### 22.4 Trial 实体组件

```csharp
public struct AdaptationTrial : IComponent
{
    public int Sequence;
    public string CandidateId;
    public string TargetCasteId;
    public string ReplacedModuleId;
    public double StartedAt;
    public double EarliestDecisionAt;
    public double DeadlineAt;
    public TrialLifecycleState State;
}

public struct TrialCohortMetrics : IComponent
{
    public CohortMetrics Baseline;
    public CohortMetrics Variant;
    public int RelevantInteractions;
}

public struct TrialDecision : IComponent
{
    public TrialOutcome Outcome;
    public float BaselineFitness;
    public float VariantFitness;
    public float TargetImprovement;
    public float GeneralViabilityRatio;
    public string ReasonId;
}
```

### 22.5 临时世界实体

```csharp
public struct BiomassDeposit : IComponent
{
    public float Remaining;
    public double ExpiresAt;
    public long SourceObjectId;
}

public struct BiomassCarrier : IComponent
{
    public float Carried;
    public Entity SourceDeposit;
}

public struct FounderGenomePayload : IComponent
{
    public HiveGenomeSnapshot Snapshot;
    public float ReservedBiomass;
}

public struct OrphanedInsect : IComponent
{
    public long FormerHiveSerial;
    public double OrphanedAt;
}
```

---

## 23. ECS 关系模型

### 23.1 个体归巢

```csharp
public struct HiveMembershipRelation : ILinkRelation
{
    public Entity Hive;
    public HiveMemberRole Role;
    public int GroupIndex;

    public Entity GetRelationKey() => Hive;
}
```

规则：

- 一个 Actor ECS 实体最多拥有一个 HiveMembershipRelation。
- 关系只能由 `HiveMembershipService` 添加、更新和移除。
- Hive 人口可以通过 IncomingLinks 校验，PopulationState 是增量缓存。
- PopulationState 与关系不一致时，以关系为权威并记录诊断错误。

### 23.2 活动试验

```csharp
public struct HiveActiveTrialRelation : ILinkRelation
{
    public Entity Trial;
    public Entity GetRelationKey() => Trial;
}

public struct TrialCohortRelation : ILinkRelation
{
    public Entity Trial;
    public TrialGroup Group;
    public Entity GetRelationKey() => Trial;
}
```

规则：

- 一座 Hive 最多指向一个活动 Trial。
- 一个个体最多属于一个 Trial。
- Baseline 与 Variant 都添加 TrialCohortRelation。
- Trial 结束后由 TrialService 统一移除全部 IncomingLinks。

### 23.3 能力与状态来源

器官能力沿用统一 AbilityGrant 关系；状态沿用 StatusRelation。虫族不新增平行的技能所有权或状态列表。

### 23.4 不使用的关系

- 不用 ActorData 保存 Hive ID。
- 不用 BuildingData 保存 Hive Entity ID。
- 不用静态 `Dictionary<long, Membership>` 作为权威成员表。
- 不让子巢通过可变 Entity 引用共享母巢 Genome。

---

## 24. 数据所有权与唯一写入者

| 状态 | 唯一写入者 | 其他系统如何交互 |
| --- | --- | --- |
| Hive 创建、状态、销毁 | `HiveLifecycleService` | Command/Event |
| HiveMembershipRelation | `HiveMembershipService` | Join/Leave Command |
| HiveBiomass | `HiveBiomassService` | Reserve/Spend/Deliver Command |
| PressureAccumulator/State | `HivePressureService` | Observation Event |
| HiveSampleBank | `HiveSampleService` | SampleObserved Event |
| HiveGenome、遗传记忆 | `HiveEvolutionService` | Trial Outcome Command |
| Trial 实体和关系 | `HiveTrialService` | Begin/Record/Resolve Command |
| HiveBroodQueue | `HiveBroodService` | Enqueue/Cancel Command |
| InsectPhenotype | `InsectPhenotypeService` | Express/Metamorphose Command |
| InsectFitnessState | `InsectFitnessService` | Damage/Task/Death Event |
| HiveTacticalPolicy | `HiveTacticalPolicyService` | Pressure/Threat Query |
| AbilityGrant/Runtime | 通用 Ability Service | Grant/Revoke Command |
| Status | 通用 Status Service | Apply/Remove Command |

服务不得直接修改其他服务拥有的组件。跨领域变化通过明确 Command 提交，已发生结果通过 Event 发布。

---

## 25. Command、Event、Rule 与 Query

### 25.1 Commands

主要 Commands：

```text
CreateHiveCommand
BeginHiveCollapseCommand
JoinHiveCommand
LeaveHiveCommand
ReserveBiomassCommand
SpendBiomassCommand
DeliverBiomassCommand
EnqueueBroodCommand
CancelBroodCommand
SpawnLarvaCommand
BeginMetamorphosisCommand
CompleteMetamorphosisCommand
BeginAdaptationTrialCommand
AssignTrialCohortCommand
ResolveAdaptationTrialCommand
CommitAdaptationCommand
RejectAdaptationCommand
ApplySomaticAdaptationCommand
CreateBiomassDepositCommand
CollectBiomassCommand
CreateFounderCommand
FoundDaughterHiveCommand
```

每个 Command 返回明确结果，例如 `Accepted`、`RejectedNoBiomass`、`RejectedInvalidHive`、`Completed`。异步孵化和蜕变返回 Ticket 或实体句柄，不用布尔值猜测最终完成。

### 25.2 Events

主要 Events：

```text
HiveCreatedEvent
HiveDamagedEvent
HiveCollapsedEvent
InsectJoinedHiveEvent
InsectLeftHiveEvent
InsectBornEvent
PhenotypeExpressedEvent
DamageObservedEvent
EnemyCapabilityObservedEvent
InsectDiedEvent
BiomassDepositCreatedEvent
BiomassDeliveredEvent
TrialStartedEvent
TrialCohortSettledEvent
TrialResolvedEvent
AdaptationCommittedEvent
AdaptationRejectedEvent
SomaticAdaptationFormedEvent
DaughterHiveFoundedEvent
```

Event 表示已经提交的事实。Handler 只能提交新 Command，不能回滚原事务。

### 25.3 Damage Rule

虫族只注册一个稳定 Rule 入口：

```text
RuleId: content.insects.phenotype_adaptation
Stage: FinalDamageStage.Adaptation
```

Rule 内部按模块稳定 ID 顺序解析防御贡献。具体模块不各自注册全局回调，避免初始化顺序改变伤害结果。

### 25.4 Queries

领域 Query 返回不可变快照：

```text
GetHiveOverview(HiveHandle)
GetHivePressureProfile(HiveHandle)
GetHiveGenome(HiveHandle)
GetActiveTrial(HiveHandle)
GetInsectPhenotype(ActorHandle)
GetBroodQueue(HiveHandle)
FindCompatibleHive(ActorHandle, Range)
CollectHiveMembers(HiveHandle, Filter)
```

UI 和调试工具不能取得可写 Entity 或组件数组。

---

## 26. 系统执行顺序

虫族系统接入项目目标调度阶段：

### 26.1 Ingress

- 消费 WorldBox Actor/Building 创建、伤害、死亡和行为结果输入。
- 校验 WorldGeneration。
- 将稳定输入转换为领域 Event 或 Command。
- 不修改 ECS 结构。

### 26.2 QueryAndPlan

- 计算活动候选。
- 计算职虫生产效用。
- 选择可采集 BiomassDeposit。
- 生成到期母巢的适应候选评分。
- 只产生计划和 Command。

### 26.3 CommandExecution

- 执行资源预留和消费。
- 更新现有非结构组件。
- 处理成员加入、退出意图。
- 开始和结束 Trial 事务。

### 26.4 AbilityScheduling 与 SkillSimulation

- 使用现有 Ability 和 Skill 系统。
- 虫族模块不建立独立技能更新循环。

### 26.5 ConsequenceExecution

- 消费 Damage、Death、AbilityCompleted 和 ActivityCompleted 事实。
- 更新 Fitness、PressureAccumulator 和 SampleBank。
- 创建后续 Command，不在事件回调中直接改结构。

### 26.6 StateTick

- 月度压力结算与衰减。
- 孵化和蜕变计时。
- Trial 最短时间和截止时间检查。
- 样本、遗传记忆、威胁信息衰减。
- 孤虫归巢和 Deposit 过期。

### 26.7 StructuralCommit

- 创建 Hive、Trial、Deposit 实体。
- 添加和移除组件与关系。
- 执行 Actor 形态转换。
- 回收已完成临时实体。

### 26.8 Maintenance 与 Recycle

- 校验 Binder 对应 WorldBox 对象仍有效。
- 对比关系与 PopulationState。
- 清理失效句柄和完成历史。
- 输出预算超限与不变量诊断。

---

## 27. WorldBox 适配边界

### 27.1 InsectWorldboxAdapter

唯一负责 WorldBox 与虫族领域互转：

- 母巢 Building 创建、受伤和摧毁。
- 虫族 Actor 创建、伤害、攻击、死亡和变形。
- WorldTile 位置、液体和可达性查询。
- ActorAsset、BuildingAsset 与 KingdomAsset 解析。
- 领域命令要求生成 Actor 或 Building 时调用 WorldBox API。

Harmony Patch 不直接引用 HiveEvolutionService 等具体实现。Patch 只调用稳定 Adapter Port。

### 27.2 Binder 规则

- `ActorBinder` 继续使用现有 ActorExtendManager。
- 新增 `HiveCoreBinder` 只保存 Building ID 和可选运行时缓存引用。
- Binder 解析失败代表宿主失效，提交生命周期命令。
- 延迟任务不保存裸 Actor、Building 或 Entity 引用，使用 WorldGeneration + ID/Handle。

### 27.3 资产级限制

以下变化必须通过固定 ActorAsset 形态实现：

- `flying`。
- `force_ocean_creature` / `force_land_creature`。
- 基础动画集合。
- 体型和碰撞表现。
- 原版近战/远程武器骨架。

以下变化通过 ECS 模块实现：

- 属性贡献。
- 最终伤害适应。
- Ability 和攻击附加效果。
- Status 反应。
- AI 权重、编队和目标偏好。
- 生物质、试验和遗传状态。

---

## 28. 静态定义与资产注册

### 28.1 ApplicationScope 资产库

建议新增：

```text
InsectCasteLibrary
InsectMorphLibrary
InsectGeneModuleLibrary
HiveOrganLibrary
HiveDoctrineLibrary
SomaticAdaptationLibrary
InsectVisualProfileLibrary
```

这些资产库只保存不可变定义，不保存当前世界母巢或个体状态。

### 28.2 注册阶段

1. RegisterDefinitions。
2. LinkAndValidateDefinitions。
3. 构建按稳定 ID 排序的候选索引。
4. 校验所有 Module 的 Slot、Caste、Morph、Ability、Status 和语义引用。
5. 校验压力响应至少包含一个正值。
6. 校验冲突和前置关系无环。
7. 校验 TrialPolicy 与 EvaluationWeights 完整。

### 28.3 数据驱动边界

- 数值、语义、槽位、前置和引用可以数据化。
- 复杂行为由明确的 Effect、Ability、Rule 或 Service 实现。
- 不在 JSON 中保存任意 C# 类型名、反射方法名或脚本字符串。
- 第一批定义可以直接在 C# Asset Library 注册，等字段稳定后再决定是否增加内容文件加载器。

---

## 29. UI 与信息呈现

### 29.1 UI 原则

- 正式 UI 只读，不提供手动进化按钮。
- UI 只消费 Feature Query 返回的 ReadModel。
- UI 不持有可写 Entity，不直接读取组件数组。
- 所有文本、模块名、候选原因和日志必须本地化。
- 使用项目现有 `UiScrollPane`、`UiWeightedSegmentBar`、对象池和 Tooltip。

### 29.2 个体详情页

在 CreatureInfo 中注册“虫群表现型”页面，展示：

- 所属母巢与生态型名称。
- 出生基因世代。
- 职虫和固定形态。
- 五个器官槽。
- 体细胞适应。
- 是否属于当前试验及分组。
- 当前职责和小队。
- 模块带来的能力与代价摘要。

### 29.3 母巢观察窗口

通过“观察母巢”世界工具选中巢核，打开只读窗口。窗口分为：

1. **概览**：人口、生物质、巢核状态、当前生态型。
2. **压力**：主要压力、证据、变化趋势和最近来源。
3. **基因**：各职虫模板的形态与器官槽。
4. **试验**：候选原因、两组数量、目标指标和剩余期限。
5. **生产**：职虫目标比例和孵化队列。
6. **历史**：最近 16 次固化、淘汰、分巢和巢核危机。

不显示内部随机 Seed 和未经压缩的公式中间值。调试模式可以额外显示完整评分分解。

### 29.4 世界日志

重要事件写入 WorldLog：

- 母巢建立。
- 第一次提出遗传试验。
- 变异固化或淘汰。
- 解锁新职虫形态。
- 形成明确生态型。
- 建立子巢。
- 母巢崩溃。

日志文字必须包含因果，例如“因长期火焰伤亡，赤脊巢群固化了元素折射膜”，而不是只显示模块 ID。

---

## 30. 视觉、动画与资源

### 30.1 资源目录

建议路径：

```text
GameResources/actors/species/other/Insects/Larva/
GameResources/actors/species/other/Insects/Worker/
GameResources/actors/species/other/Insects/Warrior/
GameResources/actors/species/other/Insects/Spitter/
GameResources/actors/species/other/Insects/Bulwark/
GameResources/actors/species/other/Insects/WingedScout/
GameResources/actors/species/other/Insects/Ravager/
GameResources/actors/species/other/Insects/Founder/
GameResources/buildings/mobs/Cultiway.InsectHive/
GameResources/cultiway/icons/insects/modules/
GameResources/cultiway/icons/insects/castes/
GameResources/cultiway/effect/insects/
```

### 30.2 视觉层级

- 固定形态使用独立主贴图和动画。
- 甲壳模块通过有限覆盖层、色调和粒子表达，不为每个组合绘制整套单位。
- 武器腺通过攻击前摇和发射点表现。
- 体细胞适应使用轻量局部标识，避免与完整遗传模块混淆。
- 试验组只在调试或选中时显示微弱标记，正式世界中不显示明显 UI 标签。
- 母巢器官使用建筑阶段贴图或独立附着视觉，不直接缩放一张母巢图代替结构变化。

### 30.3 必要动画

- 幼虫孵化。
- 幼虫结蛹。
- 完成蜕变。
- 脱壳。
- 酸液喷吐和骨刺发射。
- 工虫采集和运回。
- 建巢母虫建造巢核。
- 巢器官施工和完成。

---

## 31. 性能与内存预算

### 31.1 设计负载

首版设计目标：

- 32 座活动母巢。
- 4096 个虫族个体。
- 每座母巢最多一个活动 Trial。
- 每座母巢最多 24 个压力通道、32 个语义样本和 16 条历史摘要。
- 每个个体固定 5 个器官槽，不保存动态 Dictionary。

### 31.2 时间复杂度

| 操作 | 目标复杂度 |
| --- | --- |
| 记录一次伤害 | O(压力通道中的实际元素数)，上限常数 |
| 个体死亡 | O(1) + 固定槽位结算 |
| 成员加入/退出 | O(1) Relation 操作 |
| 月度压力结算 | O(Hive × PressureChannel) |
| 候选评估 | O(EligibleDefinitions × PressureChannel)，低频预算执行 |
| Trial 结算 | O(固定指标数) |
| UI ReadModel | O(单个 Hive 成员或固定摘要)，按需执行 |

### 31.3 禁止的性能模式

- 每帧遍历全部虫族 ActorAsset.units。
- 每次伤害重新构建母巢全部成员列表。
- 每个个体保存完整母巢压力数组。
- 每帧为所有敌人构建完整 SemanticProfile。
- 在 ECS Query 中创建或删除关系。
- 在渲染系统中修改模拟组件。
- 每次 UI Refresh 反序列化或复制整个世界状态。

### 31.4 调度预算

初始建议：

| 工作 | 每帧预算 |
| --- | ---: |
| 母巢常规 Tick | 8 |
| 候选评估 | 2 |
| 孤虫归巢 | 8 |
| Deposit 维护 | 32 |
| 到期蜕变提交 | 16 |
| Trial 决策 | 2 |

预算超出时进入队列，不通过全量补算制造卡顿。

---

## 32. 诊断与可解释性

### 32.1 决策追踪

每次候选评估记录：

```text
HiveSerial
GenomeGeneration
EvaluationSequence
TopPressures
EligibleCandidateCount
TopThreeScores
SelectedCandidate
RejectedReasons
RandomPerturbation
CurrentBiomass
CurrentPopulation
```

每次 Trial 结算记录两组指标、最终阈值和结果原因。

### 32.2 调试命令

建议提供开发模式命令：

```text
insect.hive.dump <buildingId>
insect.pressure.add <buildingId> <channel> <value>
insect.evolution.evaluate <buildingId>
insect.trial.resolve <buildingId>
insect.biomass.add <buildingId> <value>
insect.spawn <buildingId> <casteId>
insect.verify
```

正式 UI 不暴露这些命令。

### 32.3 不变量

维护阶段检查：

1. 每个虫族 Actor 恰好零或一个 HiveMembershipRelation。
2. 每个正常活动 Hive 都能解析有效巢核 Building。
3. 每座 Hive 最多一个活动 Trial。
4. 每个 Trial 恰好被一座 Hive 指向。
5. Trial 个体的职虫和出生时间满足分组条件。
6. 每个职虫模板每个槽最多一个模块。
7. 所有模块 ID、形态 ID、Ability ID 和 Status ID 都存在。
8. `Stored >= 0`、`Reserved >= 0` 且 `Stored + Reserved <= Capacity`。
9. PopulationState 与 Incoming HiveMembership Links 一致。
10. 已结束 Trial 不再拥有 Cohort IncomingLinks。
11. 已回收 Hive 不再被 Actor、Trial 或 Founder Payload 引用。

开发构建发现不变量失败时记录完整实体和关系信息，不静默修正权威状态。

---

## 33. 边界情况

### 33.1 母巢人口过低

- 少于 8 个成年个体时暂停新遗传试验。
- 生产策略优先恢复工虫和基础兵虫。
- 活动 Trial 可以标记 Interrupted，不能用极小样本固化。

### 33.2 压力在试验期间消失

- Trial 继续等待目标交互，直到截止时间。
- 截止时仍无足够交互则 `Inconclusive`。
- 不把没有敌人的长期存活误判为抗性成功。

### 33.3 变异组全部早死

- 达到最小失败样本后可以提前 Reject。
- 一般生存低于基准组 60% 时不必等待完整截止时间。
- 未孵化完成的个体不计入适应度。

### 33.4 基准组没有遭遇压力

- 不允许与不同时间、不同战争环境中的旧历史直接比较。
- 延长试验或判定无结论。
- 不用全巢历史平均值伪造同代基准。

### 33.5 模块定义热重载

当前版本不支持活动世界中改变槽位、冲突和形态引用。

- 纯本地化和视觉资源可以重载。
- 平衡数值重载后标记相关属性缓存为脏。
- 结构定义变化要求重新开始世界。

### 33.6 WorldBox 对象被力量直接删除

- Adapter 将删除视为正常生命周期事实。
- Actor 删除仍结算成员退出，但没有真实死亡上下文时不产生遗传死亡证据。
- Building 强制删除触发 Hive Collapsing，不伪造敌方攻击压力。

### 33.7 世界暂停与倍速

- 所有年、月、孵化和 Trial 时间使用世界模拟时间。
- 世界暂停时不推进。
- 倍速只提高模拟时间，不改变每帧预算；积压按队列逐步处理。

---

## 34. 代码目录与模块边界

建议垂直组织：

```text
Source/Content/Insects/
  InsectModule.cs
  Definitions/
    InsectCasteAsset.cs
    InsectMorphAsset.cs
    InsectGeneModuleAsset.cs
    HiveOrganAsset.cs
    HiveDoctrineAsset.cs
    SomaticAdaptationAsset.cs
  Components/
    HiveComponents.cs
    InsectComponents.cs
    TrialComponents.cs
    BiomassComponents.cs
  Relations/
    HiveMembershipRelation.cs
    HiveActiveTrialRelation.cs
    TrialCohortRelation.cs
  Commands/
  Events/
  Queries/
  Services/
    HiveLifecycleService.cs
    HiveMembershipService.cs
    HiveBiomassService.cs
    HivePressureService.cs
    HiveSampleService.cs
    HiveEvolutionService.cs
    HiveTrialService.cs
    HiveBroodService.cs
    InsectPhenotypeService.cs
    InsectFitnessService.cs
    HiveTacticalPolicyService.cs
  Systems/
    InsectIngressSystem.cs
    HivePressureSettlementSystem.cs
    HiveEvolutionPlanningSystem.cs
    HiveBroodPlanningSystem.cs
    InsectMetamorphosisSystem.cs
    HiveTrialResolutionSystem.cs
    InsectMaintenanceSystem.cs
  ActiveAbilities/
    InsectPhenotypeAbilityProvider.cs
    HiveOrganAbilityProvider.cs
  Combat/
    InsectAdaptationDamageRule.cs
    InsectCombatGroupProvider.cs
  WorldboxAdapters/
    InsectWorldboxAdapter.cs
  Presentation/
    InsectPresentationService.cs
    HiveReadModels.cs
    InsectReadModels.cs
```

现有资产入口继续使用 partial 文件：

```text
Source/Content/Actors.Insects.cs
Source/Content/Buildings.Insects.cs
Source/Content/KingdomAssets.Insects.cs
Source/Content/ActorJobs.Insects.cs
Source/Content/ActorTasks.Insects.cs
Source/Content/CoordinationActivities.Insects.cs
Source/Content/UI/CreatureInfoPages/InsectPhenotypePage.cs
Source/Content/UI/WindowHiveEvolution.cs
```

### 34.1 依赖方向

```text
Insect Presentation -> Insect Queries / Commands + UI Foundation
Insect Feature      -> Combat / Ability / Status / Semantics / Runtime
Worldbox Adapter    -> Insect Ports + WorldBox
Runtime/Core        -X-> Insect Feature
UI Foundation       -X-> Insect Feature
```

### 34.2 前置基础

实现前需要项目具备或先补充最小版本：

- 世界代次和 WorldOwned 实体生命周期。
- 允许 Feature 注册系统的组合根入口。
- ECS StructuralCommit 阶段。
- typed Ingress/Event/Command 接口，至少覆盖虫族需要的事实。
- Building 到 Feature Binder 的稳定适配方式。

这些是通用运行时能力，不应在虫族目录中建立一套只供虫族使用的替代框架。

---

## 35. 分阶段实施计划

### 阶段 0：运行时合同

目标是建立虫族所需的最小通用边界，不增加玩法。

- WorldGeneration 与 WorldOwned 生命周期。
- InsectModule 生命周期入口。
- Ingress、Command、Event 和 StructuralCommit 最小合同。
- HiveCoreBinder 和 HiveRepository。
- 虫族 ECS 实体计数与世界清理诊断。

完成条件：连续创建和清理世界后没有残留 Hive、Trial、Deposit 或延迟请求。

### 阶段 1：最小生命循环

内容范围：

- 原始母巢。
- 幼虫、工虫、兵虫。
- 生物质 Deposit、采集、运回和孵化。
- 个体归巢关系。
- 母巢受袭、成员死亡和崩溃。
- 只读个体表现型页面。

完成条件：一座母巢能够从初始资源出发，自主维持工虫、兵虫和生物质循环。

### 阶段 2：第一条进化闭环

内容范围：

- 火、武器、毒素三类压力。
- 层叠几丁质、滤毒表皮、火元素折射膜、酸液囊。
- 喷吐虫固定形态。
- 候选评分、Trial 实体、基准/变异分组、固化与淘汰。
- 母巢观察窗口中的压力、基因和试验页。

完成条件：长期火焰环境能通过真实对照试验固化耐火方向，并只影响后续新生代。

### 阶段 3：完整器官与职虫生产

内容范围：

- 五类个体槽位。
- 重甲虫、翼侦虫。
- 动态职虫效用和生产比例。
- 体细胞适应。
- 语义样本和遗传记忆。
- 模块来源 Ability、Status 和属性贡献统一对账。

完成条件：飞行、远程、装甲、水域和饥饿压力能产生不同的形态与生产结构。

### 阶段 4：群体战术

内容范围：

- 稳定战斗编组。
- 猎空、集火、撤退、攻城和护巢策略。
- 攻城虫。
- 巢器官：厚壁、孵化腔、储囊、感知触须、孢子炮台。
- 完整活动候选和选择原因诊断。

完成条件：神经模块真实改变目标选择、编队和撤退，而不是只修改战斗数值。

### 阶段 5：分巢与生态型

内容范围：

- 分巢囊和建巢母虫。
- 有限探索选址。
- 基因快照深拷贝。
- 子巢初始偏移和独立演化。
- 生态型命名与世界日志。

完成条件：两个子巢在不同环境运行多个世代后形成可辨识的不同生态型。

### 阶段 6：表现与平衡

内容范围：

- 全部贴图、模块覆盖层和蜕变动画。
- 长时间世界模拟参数调整。
- UI 长文本、空状态和大量历史测试。
- 性能预算、决策追踪和 Debug 命令。

完成条件：核心场景稳定、可解释，性能没有随世界总 Actor 数出现非预算增长。

---

## 36. 测试场景与验收标准

### 36.1 基础生命周期

1. 放置母巢后创建且只创建一个 Hive Entity。
2. 幼虫出生后 Actor ECS 实体获得正确归巢关系。
3. 工虫能够采集 Deposit 并增加 HiveBiomass。
4. 母巢摧毁后停止孵化，所有成员关系被处理。
5. 清理世界后虫族世界实体和索引数量归零。

### 36.2 压力正确性

1. 火元素伤害主要增加火压力，不平均增加全部元素。
2. 零伤害、无效命中和重复死亡不贡献压力。
3. 同一个体持续承伤受月度上限约束。
4. 大母巢压力按人口归一化，不因单位多自动进化更快。
5. 压力在停止暴露后按半衰期衰减。

### 36.3 试验正确性

1. 基准组和变异组来自同职虫和相近出生时间。
2. 试验期间模板变化不改写已有两组表现型。
3. 没有目标交互时不能固化。
4. 变异目标收益提高但一般生存崩溃时必须 Reject。
5. 固化后只影响新生产计划。
6. Trial 结束后所有关系和临时实体正确回收。

### 36.4 非数值适应

必须至少验证：

- 飞行压力解锁并增加翼侦虫，单位实际能够飞行和对空追击。
- 高装甲目标促进酸液喷吐，单位实际使用新的远程 Ability。
- 高伤亡促进撤退策略，小队实际放弃追击并返回巢核。
- 生物质不足提高工虫比例，实际改变下一批职虫构成。
- 水域压力产生两栖形态，实际改变可达区域。

### 36.5 分化验收

创建两个完全相同的母巢：

- A 巢长期承受火焰与近战。
- B 巢长期面对飞行与远程。
- 运行至少 30 个世界年。

验收结果：

- 两巢主要压力、试验历史和已固化模块不同。
- A 巢偏向元素甲壳、重甲和护巢。
- B 巢偏向翼侦、骨刺和猎空神经。
- 两者都受槽位和成本限制，没有获得对方全部优势。

### 36.6 性能验收

- 4096 个虫族、32 座母巢下不存在逐帧全体扫描。
- 普通伤害记录不产生托管堆分配。
- 候选评估和 Trial 决策遵守每帧预算。
- 关闭 UI 时不持续构建 ReadModel。
- 世界清理后事件队列、关系和索引不存在上一世界句柄。

---

## 37. 存档范围声明

当前版本明确不实现存档。

- 不向 ActorData 写入职虫、基因、母巢、压力、试验或样本。
- 不向 BuildingData 写入 Hive Entity、资源、基因或人口。
- 不向全局 ModSaveManager 写入世界虫族状态。
- 不建立 JSON、SQLite 或自定义文件格式。
- 不为已保存的 WorldBox 虫族 Actor/Building 增加无状态重建规则。
- 不设计版本号、迁移和兼容层。

未来开始存档设计时，应以 WorldSession 中的 Hive、Trial、Actor 虫族组件和关系为完整权威图，设计单独的世界级快照。届时另写持久化文档，不反向改变本文的数据所有权。

---

## 38. 首轮参数基线

| 参数 | 初始值 |
| --- | ---: |
| 母巢基础人口上限 | 64 |
| 孵化队列容量 | 8 |
| 同时活动遗传试验 | 1 |
| 候选评估间隔 | 1 年 |
| 同槽固化锁定 | 3 年 |
| 普通伤害压力半衰期 | 3 年 |
| 样本半衰期 | 10 年 |
| 遗传记忆半衰期 | 20 年 |
| Trial 最短时间 | 2 年 |
| Trial 最长时间 | 5 年 |
| 每组最低结算个体 | 8 |
| 最低相关交互 | 12 |
| 目标收益最低提高 | 15% |
| 一般生存最低比例 | 基准组的 90% |
| 变异组占目标职虫比例 | 50% |
| 变异组占全体新生上限 | 25% |
| 个体体细胞适应上限 | 1 |
| 体细胞相对完整模块强度 | 不超过 40% |
| 生物质样本条目上限 | 32 |
| 母巢历史摘要上限 | 16 |
| 子巢最短间隔 | 10 年 |

这些值用于第一轮模拟，不应散落硬编码在各服务中。统一由 `InsectBalanceProfile` 静态定义提供。

---

## 39. 最终架构结论

虫群自适应进化由五个互相约束的部分构成：

1. 真实事件形成有衰减、有人口归一化的环境压力。
2. 行为策略先快速响应，遗传系统只处理长期无法解决的问题。
3. 预定义模块和固定形态提供可解释、可表现、可平衡的变化空间。
4. 同代 A/B 试验使用实际生存、战斗、职责和资源效率决定固化。
5. Hive ECS 聚合根与 Actor ECS 表现型承担全部权威状态，WorldBox 对象只作为宿主。

这套结构允许虫族表现出小说式的“越打越会应对”，但不会因为一次受击立即免疫，也不会最终叠满所有能力。不同母巢会在压力、资源、试验结果和代价约束下形成独立生态型，并通过新生代、职虫比例、战术和巢穴结构把进化结果真正表现出来。
