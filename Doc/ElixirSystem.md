# 丹药系统（Elixir System）设计文档

## 目录
1. [系统概述](#系统概述)
2. [当前开发进展](#当前开发进展)
3. [核心架构](#核心架构)
4. [丹药生成机制](#丹药生成机制)
5. [丹药效果类型](#丹药效果类型)
6. [炼丹流程](#炼丹流程)
7. [框架能力分析](#框架能力分析)
8. [拓展设计方案](#拓展设计方案)
9. [TODO列表](#todo列表)

---

## 系统概述

丹药系统是修仙Mod的重要玩法模块，允许修仙者通过收集材料、研究配方、炼制丹药，从而获得各种增益效果。系统支持预定义丹药和AIGC动态生成丹药两种模式。

### 核心特点
- **动态生成**：基于LLM的AIGC系统，根据材料名称自动生成丹药名称和效果
- **材料系统**：支持灵根、金丹、具名材料等多种材料类型
- **效果框架**：支持状态增益、数据改变、恢复、数据增益四种效果类型
- **掌握系统**：角色可以掌握多个丹方，有掌握程度概念
- **传承机制**：可通过丹方书传承丹药配方

---

## 当前开发进展

### 已完成功能

#### 1. 核心组件 ✅
| 组件 | 文件位置 | 功能描述 |
|------|----------|----------|
| `Elixir` | `Components/Elixir.cs` | 丹药实体组件，存储丹药ID和强度值（品阶由实体上的`ItemLevel`组件提供） |
| `Elixirbook` | `Components/Elixirbook.cs` | 丹方书组件，引用ElixirAsset |
| `CraftingElixir` | `Components/CraftingElixir.cs` | 炼丹状态组件，存储进度 |
| `TagElixir*` | `Components/TagElixir*.cs` | 四种效果类型标签 |

#### 2. 资产定义 ✅
| 类 | 文件位置 | 功能描述 |
|------|----------|----------|
| `ElixirAsset` | `Libraries/ElixirAsset.cs` | 丹药资产定义，包含配方、效果等 |
| `ElixirLibrary` | `Libraries/ElixirLibrary.cs` | 丹药库，支持动态添加 |
| `ElixirIngredientCheck` | `Libraries/ElixirAsset.cs` | 材料检查结构 |

#### 3. AIGC生成 ✅
| 类 | 文件位置 | 功能描述 |
|------|----------|----------|
| `ElixirNameGenerator` | `AIGC/ElixirNameGenerator.cs` | 根据药效和材料生成丹药名称 |
| `ElixirEffectJsonGenerator` | `AIGC/ElixirEffectJsonGenerator.cs` | 根据材料生成效果JSON |
| `ElixirEffectGenerator` | `Libraries/ElixirEffectGenerator.cs` | 解析效果JSON并设置action |

#### 4. 行为系统 ✅
| 行为 | 文件位置 | 功能描述 |
|------|----------|----------|
| `BehFindNewElixir` | `Behaviours/BehFindNewElixir.cs` | 研究新丹方 |
| `BehFindElixirToCraft` | `Behaviours/BehFindElixirToCraft.cs` | 寻找可炼制的丹方 |
| `BehCraftElixir` | `Behaviours/BehCraftElixir.cs` | 执行炼丹过程 |
| `BehWriteElixirRecipe` | `Behaviours/BehWriteElixirRecipe.cs` | 誊抄丹方成书 |

#### 5. 预定义丹药 ✅
| 丹药 | 类型 | 功能 |
|------|------|------|
| 开灵丹 (OpenElementRootElixir) | DataChange | 诱导无灵根者产生灵根 |
| 补气丹 (WakanRestoreElixir) | Restore | 恢复灵气 |
| 悟道丹 (EnlightenElixir) | StatusGain | 临时提升悟性 |

#### 6. UI展示 ✅
- `ElixirPage`：在角色信息页面展示掌握的丹方

### 部分完成功能

#### 1. DataGain效果类型 ⚠️
- 框架已搭建，但`GenerateDataGainElixirActions`返回false，未实现实际逻辑

#### 2. 丹药自动使用 ⚠️
- StatusGain类型：当需要对应属性时自动使用
- 其他类型：updateAge时有20%几率自动使用
- 缺少更智能的使用决策

---

## 核心架构

### 类图关系

```
ElixirAsset (丹药定义)
├── id: string (唯一标识)
├── name_key: string (名称本地化key)
├── description_key: string (描述本地化key)
├── effect_type: ElixirEffectType (效果类型)
├── ingredients: ElixirIngredientCheck[] (配方材料)
├── seed_for_random_effect: int (动态丹药的随机种子)
├── craft_action: ElixirCraftDelegate (炼制时回调)
├── effect_action: ElixirEffectDelegate (使用效果)
└── consumable_check_action: ElixirCheckDelegate (使用条件检查)

ElixirIngredientCheck (材料检查)
├── ingredient_name: string (材料名称)
├── need_element_root: bool (需要灵根)
├── element_root_id: string (指定灵根类型)
├── need_jindan: bool (需要金丹)
├── jindan_id: string (指定金丹类型)
└── count: int (数量)

Elixir (丹药实体组件)
├── elixir_id: string (引用ElixirAsset)
└── value: float (丹药强度/效力)

CraftingElixir (炼丹状态组件)
├── elixir_id: string (正在炼制的丹药)
└── progress: int (炼制进度)
```

> 丹药品阶通过在丹药实体上挂载`ItemLevel`组件体现，与`Elixir`组件解耦。

### 数据流向

```
材料(TagIngredient) 
    ↓
BehFindNewElixir / BehFindElixirToCraft
    ↓
ElixirLibrary.NewElixir() [动态] / ElixirAsset.QueryInventoryForIngredients() [静态]
    ↓
ElixirEffectJsonGenerator.GenerateName() [AIGC生成效果]
    ↓
ElixirNameGenerator.GenerateName() [AIGC生成名称]
    ↓
ElixirEffectGenerator.GenerateElixirActions() [设置效果action]
    ↓
CraftingElixir实体 + CraftOccupyingRelation
    ↓
BehCraftElixir (逐步处理)
    ↓
ElixirAsset.Craft() → Elixir实体
    ↓
城市仓库 / 角色背包
    ↓
TryConsumeElixir() → 效果执行
```

---

## 丹药生成机制

### 静态丹药（预定义）

在`Elixirs.cs`中定义，使用`ExtendLibrary`框架：

```csharp
public class Elixirs : ExtendLibrary<ElixirAsset, Elixirs>
{
    public static ElixirAsset OpenElementRootElixir { get; private set; }
    
    protected override void OnInit()
    {
        OpenElementRootElixir.name_key = "Cultiway.Elixir.OpenElementRootElixir";
        OpenElementRootElixir.SetupDataChange((ae, elixir_entity, ref Elixir _) =>
        {
            ae.AddComponent(elixir_entity.GetComponent<ElementRoot>());
        });
        OpenElementRootElixir.ingredients = new ElixirIngredientCheck[] { ... };
    }
}
```

### 动态丹药（AIGC生成）

1. **触发生成**：`ElixirLibrary.NewElixir(Entity[] ingredients, ActorExtend creator)`
2. **效果生成**：调用LLM根据材料名生成效果JSON
3. **名称生成**：调用LLM根据效果描述和材料名生成丹药名
4. **Action设置**：解析JSON设置effect_action

**AIGC Prompt示例**：

效果生成SystemPrompt:
```
用户将会提供一组材料名字，你需要仔细分析它们的效果，并生成其制成的丹药药效。
要求只输出json格式的结果...
示例: {"effect_type":"StatusGain","effect_description":"此丹药由幻梦灵藤与幻影灵草带来虚幻力量...","bonus_stats":{"speed":10,"attack_speed":20}}
```

名称生成SystemPrompt:
```
为用户提供的丹药根据其药效和药材命名，仅给出一个答案(比如凝元丹)
```

---

## 丹药效果类型

### ElixirEffectType枚举

| 类型 | 说明 | 实现状态 | 使用触发 |
|------|------|----------|----------|
| `StatusGain` | 状态增益（临时buff） | ✅ 完整 | 需要属性时自动使用 |
| `DataChange` | 数据改变（如获得灵根） | ✅ 完整 | updateAge 20%几率 |
| `Restore` | 恢复类（如恢复灵气） | ✅ 完整 | updateAge 20%几率 |
| `DataGain` | 数据增益（永久提升） | ⚠️ 框架存在 | updateAge 20%几率 |

### 效果设置方法

```csharp
// 状态增益
elixirAsset.SetupStatusGain(StatusComponent status_given, StatusOverwriteStats overwrite_stats);
elixirAsset.SetupStatusGain(ElixirEffectDelegate effect_action);

// 数据改变
elixirAsset.SetupDataChange(ElixirEffectDelegate effect_action);

// 恢复类
elixirAsset.SetupRestore(ElixirEffectDelegate effect_action);

// 数据增益
elixirAsset.SetupDataGain(ElixirEffectDelegate effect_action);
```

---

## 炼丹流程

### 完整时序图

```
[角色] → [BehFindNewElixir/BehFindElixirToCraft]
           ↓
       检查背包是否有CraftingElixir
           ↓ (无)
       查找TagIngredient材料 / 匹配已有配方
           ↓
       创建CraftingElixir实体
           ↓
       为每个材料添加CraftOccupyingRelation
           ↓
       材料添加TagOccupied标签
           ↓
[角色] → [BehCraftElixir]
           ↓
       增加progress计数
           ↓
       等待随机时间(1-3秒)
           ↓ (progress >= 材料数量)
       调用ElixirAsset.Craft()
           ↓
       执行craft_action (计算丹药强度等)
           ↓
       删除材料实体
           ↓
       添加Elixir组件和对应Tag
           ↓
       添加到城市仓库
           ↓
       增加角色对该丹方的掌握程度
```

### 关键配置

```csharp
// XianSetting.cs
public const float TakenElixirProb = 0.2f;           // 自动服用丹药概率
public const float CityDistributeElixirProb = 0.1f;  // 城市分发丹药概率
```

---

## 框架能力分析

### 当前框架优势

1. **灵活的材料检查系统**
   - 支持按名称、灵根类型、金丹类型检查
   - 可组合多种条件

2. **强大的AIGC集成**
   - 异步生成，不阻塞游戏
   - 结果缓存到本地文件
   - 支持自定义Prompt

3. **完整的ECS架构**
   - 使用Friflo.Engine.ECS
   - Tag系统区分丹药类型
   - Relation系统管理材料占用

4. **动态资产库**
   - 支持运行时添加丹药定义
   - 支持存档序列化

5. **可扩展的效果系统**
   - 三种委托类型覆盖全流程
   - 可自定义任意效果逻辑

### 当前框架限制

1. **材料获取未完善**
   - TagIngredient的添加逻辑不明确
   - 缺少系统化的材料生成

2. **丹药品质待接入**
   - 需要在丹药实体挂载ItemLevel（不重复存储在Elixir内）
   - 当前效果强度计算简单

3. **炼丹技能缺失**
   - 没有炼丹等级/熟练度
   - 没有失败机制

4. **丹药副作用缺失**
   - 无毒性/耐药性
   - 无药效冲突

5. **DataGain未实现**
   - 永久属性提升类丹药无法生成

---

## 拓展设计方案

### 一、完善DataGain效果类型

**目标**：实现永久属性提升类丹药

```csharp
// ElixirEffectGenerator.cs 中添加
private static bool GenerateDataGainElixirActions(ElixirAsset elixir)
{
    // 1. 生成永久属性提升效果
    var content = ElixirDataGainJsonGenerator.Instance.GenerateName(param);
    DataGainEffect effect = JsonConvert.DeserializeObject<DataGainEffect>(content);
    
    // 2. 设置效果action
    elixir.effect_action = (ae, elixir_entity, ref Elixir elixir_comp) =>
    {
        foreach (var kv in effect.permanent_stats)
        {
            ae.AddPermanentStat(kv.Key, kv.Value * elixir_comp.value);
        }
    };
    
    // 3. 设置使用限制（每种丹药只能吃一定次数）
    elixir.consumable_check_action = (ae, _, ref Elixir _) =>
    {
        var consumed_count = ae.GetElixirConsumedCount(elixir.id);
        return consumed_count < effect.max_consume_count;
    };
    
    return true;
}
```

### 二、丹药品质系统

**品质等级**：
- 下品 (Poor) - 效果60%
- 中品 (Normal) - 效果100%
- 上品 (Good) - 效果150%
- 极品 (Excellent) - 效果200%
- 仙品 (Immortal) - 效果300%

**实现方式**：
- 直接复用通用物品等级组件`ItemLevel`挂载到丹药实体
- `Elixir`组件保持轻量，仅存储ID与强度值
- 炼制时计算品阶并写入实体的`ItemLevel`，效果倍率由`ItemLevel`映射获得

```csharp
// Elixir组件保持无品阶字段
public struct Elixir : IComponent
{
    public string elixir_id;
    public float value; // 强度/效力
}

// 炼制时写入品阶并应用倍率
public void Craft(...)
{
    ItemLevel level = CalculateElixirLevel(ae, corr_ingredients, furnace_entity);
    float effectMultiplier = GetEffectMultiplier(level); // 由阶段/等级映射到倍率

    elixir_entity.AddComponent(level); // 单独挂ItemLevel组件
    elixir_entity.AddComponent(new Elixir
    {
        elixir_id = elixir_asset.id,
        value = baseValue * effectMultiplier
    });
}

// 使用时直接从实体读取ItemLevel
public void ApplyElixirEffect(Entity elixir_entity)
{
    if (elixir_entity.TryGetComponent(out ItemLevel level))
    {
        float effectMultiplier = GetEffectMultiplier(level);
        // 应用倍率...
    }
}
```

**品质影响因素**：
- 炼丹者境界与熟练度
- 材料品质与契合度
- 丹炉品质
- 环境灵气浓度

### 三、炼丹失败机制

**失败类型**：
- 炸炉：材料全部损失，可能受伤
- 废丹：产出无效果的丹药
- 毒丹：产出有负面效果的丹药

**成功率公式**：
```
成功率 = 基础成功率 × 境界系数 × 熟练度系数 × 材料契合度 × 环境系数
```

```csharp
// BehCraftElixir.cs 修改
public override BehResult execute(Actor pObject)
{
    ...
    if (crafting_elixir.progress >= ingredients.Length)
    {
        float success_rate = CalculateSuccessRate(ae, elixir_asset);
        var rand = Randy.randomFloat(0, 1);
        
        if (rand < success_rate)
        {
            // 正常炼制
            elixir_asset.Craft(ae, crafting_elixir_entity, ...);
        }
        else if (rand < success_rate * 1.3f)
        {
            // 废丹
            CreateWasteElixir(ae, crafting_elixir_entity, ...);
        }
        else if (rand < success_rate * 1.6f)
        {
            // 毒丹
            CreatePoisonElixir(ae, crafting_elixir_entity, ...);
        }
        else
        {
            // 炸炉
            ExplodeFurnace(ae, crafting_elixir_entity, ...);
        }
    }
    ...
}
```

### 四、丹药副作用系统

**丹毒机制**：
- 每次服用丹药累积丹毒值
- 丹毒值影响修炼效率和突破成功率
- 可通过修炼或特殊丹药化解

**药效耐受**：
- 同类丹药多次服用效果递减
- 需要间隔一定时间恢复

```csharp
// 新增组件
public struct ElixirToxin : IComponent
{
    public float toxin_value;
    public float last_decay_time;
}

public struct ElixirTolerance : IComponent
{
    public Dictionary<string, float> tolerance_levels; // elixir_id -> 耐受度
    public Dictionary<string, float> last_consume_time;
}

// 修改TryConsumeElixir
public static bool TryConsumeElixir(this ActorExtend ae, Entity elixir_entity)
{
    ...
    // 检查耐受度
    float tolerance = ae.GetElixirTolerance(elixir.elixir_id);
    float effective_rate = 1f / (1f + tolerance);
    
    // 执行效果时应用衰减
    elixir_asset.effect_action?.Invoke(ae, elixir_entity, ref elixir, effective_rate);
    
    // 增加丹毒
    ae.AddElixirToxin(elixir_asset.toxin_value);
    
    // 增加耐受度
    ae.AddElixirTolerance(elixir.elixir_id, 0.1f);
    ...
}
```

### 五、特殊丹药类型

#### 5.1 突破丹
用于辅助境界突破，提高突破成功率

```csharp
BreakthroughElixir.SetupStatusGain((ae, _, ref Elixir elixir) =>
{
    var status = StatusEffects.BreakthroughBonus.NewEntity();
    status.AddComponent(new StatusOverwriteStats
    {
        stats = new BaseStats
        {
            [BaseStatses.BreakthroughSuccessRate.id] = elixir.value * 0.1f
        }
    });
    ae.AddSharedStatus(status);
});
```

#### 5.2 渡劫丹
减少天劫伤害

```csharp
TribulationElixir.SetupStatusGain((ae, _, ref Elixir elixir) =>
{
    var status = StatusEffects.TribulationResist.NewEntity();
    status.AddComponent(new AliveTimeLimit { value = 600 }); // 持续10分钟
    status.AddComponent(new StatusOverwriteStats
    {
        stats = new BaseStats
        {
            [BaseStatses.TribulationDamageReduction.id] = elixir.value * 0.2f
        }
    });
    ae.AddSharedStatus(status);
});
```

#### 5.3 化形丹
助妖兽化为人形

```csharp
TransformElixir.SetupDataChange((ae, _, ref Elixir _) =>
{
    if (ae.Base.asset.id.Contains("animal"))
    {
        ae.Transform("human_variant");
    }
});
TransformElixir.consumable_check_action = (ae, _, ref Elixir _) =>
{
    return ae.Base.asset.id.Contains("animal") && ae.HasCultisys<Xian>();
};
```

#### 5.4 易容丹
改变外貌

```csharp
DisguiseElixir.SetupStatusGain((ae, _, ref Elixir _) =>
{
    ae.Base.setRandomHeadRace();
    var status = StatusEffects.Disguised.NewEntity();
    status.AddComponent(new AliveTimeLimit { value = 3600 }); // 持续1小时
    ae.AddSharedStatus(status);
});
```

### 六、丹方传承系统增强

#### 6.1 师徒传授

```csharp
// BehTeachElixirRecipe.cs
public class BehTeachElixirRecipe : BehActor
{
    public override BehResult execute(Actor pObject)
    {
        var master = pObject.GetExtend();
        var apprentice = master.GetApprentice();
        if (apprentice == null) return BehResult.Stop;
        
        var elixir_to_teach = master.GetAllMaster<ElixirAsset>()
            .Where(x => !apprentice.HasMaster(x.Item1))
            .OrderByDescending(x => x.Item2)
            .FirstOrDefault();
            
        if (elixir_to_teach.Item1 != null)
        {
            apprentice.Master(elixir_to_teach.Item1, elixir_to_teach.Item2 * 0.5f);
            ModClass.LogInfo($"{master} 传授 {apprentice} 丹方: {elixir_to_teach.Item1.GetName()}");
        }
        
        return BehResult.Continue;
    }
}
```

#### 6.2 门派丹方库

```csharp
// SectElixirLibrary组件
public struct SectElixirLibrary : IComponent
{
    public List<string> elixir_ids;
    public Dictionary<string, int> recipe_counts; // 配方数量（可消耗）
}

// 门派成员可以学习门派丹方
public static void LearnSectElixir(this ActorExtend ae, string elixir_id)
{
    var sect = ae.GetSect();
    if (sect == null) return;
    
    ref var library = ref sect.GetComponent<SectElixirLibrary>();
    if (library.elixir_ids.Contains(elixir_id))
    {
        var asset = Libraries.Manager.ElixirLibrary.get(elixir_id);
        ae.Master(asset, 1);
    }
}
```

### 七、炼丹场景系统

#### 7.1 丹炉系统

```csharp
public struct DanFurnace : IComponent
{
    public string furnace_id;
    public int level;
    public float quality_bonus;
    public float success_rate_bonus;
    public int max_ingredients;
}

// 使用丹炉炼丹
public void CraftWithFurnace(Entity furnace_entity, ...)
{
    ref var furnace = ref furnace_entity.GetComponent<DanFurnace>();
    
    var quality_modifier = 1f + furnace.quality_bonus;
    var success_modifier = 1f + furnace.success_rate_bonus;
    
    // 应用修正...
}
```

#### 7.2 阵法辅助

```csharp
// 炼丹阵法可以提供额外加成
public struct AlchemyFormation : IComponent
{
    public string formation_id;
    public float efficiency_bonus;
    public float purity_bonus;
    public float element_affinity; // 对特定五行材料的增幅
}
```

#### 7.3 天时地利

```csharp
// 在特定时间、地点炼丹获得加成
public static float GetEnvironmentBonus(WorldTile tile, float game_time)
{
    float bonus = 1f;
    
    // 满月加成
    if (IsMoonFull(game_time)) bonus *= 1.1f;
    
    // 灵脉加成
    if (tile.HasSpiritVein()) bonus *= 1.2f;
    
    // 特定地形加成
    if (tile.Type == TileType.Mountain) bonus *= 1.05f;
    
    return bonus;
}
```

---

## TODO列表

### 高优先级 (P0)

- [ ] **完善DataGain效果生成**
  - 实现`GenerateDataGainElixirActions`
  - 添加永久属性提升的AIGC Prompt
  - 添加服用次数限制机制

- [ ] **材料系统完善**
  - 明确TagIngredient的添加来源
  - 与草药采集系统(`BehHarvestHerb`)对接
  - 添加材料品质概念

- [ ] **丹药品质系统**
  - 丹药实体挂载`ItemLevel`
  - 实现品阶计算与倍率映射
  - UI展示丹药品阶

### 中优先级 (P1)

- [ ] **炼丹技能系统**
  - 添加炼丹熟练度追踪
  - 熟练度影响成功率和品质
  - 熟练度影响可炼制的丹药等级

- [ ] **炼丹失败机制**
  - 实现炸炉、废丹、毒丹三种失败结果
  - 失败时的惩罚和材料损失
  - 失败经验也能提升熟练度

- [ ] **丹药副作用**
  - 实现丹毒累积系统
  - 实现药效耐受系统
  - 添加解毒/化毒丹药

### 低优先级 (P2)

- [ ] **特殊丹药扩展**
  - 突破丹系列
  - 渡劫丹
  - 化形丹
  - 各种状态解除丹药

- [ ] **丹方传承增强**
  - 师徒传授丹方
  - 门派丹方库
  - 丹方交易系统

- [ ] **炼丹场景系统**
  - 丹炉系统
  - 炼丹阵法
  - 环境影响

- [ ] **UI增强**
  - 丹药详情面板
  - 炼丹界面
  - 丹方收藏界面

### 技术债务

- [ ] 优化AIGC请求并发控制
- [ ] 添加丹药效果的单元测试
- [ ] 完善丹药相关日志
- [ ] 性能优化：丹药效果查询缓存

---

## 附录

### A. 相关文件索引

| 类别 | 文件路径 |
|------|----------|
| 组件 | `Source/Content/Components/Elixir.cs` |
| 组件 | `Source/Content/Components/Elixirbook.cs` |
| 组件 | `Source/Content/Components/CraftingElixir.cs` |
| 组件 | `Source/Content/Components/TagElixir*.cs` |
| 组件 | `Source/Core/Components/ItemLevel.cs` |
| 资产 | `Source/Content/Libraries/ElixirAsset.cs` |
| 资产 | `Source/Content/Libraries/ElixirLibrary.cs` |
| 生成 | `Source/Content/Libraries/ElixirEffectGenerator.cs` |
| AIGC | `Source/Content/AIGC/ElixirNameGenerator.cs` |
| AIGC | `Source/Content/AIGC/ElixirEffectJsonGenerator.cs` |
| 行为 | `Source/Content/Behaviours/BehCraftElixir.cs` |
| 行为 | `Source/Content/Behaviours/BehFindNewElixir.cs` |
| 行为 | `Source/Content/Behaviours/BehFindElixirToCraft.cs` |
| 行为 | `Source/Content/Behaviours/BehWriteElixirRecipe.cs` |
| 定义 | `Source/Content/Elixirs.cs` |
| 配置 | `Source/Content/Const/ElixirEffectType.cs` |
| 配置 | `Source/Content/Const/XianSetting.cs` |
| Patch | `Source/Content/Patch/PatchAboutElixir.cs` |
| 扩展 | `Source/Content/Extensions/ActorExtendTools.cs` |
| UI | `Source/Content/UI/CreatureInfoPages/ElixirPage.cs` |
| 本地化 | `Locales/elixirs.csv` |

### B. 配置常量

```csharp
// XianSetting.cs
TakenElixirProb = 0.2f           // 自动服用丹药概率
CityDistributeElixirProb = 0.1f  // 城市分发丹药概率
WakanRestoreLimit = 0.6f         // 灵气恢复上限比例
```

### C. 本地化Key

| Key | 中文 |
|-----|------|
| `Cultiway.Elixir.OpenElementRootElixir` | 开灵丹 |
| `Cultiway.Elixir.WakanRestoreElixir` | 补气丹 |
| `Cultiway.Elixir.EnlightenElixir` | 悟道丹 |
| `Task.Unit.Cultiway.CraftElixir` | 炼制丹药 |
| `Task.Unit.Cultiway.FindNewElixir` | 研究新丹方 |
| `Task.Unit.Cultiway.WriteElixirbook` | 誊抄丹方 |
| `book_type_Cultiway.Elixirbook` | 丹方 |

---

*文档版本: 1.0*
*最后更新: 2024年*

