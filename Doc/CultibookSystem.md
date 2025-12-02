# 功法系统 (Cultibook System) 设计文档

**版本**: v1.1  
**状态**: 开发中  
**最后更新**: 2025年1月

---

## 📋 目录

1. [系统概述](#系统概述)
2. [核心机制](#核心机制)
3. [数据结构设计](#数据结构设计)
4. [功法分类体系](#功法分类体系)
5. [修炼方式系统](#修炼方式系统)
6. [法术池系统](#法术池系统)
7. [AI行为设计](#ai行为设计)
8. [UI界面设计](#ui界面设计)
9. [技术实现](#技术实现)
10. [开发TODO](#开发todo)

---

## 系统概述

### 设计目标

功法系统是修仙体系的核心组成部分，为修士提供独特的修炼路径、属性加成和技能学习渠道。本系统旨在：

1. **丰富修炼玩法** - 不同功法提供不同的修炼方式，让玩家体验多样化的修仙路径
2. **增加角色差异化** - 功法选择影响角色发展方向
3. **提供社交互动** - 功法传承、转修等机制促进NPC间互动
4. **平衡游戏体验** - 通过掌握程度限制，避免学习过多功法导致失衡

### 核心理念

- **一主多辅**: 修士可以学习多部功法，但只有一部作为"主修"，其余为"了解"
- **专精有益**: 主修功法享受完整效果，了解的功法仅作传承用途
- **修炼方式多元化**: 不同功法提供独特的修炼途径
- **法术领悟**: 修炼功法过程中可能领悟其中蕴含的法术

---

## 核心机制

### 2.1 学习与掌握

#### 主修功法
- 每个修士**只能有一部主修功法**
- 主修功法享受**完整的属性加成**
- 主修功法提供的**修炼方式生效**
- 钻研主修功法时**可能领悟法术**
- 掌握程度影响效果强度（0-100%）

#### 了解功法
- 修士可以**了解多部功法**
- 了解的功法**不提供属性加成**
- 了解的功法**无法使用其修炼方式**
- 了解的功法**可以抄录成书**传授他人
- 了解程度记录：仅作为抄录质量的参考

#### 转修机制
- 修士可以选择**转修**，将某部了解的功法设为主修
- 转修时，原主修功法变为"了解"
- 转修有**散功风险**：
  - 基础成功率：70%
  - 受智力(intelligence)影响
  - 受新功法与灵根的契合度影响
  - 失败时损失一定灵力(wakan)，严重时可能掉落境界
- 转修后需要重新积累掌握程度

### 2.2 掌握程度

掌握程度决定功法效果的发挥比例：

| 掌握程度 | 效果比例 | 描述 |
|---------|---------|------|
| 0-20% | 基础入门 | 初步感悟，效果微弱 |
| 20-40% | 小成 | 基本掌握核心，效果一般 |
| 40-60% | 大成 | 熟练运用，效果明显 |
| 60-80% | 圆满 | 融会贯通，效果显著 |
| 80-100% | 登峰造极 | 完美掌握，效果最大化 |

**掌握程度增长因素**：
- 修炼时间
- 智力(intelligence)
- 灵根与功法属性的契合度
- 是否有师承关系（有师傅教导会加速）

### 2.3 功法传承

- **创作功法**: 元婴境界以上的修士可以创作新功法
- **著书传承**: 了解或主修的功法都可以抄录成书
- **师徒传承**: 拥有师承关系时，学习效率提升
- **宗门功法**: 宗门可以设定宗门功法，新入门弟子优先学习

---

## 数据结构设计

### 3.1 CultibookAsset (功法资源定义)

```csharp
public class CultibookAsset : Asset, IDeleteWhenUnknown
{
    // 基础信息
    public string Name;                      // 功法名称
    public string Description;               // 描述文本
    public ItemLevel Level;                  // 功法品阶（人级/地级/天级/仙级）
    
    // 属性效果（主修时生效）
    public BaseStats FinalStats;             // 满掌握时的属性加成
    
    // 元素属性要求与亲和
    public ElementRequirement ElementReq;    // 灵根需求
    public float ElementAffinity;            // 灵根契合度阈值
    
    // 境界限制
    public int MinLevel;                     // 最低境界要求
    public int MaxLevel;                     // 最高可用境界（超过后效果衰减）
    
    // 修炼方式
    public string CultivateMethodId;         // 修炼方式Asset ID
    
    // 法术池
    public List<SkillPoolEntry> SkillPool;   // 可领悟的法术列表
    
    // 冲突与兼容
    public string[] ConflictTags;            // 冲突标签（如正邪不两立）
    public string[] SynergyTags;             // 协同标签（如同源功法）
    
    // 统计
    public int Current { get; set; } = 0;    // 当前学习此功法的人数
}
```

### 3.2 ElementRequirement (灵根需求)

```csharp
public struct ElementRequirement
{
    public float MinIron;    // 最低金元素
    public float MinWood;    // 最低木元素
    public float MinWater;   // 最低水元素
    public float MinFire;    // 最低火元素
    public float MinEarth;   // 最低土元素
    public float MinNeg;     // 最低阴元素
    public float MinPos;     // 最低阳元素
    public float MinEntropy; // 最低混沌元素
    
    // 检查灵根是否满足需求
    public bool Check(ElementRoot root);
    // 计算契合度（0-1）
    public float GetAffinity(ElementRoot root);
}
```

### 3.3 CultivateMethodAsset (修炼方式资源)

修炼方式采用 Asset 而非 Enum，通过委托定义灵活的修炼行为：

```csharp
public class CultivateMethodAsset : Asset
{
    // ========== 核心委托 ==========
    
    /// <summary>
    /// 检查是否可以使用此修炼方式
    /// </summary>
    /// <param name="ae">修炼者扩展</param>
    /// <returns>是否满足修炼条件</returns>
    public Func<ActorExtend, bool> CanCultivate;
    
    /// <summary>
    /// 计算修炼效率系数
    /// </summary>
    /// <param name="ae">修炼者扩展</param>
    /// <returns>效率系数（1.0为标准）</returns>
    public Func<ActorExtend, float> GetEfficiency;
    
    /// <summary>
    /// 修炼副作用（如魔道的杀业积累）
    /// </summary>
    /// <param name="ae">修炼者扩展</param>
    /// <param name="wakanGained">本次获得的灵力</param>
    public Action<ActorExtend, float> OnSideEffect;
    
    // ========== AI行为相关 ==========
    
    /// <summary>
    /// 获取对应的行为任务ID（用于替换标准修炼行为）
    /// </summary>
    /// <param name="ae">修炼者扩展</param>
    /// <returns>行为任务ID</returns>
    public Func<ActorExtend, string> GetBehaviourJobId;
    
    // ========== 触发条件 ==========
    
    /// <summary>
    /// 修炼触发类型
    /// </summary>
    public CultivateTriggerType TriggerType = CultivateTriggerType.Active;
    
    /// <summary>
    /// 被动触发时的事件类型集合（如战斗修炼在攻击时触发）
    /// </summary>
    public HashSet<PassiveTriggerEvents> PassiveTriggerEvents = new();
}

/// <summary>
/// 修炼触发类型
/// </summary>
public enum CultivateTriggerType
{
    Active,     // 主动修炼（需要专门的修炼行为）
    Passive,    // 被动修炼（在特定事件时触发）
    Continuous  // 持续修炼（如国运修炼，不影响其他行为）
}

/// <summary>
/// 被动修炼触发事件类型
/// </summary>
public enum PassiveTriggerEvents
{
    OnKill,         // 击杀时触发
    OnAttack,       // 攻击时触发
    OnBeAttacked,   // 被攻击时触发
    OnGoodDeed,     // 行善时触发
    OnWinBattle,    // 战斗胜利时触发
    OnBuild,        // 建造时触发
    OnCraft,        // 制作时触发
    OnTrade,        // 交易时触发
}
```

**注意**: `PassiveTriggerEvents` 字段使用 `HashSet<PassiveTriggerEvents>` 类型，支持多个触发事件组合（如战斗修炼可以同时触发 OnAttack 和 OnBeAttacked）。
```

### 3.4 修炼方式集合 (CultivateMethods)

```csharp
public class CultivateMethods : ExtendLibrary<CultivateMethodAsset, CultivateMethods>
{
    /// <summary>
    /// 标准闭关修炼方式
    /// </summary>
    public static CultivateMethodAsset Standard { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        Standard.TriggerType = CultivateTriggerType.Active;
        Standard.CanCultivate = ae => ae.HasCultisys<Xian>();
        Standard.GetEfficiency = ae => ae.HasElementRoot() ? ae.GetElementRoot().GetStrength() : 1f;
        Standard.GetBehaviourJobId = ae => {
            if (ae.Base.hasHouse())
            {
                return ActorJobs.XianCultivator.id;
            }
            else
            {
                return ActorJobs.PlantXianCultivator.id;
            }
        };
    }
}
```

**注意**: 修炼方式通过 `GetBehaviourJobId` 委托指定对应的AI行为任务，实际的修炼逻辑（如吸收灵气、增加灵力等）由对应的行为任务实现。这种方式更加灵活，允许不同修炼方式使用不同的行为逻辑。

### 3.5 SkillPoolEntry (法术池条目)

```csharp
public struct SkillPoolEntry
{
    public string SkillEntityAssetId;    // 技能资源ID
    public float BaseChance;             // 基础领悟概率
    public float MasteryThreshold;       // 需要的掌握程度阈值
    public int LevelRequirement;         // 需要的境界
}
```

### 3.6 ActorCultibookState (角色功法状态组件)

```csharp
public struct ActorCultibookState : IComponent
{
    // 主修功法
    public string MainCultibookId;       // 主修功法ID
    public float MainMastery;            // 主修掌握程度
    public float AccumulatedTime;        // 累计修炼时间
    
    // 法术领悟进度
    public Dictionary<string, float> SkillProgress;  // 各法术的领悟进度
}
```

### 3.7 CultibookKnowledge (功法了解关系)

```csharp
public struct CultibookKnowledge : ILinkRelation
{
    public string CultibookId;           // 功法ID
    public float KnowledgeLevel;         // 了解程度（用于抄录质量）
    public bool IsMain;                  // 是否为主修
    
    public Entity GetRelationKey();
}
```

---

## 功法分类体系

### 4.1 品阶分类

| 品阶 | 掉落权重 | 属性加成倍率 | 法术池数量 | 描述 |
|-----|---------|------------|----------|------|
| 人级 | 60% | 1.0x | 1-2 | 入门功法，易学难精 |
| 地级 | 25% | 2.0x | 2-4 | 中阶功法，需一定天赋 |
| 天级 | 12% | 4.0x | 4-6 | 高阶功法，效果显著 |
| 仙级 | 3% | 8.0x | 6-10 | 顶级功法，万中无一 |

### 4.2 属性流派

#### 五行功法

| 流派 | 灵根要求 | 主要加成 | 代表修炼方式 |
|-----|---------|---------|------------|
| 金系 | Iron > 0.5 | 攻击、护甲 | 战斗修炼 |
| 木系 | Wood > 0.5 | 生命、恢复 | 林中共修 |
| 水系 | Water > 0.5 | 速度、灵巧 | 水中修炼 |
| 火系 | Fire > 0.5 | 暴击、爆发 | 战斗修炼 |
| 土系 | Earth > 0.5 | 防御、稳固 | 山巅修炼 |

#### 阴阳功法

| 流派 | 灵根要求 | 主要加成 | 代表修炼方式 |
|-----|---------|---------|------------|
| 阴系 | Neg > 0.5 | 闪避、暗杀 | 夜间修炼 |
| 阳系 | Pos > 0.5 | 恢复、祝福 | 日间修炼 |

#### 特殊流派

| 流派 | 要求 | 主要特点 | 修炼方式 |
|-----|-----|---------|---------|
| 魔道 | 杀生记录 | 速成、风险高 | 杀戮吸收 |
| 帝道 | 是国王/领主 | 国运加成 | 国运修炼 |
| 佛道 | 功德值 | 稳健、渡劫易 | 功德积累 |

### 4.3 冲突标签体系

```
正道标签: ["righteous", "orthodox"]
魔道标签: ["demonic", "evil"]
佛道标签: ["buddhist", "peaceful"]
杀道标签: ["killing", "battle"]
```

- 拥有 "righteous" 标签的功法与 "demonic" 标签的功法冲突
- 冲突功法**不能同时作为主修和了解**
- 尝试学习冲突功法会导致心魔（Debuff）

---

## 修炼方式系统

### 5.1 修炼方式Asset定义示例

所有修炼方式都是 `CultivateMethodAsset`，通过委托定义各自的行为逻辑。以下是各种修炼方式的具体实现：

#### 5.1.1 Standard (标准闭关修炼)

```csharp
protected override void OnInit()
{
    Standard.TriggerType = CultivateTriggerType.Active;
    Standard.CanCultivate = ae => ae.HasCultisys<Xian>();
    Standard.GetEfficiency = ae => ae.HasElementRoot() ? ae.GetElementRoot().GetStrength() : 1f;
    Standard.GetBehaviourJobId = ae => {
        if (ae.Base.hasHouse())
        {
            return ActorJobs.XianCultivator.id;
        }
        else
        {
            return ActorJobs.PlantXianCultivator.id;
        }
    };
}
```

**说明**: 标准修炼方式通过 `GetBehaviourJobId` 委托返回对应的行为任务ID。如果角色有住所，使用 `XianCultivator` 任务（在家修炼）；否则使用 `PlantXianCultivator` 任务（植物形态修炼）。实际的修炼逻辑（吸收灵气、增加灵力等）在对应的行为任务中实现。

#### 5.1.2 WaterMeditation (水中修炼)

```csharp
protected override void OnInit()
{
    // ... Standard 配置 ...
    
    WaterMeditation.TriggerType = CultivateTriggerType.Active;
    WaterMeditation.CanCultivate = ae => ae.Base.current_tile.isWater();
    WaterMeditation.GetEfficiency = ae => 
    {
        if (!ae.Base.current_tile.isWater()) return 0.5f;
        // 水中效率提升，且与水系灵根成正比
        return 1.5f * (ae.HasElementRoot() ? ae.GetElementRoot().Water : 1f);
    };
    WaterMeditation.GetBehaviourJobId = ae => ActorJobs.WaterCultivator.id; // 假设存在此任务
}
```

**说明**: 水中修炼需要角色在水中才能进行。修炼效率在水中会提升（1.5x），且与水系灵根强度成正比。需要创建专门的 `WaterCultivator` 行为任务，该任务负责寻找水域、移动到水域，并在水中执行修炼逻辑。

#### 5.1.3 KillAbsorb (杀戮吸收 - 魔道)

```csharp
protected override void OnInit()
{
    // ... 其他配置 ...
    
    KillAbsorb.TriggerType = CultivateTriggerType.Passive;
    KillAbsorb.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnKill);
    KillAbsorb.CanCultivate = ae => true; // 只要杀了就能修炼
    KillAbsorb.GetEfficiency = ae =>
    {
        // 杀得越多效率越高，但也越容易心魔
        var killCount = ae.Base.data.get("kill_count", 0);
        return 1f + Mathf.Log10(killCount + 1) * 0.5f;
    };
    KillAbsorb.OnSideEffect = (ae, wakanGained) =>
    {
        // 积累杀业
        var karma = ae.Base.data.get("karma", 0f);
        karma -= wakanGained * 0.1f; // 负值为恶业
        ae.Base.data.set("karma", karma);
        
        // 杀业过重可能触发心魔
        if (karma < -100 && Randy.randomChance(0.01f))
        {
            ae.Base.addTrait("inner_demon"); // 触发心魔状态
        }
    };
    // 被动修炼不需要 GetBehaviourJobId
}
```

**说明**: 杀戮吸收是被动修炼方式，在击杀时触发（通过 `PassiveTriggerEvents.Add(PassiveTriggerEvents.OnKill)`）。实际的灵力获取和副作用处理在事件触发系统中实现，无需专门的 `GetBehaviourJobId`。

#### 5.1.4 KingdomFortune (国运修炼 - 帝道)

```csharp
protected override void OnInit()
{
    // ... 其他配置 ...
    
    KingdomFortune.TriggerType = CultivateTriggerType.Continuous;
    KingdomFortune.CanCultivate = ae => ae.Base.isKing() || ae.Base.isCityLeader();
    KingdomFortune.GetEfficiency = ae =>
    {
        if (!ae.Base.isKing()) return 0.5f; // 城主效率减半
        return 1.0f;
    };
    KingdomFortune.OnSideEffect = (ae, wakanGained) =>
    {
        // 吸收国运可能影响国家气运
        // 这里可以实现国运减少的逻辑
    };
    // 持续修炼由专门的系统处理，不需要 GetBehaviourJobId
}
```

**说明**: 国运修炼是持续修炼方式（`Continuous`），由专门的 `ContinuousCultivateSystem` 系统每帧自动处理。系统会根据国运大小计算灵力收益，无需专门的AI行为任务。实际的灵力计算在持续修炼系统中实现。

#### 5.1.5 BattleCultivate (战斗修炼)

```csharp
protected override void OnInit()
{
    // ... 其他配置 ...
    
    BattleCultivate.TriggerType = CultivateTriggerType.Passive;
    BattleCultivate.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnAttack);
    BattleCultivate.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnBeAttacked);
    BattleCultivate.CanCultivate = ae => ae.Base.isInCombat();
    BattleCultivate.GetEfficiency = ae =>
    {
        // 战斗越激烈效率越高
        var combatIntensity = ae.Base.data.get("combat_intensity", 1f);
        return combatIntensity;
    };
    BattleCultivate.OnSideEffect = (ae, wakanGained) =>
    {
        // 战斗修炼增加战斗经验
        ae.Base.data.add("battle_experience", wakanGained);
    };
    // 被动修炼不需要 GetBehaviourJobId
}
```

**说明**: 战斗修炼是被动修炼方式，在攻击或被攻击时触发。实际的灵力获取在事件触发系统中实现，根据伤害值计算。

#### 5.1.6 MeritAccumulate (功德修炼 - 佛道)

```csharp
protected override void OnInit()
{
    // ... 其他配置 ...
    
    MeritAccumulate.TriggerType = CultivateTriggerType.Passive;
    MeritAccumulate.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnGoodDeed); // 行善时触发
    MeritAccumulate.CanCultivate = ae =>
    {
        var karma = ae.Base.data.get("karma", 0f);
        return karma >= 0; // 需要无恶业才能修炼
    };
    MeritAccumulate.GetEfficiency = ae =>
    {
        var karma = ae.Base.data.get("karma", 0f);
        return 1f + karma * 0.01f; // 功德越多效率越高
    };
    MeritAccumulate.OnSideEffect = (ae, wakanGained) =>
    {
        // 功德修炼增加karma
        var karma = ae.Base.data.get("karma", 0f);
        karma += wakanGained * 0.5f;
        ae.Base.data.set("karma", karma);
        
        // 功德深厚者渡劫更容易
        if (karma > 100)
        {
            ae.Base.addTrait("merit_protection");
        }
    };
    // 被动修炼不需要 GetBehaviourJobId
}
```

**说明**: 功德修炼是被动修炼方式，在行善时触发（通过 `PassiveTriggerEvents.Add(PassiveTriggerEvents.OnGoodDeed)`）。实际的灵力获取和功德积累在事件触发系统中实现。

### 5.2 修炼方式注册

```csharp
public class CultivateMethods : ExtendLibrary<CultivateMethodAsset, CultivateMethods>
{
    public static CultivateMethodAsset Standard { get; private set; }
    public static CultivateMethodAsset WaterMeditation { get; private set; }
    public static CultivateMethodAsset MountainAbsorb { get; private set; }
    public static CultivateMethodAsset ForestCommunion { get; private set; }
    public static CultivateMethodAsset BattleCultivate { get; private set; }
    public static CultivateMethodAsset KillAbsorb { get; private set; }
    public static CultivateMethodAsset BloodRefine { get; private set; }
    public static CultivateMethodAsset KingdomFortune { get; private set; }
    public static CultivateMethodAsset FaithPower { get; private set; }
    public static CultivateMethodAsset MeritAccumulate { get; private set; }
    
    protected override void OnInit()
    {
        // 初始化各修炼方式的委托...
    }
}
```

### 5.3 修炼方式触发集成

简化后的修炼方式系统将修炼逻辑集中在行为任务（BehaviourJob）和系统（System）中：

- **主动修炼（Active）**: 通过 `GetBehaviourJobId` 返回对应的行为任务ID，由该行为任务执行完整的修炼逻辑（检查条件、吸收灵气、增加灵力等）
- **被动修炼（Passive）**: 通过事件系统触发，在对应的事件处理中计算收益并调用 `OnSideEffect`
- **持续修炼（Continuous）**: 由专门的系统（如 `ContinuousCultivateSystem`）每帧自动处理

```csharp
// 在事件系统中注册被动修炼触发
public static class CultivateMethodTriggers
{
    public static void Init()
    {
        // 击杀触发
        ActorExtend.RegisterActionOnKill((killer, victim) =>
        {
            var mainCultibook = killer.GetMainCultibook();
            if (mainCultibook == null) return;
            
            var method = mainCultibook.GetCultivateMethod();
            if (method.TriggerType != CultivateTriggerType.Passive) return;
            if (method.PassiveTriggerEvents == null || 
                !method.PassiveTriggerEvents.Contains(PassiveTriggerEvents.OnKill)) return;
            
            // 计算修炼收益（在事件处理中实现）
            ref var xian = ref killer.GetCultisys<Xian>();
            var victimPower = victim.Base.stats[S.power];
            var efficiency = method.GetEfficiency?.Invoke(killer) ?? 1f;
            var gain = victimPower * 0.1f * efficiency; // 收益计算逻辑
            
            xian.wakan += gain;
            method.OnSideEffect?.Invoke(killer, gain);
        });
        
    }
}
```

**注意**: 
- `CultivateMethodTriggers.Init()` 在 `Content.Manager.Init()` 中调用，注册所有被动触发事件
- `ContinuousCultivateSystem` 在 `Content.Manager.Init()` 中添加到 `GeneralLogicSystems`，并已使用 `ContinuousCultivateTag` 进行性能优化
- 所有被动触发事件处理已实现：OnKill、OnAttack、OnBeAttacked

---

## 法术池系统

### 6.1 法术领悟机制

修炼主修功法时，有机会领悟其中的法术：

1. **领悟概率计算**:
```
领悟概率 = 基础概率 × 掌握程度系数 × 智力系数 × 灵根契合度
```

2. **领悟条件**:
   - 掌握程度达到法术要求的阈值
   - 境界达到法术要求
   - 当前未学习该法术

3. **领悟时机**:
   - 完成一次完整修炼后
   - 掌握程度突破关键节点时（20%/40%/60%/80%/100%）

### 6.2 法术池配置示例

```json
{
    "cultibook_id": "water_basic",
    "skill_pool": [
        {
            "skill_id": "water_arrow",
            "base_chance": 0.3,
            "mastery_threshold": 20,
            "level_requirement": 1
        },
        {
            "skill_id": "water_shield",
            "base_chance": 0.2,
            "mastery_threshold": 40,
            "level_requirement": 2
        },
        {
            "skill_id": "water_dragon",
            "base_chance": 0.05,
            "mastery_threshold": 80,
            "level_requirement": 3
        }
    ]
}
```

---

## AI行为设计

### 7.1 功法相关AI行为

#### BehChooseCultibook (选择功法)
- **触发条件**: 无主修功法且有可学习的功法
- **行为逻辑**:
  1. 获取所有可学习的功法（书籍、师承）
  2. 筛选符合灵根需求的功法
  3. 根据品阶、契合度评分
  4. 选择评分最高的功法作为主修

#### BehStudyCultibook (研习功法)
- **触发条件**: 有主修功法，掌握程度 < 100%
- **行为逻辑**:
  1. 执行对应修炼方式的行为
  2. 增加掌握程度
  3. 检查是否可以领悟法术
  4. 触发法术领悟判定

#### BehSwitchCultibook (转修功法)
- **触发条件**: 有更好的功法选择，且当前功法掌握程度较低
- **行为逻辑**:
  1. 评估转修收益
  2. 计算转修成功率
  3. 执行转修判定
  4. 处理转修结果

#### BehTeachCultibook (传授功法)
- **触发条件**: 有弟子/宗门成员，且对方无主修功法
- **行为逻辑**:
  1. 找到需要传授的对象
  2. 将功法传授给对方
  3. 建立师承关系

### 7.2 修炼方式专属行为

#### BehWaterCultivate (水中修炼)
```csharp
public class BehWaterCultivate : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        // 检查是否在水中
        if (!pObject.current_tile.isWater())
        {
            // 寻找附近的水域
            var water_tile = FindNearbyWater(pObject);
            if (water_tile == null) return BehResult.Stop;
            
            pObject.goTo(water_tile);
            return BehResult.RepeatStep;
        }
        
        // 执行水中修炼
        var ae = pObject.GetExtend();
        ref var xian = ref ae.GetCultisys<Xian>();
        
        // 水中修炼效率提升50%
        var efficiency = 1.5f * ae.GetElementRoot().Water;
        CultivateWithEfficiency(ae, ref xian, efficiency);
        
        return BehResult.Continue;
    }
}
```

#### BehKillCultivate (杀戮修炼)
```csharp
public class BehKillCultivate : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 寻找目标
        var target = FindKillTarget(pObject);
        if (target == null) return BehResult.Stop;
        
        // 攻击目标
        if (!pObject.isInAttackRange(target))
        {
            pObject.goTo(target);
            return BehResult.RepeatStep;
        }
        
        pObject.attackTarget(target);
        
        // 如果目标死亡，吸收灵力
        if (!target.data.alive)
        {
            ref var xian = ref ae.GetCultisys<Xian>();
            var absorbed = CalculateAbsorbedWakan(target);
            xian.wakan += absorbed;
            
            // 积累杀业
            ae.AddKarma(-absorbed * 0.1f);
        }
        
        return BehResult.Continue;
    }
}
```

#### BehKingdomCultivate (国运修炼)
```csharp
public class BehKingdomCultivate : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!pObject.isKing()) return BehResult.Stop;
        
        var ae = pObject.GetExtend();
        ref var xian = ref ae.GetCultisys<Xian>();
        
        // 计算国运
        var fortune = CalculateKingdomFortune(pObject);
        
        // 从国运中获取灵力（不影响正常国王行为）
        xian.wakan += fortune * 0.001f;
        
        return BehResult.Continue;
    }
}
```

### 7.3 任务与条件

```csharp
// 新增任务
public static BehaviourTaskActor StudyCultibook { get; private set; }
public static BehaviourTaskActor WaterCultivate { get; private set; }
public static BehaviourTaskActor KillCultivate { get; private set; }
public static BehaviourTaskActor KingdomCultivate { get; private set; }

// 新增条件
public class CondHasMainCultibook : BehConditionActor { }
public class CondNeedStudyCultibook : BehConditionActor { }
public class CondCanSwitchCultibook : BehConditionActor { }
public class CondIsWaterCultivator : BehConditionActor { }
public class CondIsKillCultivator : BehConditionActor { }
public class CondIsKingCultivator : BehConditionActor { }
```

---

## UI界面设计

### 8.1 功法信息页面 (CultibookPage)

显示角色的功法信息：

```
┌─────────────────────────────────────┐
│ 【功法】                              │
├─────────────────────────────────────┤
│ ★ 主修功法                           │
│   名称: 太玄真经                       │
│   品阶: 天级                          │
│   掌握: ████████░░ 82%              │
│   修炼方式: 山巅修炼                    │
│   属性加成:                           │
│     - 攻击力 +24.6                   │
│     - 灵力上限 +164.0                │
│   可领悟法术:                         │
│     ✓ 金剑术 (已领悟)                 │
│     ○ 金刃风暴 (需80%掌握)            │
│     ○ 金身术 (需100%掌握)             │
├─────────────────────────────────────┤
│ ○ 了解功法                           │
│   - 水灵诀 (30%)                     │
│   - 基础养气术 (45%)                  │
└─────────────────────────────────────┘
```

### 8.2 功法书界面 (CultibookTooltip)

鼠标悬停显示功法书信息：

```
┌─────────────────────────────────────┐
│ 《太玄真经》                          │
│ 品阶: 天级 ★★★                       │
├─────────────────────────────────────┤
│ 灵根需求:                            │
│   金元素 ≥ 0.6                       │
│ 境界要求:                            │
│   筑基期以上                          │
├─────────────────────────────────────┤
│ 修炼方式: 山巅修炼                     │
│ 在高处修炼可获得额外收益                 │
├─────────────────────────────────────┤
│ 满掌握属性加成:                        │
│   攻击力 +30                         │
│   灵力上限 +200                       │
│   金系亲和 +0.5                       │
├─────────────────────────────────────┤
│ 可领悟法术:                           │
│   - 金剑术 (20%)                     │
│   - 金刃风暴 (60%)                   │
│   - 金身术 (100%)                    │
├─────────────────────────────────────┤
│ 抄录者: 张三丰                        │
│ 传承: 武当派                          │
└─────────────────────────────────────┘
```

---

## 技术实现

### 9.1 现有代码修改

#### CultibookAsset 扩展
```csharp
// 修改 Source/Content/Libraries/CultibookAsset.cs
public class CultibookAsset : Asset, IDeleteWhenUnknown
{
    // 保留原有字段
    public BaseStats FinalStats;
    public string Name;
    public ItemLevel Level;
    public int Current { get; set; } = 0;
    
    // 新增字段
    public string Description;
    public ElementRequirement ElementReq;
    public float ElementAffinityThreshold = 0.3f;
    public int MinLevel = 0;
    public int MaxLevel = 20;
    public string CultivateMethodId = "Standard";  // 修炼方式Asset ID
    public List<SkillPoolEntry> SkillPool = new();
    public string[] ConflictTags = Array.Empty<string>();
    public string[] SynergyTags = Array.Empty<string>();
    
    // 获取修炼方式Asset
    public CultivateMethodAsset GetCultivateMethod()
    {
        if (string.IsNullOrEmpty(CultivateMethodId))
        {
            CultivateMethodId = "Standard";
        }
        return Manager.CultivateMethodLibrary.get(CultivateMethodId);
    }
}
```

#### ActorCultibookState 组件
```csharp
// 新增 Source/Content/Components/ActorCultibookState.cs
public struct ActorCultibookState : IComponent
{
    public string MainCultibookId;
    public float MainMastery;
    public float AccumulatedTime;
    public Dictionary<string, float> SkillProgress;
    
    public bool HasMainCultibook => !string.IsNullOrEmpty(MainCultibookId);
    
    public CultibookAsset GetMainCultibook()
    {
        return Libraries.Manager.CultibookLibrary.get(MainCultibookId);
    }
}
```

#### ActorExtend 扩展方法
```csharp
// 扩展 Source/Content/Extensions/ActorExtendTools.cs

// 获取主修功法
public static CultibookAsset GetMainCultibook(this ActorExtend ae)
{
    if (!ae.TryGetComponent(out ActorCultibookState state)) return null;
    if (!state.HasMainCultibook) return null;
    return state.GetMainCultibook();
}

// 设置主修功法
public static void SetMainCultibook(this ActorExtend ae, CultibookAsset cultibook)
{
    ref var state = ref ae.GetOrAddComponent<ActorCultibookState>();
    state.MainCultibookId = cultibook.id;
    state.MainMastery = 0;
    state.AccumulatedTime = 0;
}

// 转修功法
public static bool TrySwitchMainCultibook(this ActorExtend ae, CultibookAsset newCultibook)
{
    ref var state = ref ae.GetOrAddComponent<ActorCultibookState>();
    var oldCultibook = state.GetMainCultibook();
    
    // 计算转修成功率
    float successRate = CalculateSwitchSuccessRate(ae, newCultibook);
    
    if (!Randy.randomChance(successRate))
    {
        // 转修失败，损失灵力
        ref var xian = ref ae.GetCultisys<Xian>();
        xian.wakan *= 0.5f;
        return false;
    }
    
    // 转修成功
    if (oldCultibook != null)
    {
        // 将原主修变为了解
        ae.Master(oldCultibook, state.MainMastery * 0.5f);
    }
    
    state.MainCultibookId = newCultibook.id;
    state.MainMastery = ae.GetMaster(newCultibook) * 0.8f; // 了解程度部分转化
    state.AccumulatedTime = 0;
    
    return true;
}

// 计算转修成功率
private static float CalculateSwitchSuccessRate(ActorExtend ae, CultibookAsset newCultibook)
{
    float baseRate = 0.7f;
    float intelligenceBonus = ae.GetStat(S.intelligence) / 100f * 0.2f;
    float affinityBonus = newCultibook.ElementReq.GetAffinity(ae.GetElementRoot()) * 0.1f;
    
    return Mathf.Clamp01(baseRate + intelligenceBonus + affinityBonus);
}

// 尝试领悟法术
public static bool TryComprehendSkill(this ActorExtend ae)
{
    var mainCultibook = ae.GetMainCultibook();
    if (mainCultibook == null) return false;
    
    ref var state = ref ae.GetComponent<ActorCultibookState>();
    
    foreach (var entry in mainCultibook.SkillPool)
    {
        if (state.MainMastery < entry.MasteryThreshold) continue;
        if (ae.HasSkill(entry.SkillEntityAssetId)) continue;
        
        float chance = entry.BaseChance 
            * (state.MainMastery / 100f)
            * (ae.GetStat(S.intelligence) / 50f);
        
        if (Randy.randomChance(chance))
        {
            ae.LearnSkillFromCultibook(entry.SkillEntityAssetId);
            return true;
        }
    }
    
    return false;
}
```

### 9.2 新增文件列表

```
Source/Content/
├── Components/
│   ├── ActorCultibookState.cs      # 角色功法状态组件
│   ├── ContinuousCultivateTag.cs   # 持续修炼标记组件（性能优化）
│   └── CultibookKnowledge.cs       # 功法了解关系
├── AIGC/
│   └── CultibookLLMGenerator.cs    # 调用LLM生成功法蓝图
├── Libraries/
│   ├── CultivateMethodAsset.cs     # 修炼方式资源定义
│   └── CultivateMethodLibrary.cs   # 修炼方式库（AssetLibrary）
├── Systems/
│   └── Logic/
│       └── ContinuousCultivateSystem.cs # 持续修炼系统（国运等）
├── Behaviours/
│   ├── BehChooseCultibook.cs       # 选择功法行为
│   ├── BehStudyCultibook.cs        # 研习功法行为
│   ├── BehSwitchCultibook.cs       # 转修功法行为
│   ├── BehTeachCultibook.cs        # 传授功法行为
│   └── Conditions/
│       ├── CondHasMainCultibook.cs
│       ├── CondNeedStudyCultibook.cs
│       ├── CondCanSwitchCultibook.cs
│       └── CondCultivateMethod.cs
├── Extensions/
│   └── ActorExtendTools.cs         # 功法相关扩展方法（已包含）
├── CultivateMethods.cs             # 修炼方式集合（ExtendLibrary，自动注册）
└── CultivateMethodTriggers.cs      # 修炼方式触发器（被动触发集成）

Source/Content/UI/
└── CreatureInfoPages/
    └── CultibookPage.cs            # 功法页面（已存在，需扩展）

Content/
└── Cultibooks/
    └── Cultibooks.json             # 功法配置

Locales/
└── cultibooks.csv                  # 功法本地化
```

### 9.3 LLM功法生成方案

**配置开关**
- `default_config.json` → `AIGCSettings.ENABLE_CULTIBOOK_LLM`，默认关闭，避免无 API Key 时阻塞。
- 只有在 BaseURL/APIKey/Model 均填写且开关开启的情况下才会进入 LLM 流程。

**调用流程**
1. `BookManagerTools.CreateNewCultibook` 首先检查配置，若开启则收集角色信息（灵根、境界、功法、技能）。
2. `CultibookLLMGenerator` 构造提示词：包含可用的属性列表、修炼方式列表、已知技能（附真实 `SkillEntityAssetID`）、角色背景等。
3. 通过 `Manager.RequestResponseContent` 请求 LLM，要求输出固定 JSON：
   ```json
   {
     "name": "string",
     "description": "string",
     "cultivate_method_id": "string",
     "min_level": 0,
     "max_level": 0,
     "element_requirement": { "iron": 0, ... },
     "stats": [{ "id": "damage", "value": 0 }],
     "skill_pool": [{ "skill_id": "Skill.Fireball", "chance": 0.05, "mastery_threshold": 30, "level_requirement": 5 }],
     "tags": { "conflict": [], "synergy": [] }
   }
   ```
4. 解析结果，校验字段合法性（元素/境界/概率全部 clamp）。若某字段缺失，则回退至旧的随机生成逻辑。
5. 根据蓝图生成 `CultibookAsset`，保存到 `CultibookLibrary` 并写回 `Book`。

**回退策略**
- LLM 请求失败、解析失败或生成内容为空 → 自动回到原有的统计分布逻辑。
- SkillPool 若引用了未知技能 → 自动过滤，不足时由默认逻辑补足。

---

## 开发TODO

### 阶段一：核心框架 (预计1周)

- [x] **T1.1** 扩展 `CultibookAsset` 数据结构 ✅
  - [x] 添加 Description 字段
  - [x] 添加 ElementRequirement 字段
  - [x] 添加 ElementAffinityThreshold 字段
  - [x] 添加 MinLevel/MaxLevel 境界限制字段
  - [x] 添加 CultivateMethodId 字段
  - [x] 添加 SkillPool 字段
  - [x] 添加 ConflictTags/SynergyTags 冲突/协同标签字段

- [x] **T1.1.1** 创建 `ElementRequirement` 结构体 ✅
  - [x] 实现所有五行阴阳元素字段
  - [x] 实现 `Check()` 方法（检查灵根是否满足需求）
  - [x] 实现 `GetAffinity()` 方法（计算契合度）

- [x] **T1.1.2** 创建 `SkillPoolEntry` 结构体 ✅
  - [x] 实现 SkillEntityAssetId 字段
  - [x] 实现 BaseChance 字段
  - [x] 实现 MasteryThreshold 字段
  - [x] 实现 LevelRequirement 字段

- [x] **T1.2** 创建 `ActorCultibookState` 组件 ✅
  - [x] 实现主修功法管理
  - [x] 实现掌握程度管理
  - [x] 实现法术领悟进度管理

- [x] **T1.3** 扩展 ActorExtend ✅
  - [x] 实现 `GetMainCultibook()` 方法
  - [x] 实现 `SetMainCultibook()` 方法
  - [x] 实现 `GetMainCultibookMastery()` 方法
  - [x] 实现 `AddMainCultibookMastery()` 方法
  - [x] 实现 `TrySwitchMainCultibook()` 方法
  - [x] 实现 `CalculateSwitchSuccessRate()` 辅助方法
  - [x] 实现 `TryComprehendSkill()` 方法
  - [x] 实现 `LearnSkillFromCultibook()` 辅助方法
  - [x] 实现 `HasSkill()` 辅助方法

- [x] **T1.4** 修改现有功法学习逻辑 ✅
  - [x] 区分"设为主修"和"了解"
    - [x] 如果没有主修功法，新学习的功法设为主修（初始掌握1%）
    - [x] 如果已有主修，新学习的功法添加为了解（了解程度上限50%）
  - [x] 修改 `BookTypes.LearnCultibook` 实现上述逻辑
  - [x] 修改属性加成逻辑（仅主修生效）
    - [x] 仅应用主修功法的 `FinalStats`
    - [x] 根据主修掌握程度计算加成比例

### 阶段二：修炼方式系统 (预计1.5周)

- [x] **T2.1** 创建修炼方式Asset框架 ✅
  - [x] 创建 `CultivateMethodAsset` 类（含委托定义）
  - [x] 创建 `CultivateMethods` 库（ExtendLibrary）
  - [x] 定义 `CultivateTriggerType` 枚举（Active/Passive/Continuous）

- [x] **T2.2** 实现主动修炼方式（Active）- 基础框架 ✅
  - [x] Standard - 标准闭关修炼
    - [x] CanCultivate: 检查是否有修仙状态
    - [x] GetEfficiency: 根据灵根强度计算效率
    - [x] GetBehaviourJobId: 返回对应的行为任务ID（有住所用XianCultivator，否则用PlantXianCultivator）
  - [x] WaterMeditation - 水中修炼 ✅
    - [x] CanCultivate: 检查是否在水中
    - [x] GetEfficiency: 水中1.5x，与水系灵根相关
    - [x] GetBehaviourJobId: 返回水中修炼任务ID
  - [ ] MountainAbsorb - 山巅修炼（待实现）
  - [ ] ForestCommunion - 林中修炼（待实现）

- [x] **T2.3** 实现被动修炼方式（Passive）✅
  - [x] 被动触发事件框架 ✅
    - [x] 实现 OnKill 触发处理
    - [x] 实现 OnAttack 触发处理
    - [x] 实现 OnBeAttacked 触发处理
  - [x] BattleCultivate - 战斗修炼 ✅
    - [x] PassiveTriggerEvents: 添加 OnAttack, OnBeAttacked
    - [x] CanCultivate: 检查是否有修仙状态
    - [x] GetEfficiency: 基础效率1.0（可后续扩展）
    - [ ] OnSideEffect: 增加战斗经验（待后续扩展）
  - [x] KillAbsorb - 杀戮吸收 ✅
    - [x] PassiveTriggerEvents: 添加 OnKill
    - [x] CanCultivate: 检查是否有修仙状态
    - [x] GetEfficiency: 基础效率1.0（可后续扩展）
    - [ ] OnSideEffect: 积累杀业/karma（待后续扩展）
  - [ ] MeritAccumulate - 功德修炼（Asset待实现）
    - [ ] PassiveTriggerEvents: 添加 OnGoodDeed
    - [ ] CanCultivate: 检查是否有功德（karma >= 0）
    - [ ] GetEfficiency: 根据功德值计算效率
    - [ ] OnSideEffect: 增加功德值

- [x] **T2.4** 实现持续修炼方式（Continuous）✅
  - [x] KingdomFortune - 国运修炼 ✅
    - [x] CanCultivate: 需要主修者为国王或城主
    - [x] GetEfficiency: 国王1.0，城主0.5
    - [ ] OnSideEffect: 可选的国家气运影响（待后续扩展）
  - [x] 创建 `ContinuousCultivateSystem` 系统 ✅
    - [x] 在系统中实现持续修炼的灵力收益计算
    - [x] 创建 `ContinuousCultivateTag` 标记组件用于性能优化
    - [x] 在系统构造函数中添加过滤条件，只查询有标记的单位
    - [x] 在 `SetMainCultibook` 中自动管理标记

- [x] **T2.5** 修炼触发集成 ✅
  - [x] 创建 `CultivateMethodTriggers` 类 ✅
  - [x] 注册 OnKill 触发 ✅
    - [x] 实现 `OnKillTrigger` 方法
    - [x] 根据目标强度计算灵力收益
  - [x] 注册 OnAttack/OnBeAttacked 触发 ✅
    - [x] 在 `ActorExtend` 中添加 `RegisterActionOnBeAttacked` 方法
    - [x] 实现 `OnAttackTrigger` 方法（攻击时触发）
    - [x] 实现 `OnBeAttackedTrigger` 方法（被攻击时触发）
  - [ ] 注册 OnGoodDeed 触发（待实现）
  - [ ] 创建通用 `BehGenericCultivate` 行为（待实现）

### 阶段三：法术池与领悟 (预计1周)

- [x] **T3.1** 法术池系统（部分完成）
  - [x] 实现 `SkillPoolEntry` 数据结构 ✅
  - [ ] 实现法术领悟概率计算
  - [ ] 实现领悟判定逻辑

- [ ] **T3.2** 法术领悟触发
  - [ ] 修炼完成后触发判定
  - [ ] 掌握程度突破时触发判定
  - [ ] 实现领悟动画/通知

- [ ] **T3.3** 配置法术池
  - [ ] 为现有法术配置功法归属
  - [ ] 创建功法-法术配置文件

### 阶段四：AI行为完善 (预计1周)

- [ ] **T4.1** 功法选择AI
  - [ ] 实现 `BehChooseCultibook`
  - [ ] 实现功法评估算法（品阶、契合度等）

- [x] **T4.2** 转修AI
  - [x] 实现 `BehSwitchCultibook`
  - [x] 实现转修时机判断
  - [x] 实现转修风险评估

- [ ] **T4.3** 传承AI
  - [ ] 实现 `BehTeachCultibook`
  - [ ] 实现师承关系建立
  - [ ] 宗门功法传授

- [x] **T4.4** 改进AI
  - [x] 实现 `BehImproveCultibook`

### 阶段五：UI与本地化 (预计0.5周)

- [x] **T5.1** 扩展功法信息页面 ✅
  - [x] 显示主修/了解区分
  - [x] 显示掌握程度进度条（文本形式）
  - [x] 显示修炼方式（名称和描述）
  - [x] 显示可领悟法术（包含已领悟状态）

- [x] **T5.2** 功法书Tooltip ✅
  - [x] 显示完整功法信息（名称、品阶、描述）
  - [x] 显示灵根需求（所有非零元素需求）
  - [x] 显示境界要求
  - [x] 显示修炼方式（名称和描述）
  - [x] 显示满掌握属性加成
  - [x] 显示法术池（包含掌握要求和境界要求）
  - [x] 显示抄录者信息

- [x] **T5.3** 本地化 ✅
  - [x] 已在 `books.csv` 中添加修炼方式名称和描述
    - [x] Standard - 标准闭关
    - [x] WaterMeditation - 水中修炼
    - [x] BattleCultivate - 战斗修炼
    - [x] KillAbsorb - 杀戮吸收
    - [x] KingdomFortune - 国运修炼
  - [ ] 添加更多UI文本本地化（待完善）

### 阶段六：内容填充 (预计1周)

- [ ] **T6.1** 创建基础功法
  - [ ] 5个人级功法（每种灵根1个）
  - [ ] 3个地级功法
  - [ ] 2个天级功法
  - [ ] 1个仙级功法

- [ ] **T6.2** 配置功法法术池
  - [ ] 关联现有法术
  - [ ] 平衡领悟概率

- [ ] **T6.3** 创建特殊功法
  - [ ] 1-2个魔道功法
  - [ ] 1个帝道功法
  - [ ] 1个佛道功法

- [x] **T6.4** LLM 功法生成
  - [x] 新增配置开关与 `CultibookLLMGenerator`
  - [x] 规范提示词与 JSON Schema
  - [x] `BookManagerTools` 集成与回退逻辑

### 阶段七：测试与平衡 (预计0.5周)

- [ ] **T7.1** 功能测试
  - [ ] 测试功法学习流程
  - [ ] 测试转修机制
  - [ ] 测试各修炼方式
  - [ ] 测试法术领悟

- [ ] **T7.2** 平衡性调整
  - [ ] 调整掌握程度增长速度
  - [ ] 调整转修成功率
  - [ ] 调整法术领悟概率
  - [ ] 调整属性加成数值

- [ ] **T7.3** AI行为测试
  - [ ] 测试功法选择合理性
  - [ ] 测试修炼行为正确性
  - [ ] 测试传承行为

---

## 时间估算

| 阶段 | 任务 | 预计时间 |
|-----|------|---------|
| 阶段一 | 核心框架 | 1周 |
| 阶段二 | 修炼方式系统 | 1.5周 |
| 阶段三 | 法术池与领悟 | 1周 |
| 阶段四 | AI行为完善 | 1周 |
| 阶段五 | UI与本地化 | 0.5周 |
| 阶段六 | 内容填充 | 1周 |
| 阶段七 | 测试与平衡 | 0.5周 |
| **总计** | | **6.5周** |

---

## 附录

### A. 预设功法列表

| 名称 | 品阶 | 灵根 | 修炼方式 | 描述 |
|-----|-----|-----|---------|------|
| 基础养气术 | 人级 | 无 | 标准 | 最基础的修炼功法 |
| 水灵诀 | 人级 | 水 | 水中修炼 | 水系入门功法 |
| 烈火焚心诀 | 人级 | 火 | 战斗修炼 | 火系入门功法 |
| 玄木长生诀 | 人级 | 木 | 林中修炼 | 木系入门功法 |
| 金刚不坏经 | 人级 | 金 | 战斗修炼 | 金系入门功法 |
| 厚土真经 | 人级 | 土 | 山巅修炼 | 土系入门功法 |
| 九幽魔功 | 地级 | 阴 | 杀戮吸收 | 魔道功法 |
| 太玄真经 | 天级 | 金 | 山巅修炼 | 金系高阶功法 |
| 苍天帝经 | 天级 | 无 | 国运修炼 | 帝道功法 |
| 混元无极功 | 仙级 | 无 | 标准 | 顶级通用功法 |

### B. 修炼效率公式

```
最终效率 = 基础效率 × 方式系数 × 契合度 × 境界系数 × 天赋系数

基础效率 = 1.0
方式系数 = 修炼方式Asset.EfficiencyCalculator(actor)
契合度 = 功法灵根需求.GetAffinity(角色灵根)
境界系数 = 1 + (当前境界 - 功法最低境界) × 0.1
天赋系数 = 1 + 有修炼天赋特质 × 0.3
```

### C. 转修成功率公式

```
成功率 = 基础成功率 + 智力加成 + 契合度加成 - 境界惩罚

基础成功率 = 0.7
智力加成 = min(智力 / 100 × 0.2, 0.2)
契合度加成 = 新功法契合度 × 0.1
境界惩罚 = max(0, (当前境界 - 3) × 0.05)  // 金丹后转修更难
```

---

## 待完善细节与代码TODO

### 代码中的TODO项

以下是从代码实现中收集到的待完善项目：

#### 转修失败的额外惩罚
**位置**: `Source/Content/Extensions/ActorExtendTools.cs:213`  
**当前状态**: 转修失败时仅损失50%灵力  
**待完善内容**:
- [ ] 严重转修失败可能导致境界掉落
- [ ] 添加心魔/负面状态效果
- [ ] 根据失败严重程度分级惩罚
- [ ] 添加恢复期（短时间内无法再次转修）

**建议实现**:
```csharp
// 转修失败时的额外惩罚
if (!Randy.randomChance(successRate))
{
    ref var xian = ref ae.GetCultisys<Xian>();
    xian.wakan *= 0.5f;
    
    // TODO: 额外的惩罚
    // - 严重失败可能掉落境界
    // - 添加心魔状态（负面buff）
    // - 设置转修冷却时间
}
```

#### 持续修炼系统性能优化
**位置**: `Source/Content/Systems/Logic/ContinuousCultivateSystem.cs`  
**当前状态**: 已创建标记组件优化查询性能  
**已完成内容**:
- [x] 创建 `ContinuousCultivateTag` 标记组件
- [x] 在系统构造函数中添加过滤条件，只查询有标记的单位
- [x] 在 `SetMainCultibook` 中自动管理标记（设置/切换主修功法时自动添加/移除）
- [x] 在 `TrySwitchMainCultibook` 中自动管理标记

**性能优化效果**:
- 查询量大幅减少：仅查询有持续修炼标记的单位
- 预期性能提升：如果只有10%的单位使用持续修炼，查询量减少约90%

---

## 实现进度总结

### 已完成阶段

- ✅ **阶段一：核心框架** - 100% 完成
  - 所有核心数据结构已实现
  - ActorExtend 扩展方法已完成
  - 功法学习逻辑已实现

- ✅ **阶段二：修炼方式系统** - 90% 完成
  - 框架和主要修炼方式已实现
  - Standard、WaterMeditation、BattleCultivate、KillAbsorb、KingdomFortune 已实现
  - 被动触发系统已集成
  - 持续修炼系统已优化

- ⚠️ **阶段三：法术池与领悟** - 30% 完成
  - 数据结构已实现
  - 领悟逻辑待实现

- ✅ **阶段五：UI与本地化** - 95% 完成
  - CultibookPage 已完成
  - CultibookTooltip 已完成
  - 主要本地化已完成

### 待完成工作

- 阶段三：法术领悟触发和判定逻辑
- 阶段四：AI行为完善
- 阶段六：内容填充
- 阶段七：测试与平衡

---

**文档维护**: 请在每次功法系统更新后更新此文档  
**下次审查**: 完成阶段三后

