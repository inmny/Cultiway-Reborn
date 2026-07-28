# 宗门与国家冲突设计

## 文档状态

本文记录宗门与国家进入敌对状态后，人物如何决定效忠对象、加入哪一方、寻找和维持攻击目标，以及该系统如何接入原版战斗与战争逻辑。

当前仅完成设计，不包含代码实现。第一阶段以“国家镇压宗门、宗门防守驻地”为范围，不处理宗门占领城市。

## 设计目标

- 保留人物原有的城市与国家身份，不把宗门强行伪装成国家。
- 允许同一国家的人因宗门立场不同而分属敌对双方。
- 将政治敌对、个人效忠、实际参战和攻击合法性分开处理。
- 让普通国家战争继续使用原版逻辑，仅让宗门冲突参与者进入扩展流程。
- 让近战、远程、技能、范围伤害、建筑和 AI 任务使用同一套敌我结论。
- 为后续宗门与宗门冲突、盟宗参战、条约援助和国家招安留下扩展空间。

## 原版约束

### 敌人搜索以 Kingdom 为唯一阵营键

原版 [`BaseSimObject.findEnemyObjectTarget`](../.GameSource/Assets/Scripts/Assembly-CSharp/BaseSimObject.cs) 使用：

```text
EnemiesFinder.findEnemiesFrom(current_tile, kingdom)
```

[`EnemiesFinder`](../.GameSource/Assets/Scripts/Assembly-CSharp/EnemiesFinder.cs) 的缓存以 `Kingdom` 为键。  
[`EnemyFinderContainer`](../.GameSource/Assets/Scripts/Assembly-CSharp/EnemyFinderContainer.cs) 再按区块中的 `kingdom.id` 归组单位和建筑，并通过 `pMainKingdom.isEnemy(...)` 判断是否加入敌人列表。

这意味着原版无法表达以下情况：

- 两个相同 `Kingdom` 的人物在宗门冲突中分属两方。
- 两个不同 `Kingdom` 的人物因共同保卫宗门而暂时成为友军。
- 国家只敌对某一个宗门，而不敌对其他宗门。

### 攻击链并不只有 canAttackTarget

[`BaseSimObject.canAttackTarget`](../.GameSource/Assets/Scripts/Assembly-CSharp/BaseSimObject.cs) 主要判断目标是否存活、能否被攻击、飞行与水陆限制、能否攻击建筑等条件。它只在部分文明单位规则中调用 `areFoes`，不能单独承担敌我判定。

完整攻击链还包括：

- `BaseSimObject.findEnemyObjectTarget`：发现候选目标。
- `BaseSimObject.areFoes`：直接调用 `kingdom.isEnemy(target.kingdom)`。
- `Actor.shouldContinueToAttackTarget`：决定是否继续攻击当前目标。
- `BehFightCheckEnemyIsOk`：再次验证当前目标。
- `Actor.checkSpecialAttackLogic`：根据是否同国、是否敌国决定战斗是否致命。
- 技能、投射物和范围碰撞中的独立敌我判断。

因此，只修改 `canAttackTarget` 会出现“找不到目标”“刚设定就清除”“普通攻击有效但技能不生效”或“双方只打晕不致死”等不一致。

### 战略战争目标以 City 为单位

[`WarManager.getWarCities`](../.GameSource/Assets/Scripts/Assembly-CSharp/WarManager.cs) 返回敌对国家的城市。  
[`CityBehCheckAttackZone`](../.GameSource/Assets/Scripts/Assembly-CSharp/ai/behaviours/CityBehCheckAttackZone.cs) 从中选择 `target_attack_city` 和 `target_attack_zone`。  
[`BehCityActorCheckAttack`](../.GameSource/Assets/Scripts/Assembly-CSharp/ai/behaviours/BehCityActorCheckAttack.cs) 再把军队移动到目标城市区域。

宗门驻地不是 `City`，所以原版战争系统不会主动把宗门大殿或驻地区域选为进攻目标。

### 当前宗门建筑阵营只是技术阵营

宗门建筑当前统一使用 `KingdomAssets.SectBuildings`。这个技术阵营用于让建筑具备完整的原版建筑运行上下文，不代表任何一个具体宗门。

具体归属必须通过建筑上的 `BuildingDataKeys.SectID_Long` 解析。绝不能令某个国家与 `SectBuildings` 全局敌对，否则该国家会同时敌对世界上的所有宗门建筑。

## 核心模型

人物在冲突中同时具有三种彼此独立的身份。

### 城市与国家身份

继续使用原版：

- `Actor.city`：生活、职业、家庭和城市工作归属。
- `Actor.kingdom`：国籍、原版战争和常规敌我关系。

宗门冲突原则上不直接改写 `Actor.kingdom`。

### 冲突效忠决定

`SectConflictLoyalty` 表示人物在某次冲突中的长期选择：

```text
ConflictId
Decision: Realm | Sect | Neutral
DecisionTime
Reasons
```

效忠决定在冲突动员时计算一次，并在本次冲突中保持稳定，避免人物因短期数值波动反复倒戈。重大剧情事件可以显式触发重新评估，但不能每个 AI 周期重算。

### 当前参战状态

`SectConflictParticipant` 表示人物当前是否被实际动员：

```text
ConflictId
Side: Kingdom | Sect
CommandSource
RulesOfEngagement
CurrentObjective
```

效忠不等于参战。宗门忠诚者可以负责疏散、后勤或保持非战斗状态；只有拥有 `SectConflictParticipant` 的人物才进入宗门冲突的战斗搜索和任务流程。

## 冲突对象

宗门与国家冲突不能直接复用原版 `War`，因为原版战争两侧都要求是 `Kingdom`。应建立独立的 `SectKingdomConflict`：

```text
Id
SectId
KingdomId
Stage
Cause
ResidenceZones
RulesOfEngagement
Objectives
StartedAt
```

建议阶段：

1. `Mobilizing`：双方动员，成员作出效忠决定。
2. `Active`：执行进攻、防守、疏散和撤退任务。
3. `Ceasefire`：停止生成新目标，清理现有战斗。
4. `Settled`：结算结果并移除运行期状态。

冲突是外交关系恶化后的执行对象，不等同于条约。未来可以由条约违约、国家禁令、宗门袭击、拒绝交人等事件创建冲突；停战结果再生成由多个条款组成的新条约。

## 效忠决策

### 国家一方倾向

- 人物是国王、城主、将领、士兵或重要城市职务持有者。
- 家庭、氏族和主要社会关系集中在该国城市。
- 在城市和国家中生活时间长、地位高、满意度高。
- 宗门主动袭击其家乡、亲属或平民。
- 与国家领袖关系好，与宗门领袖关系差。

### 宗门一方倾向

- 人物是掌门、长老、执事或宗门核心传承者。
- 入宗时间长、贡献高、从宗门获得的利益多。
- 师父、徒弟和主要传承关系集中在宗门。
- 国家无正当理由迫害宗门，或要求交出其师徒、亲属。
- 与宗门领袖关系好，与国家统治者关系差。

### 中立倾向

- 对国家和宗门的归属都很弱。
- 性格厌战，且本人没有受到直接伤害。
- 家庭和师门分属两方，参战代价过高。
- 境界、年龄或身体状态不适合战斗。

评分采用确定性权重，并只加入很小的随机扰动。日志应记录最终得分和主要原因，确保长时间模拟后能够解释人物为何选择某一方。

## 不同决定的行为

### 效忠国家

- 立即退出宗门；未来如需要“挂名、监视或停权”再增加独立状态。
- 保留原有城市、国家、职业和军队关系。
- 国家动员后可成为 `Kingdom` 方参与者。
- 若原本担任宗门职务，宗门立即重新评定空缺。

### 效忠宗门

- 保留宗门成员关系。
- 若人物属于敌对国家的城市军队，则退出城市和军队指挥关系。
- 可以调用 `joinCity(null)` 脱离城市；原版该操作不会自动改写 `kingdom`，因此可暂时保留国籍作为人物出身。
- 国家可将其视为叛乱者、逃亡者或非法宗门成员，但该法律身份不通过替换 `kingdom` 表达。
- 宗门动员后可成为 `Sect` 方参与者，并前往驻地接受任务。

### 保持中立

- 不添加参战组件，不为任何一方执行军事任务。
- 优先离开交战区域，避免被普通目标搜索选中。
- 是否保留宗门身份由冲突起因决定；第一阶段默认保留，但不享有战时指挥权。

## 统一敌我解析

新增唯一入口 `CombatHostilityResolver`，所有宗门冲突相关战斗逻辑都依赖它，不在各个行为和技能中重复判断。

建议拆分两个问题：

```text
TryResolveRelation(source, target, out relation)
CanEngage(source, target, context)
```

`relation` 只表示本次冲突中的 `Friendly`、`Neutral` 或 `Hostile`。  
`CanEngage` 再根据交战规则判断敌对目标当前是否允许被攻击。

解析优先级：

1. 双方参加同一冲突且同属一方：`Friendly`。
2. 双方参加同一冲突且分属两方：`Hostile`。
3. 参与者面对该冲突明确指定的敌方人物或建筑目标：按所属方解析。
4. 参与者面对对方非战斗人员：按交战规则解析，默认 `Neutral`。
5. 与当前冲突无关：回退到原版 `kingdom.isEnemy`。
6. 发狂、附身和个人仇恨等原版特殊行为继续作为独立例外处理。

冲突解析只替代阵营语义，不替代原版的存活、距离、飞行、水陆、建筑可攻击性等物理和行为校验。

## 交战规则

敌对关系不代表可以攻击对方所有人物和建筑。`RulesOfEngagement` 应限制允许攻击的目标类别。

### 防御

- 攻击正在进攻宗门驻地的参与者。
- 攻击已对己方造成伤害的单位。
- 不主动攻击敌国平民和普通城市建筑。

### 有限冲突

- 攻击敌方参战者。
- 攻击驻地内的守军、塔楼和被指定的军事目标。
- 国家一方可攻击宗门大殿等核心目标。
- 宗门一方不能因为国家敌对而自动攻击所有城市建筑。

### 全面镇压或全面战争

- 可以扩大到敌方核心成员、战略建筑和补给目标。
- 平民与普通住宅仍默认受保护；只有明确的极端规则才允许攻击。

交战规则适合设计为资产或条款组合，使冲突起因、宗门特质、国家特质和停战条件能够共同决定允许的目标范围。

## 目标搜索

### 普通单位

没有 `SectConflictParticipant` 的单位继续使用原版 `EnemiesFinder`，避免影响普通国家战争和世界生物。

### 冲突参与者

参与者使用 `SectConflictEnemyFinder`：

1. 读取视野范围内区块的 `ChunkObjectContainer.units_all` 和 `buildings_all`。
2. 通过 `CombatHostilityResolver` 筛出敌对对象。
3. 通过 `CanEngage` 排除交战规则禁止的目标。
4. 继续使用原版距离、可达性、攻击方式和忽略目标规则选出最终目标。

国家镇压军也必须添加参与者组件，否则它们仍会使用原版按 `Kingdom` 缓存的目标列表，无法发现同国的宗门忠诚者。

如果需要缓存，缓存键应至少包含：

```text
ConflictId + Side + ChunkId + Range + RulesVersion
```

不能继续只用 `Kingdom` 作为键。

### 宗门建筑

发现宗门建筑后必须：

1. 检查建筑是否为宗门建筑。
2. 读取 `BuildingDataKeys.SectID_Long`。
3. 确认它属于当前冲突中的宗门。
4. 根据建筑类型和交战规则判断是否可攻击。

`KingdomAssets.SectBuildings` 不能参与具体宗门归属判断。

## 战略任务

原版城市军队只能进攻敌对城市，因此宗门冲突需要独立目标和任务链。

### 国家一方

- 从临近城市选择可用军队。
- 集结并移动到宗门驻地外围。
- 包围驻地、清除守军。
- 进攻指定的宗门建筑，第一阶段以宗门大殿为主要目标。
- 达成目标、损失过高或停战后撤退。

### 宗门一方

- 将参战成员召回驻地。
- 防守驻地边界和宗门建筑。
- 拦截进入驻地的敌方参与者。
- 护送中立成员和低境界成员离开战区。
- 大殿失守或战力不足时撤退。

### 工作优先级

当前 `PatchActor.CanUseCultiwayJobSelection` 会让处于城市进攻命令或危险状态中的战士继续执行原版城市任务。因此宗门冲突任务必须在该分流之前取得优先级，或者从城市/军队层直接下达，不能仅把新任务加入普通宗门工作随机池。

宗门忠诚者退出城市后，还需要解除原有军队编组和城市职业任务，防止同一人物同时接受两套命令。

## 必须统一接入的位置

实现时需要逐项核对，不能只改其中一个入口：

- 敌人候选搜索：`BaseSimObject.findEnemyObjectTarget`。
- 当前目标维持：`Actor.shouldContinueToAttackTarget`。
- 战斗目标验证：`BehFightCheckEnemyIsOk`。
- 致命与非致命伤害：`Actor.checkSpecialAttackLogic`。
- 受击反击和个人仇恨。
- 普通投射物和建筑远程攻击。
- `SkillUtils.IterEnemyInSphere`。
- `LogicActorCollisionSystem`。
- `LogicSkillPersistentSystem`。
- `WanfaTestCastSession` 及其他独立技能筛选。
- 城市军队任务与宗门冲突任务的优先级。
- 冲突结束时的目标、仇恨、任务和参战组件清理。

同一攻击在“寻找目标、继续攻击、结算伤害、技能命中”四个阶段必须得到相同的阵营结论。

## 冲突结束

进入 `Ceasefire` 后：

- 不再分配新攻击目标和军事任务。
- 清除参与者指向本次冲突对象的攻击目标。
- 清除由本次冲突产生的敌对追击和仇恨记录。
- 解除军队集结与驻地防守命令。
- 移除 `SectConflictParticipant`。
- 保留 `SectConflictLoyalty` 到结算完成，以决定退出宗门、流亡、赦免、惩罚和重新入城。

进入 `Settled` 后再生成外交与人物后果。不能只删除冲突对象，否则残留的攻击目标和个人仇恨会让停战立即失效。

## 不采用的方案

### 每个宗门创建一个伪 Kingdom

这会把宗门错误地接入国家人口、城市占领、外交、战争、颜色、地图和统计系统，并破坏跨国宗门的设定。维护成本远高于独立冲突阵营。

### 把宗门成员统一切换到 SectBuildings

`SectBuildings` 是所有宗门共用的建筑技术阵营。切换后不同宗门无法区分，还会破坏人物原有城市和国家关系。

### 直接让国家敌对 SectBuildings

这会让国家同时敌对全部宗门建筑，而不是只敌对目标宗门。

### 只修改 areFoes 或 canAttackTarget

原版目标缓存、持续攻击、伤害逻辑、技能筛选和军队任务仍会使用旧结论，最终必然出现行为不一致。

## 分阶段实现

### 第一阶段：驻地防卫闭环

- 建立 `SectKingdomConflict`。
- 完成一次性效忠决策。
- 添加参战组件与统一敌我解析。
- 国家军队能够前往目标宗门驻地。
- 宗门成员能够回防。
- 双方能够正确发现、攻击和结束攻击。
- 宗门大殿作为国家一方的主要战略目标。

### 第二阶段：完整战斗接入

- 覆盖技能、投射物、范围伤害和建筑攻击。
- 加入交战规则资产。
- 支持非战斗人员疏散和有限目标。
- 增加按冲突与阵营缓存的敌人搜索。

### 第三阶段：冲突结算

- 投降、停战、招安、赦免、流亡和宗门解散。
- 根据人物效忠决定处理宗门、城市和职务关系。
- 将结果写入模块化条约条款。

### 后续阶段

- 宗门主动袭击国家目标。
- 盟宗、附属宗门和国家盟友参战。
- 宗门与宗门冲突。
- 多个冲突的优先级与有限并行参战。

第一阶段限制人物同时只参加一个战术冲突。出现多个冲突时，按驻地防卫、家乡防卫、外部远征的顺序选择当前任务，避免不同冲突同时给人物下达相反命令。

## 验证清单

- 同国的国家忠诚者和宗门忠诚者可以互相识别为敌人。
- 不同国家但同属宗门防守方的人不会互相攻击。
- 非参战平民不会仅因所属国家或宗门被自动选为目标。
- 国家只攻击目标宗门的建筑，不影响其他宗门。
- 普通攻击、技能和范围伤害使用一致的敌我关系。
- 目标离开战区、投降或停战后，攻击能正常停止。
- 宗门忠诚者不会同时执行城市军队任务。
- 普通国家战争在没有宗门冲突参与者时保持原版行为。
- 冲突结束后不存在残留参战组件、任务或针对本次冲突的攻击目标。
