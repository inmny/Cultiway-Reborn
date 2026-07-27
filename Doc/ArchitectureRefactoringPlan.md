# Cultiway 架构整理与渐进式重构方案

## 1. 文档目的

本文用于指导 Cultiway 后续的系统性重构。目标不是单纯整理目录、统一命名或减少文件数量，而是建立以下可长期维护的边界：

- 明确应用级、世界级、角色级和界面级状态的所有权与生命周期。
- 明确哪些代码可以修改 ECS，哪些代码只能查询或提交请求。
- 统一命令、事件、同步结算和延迟后果的语义。
- 区分能力、技能、状态、物品与能力来源，避免继续为每个玩法建立独立运行时。
- 让修仙、魔法、骑士、法器、宗门和世界工具能够作为相对独立的功能模块演进。
- 让系统注册顺序、失败行为、世界切换行为和运行线程都可以直接从代码结构中确定。

本方案采用渐进迁移，不进行一次性全仓重写。每个阶段都应保持项目可编译，并尽可能保持原有玩法行为不变。

---

## 2. 当前架构的确定性问题

### 2.1 组合根分散

当前模块和系统注册分散在多个位置：

- `Source/ModClass.cs`
- `Source/Content/Manager.cs`
- `Source/Core/Libraries/Manager.cs`
- `Source/Content/Libraries/Manager.cs`
- `Source/WorldboxGame.cs` 及其 partial 嵌套类型
- `Source/Patch/Manager.cs`
- `Source/Content/Patch/Manager.cs`
- 部分 Service 的 `Init()` 或构造函数

同时存在以下注册方式：

- 手工创建和注册。
- 按命名空间反射扫描 `ICanInit`。
- 按类型名称反射扫描 Harmony Patch。
- `DependencyAttribute` 排序。
- 构造函数内直接创建资产、系统或运行时实体。

因此，完整启动顺序无法从一个地方直接读出。初始化失败后，`Try.Start` 和局部 `try/catch` 还可能保留半初始化状态。

### 2.2 世界状态没有统一作用域

`ModClass.W` 在 Mod 加载时创建一次，之后跨多个世界继续存在。当前 `MapBox.clearWorld` 补丁只清理部分管理器、队列和静态字典，没有统一清理所有世界实体。

已知问题包括：

- `ActorExtendManager.Clear()` 和 `BookExtendManager.Clear()` 只清字典，不负责统一删除对应 ECS 实体。
- `CityExtendManager` 没有统一的世界清理入口。
- 魔网、万法阁、百宝阁、世界工具、技能队列、寻路和列车各自维护 `ClearWorldState`。
- 世界清理补丁既直接认识具体模块，又允许模块注册清理回调。
- 延迟请求通常保存 `ActorExtend`、`Actor`、`Entity` 等运行时引用，没有统一的世界代次校验。

这意味着旧世界实体、旧引用、旧索引或旧请求是否被完整清除，取决于各模块是否记得单独注册清理逻辑。

### 2.3 Core 与 Content 依赖方向不稳定

`Source/Core` 中已有多个文件直接引用 `Cultiway.Content`，例如：

- `ActorExtend`
- `ActorTransformationService`
- `CityExtend`
- `Sect`
- `SectManager`
- `SectScriptureCollection`
- `ElementRootRainService`
- 部分通用 System

这说明当前 `Core` 并不是稳定的基础层。功能代码可以反向进入 Core，导致后续无法判断一个公共类型是否真的可被所有功能复用。

### 2.4 ActorExtend 承担了过多职责

`ActorExtend` 及其 partial 文件目前同时负责：

- 原版角色与 ECS 实体绑定。
- ECS 组件读写。
- 属性缓存和属性贡献回调。
- 灵根、天赋和修炼体系。
- 技能学习、替换、缓存和施放。
- 状态持有和状态属性合并。
- 特殊物品背包。
- 动态资产掌握。
- 势力、宗门和师徒关系。
- 战斗动作选择、距离判断和伤害结算。
- 最近攻击者记录。
- 角色死亡、复制和清理。
- 多种静态领域事件注册。

同时，Content 还有一个大型 `ActorExtendTools` 扩展集合。两者共同形成了事实上的全局角色服务定位器。

### 2.5 事件与回调机制不统一

当前至少存在以下事件机制：

1. `ActorExtend.RegisterActionOn...` 同步静态委托。
2. `EventSystemHub` 加 `ConcurrentQueue` 的延迟事件。
3. `ProgressionLifecycle` 的同步观察者列表。
4. `ArtifactAbilityDispatcher` 的法器专用事件分发。
5. Service 自身暴露的 C# `event Action`。
6. Harmony Patch 直接调用 Content 服务。

这些机制在线程、执行时机、异常处理、优先级、返回值和世界清理语义上并不一致。

### 2.6 ECS 写入权限没有统一规则

`EntityStoreLock` 声明用于保护全部 ECS 写操作，但实际只有少量入口使用。其他代码仍直接执行：

- `CreateEntity`
- `AddComponent`
- `RemoveComponent`
- `AddRelation`
- `RemoveRelation`
- `AddTag<TagRecycle>`

部分逻辑发生在 Friflo Query 外，部分发生在 WorldBox 并行回调中，部分通过 CommandBuffer 延迟。当前没有一个统一规则可以回答“这里能否直接修改结构”。

### 2.7 Ability、Skill、Status 与来源系统重叠

当前存在以下相互重叠的运行时：

- `ActiveAbilityService`
- `SkillLibV3`
- `StatusEffectAsset` 和状态系统
- `ArtifactAbilityDispatcher` 与法器能力运行时
- `CoreFormationEffectHandlers` 与形成效果运行时
- 原版 CombatAction
- 卷轴、符箓等消耗品 Provider

它们分别处理部分以下职责：

- 可用条件。
- AI 权重。
- 目标选择。
- 消耗。
- 冷却。
- 充能。
- 触发事件。
- 持续状态。
- 技能实体生成。
- 动画和视觉效果。

这些概念有公共部分，但不能直接全部合并成 Skill。Skill 只是能力产生的一种空间和时间执行形式。

### 2.8 实体所有权和回收语义隐式

目前常通过以下机制组合表达生命周期：

- `TagPrefab`
- `TagOccupied`
- `TagRecycle`
- `TagConsumed`
- `TagUncompleted`
- `SkillMasterRelation`
- 动态 Asset 的 `Current` 计数
- Actor、Book、Sect 或魔网手工 Master

创建方、临时持有方和最终所有者之间没有统一的“创建、接管、释放”事务。调用方需要了解多种 Tag 和 Relation 的组合规则，容易出现提前回收、无法回收或跨世界残留。

### 2.9 AI 工作选择集中且难扩展

`Source/Content/Patch/PatchActor.cs` 直接认识魔法、骑士、修仙、炼丹、符箓、法器、宗门和师徒等功能，并通过多层概率向同一个字符串池添加工作。

后果包括：

- 新功能必须修改中心补丁。
- 不同功能的概率难以比较。
- 无法说明角色为什么选择某项工作。
- 紧急工作、长期目标、普通闲暇活动和修炼被混在同一选择层。
- 选择失败后的回退语义不统一。

### 2.10 UI 仍直接依赖运行时细节

尽管 `Source/UI/Foundation`、`Controls` 和 `Adapters` 已经建立，部分功能 UI 仍直接读取：

- ECS Entity 和组件。
- `World.world`。
- 静态 Manager。
- Content 的具体类型。

这会让 UI 刷新、世界切换、测试和业务重构互相影响。UI 应通过稳定查询模型读取数据，并通过命令接口提交操作。

---

## 3. 重构原则

### 3.1 不进行大爆炸式重写

新架构先包裹旧入口，再逐个迁移调用方。旧入口只有在没有调用方后才删除。

### 3.2 不以移动文件代替重构

先改变依赖和状态所有权，再移动目录。仅把原文件移动到新目录但保留原有静态调用，不算完成重构。

### 3.3 不建立万能框架

不使用一个携带大量 `object`、字符串类型码和反射分发的通用系统替代全部玩法。公共层只抽象已经确认存在的稳定语义。

### 3.4 保留领域差异

- Ability 表示一个可以被触发或主动使用的能力。
- Skill 表示带有轨迹、碰撞、作用范围和动画生命周期的执行。
- Status 表示附着于目标的持续状态。
- Artifact、CoreFormation、Cultisys、Consumable 表示能力来源。
- Effect 表示一次明确的世界修改意图。

这些概念应协作，而不是合并为一个巨型类型。

### 3.5 单一写入权

每类关键状态只能由一个领域服务修改：

- Status 只能由 `StatusService` 创建、刷新和移除。
- 技能容器所有权只能由 `SkillOwnershipService` 修改。
- 特殊物品库存只能由 `InventoryService` 修改。
- 冷却只能由 `AbilityRuntimeService` 修改。
- 境界只能由 `ProgressionService` 修改。
- 世界级索引只能由所属 World Service 修改。

### 3.6 查询、命令和事件分离

- Query：无副作用，返回不可变快照或 ReadModel。
- Command：表达修改意图，返回明确结果。
- Event：表达已经提交的事实，不允许观察者回滚原事务。
- Rule：同步结算中的有序规则，只修改本次结算上下文。

---

## 4. 目标分层

```text
Source/
  Host/
    Composition/
    Lifecycle/
    Scheduling/
    WorldboxAdapters/

  Runtime/
    Ecs/
    Commands/
    Events/
    Ownership/
    WorldSession/
    Diagnostics/

  Domain/
    Actors/
    Combat/
    Abilities/
    Skills/
    Statuses/
    Items/
    Crafting/
    Progression/
    Semantics/
    Filtering/

  Features/
    Xian/
    Magic/
    Knight/
    Artifacts/
    Sects/
    MasterApprentice/
    Wanfa/
    Baibao/
    WorldTools/

  UI/
    Foundation/
    Controls/
    Adapters/
    FeaturePresentation/
```

第一阶段仍保持单一程序集，先通过命名空间和架构检查脚本约束依赖。只有在模块边界稳定后，才考虑拆分程序集。

### 4.1 依赖方向

```text
Host
  -> Runtime
  -> Domain
  -> Features
  -> Presentation

Features -> Domain -> Runtime
Presentation -> Feature Queries / Commands + UI Foundation
WorldboxAdapters -> Runtime 或 Domain Port
```

禁止以下依赖：

- Runtime 依赖具体 Feature。
- Domain 依赖具体 Feature。
- UI Foundation 或 Controls 依赖 ECS 和 Content 类型。
- Harmony Patch 直接依赖具体 Feature 实现。
- Feature 直接注册 SystemRoot、Harmony Patch 或世界清理回调。

---

## 5. 作用域与生命周期

### 5.1 ApplicationScope

从 Mod 加载到 Mod 卸载始终存在：

- 静态资产定义。
- 语义图。
- 本地化。
- UI prefab 和资源引用。
- 模块目录。
- 系统定义和调度定义。

ApplicationScope 不保存任何具体世界中的角色、城市、技能实例或队列。

### 5.2 WorldSession

每次创建或加载世界时创建，清理世界时整体销毁：

- 当前世界代次 `Generation`。
- 世界根实体。
- Actor、Book、City Repository。
- 技能施放请求队列。
- 状态和能力运行时。
- 魔网索引。
- 万法阁和百宝阁世界会话。
- 世界工具雨滴载荷。
- 世界级缓存和倒排索引。
- 当前世界的 CommandBus 与 EventBus。

所有延迟命令和句柄都必须携带世界代次。代次不匹配时直接丢弃，不能重新解析到新世界中 ID 相同的对象。

### 5.3 ActorScope

角色存活期间存在：

- ActorHandle。
- 角色 ECS 组件。
- 能力授予和运行时状态。
- 冷却和充能。
- 状态实例关系。
- 物品库存关系。
- 修炼体系组件。

### 5.4 UiSession

窗口或世界 UI 会话期间存在：

- 当前选择。
- 搜索、筛选和排序状态。
- 编辑草稿。
- 对 ReadModel 的订阅。

UiSession 不持有可写 Entity 引用。世界结束时统一关闭或失效。

### 5.5 单一 EntityStore 的渐进方案

短期不立即拆分 EntityStore。先在现有 Store 中加入明确的实体作用域：

- `AppOwned`
- `WorldOwned { Generation }`
- `TransientOwned { Generation }`
- `PendingRecycle`

所有运行时实体通过统一工厂创建并带上作用域。世界清理时按 Generation 删除全部世界实体，同时保留资产预制体。

只有在实体创建全部经过工厂后，才评估是否将定义 Store 和世界 Store 物理拆分。

---

## 6. 模块装配

### 6.1 显式模块目录

用显式模块列表取代按命名空间和类名反射扫描：

```text
RuntimeModule
ActorModule
CombatModule
AbilityModule
SkillModule
StatusModule
ProgressionModule
XianModule
MagicModule
ArtifactModule
SectModule
WorldToolModule
PresentationModule
```

模块可以声明依赖，但相同优先级必须按稳定 ID 排序。

### 6.2 模块生命周期

每个模块按固定阶段参与装配：

1. `RegisterServices`
2. `RegisterDefinitions`
3. `LinkAndValidateDefinitions`
4. `RegisterSystems`
5. `RegisterWorldboxAdapters`
6. `RegisterPresentation`
7. `StartApplication`
8. `StartWorld`
9. `StopWorld`
10. `ReloadContent`

禁止构造函数和静态字段初始化产生注册副作用。

### 6.3 失败策略

- Runtime、Actor、Combat 等必需模块失败：停止 Mod 加载。
- 可选玩法模块失败：整个模块标记为 Disabled，不注册其系统、Patch 和 UI。
- 资产 Link 或 Validate 失败：在进入世界前报出模块 ID、资产 ID 和依赖链。
- 不允许吞掉异常后继续使用未完成初始化的模块。

---

## 7. 调度与线程模型

### 7.1 逻辑阶段

建议固定为：

```text
Ingress
QueryAndPlan
CommandExecution
AbilityScheduling
SkillSimulation
ConsequenceExecution
StateTick
StructuralCommit
Maintenance
Recycle
```

Render 使用独立阶段，只读取已经提交的模拟状态和 VisualCue。

### 7.2 单写者规则

- Mod ECS 只允许逻辑主线程写入。
- Harmony 或 WorldBox 并行回调只能写入线程安全队列。
- ECS Query 内不得直接进行结构修改。
- Query 需要产生结构变化时，先收集不可变请求，离开 Query 后执行。
- Render System 不得创建或修改模拟实体。
- 允许 Friflo 内部并行执行只读或明确无冲突的组件更新，但结构提交仍回到主线程阶段。

### 7.3 稳定句柄

延迟请求不保存长期 `ActorExtend`、`Actor` 或裸 `Entity` 引用，而保存：

```text
WorldGeneration
ObjectKind
ObjectId 或 EntityPid
可选的版本号
```

处理时通过 WorldSession Repository 重新解析，并校验对象仍属于当前世界且仍可用。

---

## 8. 命令、事件与同步规则

### 8.1 Command

Command 表示希望发生的修改，例如：

- `SubmitAbilityUse`
- `ApplyStatus`
- `RemoveStatus`
- `SpawnSkill`
- `TransferItem`
- `AdvanceProgression`
- `PublishMagicSpell`

Command 必须返回明确结果，而不是只返回“已进入队列”：

```text
Accepted
Started
Rejected
Completed
Interrupted
```

需要异步完成的命令返回 Ticket。资源消耗、冷却和使用积累分别绑定到明确状态，不能根据“入队成功”推测技能已经释放。

### 8.2 Event

Event 只表示已经发生的事实，例如：

- `DamageResolved`
- `AbilityStarted`
- `SkillEmitted`
- `AbilityCompleted`
- `StatusApplied`
- `ProgressionCommitted`
- `ItemTransferred`
- `ActorDied`

Event Handler 不直接修改事件所属的原事务，只能提交新的 Command。

### 8.3 RulePipeline

伤害等必须同步返回结果的流程使用有序 Rule：

```text
Validate
PowerSuppression
Resistance
Avoidance
Adaptation
Shield
DamageCap
Survival
Commit
```

规则注册必须包含稳定 ID 和阶段。阶段内顺序固定，不依赖模块初始化先后。

Rule 只能：

- 读取不可变定义。
- 读取已经存在的角色状态。
- 修改本次 `DamageContext`。
- 向本次事务 Outbox 写入后续命令。

Rule 不得直接创建实体、状态、动画或新的技能序列。

---

## 9. Ability、Skill、Status 和 Effect

### 9.1 AbilityDefinition

描述能力本身：

- 稳定 ID。
- 主动或被动触发方式。
- 目标模式。
- 可用条件。
- 资源策略。
- 冷却策略。
- 充能策略。
- AI UseProfile。
- 一个或多个 Effect。

### 9.2 AbilityGrant

描述角色为何拥有能力：

- Owner。
- AbilityDefinition ID。
- SourceKind。
- SourceHandle。
- InstanceKey。
- Potency。

来源可以是：

- LearnedSkill。
- CoreFormation。
- Artifact。
- Status。
- Consumable。
- Cultisys。

### 9.3 AbilityRuntime

按 Grant InstanceKey 保存：

- 冷却。
- 充能。
- 激活状态。
- 持续时间。
- 触发计数。
- 能力专属的有界运行时状态。

冷却键使用具体 Grant 或容器实例身份，不使用 SkillEntityAsset ID。

### 9.4 Skill

Skill 只负责：

- 施放计划。
- 发射序列。
- 轨迹。
- 碰撞。
- 命中记忆。
- 作用范围。
- 空间持续体。
- 动画生命周期。

Skill 不拥有全部能力决策，也不直接承担法器、金丹或状态的来源语义。

### 9.5 Status

StatusDefinition 明确声明：

- 正面或负面。
- 堆叠策略：Refresh、Stack、Replace、Independent。
- 最大层数。
- 持续时间规则。
- 属性贡献。
- Tick 行为。
- 可选 AbilityGrant。
- VisualCue。

状态实例只能由 `StatusService` 创建和移除。同类型同来源的刷新行为不能继续隐藏在调用方约定中。

### 9.6 Effect

优先提供少量稳定的 Effect Command：

- Damage。
- ApplyStatus。
- RemoveStatus。
- RestoreResource。
- MoveOrKnockback。
- SpawnSkill。
- EmitVisualCue。
- ModifyWorld。
- TriggerProgression。

不适合公共化的玩法效果仍留在 Feature 内，但必须通过领域 Service 修改状态。

### 9.7 迁移顺序

Ability Kernel 建立后按以下顺序迁移：

1. CoreFormation。
2. LearnedSkill。
3. Scroll 和 Talisman。
4. Artifact。
5. 其他状态或体系被动。

形成效果刚完成一次 Skill/Status 迁移，范围清晰，适合作为第一套完整样板。

---

## 10. ActorExtend 拆分

最终 `ActorExtend` 只保留：

- 原版 Actor 与 ECS Entity 绑定。
- 稳定 ActorHandle。
- 最基础的组件查询便利入口。
- 生命周期转发。

其余职责迁移到：

| 当前职责 | 目标服务 |
| --- | --- |
| 属性缓存与贡献 | `ActorStatPipeline` |
| 状态关系 | `StatusService` |
| 特殊物品 | `InventoryService` |
| 技能学习与替换 | `SkillKnowledgeService` |
| 能力枚举与运行时 | `AbilityService` |
| 修炼体系准入 | `CultivationRegistry` |
| 境界变更 | `ProgressionService` |
| 战斗距离和动作选择 | `CombatPlanner` |
| 伤害结算 | `DamagePipeline` |
| 最近攻击者 | `CombatMemoryService` |
| 师徒关系 | `MasterApprenticeService` |
| 宗门关系 | `SectMembershipService` |
| 动态资产掌握 | `KnowledgeService` 或对应 Feature Repository |

迁移期间可以保留 ActorExtend 的兼容转发方法，但新代码不得继续调用旧入口。

---

## 11. 属性系统

建立带稳定 ID 的 `IActorStatContributor`：

- `Id`
- `Order`
- `Contribute(ActorStatContext, BaseStats output)`

属性来源包括：

- 灵根和天赋。
- 修炼体系。
- 金丹与元婴。
- 状态。
- 法器和法器元灵。
- 血脉。
- 宗门设施。
- 永久属性。

属性缓存失效使用明确原因：

```text
CultisysChanged
EquipmentChanged
StatusChanged
FormationChanged
SemanticProfileChanged
WorldModifierChanged
```

不再依赖任意模块向 `ActorExtend` 静态委托追加回调。

---

## 12. 物品、知识和制作

### 12.1 区分物品与知识

- ItemInstance：实际可持有、装备、消耗或掉落的实体。
- Knowledge：角色掌握的配方、功法、法术或设计。
- Definition/Blueprint：共享且不可变的内容定义。

不要继续使用同一个 Master 机制同时表达所有这些关系。

### 12.2 InventoryService

唯一负责：

- Add。
- Remove。
- Transfer。
- Equip。
- Unequip。
- Consume。
- Drop。

所有操作返回事务结果，并维护唯一所有者。

### 12.3 CraftingService

统一制作生命周期：

```text
Plan
ReserveIngredients
Start
Progress
CommitOutput
CancelOrFail
ReleaseReservation
```

炼丹、符箓、卷轴和法器只提供：

- 配方或规划器。
- 材料语义。
- 制作时长与资源。
- 输出构建器。
- 成功和失败规则。

这样可以逐步去掉各制作行为中手工管理 `TagOccupied`、`TagConsumed` 和 `TagUncompleted` 的逻辑。

---

## 13. 修炼体系与 Progression

现有 Progression 已具有以下正确方向：

- Query 与 Execute 分离。
- Minor 与 Major 分离。
- Natural、Grant、Synchronize、Transfer 语义明确。
- Requirement、Transformation、Reward 和 FailureEffect 分离。
- 可扩展 Preparation 与 Challenge。

因此不应重写 Progression 核心，只需：

- 从静态注册表迁移到 `CultivationRegistry`。
- 将 `object payload` 逐步替换为过渡定义内部受控的类型载荷。
- 把 WorldRecord、视觉和日志观察者迁移到统一 EventBus。
- 让 Xian、Magic 和 Knight 在各自 FeatureModule 中注册进阶图。
- 禁止 Feature 绕过 ProgressionService 直接写 CurrLevel。

---

## 14. AI 工作与战斗决策

### 14.1 工作选择

建立 `IActivityCandidateProvider`。每个 Feature 只提交候选：

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

中心选择器负责：

- 先处理强制和紧急工作。
- 再处理长期目标。
- 最后对普通候选进行效用加权。
- 记录选择原因和被淘汰原因。
- 失败时尝试下一个候选，而不是重新随机整套逻辑。

Harmony Patch 只调用统一选择器，不再认识魔法、炼丹、宗门或师徒。

### 14.2 战斗动作

将当前攻击流程拆成：

```text
TargetRetention
CombatStyle
RangeIntent
ActionCandidateCollection
ActionSelection
ActionExecution
ExecutionResult
```

普通近战、远程武器、原版法术和主动能力都是 ActionCandidate。只有实际 Started 后才启动对应冷却、播放攻击动作和消耗资源。

---

## 15. UI 与展示模型

每个 Feature 对 UI 提供两类接口：

- Query：返回不可变 ReadModel。
- Command：执行学习、上传、制作、装备、筛选或世界工具操作。

例如魔网 UI 不直接读取 MagicWebManager 的字典，而读取：

```text
MagicWebBrowserModel
MagicSpellListItemModel
MagicSpellDetailModel
```

UI 规则：

- Foundation 和 Controls 不依赖 ECS、Content 或具体玩法。
- Feature UI 不直接创建、删除或修改 Entity。
- 世界结束时所有 UiSession 自动失效。
- 事件订阅返回可释放句柄，窗口关闭时解除。
- Tooltip 使用 Presentation Service 生成展示数据。

---

## 16. 建议保留的现有实现

以下部分方向正确，应尽量保留并围绕新边界调整：

- Progression 的 Query、Mode、Result 和阶段模型。
- ActiveAbility Provider 的“能力来源适配器”思想。
- SkillLibV3 的轨迹、碰撞、施放序列和命中模型。
- ActorFilter 的逻辑表达式模型。
- Semantics 的共享语义图和 Contributor 思想。
- UI Foundation、Controls 和 Adapters。
- ModSaveManager 的文档版本、迁移和原子写入模型。
- 性能调度器中明确的阶段预算与诊断能力。

---

## 17. 渐进迁移阶段

### 阶段 0：建立基线

工作内容：

- 记录完整启动顺序和系统顺序。
- 为世界创建、世界清理、角色创建和角色死亡增加计数诊断。
- 建立关键玩法特征测试或 Debug 场景。
- 添加架构检查脚本。

必须覆盖的基线场景：

- 角色出生并获得修炼体系。
- 小境界和大境界进阶。
- 近战、原版远程、法术和被动反击。
- Status 施加、刷新、过期和清除。
- 法术学习、改进、上传和遗忘。
- 法器制作、装备、能力触发和死亡回收。
- 连续创建和清理多个世界。

完成条件：

- 能够检测世界切换前后的实体、队列和索引数量。
- 能够比较重构前后的关键行为结果。

### 阶段 1：WorldSession 与组合根

工作内容：

- 引入显式 ModuleCatalog。
- 引入 ApplicationScope 和 WorldSession。
- 为当前 EntityStore 加入 WorldGeneration。
- 集中系统注册和系统阶段。
- 把现有 ClearWorldState 包装进 WorldSession StopWorld。

完成条件：

- `PatchMapBox` 不再直接认识万法阁、魔网、列车或世界工具。
- 世界结束后不存在上一代 WorldOwned 实体和排队请求。
- City、Book、Actor Repository 都在一个入口重建。

### 阶段 2：命令、事件与 ECS 单写者

工作内容：

- 引入 typed CommandBus 和 EventBus。
- Harmony 并行回调改为提交稳定句柄请求。
- 建立 Damage RulePipeline 和事务 Outbox。
- 统一 StructuralCommit 阶段。

完成条件：

- ECS 结构修改只发生在允许的阶段。
- Render Query 不再创建模拟实体。
- 不再依赖 `EntityStoreLock` 作为不完整的全局约定。

### 阶段 3：拆薄 ActorExtend

工作内容：

- 迁出状态、库存、技能知识、属性和战斗记忆。
- 建立 ActorRepository 和 ActorHandle。
- 将静态属性贡献委托迁移为注册式 Contributor。
- 将 ActorExtend 静态领域事件迁移到 EventBus 或 RulePipeline。

完成条件：

- `Source/Core/ActorExtend*` 不引用 `Cultiway.Content`。
- 新功能不再向 ActorExtend 添加领域方法或静态回调。

### 阶段 4：Ability 与 Effect Kernel

工作内容：

- 把 ActiveAbility 从 SkillLibV3 移到独立 Abilities Domain。
- 建立 AbilityDefinition、Grant、Runtime、Request 和 Ticket。
- 建立 StatusService 与明确堆叠规则。
- 建立 Effect Command。
- 迁移 CoreFormation。
- 迁移 LearnedSkill、Scroll、Talisman 和 Artifact。

完成条件：

- 所有生产施放通过统一 Ability Request。
- 所有冷却按 Grant 实例管理。
- 金丹元婴和法器不再维护独立的通用冷却、触发和状态创建框架。
- SkillLib 只负责 Skill 执行。

### 阶段 5：物品与制作事务

工作内容：

- 建立 InventoryService。
- 建立 CraftingService。
- 迁移炼丹、符箓、卷轴和法器。
- 统一材料保留、失败和产物接管。

完成条件：

- 物品只有一个所有者。
- 制作中断不会留下永久 Occupied 实体。
- 调用方不再手工组合多种生命周期 Tag。

### 阶段 6：AI 决策

工作内容：

- 建立 ActivityCandidateProvider。
- 按功能迁移工作候选。
- 建立 CombatAction Candidate 和 ExecutionResult。
- 删除 PatchActor 中的功能知识。

完成条件：

- 新增修炼体系或生产工作不修改中心 Patch。
- 可以输出角色选中和拒绝每项工作的原因。

### 阶段 7：功能垂直化与 UI

工作内容：

- 将 Xian、Magic、Knight、Artifacts、Sects 等整理为垂直模块。
- 拆分超大定义文件和窗口文件。
- 建立 Feature Query、Command 和 ReadModel。
- UI 移除直接 ECS 写入。

完成条件：

- 每个功能的定义、规则、系统、适配器和展示可以从单一目录定位。
- UI Foundation 和 Controls 不引用玩法类型。

### 阶段 8：删除兼容层

工作内容：

- 删除旧静态注册表和兼容转发。
- 删除未使用 Tag、Relation、Manager 和 ClearWorldState。
- 更新 ProjectStatus、ProjectOverview 和各系统文档。
- 完成架构检查的硬性门禁。

---

## 18. 架构检查规则

建议在 `Scripts` 下建立自动检查，并在每次构建前或提交前运行。

最低规则：

- `Source/Core` 或未来 Domain/Runtime 不得引用具体 Feature。
- UI Foundation/Controls 不得引用 ECS 和 Content。
- 只有 Host 可以直接向 SystemRoot 注册系统。
- 只有 WorldboxAdapters 可以声明 HarmonyPatch。
- 只有 EntityFactory 可以创建运行时实体。
- 只有所属 Service 可以修改关键关系和组件。
- 不允许新增 `RegisterActionOn...` 静态委托。
- 不允许新增功能专属 `ClearWorldState`。
- 不允许延迟命令保存无代次的裸 Actor 或 Entity。
- 不允许 Feature 直接写 CurrLevel。
- 不允许 UI 直接修改 ECS。

---

## 19. 提交和回滚策略

每个阶段应拆成小提交：

1. 新增合同和诊断。
2. 添加旧实现适配器。
3. 迁移一个调用方。
4. 验证行为。
5. 删除该调用方对应旧路径。

禁止在同一提交中同时：

- 改变架构边界。
- 大规模移动文件。
- 修改数值平衡。
- 改变视觉资源。
- 引入新玩法。

任何阶段发现行为差异时，应能够只回滚该调用方迁移，而不回滚已经稳定的运行时基础。

---

## 20. 总体验收条件

重构完成后，应满足：

- 启动模块、依赖和系统顺序可以从一个组合根完整读出。
- 世界创建和世界销毁具有严格对称的生命周期。
- 旧世界命令、实体和索引不可能进入新世界。
- ECS 结构修改遵守单写者和固定提交阶段。
- Combat 同步规则不产生结构变化。
- Ability、Skill、Status 和 Source 的职责清晰。
- ActorExtend 不再是领域服务集合。
- 新功能通过模块、Provider、Contributor 或 Command Handler 接入，不修改中心巨型分支。
- UI 只消费 ReadModel 并提交 Command。
- Progression、制作、施放和物品转移都返回可观察结果。
- 架构检查能够阻止重新引入 Core -> Feature、UI -> ECS 写入等依赖。

---

## 21. 推荐的实际第一批工作

真正开始重构时，第一批只做以下内容，不迁移具体玩法：

1. 添加架构边界检查。
2. 添加世界实体和队列诊断。
3. 建立 `ModuleCatalog` 和固定系统阶段描述。
4. 建立 `WorldSession` 与 `WorldGeneration`。
5. 将当前所有世界清理入口先包装进 WorldSession。
6. 验证连续创建、清理三个世界后不存在上一代运行时状态。

这批完成后，再开始命令、事件和 Ability 的统一。否则后续任何运行时整理都会继续建立在不明确的世界生命周期之上。
