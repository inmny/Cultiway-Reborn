# 骑士体系 (Knight System) 设计文档

**版本**: v0.2（已实现 Slice 1-3 + 毕业钩子）
**状态**: 核心玩法已实现并编译通过（Slice 1-2 已在游戏内测通）；血脉持久化暂缓
**最后更新**: 2026年7月

> **实现状态速览（2026-07-16）**
> - ✅ Slice 1 骨架+升级循环、Slice 2 练习 ActorJob+闪避钩子、Slice 3 血脉（快照/父母表/回溯/加成层/防雪崩）、毕业钩子占位。
> - ⚠️ **两处实现偏离原设计**：
>   1. 父母抓取用 **`BabyMaker.makeBaby` 的 Harmony Postfix**（`PatchBabyMaker.cs`），**不是 transpiler**——因为 `createBabyActorFromData(ActorData, Tile, City)` 签名里没有父母，父母在 `makeBaby(pParent1, pParent2)` 里。
>   2. 血脉 + 父母表当前为**内存态、单次对局内有效**；**持久化暂缓**——模组 `ModSaveManager` 是全局跨世界的，且无"世界载入"钩子重建动态资产。需等模组的"每世界存档"基础设施。
> - 数值（几率/系数/曲线/CSV 行）按 `AGENTS.md` 后期手测调；旋钮集中在 `Source/Content/Const/KnightSetting.cs`。

---

## 📋 设计目标

仿照魔法体系（`Magic` / `CultisysAsset<T>`）新增一套**武艺/血脉**线路，作为第三个 `ICultisysComponent`。

- 不看元素亲和：给**没有灵根、走不了修仙/魔法**的士兵另一条成长出路。
- 升级主要靠**战斗 + 练习**，每级提升个人属性。
- 变体「血脉骑士」：修到 9 级的系统会备份其个人属性；其血亲后代步入骑士体系时获得血脉加成（始祖属性 × 系数）。

---

## 1. 定位与共存关系

- 骑士 = 第三个 `ICultisysComponent`，镜像魔法的 `CultisysAsset<Knight>` 结构，复用 `ProgressionService` 引擎与 `Cultisyses` 注册器。
- **与修仙互斥，与魔法可共存**（骑士 + 法师可叠加，如战斗法师/魔剑士）。
- 实质人群天然不重叠：有（非凡）灵根 → 出生即修仙/魔法；无灵根 → 可成为骑士。

### 互斥落地
- 骑士获取规则查「非修仙」；修仙/魔法获取规则补查「非骑士」（防后天得灵根转修仙）。

---

## 2. 获取（士兵有几率觉醒）

### 资格
`职业 ∈ {Warrior, Leader, King}` AND `种族 ∈ {人类, 兽人, 矮人, 精灵}`（给这 4 个种族的 `available_cultisys_ids` 加 `"Knight"`） AND 未觉醒 AND 非修仙。

> 「人类」指原版 Human（非模组的东方人类）。

### 触发
- **每月 QuerySystem**（仿 `RestoreMagicResourceSystem`）对每个合格且未觉醒的单位 `Randy.randomChance(0.005)`，命中即授予骑士 0 级（`ActorExtend.NewCultisys<Knight>`）。
- 基础几率 `0.005/月`（约 12 年过半、35 年近 95%），全员平，做成 `KnightSetting.AcquisitionChancePerMonth` 可调。
- 月度模型对职业变动鲁棒（退役/继位/降职自动随当月状态走），「老兵更易觉醒」自然涌现。

---

## 3. 升级（斗气门槛突破，照搬魔法）

### 资源
**斗气 (vigor)**。仅两个来源，**无被动回复**：

- **战斗**：`RegisterActionOnKill` + `RegisterActionOnBeAttacked` 给斗气（**跳过每次攻击**、只计敌对击杀），按对手战力缩放 + 弱敌地板；单次封顶到「当前缺口」。
  - 模板：`Source/Content/CultivateMethodTriggers.cs`（已按对手 `GetPowerLevel()` 缩放并 clamp）。
- **练习**：和平期 ActorJob `KnightCultivator → DailyKnightTrain`（照 `XianCultivator` 结构、复用 `CanUseCultiwayJobSelection` 战时不练、MVP 不绑建筑）；纯练习可突破但**慢**。

### 突破
- 斗气满 → 自动尝试（`TryAdvanceNaturally`，仿魔法）。
- **带失败几率，随等级升高（8→9 极难）** → 9 级稀有、血脉始祖来之不易。
- **失败 = 清空斗气重攒**（`FailureEffects = { 斗气 = 0 }`，与 `SuccessCosts` 同款但不升 Level）。
- **突破冷却 1–2 月**：无论成败，冷却内不自动重试，防一场仗连升多级。

### 满级
- **9 级封顶**（0–9，10 档，同魔法）。
- 9 级既是属性封顶，也是「成为血脉始祖」的瞬间。
- 留**毕业钩子（空操作占位）**，以后接「9 级后转其他体系」。

---

## 4. 属性（Knight.csv 8 列 + PowerLevel）

### 列集合
`level, MaxVigor, health, armor, HealthRegen, attack_speed, critical_chance, knight_evasion`

- `MaxVigor`（斗气上限）：照魔法 `MaxSpirit` **指数级增长**，让每级所需斗气递增、曲线自然变缓。
- `health / armor / HealthRegen`：生存类，线性到温和增长。
- `attack_speed`（原版 base_stat，`BaseStatsLibrary.cs:226`）、`critical_chance`（原版，`AttackData.cs:55`）：直接当 CSV 列。
- `knight_evasion`：**自定义 stat**（加进 `Source/Content/BaseStatses.cs`），WorldBox 无原生闪避数值。
- **攻击性成长**走 `PowerLevels[]`（升级自动套，不占列）。
- **无任何元素列**（金抗/木抗/精通全跳过）。

### 闪避实现
- 新建 `knight_evasion` 自定义 BaseStat。
- 挂 `ActorExtend.RegisterActionBeforeBeAttacked`（能 `ref damage`）：`Randy.randomChance(受害者 knight_evasion)` 命中 → `damage = 0` + `EffectsLibrary.spawnAt("fx_dodge", ...)`。
- 参考既有 `ArtifactAbilityRuntimeBridge.cs`（同样用了 `RegisterActionBeforeBeAttacked`）。

### 主动能力
- 本次**纯属性**；`CultisysAsset<Knight>.Skills[]` **预留空**，以后加招式是干净扩展点。

---

## 5. 血脉（BloodlineKnight 加成层）

### 本体论
- 「血脉骑士」= 普通骑士 + 一层血脉加成（**非独立路线**）。
- **始祖** = 修到 9 级的单位；**后代** = 血亲后代。

### 触发
- 首次成功突破到 9 级 → 一次性快照（挂 `ProgressionLifecycle.RegisterCommitted`，过滤 `Cultisys == Knight && ToLevel == 9`；模板 `BreakthroughVisualTrigger.cs`）。

### 快照内容
7 项战斗属性 + `PowerLevels[9]`：
`health, armor, HealthRegen, attack_speed, critical_chance, knight_evasion` + `PowerLevels[9]`
- 取**始祖当下真实值**（贴「备份个人属性」原意）。
- **剔除始祖自身继承来的血脉加成**（防代代滚雪球）——靠「血脉加成是独立一层 stats builder」在快照时减去。
- 接受装备加成的小幅混入（MVP）。

### 存储（DynamicAssetLibrary + IDeleteWhenUnknown）
- `class BloodlineAsset : Asset, IDeleteWhenUnknown`，字段含始祖 ActorID + 上述快照。
- `class BloodlineLibrary : DynamicAssetLibrary<BloodlineAsset>`，照 `CultibookLibrary` / `CultibookAsset`（`Source/Content/Libraries/`）抄。
- 后代 `ActorExtend.Master<BloodlineAsset>(...)` 持有 → `IDeleteWhenUnknown.Current` 引用计数 +1；最后一个后裔失去/死亡 → `RecycleUnknownAssetsSystem` 自动回收（=「无后裔即回收」）。

### 血统追溯
- **父母表**：4 种族出生时（扩展 `createBabyActorFromData` transpiler，`PatchActor.cs:256-269`）记录 `(单位ID → 母ID, 父ID)`，**单独持久化**。
- 入骑士时**回溯 ≤5 代**（父系母系都查），取**衰减后实际加成最高**的单一始祖 → 用其 ActorID 查 `BloodlineLibrary` → `Master`。**不叠加**。

### 加成公式
独立 stats-builder 层（仿 `Cultisyses.Xian.cs:83` 的 `RegisterCachedStatsBuilder`）：

```
后代加成 = 0.3 × 0.7^(代数−1) × (后代骑士等级 / 9) × 始祖快照
```

- 基础系数 `0.3`、每代衰减 `×0.7`、随后代等级逐步解锁（0 级为 0、9 级给满）。
- 隔代衰减 + 随后代等级解锁；仅在该后代持有骑士组件期间生效。
- 「衰减后实际最强」= 取 `0.7^(代数−1) × 始祖快照` 最大的那个始祖。

---

## 6. 存档

- **骑士组件（等级/斗气）**：照搬修仙 ECS 存档写法（`Xian` 怎么接就怎么接）。当前 ECS 存档为模组既有全局问题（ProjectOverview「存档无效」指此层），不在本次修复范围。
- **血脉快照 + Master 关系**：动态资产载入时被 `ClearDynamicAssets()`（`PatchMapBox.cs:91-94`）清空 → 靠「修仙式存档重建」恢复（快照内容存 library 存档文档、Master 关系随 actor 持久化）。
- **父母表**：独立持久化（每 actor custom_data 或独立小存档文档）。
- **净效果**：血脉特性本身跨存档/跨始祖死亡都稳；骑士等级随模组整体存档状态。

---

## 7. 实现落点（文件清单，待实现）

| 关注点 | 魔法参考文件 | 骑士对应（建议） |
|---|---|---|
| 组件 struct | `Source/Content/Components/Magic.cs` | `Source/Content/Components/Knight.cs` |
| 常量 | `Source/Content/Const/MagicSetting.cs` | `Source/Content/Const/KnightSetting.cs` |
| 体系注册 | `Source/Content/Cultisyses.Magic.cs` | `Source/Content/Cultisyses.Knight.cs`（加 `partial InitKnight()`） |
| 等级表 | `Content/Cultisys/Magic.csv` | `Content/Cultisys/Knight.csv` |
| 战斗灌斗气 | `Source/Content/CultivateMethodTriggers.cs` | 扩展同文件 / 新增 Knight 触发器 |
| 练习行为 | `Source/Content/Behaviours/BehMagicMeditate.cs` + `ActorJobs.cs` | `BehKnightTrain.cs` + `KnightCultivator` job |
| 自定义 stat | `Source/Content/BaseStatses.cs` | 加 `knight_evasion` |
| 闪避钩子 | `Source/Content/Artifacts/ArtifactAbilityRuntimeBridge.cs` | 同款 `RegisterActionBeforeBeAttacked` |
| 血脉资产 | `Source/Content/Libraries/CultibookAsset.cs` + `CultibookLibrary.cs` | `BloodlineAsset.cs` + `BloodlineLibrary.cs` |
| 9 级快照触发 | `Source/Content/BreakthroughVisualTrigger.cs` | 同款 `ProgressionLifecycle.RegisterCommitted` |
| 父母 ID 抓取 | `Source/Patch/PatchActor.cs:256-269`（createBabyActorFromData） | 扩展 transpiler 记录父母 ID |
| 血脉加成层 | `Source/Content/Cultisyses.Xian.cs:83` | 同款 `RegisterCachedStatsBuilder` |
| 存档重建 | 修仙 library 存档 / actor 存档 | 照搬 |

---

## 8. 未决 / 未来 / 待调

- **9 级后转其他体系**：本次只留空毕业钩子；保留 vs 让位、目标体系是什么 → 待那些体系动工再定。
- **主动招式**：本次不做，`Skills[]` 预留。
- **数值**（`0.005/月`、`0.3 / 0.7`、失败曲线、Knight.csv 各行、突破冷却月数、回溯代数）：按 `AGENTS.md` 后期手测调。
- **毕业钩子的转职语义**：待定。

---

## 9. 开发 TODO（建议分阶段）

### 阶段一：骨架
- [ ] `Knight` 组件 + `KnightSetting` + `Cultisyses.Knight.cs`（注册、获取规则、CSV 加载、stats builder、PowerLevels）
- [ ] `Content/Cultisys/Knight.csv`（8 列，初版数值）
- [ ] 获取：月度 QuerySystem + `Randy.randomChance` + 4 种族 `available_cultisys_ids`
- [ ] 互斥：Xian/Magic 获取补查「非骑士」

### 阶段二：升级循环
- [ ] 斗气资源 + 突破 profile（带失败几率、随等级升高、失败清空、突破冷却）
- [ ] 战斗灌斗气（杀敌 + 被击中，缩放 + 地板）
- [ ] 练习 ActorJob `KnightCultivator → DailyKnightTrain`

### 阶段三：闪避
- [ ] `knight_evasion` 自定义 stat + `RegisterActionBeforeBeAttacked` 钩子

### 阶段四：血脉
- [ ] `BloodlineAsset` + `BloodlineLibrary`（DynamicAssetLibrary + IDeleteWhenUnknown）
- [ ] 9 级快照触发（`ProgressionLifecycle.RegisterCommitted`）
- [ ] 父母表（扩展 `createBabyActorFromData` transpiler）+ 回溯 ≤5 代
- [ ] 血脉加成 stats-builder 层 + 公式
- [ ] 防雪崩（快照剔除继承加成）

### 阶段五：存档重建 + 毕业钩子占位
- [ ] 血脉快照 / Master 关系 / 父母表的存档重建
- [ ] 毕业钩子空操作占位

---

**文档维护**：实现推进时同步更新本文档与各阶段勾选。
