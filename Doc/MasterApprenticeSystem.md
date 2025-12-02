# 师徒传承系统 (Master-Apprentice System) 设计文档

**版本**: v1.0  
**状态**: 设计中  
**最后更新**: 2025年11月

---

## 📋 目录

1. [系统概述](#系统概述)
2. [核心机制](#核心机制)
3. [数据结构设计](#数据结构设计)
4. [师徒关系类型](#师徒关系类型)
5. [传承内容系统](#传承内容系统)
6. [师徒互动系统](#师徒互动系统)
7. [宗门师徒体系](#宗门师徒体系)
8. [AI行为设计](#ai行为设计)
9. [UI界面设计](#ui界面设计)
10. [技术实现](#技术实现)
11. [开发TODO](#开发todo)

---

## 系统概述

### 设计目标

师徒传承系统是修仙世界中至关重要的社会关系系统，为修士提供知识传承、修炼指导和社交互动。本系统旨在：

1. **知识传承** - 师傅可以将功法、技能、丹方等传授给弟子
2. **修炼加速** - 有师傅指导的弟子修炼效率提升
3. **社交网络** - 构建修仙世界的师门关系网络
4. **宗门发展** - 师徒关系是宗门发展壮大的基础
5. **故事驱动** - 师徒关系产生丰富的游戏事件和剧情

### 核心理念

- **一师多徒**: 一位师傅可以收多名弟子，但弟子通常只有一位正式师傅
- **传承有序**: 知识传承需要时间和条件，不是瞬间完成
- **互利共赢**: 师徒关系对双方都有益处（师傅获得贡献，弟子获得传承）
- **关系动态**: 师徒关系可以随时间发展、疏远甚至断裂
- **继承延续**: 师傅故去后，大弟子可继承衣钵

---

## 核心机制

### 2.1 拜师条件

#### 对师傅的要求
- **境界要求**: 师傅境界必须高于弟子至少一个大境界（如筑基收练气弟子）
- **功法掌握**: 师傅主修功法掌握程度需达到 40% 以上
- **弟子上限**: 根据境界决定可收弟子数量
  - 筑基: 最多2名弟子
  - 金丹: 最多5名弟子
  - 元婴: 最多10名弟子
  - 化神及以上: 无上限
- **特质限制**: 某些特质的修士不能收徒（如独行侠、魔道修士等）

#### 对弟子的要求
- **境界限制**: 弟子境界不能高于师傅
- **无师状态**: 弟子当前没有正式师傅（但可以有记名弟子身份）
- **灵根匹配**: 弟子灵根与师傅功法有一定契合度
- **关系要求**: 师徒双方关系不能为敌对

#### 拜师成功率
```
成功率 = 基础成功率 × 灵根契合度 × 关系系数 × 智力系数
基础成功率 = 0.7
灵根契合度 = 弟子灵根与师傅功法的契合度 (0-1)
关系系数 = 1 + 双方关系值 / 100 (0.5-1.5)
智力系数 = 弟子智力 / 50 (0.5-2.0)
```

### 2.2 师徒关系等级

| 关系等级 | 亲密度范围 | 描述 | 传承效率 |
|---------|-----------|------|---------|
| 记名弟子 | 0-30 | 初入师门，身份未定 | 50% |
| 入室弟子 | 30-60 | 正式拜师，可学核心 | 80% |
| 亲传弟子 | 60-90 | 深受器重，重点培养 | 100% |
| 衣钵传人 | 90-100 | 继承衣钵，传承正统 | 120% |

**亲密度增长因素**:
- 日常跟随师傅修炼 (+0.1/天)
- 完成师傅布置的任务 (+1-5/次)
- 为宗门/师门做出贡献 (+1-10/次)
- 师傅主动传授知识 (+2/次)
- 共同战斗并获胜 (+3/次)

**亲密度降低因素**:
- 长时间不联系 (-0.05/天)
- 违背师傅意愿 (-5/次)
- 背叛师门 (-50)
- 转投他门 (-100，关系断裂)

### 2.3 师徒关系解除

#### 正常解除
- **出师**: 弟子境界达到师傅境界或功法掌握达到100%，可申请出师
- **逐出师门**: 师傅主动解除关系，弟子保留所学但失去师门身份
- **师傅仙逝**: 师傅死亡后，弟子自动成为独立修士

#### 异常解除
- **背叛师门**: 弟子主动背叛，永久成为师门敌人
- **转修异道**: 弟子修炼与师门冲突的功法，被动解除
- **师傅堕魔**: 师傅堕入魔道，弟子可选择跟随或离开

---

## 数据结构设计

### 3.1 MasterApprenticeRelation (师徒关系组件)

```csharp
/// <summary>
/// 师徒关系组件 - 存储在弟子Entity上，指向师傅
/// </summary>
public struct MasterApprenticeRelation : ILinkRelation
{
    // 关系目标（师傅Entity）
    public Entity Master;
    
    // 关系类型
    public MasterApprenticeType RelationType;
    
    // 亲密度 (0-100)
    public float Intimacy;
    
    // 拜师时间（世界时间戳）
    public float ApprenticeTime;
    
    // 已传承的功法数量
    public int TransferredCultibookCount;
    
    // 已传承的技能数量
    public int TransferredSkillCount;
    
    // 是否为衣钵传人
    public bool IsSuccessor;
    
    public Entity GetRelationKey() => Master;
}

/// <summary>
/// 师徒关系类型枚举
/// </summary>
public enum MasterApprenticeType
{
    Nominal,      // 记名弟子
    Formal,       // 入室弟子
    Direct,       // 亲传弟子
    Successor     // 衣钵传人
}
```

### 3.2 MasterApprenticeState (师傅状态组件)

```csharp
/// <summary>
/// 师傅状态组件 - 存储在师傅Entity上
/// </summary>
public struct MasterApprenticeState : IComponent
{
    // 当前弟子数量
    public int ApprenticeCount;
    
    // 最大弟子数量
    public int MaxApprenticeCount;
    
    // 衣钵传人ID（如果已指定）
    public long SuccessorActorId;
    
    // 收徒意愿 (0-100，影响AI主动收徒概率)
    public float RecruitWillingness;
    
    // 传授意愿 (0-100，影响AI主动传授概率)
    public float TeachWillingness;
    
    // 师门风格
    public MasterStyle Style;
}

/// <summary>
/// 师门风格枚举 - 影响AI行为
/// </summary>
public enum MasterStyle
{
    Strict,      // 严厉型 - 高要求，高成长
    Gentle,      // 温和型 - 亲密度增长快
    Laissez,     // 放任型 - 自由发展
    Demanding,   // 苛求型 - 要求弟子贡献
    Protective   // 保护型 - 为弟子出头
}
```

### 3.3 ApprenticeData (弟子数据)

```csharp
/// <summary>
/// 弟子数据 - 用于记录弟子的详细信息
/// </summary>
public class ApprenticeData
{
    // 弟子ActorID
    public long ActorId;
    
    // 拜师时间
    public float ApprenticeTime;
    
    // 关系类型
    public MasterApprenticeType RelationType;
    
    // 亲密度
    public float Intimacy;
    
    // 传承记录
    public List<TransferRecord> TransferHistory;
}

/// <summary>
/// 传承记录
/// </summary>
public struct TransferRecord
{
    public string ContentType;    // 内容类型: Cultibook/Skill/Elixir
    public string ContentId;      // 内容ID
    public float TransferTime;    // 传承时间
    public float Quality;         // 传承质量 (0-100)
}
```

### 3.4 SectData 扩展

```csharp
/// <summary>
/// 宗门数据扩展
/// </summary>
public class SectData : MetaObjectData
{
    // 原有字段
    public string FounderActorName;
    public long FounderActorID;
    public int BannerBackgroundIndex;
    public int BannerIconIndex;
    
    // ======== 新增字段 ========
    
    // 当前掌门ID
    public long CurrentLeaderID;
    
    // 长老列表（ActorID列表）
    public List<long> ElderIDs;
    
    // 宗门功法ID列表
    public List<string> SectCultibookIds;
    
    // 宗门技能ID列表
    public List<string> SectSkillIds;
    
    // 宗门丹方ID列表
    public List<string> SectElixirIds;
    
    // 师徒关系图（师傅ID -> 弟子ID列表）
    public Dictionary<long, List<long>> MasterApprenticeMap;
    
    // 宗门贡献点记录
    public Dictionary<long, int> ContributionPoints;
    
    // 宗门规模
    public SectScale Scale;
}

/// <summary>
/// 宗门规模枚举
/// </summary>
public enum SectScale
{
    Tiny,       // 微型（1-10人）
    Small,      // 小型（11-50人）
    Medium,     // 中型（51-200人）
    Large,      // 大型（201-1000人）
    Huge,       // 巨型（1000+人）
}
```

---

## 师徒关系类型

### 4.1 个人师徒关系

最基本的师徒关系，师傅个人收弟子。

**特点**:
- 不需要宗门背景
- 师傅全权负责弟子培养
- 传承内容由师傅自主决定
- 关系更加紧密和个人化

**适用场景**:
- 散修收徒
- 宗门外的个人传承
- 隐世高人收徒

### 4.2 宗门师徒关系

在宗门体系内建立的师徒关系。

**特点**:
- 需要宗门认可
- 传承包含宗门功法和技能
- 弟子同时获得宗门身份
- 受宗门规矩约束

**层级结构**:
```
宗门
├── 掌门（宗主）
│   ├── 亲传弟子
│   └── 入室弟子
├── 长老
│   ├── 亲传弟子
│   └── 入室弟子
├── 内门弟子（有师傅）
│   └── 由长老或亲传弟子指导
├── 外门弟子（无正式师傅）
│   └── 由宗门统一培养
└── 记名弟子
    └── 挂名，资源有限
```

### 4.3 隔代师徒关系

师祖与徒孙之间的特殊关系。

**触发条件**:
- 师傅的师傅仍在世
- 徒孙表现出色或天赋异禀
- 师祖主动关注或指点

**效果**:
- 可从师祖处获得额外传承
- 提升在师门中的地位
- 可能被指定为隔代传人

---

## 传承内容系统

### 5.1 功法传承

**传承流程**:
1. 师傅选择要传授的功法
2. 检查弟子是否满足功法要求（灵根、境界）
3. 开始传承过程（需要时间）
4. 根据传承效率确定弟子获得的掌握程度

**传承效率计算**:
```
传承效率 = 基础效率 × 关系系数 × 师傅掌握度 × 智力系数
基础效率 = 30%
关系系数 = 师徒关系等级对应的传承效率 (50%-120%)
师傅掌握度 = 师傅对该功法的掌握程度 / 100
智力系数 = (师傅智力 + 弟子智力) / 100

弟子获得掌握度 = min(师傅掌握度 × 传承效率, 80%)
```

**限制**:
- 弟子通过传承获得的功法掌握度不能超过师傅
- 传承后弟子仍需自己修炼才能继续提升
- 某些功法标记为"不可传授"

### 5.2 技能传承

**传承方式**:
1. **口授心传**: 师傅直接传授技能心法
2. **技能书传承**: 师傅编写技能书给弟子
3. **实战教学**: 在战斗中演示和指导

**传承效果**:
- 直接传授: 弟子直接学会技能，但领悟度较低
- 技能书: 弟子需要自己研读，但可以反复学习
- 实战教学: 领悟度最高，但需要师徒同时在场

### 5.3 丹方传承

**传承条件**:
- 师傅掌握该丹方
- 弟子境界达到炼丹要求（通常为金丹期）

**传承效果**:
- 弟子获得丹方知识
- 初始掌握度取决于传承质量
- 需要实际炼制才能提升掌握度

### 5.4 修炼经验传承

**传承内容**:
- 修炼心得
- 突破经验
- 避劫技巧
- 战斗技巧

**效果**:
- 提升弟子修炼速度
- 降低突破失败率
- 提高渡劫成功率
- 增加战斗能力

---

## 师徒互动系统

### 6.1 日常互动

#### 跟随修炼
- 弟子跟随师傅修炼，效率提升30%
- 增加亲密度
- 有机会领悟师傅的战斗技巧

#### 请教问题
- 弟子主动请教，增加功法掌握度
- 消耗师傅的时间（影响师傅修炼）
- 增加亲密度

#### 布置任务
- 师傅布置修炼任务给弟子
- 完成任务获得奖励和亲密度
- 失败可能降低亲密度

### 6.2 重要事件

#### 传授仪式
- 正式传授功法/技能时的仪式
- 增加传承效率
- 产生世界日志

#### 入室礼
- 弟子正式成为入室弟子
- 解锁核心传承
- 增加宗门贡献

#### 出师礼
- 弟子修为有成，离开师门
- 关系转变为"同门"
- 保留师徒情谊

#### 衣钵传承
- 师傅选定衣钵传人
- 传人获得师傅最核心的传承
- 继承师傅的社会关系

### 6.3 特殊互动

#### 为徒出头
- 弟子被欺负时，师傅出面保护
- 增加弟子对师傅的忠诚
- 可能引发门派冲突

#### 传承遗志
- 师傅临终前的最后传承
- 效率大幅提升
- 可能包含秘传内容

#### 同门较艺
- 同门弟子之间的切磋
- 提升战斗技巧
- 建立同门关系

---

## 宗门师徒体系

### 7.1 宗门职位

| 职位 | 要求 | 权限 | 弟子上限 |
|-----|------|------|---------|
| 宗主 | 元婴期+ | 全部权限 | 20 |
| 长老 | 元婴期 | 收徒、传授宗门功法 | 10 |
| 内门执事 | 金丹期 | 指导外门弟子 | 5 |
| 内门弟子 | 筑基期 | 学习宗门核心 | - |
| 外门弟子 | 练气期 | 学习基础功法 | - |
| 杂役弟子 | 无要求 | 无修炼资源 | - |

### 7.2 宗门功法传承

**传承规则**:
- 宗门功法由宗门统一管理
- 不同职位可学习不同等级的功法
- 传承需要贡献点
- 禁止外传（否则视为叛门）

**传承层级**:
```
入门功法（所有弟子可学）
├── 基础护体术
├── 基础养气术
└── 基础剑术

核心功法（内门可学）
├── 宗门主修功法
├── 宗门秘术
└── 宗门炼丹术

镇派功法（亲传可学）
├── 镇派绝学
├── 祖传心法
└── 失传秘法
```

### 7.3 宗门贡献系统

**获取方式**:
- 完成宗门任务
- 为宗门战斗
- 上交资源
- 招募新弟子
- 创造新功法/丹方

**使用方式**:
- 兑换功法传承
- 兑换丹药
- 兑换法宝
- 提升宗门地位
- 申请闭关资源

---

## AI行为设计

### 8.1 师傅行为

#### BehRecruitApprentice (收徒)
```csharp
/// <summary>
/// AI行为：寻找并收取弟子
/// </summary>
public class BehRecruitApprentice : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 检查是否可以收徒
        if (!CanRecruit(ae)) return BehResult.Stop;
        
        // 寻找合适的弟子候选
        var candidate = FindApprenticeCandidate(pObject);
        if (candidate == null) return BehResult.Stop;
        
        // 尝试收徒
        if (TryRecruit(ae, candidate.GetExtend()))
        {
            return BehResult.Continue;
        }
        
        return BehResult.Stop;
    }
}
```

#### BehTeachApprentice (传授)
```csharp
/// <summary>
/// AI行为：向弟子传授知识
/// </summary>
public class BehTeachApprentice : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 获取需要传授的弟子
        var apprentice = GetApprenticeNeedTeaching(ae);
        if (apprentice == null) return BehResult.Stop;
        
        // 选择传授内容
        var content = SelectTeachContent(ae, apprentice);
        if (content == null) return BehResult.Stop;
        
        // 执行传授
        ExecuteTeaching(ae, apprentice, content);
        
        return BehResult.Continue;
    }
}
```

#### BehProtectApprentice (保护弟子)
```csharp
/// <summary>
/// AI行为：保护弟子
/// </summary>
public class BehProtectApprentice : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 检查是否有弟子处于危险中
        var endangeredApprentice = GetEndangeredApprentice(ae);
        if (endangeredApprentice == null) return BehResult.Stop;
        
        // 前往保护
        var threat = endangeredApprentice.GetExtend().GetCurrentThreat();
        if (threat != null)
        {
            pObject.setAttackTarget(threat);
            return BehResult.Continue;
        }
        
        return BehResult.Stop;
    }
}
```

### 8.2 弟子行为

#### BehSeekMaster (寻师)
```csharp
/// <summary>
/// AI行为：寻找师傅
/// </summary>
public class BehSeekMaster : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 检查是否需要师傅
        if (ae.HasMaster()) return BehResult.Stop;
        
        // 寻找合适的师傅
        var potentialMaster = FindPotentialMaster(pObject);
        if (potentialMaster == null) return BehResult.Stop;
        
        // 前往拜师
        if (!pObject.isInAttackRange(potentialMaster))
        {
            pObject.goTo(potentialMaster);
            return BehResult.RepeatStep;
        }
        
        // 尝试拜师
        TryBeApprentice(ae, potentialMaster.GetExtend());
        
        return BehResult.Continue;
    }
}
```

#### BehFollowMaster (跟随师傅)
```csharp
/// <summary>
/// AI行为：跟随师傅修炼
/// </summary>
public class BehFollowMaster : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var master = ae.GetMaster();
        
        if (master == null || !master.Base.data.alive) 
            return BehResult.Stop;
        
        // 跟随师傅
        if (pObject.getDistanceTo(master.Base) > 3)
        {
            pObject.goTo(master.Base.current_tile);
            return BehResult.RepeatStep;
        }
        
        // 跟随修炼（效率提升）
        FollowCultivate(ae, master);
        
        return BehResult.Continue;
    }
}
```

#### BehCompleteTask (完成师傅任务)
```csharp
/// <summary>
/// AI行为：完成师傅布置的任务
/// </summary>
public class BehCompleteTask : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 获取当前任务
        var task = ae.GetCurrentMasterTask();
        if (task == null) return BehResult.Stop;
        
        // 执行任务
        var result = ExecuteTask(ae, task);
        
        if (result == TaskResult.Completed)
        {
            OnTaskCompleted(ae, task);
            return BehResult.Continue;
        }
        else if (result == TaskResult.InProgress)
        {
            return BehResult.RepeatStep;
        }
        
        return BehResult.Stop;
    }
}
```

### 8.3 任务与条件

```csharp
// 师傅相关任务
public static class MasterApprenticeTasks
{
    public static BehaviourTaskActor RecruitApprentice;   // 收徒
    public static BehaviourTaskActor TeachApprentice;     // 传授
    public static BehaviourTaskActor AssignTask;          // 布置任务
    public static BehaviourTaskActor ProtectApprentice;   // 保护弟子
    public static BehaviourTaskActor DesignateSuccessor;  // 指定传人
}

// 弟子相关任务
public static class ApprenticeTasks
{
    public static BehaviourTaskActor SeekMaster;          // 寻师
    public static BehaviourTaskActor FollowMaster;        // 跟随师傅
    public static BehaviourTaskActor RequestTeaching;     // 请教
    public static BehaviourTaskActor CompleteTask;        // 完成任务
    public static BehaviourTaskActor ServeTask;           // 服务师门
}

// 条件类
public class CondHasApprentice : BehConditionActor { }      // 有弟子
public class CondCanRecruit : BehConditionActor { }         // 可以收徒
public class CondHasMaster : BehConditionActor { }          // 有师傅
public class CondNeedMaster : BehConditionActor { }         // 需要师傅
public class CondHasTask : BehConditionActor { }            // 有任务
public class CondApprenticeInDanger : BehConditionActor { } // 弟子有危险
```

### 8.4 ActorJob 设计

```csharp
public class ActorJobs : ExtendLibrary<ActorJob, ActorJobs>
{
    // 现有任务...
    
    // ======== 新增任务 ========
    
    // 师傅工作
    public static ActorJob MasterDuty { get; private set; }
    
    // 弟子工作
    public static ActorJob ApprenticeDuty { get; private set; }
    
    protected override void OnInit()
    {
        // 现有初始化...
        
        // 师傅工作
        MasterDuty.addTask(ActorTasks.TeachApprentice.id);
        MasterDuty.addCondition(new CondHasApprentice());
        MasterDuty.addCondition(new CondApprenticeNeedTeaching());
        MasterDuty.addTask(ActorTasks.RecruitApprentice.id);
        MasterDuty.addCondition(new CondCanRecruit());
        MasterDuty.addCondition(new CondWillingToRecruit());
        MasterDuty.addTask(ActorTasks.ProtectApprentice.id);
        MasterDuty.addCondition(new CondApprenticeInDanger());
        MasterDuty.addTask(ActorTasks.EndJob.id);
        
        // 弟子工作
        ApprenticeDuty.addTask(ActorTasks.CompleteTask.id);
        ApprenticeDuty.addCondition(new CondHasTask());
        ApprenticeDuty.addTask(ActorTasks.FollowMaster.id);
        ApprenticeDuty.addCondition(new CondHasMaster());
        ApprenticeDuty.addCondition(new CondMasterCultivating());
        ApprenticeDuty.addTask(ActorTasks.RequestTeaching.id);
        ApprenticeDuty.addCondition(new CondHasMaster());
        ApprenticeDuty.addCondition(new CondNeedTeaching());
        ApprenticeDuty.addTask(ActorTasks.SeekMaster.id);
        ApprenticeDuty.addCondition(new CondNeedMaster());
        ApprenticeDuty.addCondition(new CondHasMaster(), false);
        ApprenticeDuty.addTask(ActorTasks.EndJob.id);
    }
}
```

---

## UI界面设计

### 9.1 师徒信息页面 (MasterApprenticePage)

显示角色的师徒关系信息：

```
┌─────────────────────────────────────┐
│ 【师徒关系】                          │
├─────────────────────────────────────┤
│ ★ 师傅信息                           │
│   师傅: 张三丰 (武当派宗主)             │
│   关系: 亲传弟子                       │
│   亲密度: ████████░░ 85%            │
│   拜师时间: 100年前                   │
│                                      │
│   已获传承:                           │
│     ✓ 太极拳法 (掌握65%)              │
│     ✓ 纯阳无极功 (掌握42%)            │
│     ✓ 太极剑 (已习得)                 │
├─────────────────────────────────────┤
│ ○ 弟子列表 (3/5)                     │
│   1. 宋青书 - 入室弟子 (亲密度: 60%)   │
│   2. 宋远桥 - 亲传弟子 (亲密度: 78%)   │
│   3. [衣钵] 张无忌 - 衣钵传人 (95%)    │
├─────────────────────────────────────┤
│ ○ 同门                              │
│   - 俞岱岩 (师兄)                     │
│   - 殷梨亭 (师弟)                     │
└─────────────────────────────────────┘
```

### 9.2 拜师界面 (ApprenticeWindow)

```
┌─────────────────────────────────────┐
│           【拜师仪式】                 │
├─────────────────────────────────────┤
│                                      │
│   师傅: 张三丰                        │
│   境界: 元婴后期                       │
│   宗门: 武当派                        │
│                                      │
│   可传授功法:                         │
│     • 太极拳法 (天级)                  │
│     • 纯阳无极功 (地级)                │
│                                      │
│   可传授技能:                         │
│     • 太极剑                          │
│     • 绵掌                           │
│                                      │
│   拜师成功率: 85%                     │
│   (灵根契合: 良好)                    │
│                                      │
├─────────────────────────────────────┤
│     [确认拜师]      [取消]            │
└─────────────────────────────────────┘
```

### 9.3 传授界面 (TeachingWindow)

```
┌─────────────────────────────────────┐
│           【传授功法】                 │
├─────────────────────────────────────┤
│ 师傅: 张三丰                          │
│ 弟子: 张无忌                          │
│                                      │
│ 选择传授内容:                         │
│                                      │
│ ○ 功法传授                           │
│   □ 太极拳法      预计获得: 35%       │
│   ☑ 纯阳无极功    预计获得: 28%       │
│                                      │
│ ○ 技能传授                           │
│   □ 太极剑        传授效率: 高        │
│   □ 绵掌          传授效率: 中        │
│                                      │
│ 传授耗时: 约3天                       │
│ 传承效率: 112% (衣钵传人加成)          │
│                                      │
├─────────────────────────────────────┤
│     [开始传授]      [取消]            │
└─────────────────────────────────────┘
```

### 9.4 宗门师徒谱 (SectLineageWindow)

```
┌─────────────────────────────────────────────────────┐
│              武当派师徒传承谱系                         │
├─────────────────────────────────────────────────────┤
│                                                      │
│                    张三丰 (祖师)                       │
│                        │                             │
│         ┌──────┬──────┼──────┬──────┐               │
│         │      │      │      │      │               │
│       宋远桥  俞莲舟  俞岱岩  张松溪  殷梨亭             │
│         │             │                              │
│    ┌────┴────┐    ┌──┴──┐                          │
│    │         │    │     │                           │
│   宋青书    张无忌 ...   ...                          │
│   (入室)   (衣钵)                                    │
│                                                      │
├─────────────────────────────────────────────────────┤
│ 宗门人数: 127人                                       │
│ 师徒对数: 45对                                        │
│ 平均辈分: 3.2代                                       │
└─────────────────────────────────────────────────────┘
```

---

## 技术实现

### 10.1 新增文件列表

```
Source/Content/
├── Components/
│   ├── MasterApprenticeRelation.cs    # 师徒关系组件
│   ├── MasterApprenticeState.cs       # 师傅状态组件
│   └── ApprenticeTask.cs              # 弟子任务组件
├── Behaviours/
│   ├── Masters/
│   │   ├── BehRecruitApprentice.cs    # 收徒行为
│   │   ├── BehTeachApprentice.cs      # 传授行为
│   │   ├── BehAssignTask.cs           # 布置任务
│   │   ├── BehProtectApprentice.cs    # 保护弟子
│   │   └── BehDesignateSuccessor.cs   # 指定传人
│   ├── Apprentices/
│   │   ├── BehSeekMaster.cs           # 寻师行为
│   │   ├── BehFollowMaster.cs         # 跟随师傅
│   │   ├── BehRequestTeaching.cs      # 请教行为
│   │   └── BehCompleteTask.cs         # 完成任务
│   └── Conditions/
│       ├── CondHasApprentice.cs       # 有弟子
│       ├── CondCanRecruit.cs          # 可以收徒
│       ├── CondHasMaster.cs           # 有师傅
│       ├── CondNeedMaster.cs          # 需要师傅
│       ├── CondHasTask.cs             # 有任务
│       └── CondApprenticeInDanger.cs  # 弟子有危险
├── Extensions/
│   └── MasterApprenticeTools.cs       # 师徒系统扩展方法
├── Libraries/
│   ├── MasterTaskAsset.cs             # 师傅任务资源
│   └── MasterTaskLibrary.cs           # 师傅任务库
├── Systems/
│   └── Logic/
│       ├── MasterApprenticeSystem.cs  # 师徒关系系统
│       └── ApprenticeGrowthSystem.cs  # 弟子成长系统
├── MasterApprenticeTasks.cs           # 师徒任务定义
├── MasterApprenticeJobs.cs            # 师徒工作定义
└── MasterApprenticeEvents.cs          # 师徒事件定义

Source/Content/UI/
└── CreatureInfoPages/
    └── MasterApprenticePage.cs        # 师徒信息页面

Source/UI/
├── ApprenticeWindow.cs                # 拜师界面
├── TeachingWindow.cs                  # 传授界面
└── SectLineageWindow.cs               # 宗门师徒谱

Content/
└── MasterTasks/
    └── MasterTasks.json               # 师傅任务配置

Locales/
└── master_apprentice.csv              # 师徒系统本地化
```

### 10.2 ActorExtend 扩展

```csharp
// 扩展 Source/Content/Extensions/MasterApprenticeTools.cs

public static class MasterApprenticeTools
{
    // ======== 师傅相关方法 ========
    
    /// <summary>
    /// 检查是否可以收徒
    /// </summary>
    public static bool CanRecruit(this ActorExtend ae)
    {
        // 检查境界
        if (!ae.HasCultisys<Xian>()) return false;
        ref var xian = ref ae.GetCultisys<Xian>();
        if (xian.level < XianLevels.JiDan) return false;  // 至少筑基
        
        // 检查功法掌握
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        if (ae.GetMainCultibookMastery() < 40) return false;
        
        // 检查弟子数量
        if (!ae.TryGetComponent(out MasterApprenticeState state)) return true;
        return state.ApprenticeCount < state.MaxApprenticeCount;
    }
    
    /// <summary>
    /// 获取最大弟子数量
    /// </summary>
    public static int GetMaxApprenticeCount(this ActorExtend ae)
    {
        if (!ae.HasCultisys<Xian>()) return 0;
        ref var xian = ref ae.GetCultisys<Xian>();
        
        return xian.level switch
        {
            >= XianLevels.HuaShen => 999,
            >= XianLevels.YuanYing => 10,
            >= XianLevels.JinDan => 5,
            >= XianLevels.ZhuJi => 2,
            _ => 0
        };
    }
    
    /// <summary>
    /// 收取弟子
    /// </summary>
    public static bool TryRecruit(this ActorExtend master, ActorExtend apprentice, 
        MasterApprenticeType type = MasterApprenticeType.Nominal)
    {
        if (!master.CanRecruit()) return false;
        if (apprentice.HasMaster()) return false;
        
        // 计算成功率
        float successRate = CalculateRecruitSuccessRate(master, apprentice);
        if (!Randy.randomChance(successRate)) return false;
        
        // 建立关系
        apprentice.E.AddRelation(new MasterApprenticeRelation
        {
            Master = master.E,
            RelationType = type,
            Intimacy = 0,
            ApprenticeTime = World.world.getCurWorldTime(),
            TransferredCultibookCount = 0,
            TransferredSkillCount = 0,
            IsSuccessor = false
        });
        
        // 更新师傅状态
        ref var state = ref master.GetOrAddComponent<MasterApprenticeState>();
        state.ApprenticeCount++;
        if (state.MaxApprenticeCount == 0)
        {
            state.MaxApprenticeCount = master.GetMaxApprenticeCount();
        }
        
        // 触发事件
        MasterApprenticeEvents.OnRecruit(master, apprentice);
        
        return true;
    }
    
    /// <summary>
    /// 传授功法
    /// </summary>
    public static bool TeachCultibook(this ActorExtend master, ActorExtend apprentice, 
        CultibookAsset cultibook)
    {
        // 检查师傅是否掌握该功法
        var masterMastery = master.GetMaster(cultibook);
        if (masterMastery <= 0) return false;
        
        // 计算传承效率
        float efficiency = CalculateTeachEfficiency(master, apprentice);
        
        // 计算弟子获得的掌握度
        float gainedMastery = Mathf.Min(masterMastery * efficiency, 80f);
        
        // 更新弟子功法状态
        var currentMastery = apprentice.GetMaster(cultibook);
        if (currentMastery <= 0)
        {
            // 新学功法
            if (apprentice.GetMainCultibook() == null)
            {
                apprentice.SetMainCultibook(cultibook);
                apprentice.AddMainCultibookMastery(gainedMastery);
            }
            apprentice.Master(cultibook, gainedMastery);
        }
        else
        {
            // 已有功法，增加掌握度
            apprentice.Master(cultibook, Mathf.Max(currentMastery, gainedMastery));
        }
        
        // 更新师徒关系
        UpdateRelationAfterTeaching(master, apprentice);
        
        return true;
    }
    
    // ======== 弟子相关方法 ========
    
    /// <summary>
    /// 检查是否有师傅
    /// </summary>
    public static bool HasMaster(this ActorExtend ae)
    {
        return ae.E.GetRelations<MasterApprenticeRelation>().Any();
    }
    
    /// <summary>
    /// 获取师傅
    /// </summary>
    public static ActorExtend GetMaster(this ActorExtend ae)
    {
        var relations = ae.E.GetRelations<MasterApprenticeRelation>();
        if (!relations.Any()) return null;
        
        var masterEntity = relations.First().Master;
        if (masterEntity.IsNull) return null;
        
        return masterEntity.GetComponent<ActorBinder>()._ae;
    }
    
    /// <summary>
    /// 获取师徒关系
    /// </summary>
    public static ref MasterApprenticeRelation GetMasterRelation(this ActorExtend ae)
    {
        return ref ae.E.GetRelations<MasterApprenticeRelation>().First();
    }
    
    /// <summary>
    /// 增加亲密度
    /// </summary>
    public static void AddIntimacy(this ActorExtend ae, float amount)
    {
        if (!ae.HasMaster()) return;
        ref var relation = ref ae.GetMasterRelation();
        relation.Intimacy = Mathf.Clamp(relation.Intimacy + amount, 0, 100);
        
        // 检查是否升级关系类型
        UpdateRelationType(ref relation);
    }
    
    /// <summary>
    /// 尝试出师
    /// </summary>
    public static bool TryGraduate(this ActorExtend ae)
    {
        if (!ae.HasMaster()) return false;
        
        var master = ae.GetMaster();
        ref var relation = ref ae.GetMasterRelation();
        
        // 检查出师条件
        // 1. 境界达到师傅境界
        // 2. 或功法掌握达到100%
        bool canGraduate = false;
        
        if (ae.HasCultisys<Xian>() && master.HasCultisys<Xian>())
        {
            ref var apprenticeXian = ref ae.GetCultisys<Xian>();
            ref var masterXian = ref master.GetCultisys<Xian>();
            canGraduate = apprenticeXian.level >= masterXian.level;
        }
        
        if (!canGraduate)
        {
            canGraduate = ae.GetMainCultibookMastery() >= 100;
        }
        
        if (!canGraduate) return false;
        
        // 执行出师
        MasterApprenticeEvents.OnGraduate(master, ae);
        
        // 移除师徒关系，转为同门关系
        ae.E.RemoveRelation<MasterApprenticeRelation>(master.E);
        
        // 更新师傅状态
        ref var state = ref master.GetComponent<MasterApprenticeState>();
        state.ApprenticeCount--;
        
        return true;
    }
    
    // ======== 辅助方法 ========
    
    private static float CalculateRecruitSuccessRate(ActorExtend master, ActorExtend apprentice)
    {
        float baseRate = 0.7f;
        
        // 灵根契合度
        var mainCultibook = master.GetMainCultibook();
        float affinityBonus = 0;
        if (mainCultibook != null && apprentice.HasElementRoot())
        {
            affinityBonus = mainCultibook.ElementReq.GetAffinity(apprentice.GetElementRoot()) * 0.2f;
        }
        
        // 关系系数
        float relationBonus = 0; // TODO: 获取双方关系值
        
        // 智力系数
        float intelligenceBonus = apprentice.GetStat(S.intelligence) / 50f * 0.1f;
        
        return Mathf.Clamp01(baseRate + affinityBonus + relationBonus + intelligenceBonus);
    }
    
    private static float CalculateTeachEfficiency(ActorExtend master, ActorExtend apprentice)
    {
        float baseEfficiency = 0.3f;
        
        // 关系系数
        ref var relation = ref apprentice.GetMasterRelation();
        float relationMultiplier = relation.RelationType switch
        {
            MasterApprenticeType.Nominal => 0.5f,
            MasterApprenticeType.Formal => 0.8f,
            MasterApprenticeType.Direct => 1.0f,
            MasterApprenticeType.Successor => 1.2f,
            _ => 0.5f
        };
        
        // 智力系数
        float intelligenceMultiplier = 
            (master.GetStat(S.intelligence) + apprentice.GetStat(S.intelligence)) / 100f;
        
        return baseEfficiency * relationMultiplier * intelligenceMultiplier;
    }
    
    private static void UpdateRelationType(ref MasterApprenticeRelation relation)
    {
        relation.RelationType = relation.Intimacy switch
        {
            >= 90 when relation.IsSuccessor => MasterApprenticeType.Successor,
            >= 60 => MasterApprenticeType.Direct,
            >= 30 => MasterApprenticeType.Formal,
            _ => MasterApprenticeType.Nominal
        };
    }
    
    private static void UpdateRelationAfterTeaching(ActorExtend master, ActorExtend apprentice)
    {
        ref var relation = ref apprentice.GetMasterRelation();
        relation.TransferredCultibookCount++;
        relation.Intimacy = Mathf.Min(relation.Intimacy + 2, 100);
        UpdateRelationType(ref relation);
    }
}
```

### 10.3 事件系统

```csharp
// Source/Content/MasterApprenticeEvents.cs

public static class MasterApprenticeEvents
{
    /// <summary>
    /// 收徒事件
    /// </summary>
    public static void OnRecruit(ActorExtend master, ActorExtend apprentice)
    {
        // 产生世界日志
        WorldLogs.LogMasterRecruit(master.Base, apprentice.Base);
        
        // 触发宗门事件（如果在宗门内）
        if (master.sect != null)
        {
            master.sect.OnMemberRecruit(master.Base, apprentice.Base);
        }
    }
    
    /// <summary>
    /// 传授事件
    /// </summary>
    public static void OnTeach(ActorExtend master, ActorExtend apprentice, string contentType)
    {
        WorldLogs.LogMasterTeach(master.Base, apprentice.Base, contentType);
    }
    
    /// <summary>
    /// 出师事件
    /// </summary>
    public static void OnGraduate(ActorExtend master, ActorExtend apprentice)
    {
        WorldLogs.LogApprenticeGraduate(master.Base, apprentice.Base);
    }
    
    /// <summary>
    /// 叛师事件
    /// </summary>
    public static void OnBetray(ActorExtend master, ActorExtend apprentice)
    {
        WorldLogs.LogApprenticeBetray(master.Base, apprentice.Base);
        
        // 成为师门敌人
        // TODO: 实现敌对关系
    }
    
    /// <summary>
    /// 衣钵传承事件
    /// </summary>
    public static void OnSuccession(ActorExtend master, ActorExtend successor)
    {
        WorldLogs.LogSuccession(master.Base, successor.Base);
        
        // 传承所有核心内容
        TransferAllCoreContent(master, successor);
    }
}
```

---

## 开发TODO

### 阶段一：核心框架 (预计1周) ✅ **已完成**

- [x] **T1.1** 创建 `MasterApprenticeRelation` 组件 ✅
  - [x] 定义关系类型枚举 (`MasterApprenticeType`)
  - [x] 实现 `ILinkRelation` 接口
  - [x] 添加亲密度、传承记录等字段
  - **位置**: `Source/Core/Components/MasterApprenticeRelation.cs`

- [x] **T1.2** 创建 `MasterApprenticeState` 组件 ✅
  - [x] 弟子数量管理
  - [x] 最大弟子数量计算
  - [x] 师门风格定义 (`MasterStyle`)
  - **位置**: `Source/Core/Components/MasterApprenticeState.cs`

- [x] **T1.3** 扩展 `ActorExtend` - 基础方法 ✅
  - [x] `HasMaster()` - 检查是否有师傅
  - [x] `GetMaster()` - 获取师傅
  - [x] `GetApprentices()` - 获取弟子列表
  - [x] `CanRecruit()` - 检查是否可收徒
  - [x] `GetMaxApprenticeCount()` - 获取最大弟子数
  - **位置**: `Source/Content/Extensions/MasterApprenticeTools.cs`

- [x] **T1.4** 扩展 `ActorExtend` - 核心方法 ✅
  - [x] `TryRecruit()` - 收徒方法
  - [x] `AddIntimacy()` - 增加亲密度
  - [x] `UpdateRelationType()` - 更新关系类型（内部方法）
  - [x] `GetIntimacy()` - 获取亲密度
  - [x] `GetRelationType()` - 获取关系类型
  - [x] `TryGraduate()` - 出师方法
  - **位置**: `Source/Content/Extensions/MasterApprenticeTools.cs`

### 阶段二：传承系统 (预计1.5周) ⚠️ **部分完成**

- [x] **T2.1** 功法传承实现 ✅
  - [x] `TeachCultibook()` - 传授功法
  - [x] 传承效率计算 (`CalculateTeachEfficiency`)
  - [x] 传承限制检查
  - **位置**: `Source/Content/Extensions/MasterApprenticeTools.cs`

- [ ] **T2.2** 技能传承实现
  - [ ] `TeachSkill()` - 传授技能
  - [ ] 口授心传方式
  - [ ] 实战教学方式

- [ ] **T2.3** 丹方传承实现
  - [ ] `TeachElixir()` - 传授丹方
  - [ ] 传承条件检查

- [ ] **T2.4** 修炼经验传承
  - [ ] 传授修炼心得
  - [ ] 传授战斗技巧
  - [ ] 效果实现

### 阶段三：AI行为系统 (预计1.5周) ✅ **已完成**

- [x] **T3.1** 师傅行为 ✅
  - [x] `BehRecruitApprentice` - 收徒行为
  - [x] `BehTeachApprentice` - 传授行为
  - [ ] `BehAssignTask` - 布置任务（未实现）
  - [ ] `BehProtectApprentice` - 保护弟子（未实现）
  - [ ] `BehDesignateSuccessor` - 指定传人（未实现）
  - **位置**: `Source/Content/Behaviours/Masters/`

- [x] **T3.2** 弟子行为 ✅
  - [x] `BehSeekMaster` - 寻师行为
  - [x] `BehFollowMaster` - 跟随师傅
  - [ ] `BehRequestTeaching` - 请教行为（未实现）
  - [ ] `BehCompleteTask` - 完成任务（未实现）
  - **位置**: `Source/Content/Behaviours/Apprentices/`

- [x] **T3.3** 条件类实现 ✅
  - [x] `CondHasApprentice`
  - [x] `CondCanRecruit`
  - [x] `CondHasMaster`
  - [x] `CondNeedMaster`
  - [x] `CondApprenticeNeedTeaching`
  - [x] `CondMasterCultivating`
  - [ ] `CondHasTask`（未实现）
  - [ ] `CondApprenticeInDanger`（未实现）
  - **位置**: `Source/Content/Behaviours/Conditions/`

- [x] **T3.4** ActorJob 配置 ✅
  - [x] `MasterDuty` - 师傅工作
  - [x] `ApprenticeDuty` - 弟子工作
  - [x] 集成到现有工作系统
  - [x] 在 `PatchActor.cs` 中添加工作分配逻辑（仅元婴期收徒）
  - **位置**: `Source/Content/ActorJobs.cs`, `Source/Content/Patch/PatchActor.cs`

### 阶段四：师徒互动系统 (预计1周)

- [ ] **T4.1** 日常互动实现
  - [ ] 跟随修炼逻辑
  - [ ] 请教问题逻辑
  - [ ] 布置任务逻辑

- [ ] **T4.2** 重要事件实现
  - [ ] 传授仪式
  - [ ] 入室礼
  - [ ] 出师礼
  - [ ] 衣钵传承

- [ ] **T4.3** 事件系统
  - [ ] `MasterApprenticeEvents` 实现
  - [ ] 世界日志集成
  - [ ] 宗门事件触发

### 阶段五：宗门集成 (预计1周)

- [ ] **T5.1** 扩展 `SectData`
  - [ ] 添加师徒关系图
  - [ ] 添加宗门职位系统
  - [ ] 添加宗门功法列表

- [ ] **T5.2** 扩展 `Sect` 类
  - [ ] 宗门师徒管理方法
  - [ ] 宗门传承规则
  - [ ] 贡献点系统

- [ ] **T5.3** 宗门特有行为
  - [ ] 宗门收徒
  - [ ] 宗门传授
  - [ ] 宗门任务分配

### 阶段六：UI实现 (预计1周)

- [ ] **T6.1** `MasterApprenticePage` 实现
  - [ ] 师傅信息显示
  - [ ] 弟子列表显示
  - [ ] 同门显示

- [ ] **T6.2** `ApprenticeWindow` 实现
  - [ ] 拜师界面
  - [ ] 拜师确认逻辑

- [ ] **T6.3** `TeachingWindow` 实现
  - [ ] 传授内容选择
  - [ ] 传授预览

- [ ] **T6.4** `SectLineageWindow` 实现
  - [ ] 师徒谱系图
  - [ ] 关系可视化

### 阶段七：本地化与配置 (预计0.5周) ⚠️ **部分完成**

- [x] **T7.1** 本地化 ✅
  - [x] 任务本地化（在 `tasks.csv` 中添加师徒任务）
    - [x] `Task.Unit.Cultiway.RecruitApprentice` - 收徒
    - [x] `Task.Unit.Cultiway.TeachApprentice` - 传授弟子
    - [x] `Task.Unit.Cultiway.SeekMaster` - 寻师
    - [x] `Task.Unit.Cultiway.FollowMaster` - 跟随师傅
  - [ ] 创建 `master_apprentice.csv`（未实现）
  - [ ] 所有UI文本本地化（部分完成，UI组件存在但功能待完善）
  - [ ] 事件日志本地化（未实现）
  - **位置**: `Locales/tasks.csv`

- [ ] **T7.2** 配置文件
  - [ ] 师傅任务配置（未实现）
  - [ ] 关系参数配置（当前硬编码在代码中）
  - [ ] 传承效率配置（当前硬编码在代码中）

### 阶段八：测试与平衡 (预计0.5周)

- [ ] **T8.1** 功能测试
  - [ ] 拜师流程测试
  - [ ] 传承流程测试
  - [ ] 出师流程测试
  - [ ] AI行为测试

- [ ] **T8.2** 平衡性调整
  - [ ] 收徒条件调整
  - [ ] 传承效率调整
  - [ ] 亲密度增长调整

---

## 时间估算

| 阶段 | 任务 | 预计时间 |
|-----|------|---------|
| 阶段一 | 核心框架 | 1周 |
| 阶段二 | 传承系统 | 1.5周 |
| 阶段三 | AI行为系统 | 1.5周 |
| 阶段四 | 师徒互动系统 | 1周 |
| 阶段五 | 宗门集成 | 1周 |
| 阶段六 | UI实现 | 1周 |
| 阶段七 | 本地化与配置 | 0.5周 |
| 阶段八 | 测试与平衡 | 0.5周 |
| **总计** | | **8周** |

---

## 附录

### A. 师徒关系等级对照表

| 亲密度 | 关系类型 | 传承效率 | 可学内容 |
|-------|---------|---------|---------|
| 0-30 | 记名弟子 | 50% | 基础功法 |
| 30-60 | 入室弟子 | 80% | 核心功法 |
| 60-90 | 亲传弟子 | 100% | 全部功法 |
| 90-100 | 衣钵传人 | 120% | 秘传绝学 |

### B. 境界与弟子数量限制

| 境界 | 最大弟子数 | 说明 |
|-----|-----------|------|
| 练气 | 0 | 不能收徒 |
| 筑基 | 2 | 初步可收徒 |
| 金丹 | 5 | 正常收徒 |
| 元婴 | 10 | 可组建师门 |
| 化神+ | 无限 | 创立宗派 |

### C. 传承效率计算公式

```
传承效率 = 基础效率 × 关系系数 × 师傅掌握度 × 智力系数

基础效率 = 0.3
关系系数 = 
  - 记名弟子: 0.5
  - 入室弟子: 0.8
  - 亲传弟子: 1.0
  - 衣钵传人: 1.2
师傅掌握度 = 师傅对该内容的掌握程度 / 100
智力系数 = (师傅智力 + 弟子智力) / 100

弟子获得掌握度 = min(师傅掌握度 × 传承效率, 80%)
```

### D. 亲密度变化规则

**增加**:
| 行为 | 亲密度变化 |
|-----|-----------|
| 日常跟随修炼 | +0.1/天 |
| 完成简单任务 | +1 |
| 完成困难任务 | +3 |
| 完成特殊任务 | +5 |
| 师傅传授 | +2 |
| 共同战斗获胜 | +3 |
| 为师门贡献 | +1~10 |

**减少**:
| 行为 | 亲密度变化 |
|-----|-----------|
| 长时间不联系 | -0.05/天 |
| 违背师傅意愿 | -5 |
| 任务失败 | -1 |
| 背叛师门 | -50 |
| 转投他门 | -100 |

### E. 预设师傅任务

| 任务类型 | 描述 | 奖励亲密度 |
|---------|------|-----------|
| 采集灵草 | 收集指定数量的灵草 | +1~2 |
| 猎杀妖兽 | 击杀指定妖兽 | +2~3 |
| 送信任务 | 传递消息给指定人 | +1 |
| 修炼任务 | 达到指定修为 | +3~5 |
| 炼丹任务 | 炼制指定丹药 | +2~3 |
| 守护任务 | 守护指定地点 | +2 |
| 比武任务 | 与同门切磋 | +1~2 |

---

## 与现有系统的关联

### 与功法系统的关联

- 师傅可以传授主修功法给弟子
- 传授效率受师傅功法掌握程度影响
- 弟子可以从师傅处学习功法相关的法术

### 与宗门系统的关联

- 宗门内可以建立正式的师徒体系
- 宗门长老/掌门有收徒特权
- 宗门功法通过师徒关系传承

### 与境界系统的关联

- 师傅境界决定可收弟子数量
- 境界差距影响传承效率
- 弟子境界达到师傅境界可申请出师

### 与AI行为系统的关联

- 新增师傅/弟子专属工作
- 与现有修炼行为协同
- 师徒关系影响AI决策

---

## 实现进度总结

### ✅ 已完成

- **阶段一：核心框架** - 100% 完成
  - 所有核心组件已创建（`MasterApprenticeRelation`, `MasterApprenticeState`）
  - 所有扩展方法已实现（`MasterApprenticeTools.cs`）
  - 数据结构完整，支持师徒关系管理
  - **组件位置**: `Source/Core/Components/`

- **阶段三：AI行为系统** - 约70% 完成
  - 核心行为已实现（收徒、传授、寻师、跟随）
  - 主要条件类已实现
  - ActorJob配置已完成
  - 工作分配逻辑已集成（`PatchActor.cs`，与副职业pool统一）
  - 缺少：任务系统、保护弟子、指定传人等高级行为
  - **位置**: `Source/Content/Behaviours/Masters/`, `Source/Content/Behaviours/Apprentices/`

- **阶段二：传承系统** - 约25% 完成
  - 功法传承已实现（`TeachCultibook()`）
  - 传承效率计算已实现
  - 缺少：技能传承、丹方传承、修炼经验传承

- **阶段七：本地化** - 约30% 完成
  - 任务本地化已完成（`Locales/tasks.csv`）
  - UI组件存在但功能待完善（`UnitMasterApprenticeElement.cs`）
  - 缺少：专门的本地化文件、事件日志本地化

### ⚠️ 待完成

- **阶段二**：技能/丹方/修炼经验传承
- **阶段三**：高级行为（任务、保护、传人）
- **阶段四**：师徒互动系统（日常互动、重要事件）
- **阶段五**：宗门集成
- **阶段六**：UI完整实现
- **阶段七**：完整本地化和配置
- **阶段八**：测试与平衡

### 📝 实现说明

**组件位置变更**：
- `MasterApprenticeRelation` 和 `MasterApprenticeState` 位于 `Source/Core/Components/`（而非 `Source/Content/Components/`）
- 这样设计是因为这些是核心系统组件，不依赖Content层

**工作分配机制**：
- 在 `PatchActor.cs` 中实现，与副职业系统统一（放入pool随机选择）
- 只有元婴期及以上才会分配师傅工作（主动收徒）
- 师傅工作概率：有弟子80%，可收徒50%
- 弟子工作概率：30%

**当前限制**：
- 传承系统仅支持功法传承
- 缺少任务系统和更多互动方式
- UI显示功能待完善
- 事件系统未实现

---

**文档维护**: 请在每次师徒系统更新后更新此文档  
**最后更新**: 2025年11月  
**下次审查**: 完成阶段二和阶段四后

