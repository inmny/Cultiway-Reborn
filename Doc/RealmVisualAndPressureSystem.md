# 境界视觉表现与威压系统设计文档

**版本**: v1.0  
**状态**: 设计中  
**最后更新**: 2025年11月

---

## 📋 目录

1. [系统概述](#系统概述)
2. [境界视觉表现系统](#境界视觉表现系统)
3. [境界威压系统](#境界威压系统)
4. [数据结构设计](#数据结构设计)
5. [渲染系统设计](#渲染系统设计)
6. [AI行为交互](#ai行为交互)
7. [技术实现](#技术实现)
8. [资源需求](#资源需求)
9. [开发TODO](#开发todo)

---

## 系统概述

### 设计目标

本系统旨在为不同修仙境界的角色提供独特的视觉表现，并实现境界差距带来的威压效果，增强游戏的沉浸感和修仙世界的等级感。

### 核心理念

1. **视觉层次分明** - 玩家能通过视觉效果直观判断角色的修炼境界
2. **威压即实力** - 高境界修士自带威压，对低境界修士产生实际效果
3. **性能可控** - 特效系统需要考虑性能，支持配置开关
4. **可扩展性** - 预留新境界和新效果的扩展接口

### 境界等级参考

根据现有系统 (`Source/Content/Const/XianLevels.cs`)：

| 境界常量 | 值 | 对应境界 | 视觉等级 |
|---------|---|---------|---------|
| - | 0 | 凡人 | 无特效 |
| XianBase | 1 | 练气/筑基 | 基础光环 |
| Jindan | 2 | 金丹 | 中级光环 |
| Yuanying | 3 | 元婴 | 高级光环 |
| - | 4+ | 化神及以上 | 顶级光环 |

---

## 境界视觉表现系统

### 2.1 视觉效果层级

#### 第一层：身体光晕 (Body Aura)

围绕角色身体的基础光晕效果，颜色和强度根据境界变化。

| 境界 | 光晕颜色 | 透明度 | 大小倍率 | 描述 |
|-----|---------|-------|---------|------|
| 凡人 | 无 | 0 | 0 | 无光晕 |
| 练气 | 淡白色 | 0.1-0.2 | 1.0 | 微弱的灵气波动 |
| 筑基 | 淡蓝色 | 0.2-0.3 | 1.2 | 稳定的灵气流转 |
| 金丹 | 金色/元素色 | 0.3-0.4 | 1.5 | 金丹之光外显 |
| 元婴 | 多彩渐变 | 0.4-0.5 | 1.8 | 元婴神识外放 |
| 化神 | 纯白耀眼 | 0.5-0.6 | 2.2 | 天地法则共鸣 |

**元素色映射** (基于 `ElementRoot` 组件):
- 金: `#FFD700` (金黄色)
- 木: `#228B22` (森林绿)
- 水: `#4169E1` (皇家蓝)
- 火: `#FF4500` (橙红色)
- 土: `#8B4513` (赭石色)

#### 第二层：元素粒子 (Element Particles)

根据角色灵根属性生成的元素粒子效果。

```
粒子数量 = 基础数量 × 境界倍率 × 灵根强度
基础数量 = 3
境界倍率: 练气=1, 筑基=1.5, 金丹=2, 元婴=3, 化神=5
灵根强度 = 对应灵根值 / 总灵根值
```

**粒子特征**:
- 金: 金属碎片闪烁
- 木: 绿叶飘落
- 水: 水滴悬浮
- 火: 火星迸发
- 土: 土石环绕

#### 第三层：特殊境界标识 (Realm Indicator)

金丹以上境界的特殊视觉标识。

**金丹境界**:
- 腹部位置显示微小的金丹虚影
- 金丹转数用光环数量表示 (1-9转)
- 金丹类型影响虚影形状和颜色

**元婴境界**:
- 头顶显示小型元婴虚影
- 元婴强度影响虚影清晰度
- 元婴动作与本体同步

#### 第四层：气势外放 (Aura Release)

主动释放威压或战斗状态时的增强视觉效果。

- 光晕范围扩大 2-5 倍
- 透明度提升至 0.6-0.8
- 添加脉冲动画效果
- 产生地面涟漪效果

### 2.2 动画效果设计

#### 呼吸动画 (Breathing Animation)

光晕的缓慢脉动，模拟呼吸节奏。

```csharp
// 伪代码示例
float breathPhase = Time.time * breathSpeed;
float breathScale = 1 + Mathf.Sin(breathPhase) * breathAmplitude;
auraTransform.localScale = baseScale * breathScale;
```

**参数配置**:
- 呼吸周期: 2-4秒
- 振幅: 0.05-0.15 (境界越高振幅越大)

#### 修炼状态动画 (Cultivation Animation)

修炼时的特殊视觉效果。

- 灵气汇聚效果: 周围灵气向角色流动
- 光晕强化: 透明度和大小临时提升
- 粒子加速: 元素粒子旋转加速

#### 突破动画 (Breakthrough Animation)

境界突破时的视觉表现。

1. **蓄力阶段** (1-2秒)
   - 光晕收缩至角色身体
   - 能量压缩效果
   
2. **爆发阶段** (0.5秒)
   - 光晕瞬间扩张
   - 产生冲击波
   - 天空闪电（高境界突破）
   
3. **稳定阶段** (2-3秒)
   - 新境界光晕逐渐稳定
   - 元素粒子更新

### 2.3 特殊状态视觉

#### 战斗状态

- 光晕颜色偏红
- 粒子运动加剧
- 添加战意火焰效果

#### 受伤状态

- 光晕闪烁不稳定
- 出现裂纹效果
- 粒子减少

#### 濒死状态

- 光晕几乎消失
- 只有微弱光点
- 境界标识消失

---

## 境界威压系统

### 3.1 威压机制

#### 威压值计算

```
威压值 = 基础威压 × 境界倍率 × 金丹/元婴加成 × 特质加成

基础威压 = PowerLevel × 10
境界倍率: 
  - 练气: 1.0
  - 筑基: 2.0
  - 金丹: 5.0
  - 元婴: 15.0
  - 化神: 50.0

金丹加成 = 1 + (金丹转数 × 0.1) + (金丹强度 × 0.2)
元婴加成 = 1 + (元婴阶段 × 0.15) + (元婴强度 × 0.3)
```

#### 威压抵抗计算

```
抵抗值 = 基础抵抗 × 境界倍率 × 意志加成 × 特质加成

基础抵抗 = PowerLevel × 8
意志加成 = 1 + (intelligence / 100) × 0.5
```

#### 威压效果判定

```
有效威压 = 施压者威压值 - 受压者抵抗值

if (有效威压 <= 0) {
    // 无效果
} else if (有效威压 < 50) {
    // 轻微效果
} else if (有效威压 < 150) {
    // 中等效果
} else if (有效威压 < 300) {
    // 严重效果
} else {
    // 压制效果
}
```

### 3.2 威压效果层级

#### 第一级：威慑 (Intimidation)

**触发条件**: 有效威压 1-49

**效果**:
- 降低命中率 5-15%
- 降低闪避率 5-10%
- 轻微移速下降 5%

**视觉表现**:
- 受压者光晕微微颤抖
- 偶尔冷汗粒子效果

**持续时间**: 威压范围内持续

#### 第二级：恐惧 (Fear)

**触发条件**: 有效威压 50-149

**效果**:
- 降低所有属性 10-20%
- 降低攻击力 15%
- 降低移速 15%
- 可能触发逃跑行为

**视觉表现**:
- 受压者身体颤抖动画
- 冷汗粒子增加
- 光晕不稳定闪烁

**持续时间**: 威压范围内持续，离开后 3-5秒恢复

#### 第三级：威压崩溃 (Pressure Collapse)

**触发条件**: 有效威压 150-299

**效果**:
- 降低所有属性 30-50%
- 无法主动攻击
- 移速降低 50%
- 高概率触发逃跑
- 持续掉血（心神损伤）

**视觉表现**:
- 受压者跪地动画
- 大量冷汗粒子
- 恐惧表情特效
- 光晕极度不稳定

**持续时间**: 威压范围内持续，离开后 10-15秒恢复

#### 第四级：碾压 (Crush)

**触发条件**: 有效威压 ≥ 300

**效果**:
- 完全无法行动
- 持续受到真实伤害
- 可能直接击杀（心神崩溃）
- 永久性恐惧debuff（对施压者）

**视觉表现**:
- 受压者趴倒在地
- 身体陷入地面效果
- 黑暗笼罩效果
- 光晕完全熄灭

**持续时间**: 威压范围内持续

### 3.3 威压范围与触发

#### 被动威压 (Passive Aura)

高境界修士自带的被动威压场。

```
被动威压范围 = 基础范围 × 境界倍率
基础范围 = 3 tiles
境界倍率: 筑基=1, 金丹=2, 元婴=3, 化神=5

被动威压强度 = 完整威压值 × 0.3
```

**触发条件**: 
- 始终开启
- 对非友好单位生效
- 对同境界以下生效

#### 主动威压 (Active Pressure)

主动释放的威压技能。

```
主动威压范围 = 被动范围 × 2-3
主动威压强度 = 完整威压值 × 1.0
主动威压消耗 = 灵力 × 0.1 / 秒
```

**触发条件**:
- 手动释放或AI决策释放
- 消耗灵力维持
- 可被打断

#### 战意威压 (Combat Pressure)

战斗中自动释放的威压。

```
战意威压范围 = 被动范围 × 1.5
战意威压强度 = 完整威压值 × 0.5
```

**触发条件**:
- 进入战斗状态自动触发
- 仅对当前敌人生效
- 无额外消耗

### 3.4 威压抵抗与免疫

#### 抵抗来源

| 来源 | 抵抗加成 |
|-----|---------|
| 同境界 | +50% |
| 高一境界 | +100% (免疫) |
| 意志特质 | +20-50% |
| 不动心境界 | +100% |
| 护心法宝 | +30-80% |
| 宗门护法 | +20% |

#### 特殊免疫情况

- 同宗门成员（可配置）
- 友好关系（减免50%）
- 主角特质（部分抵抗）
- 天命之人（完全免疫低级威压）

---

## 数据结构设计

### 4.1 新增组件

#### RealmVisual 组件

```csharp
// Source/Content/Components/RealmVisual.cs
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>
/// 境界视觉表现组件
/// 存储角色的视觉效果状态
/// </summary>
public struct RealmVisual : IComponent
{
    /// <summary>
    /// 当前光晕等级 (0-5)
    /// </summary>
    public int aura_level;
    
    /// <summary>
    /// 光晕颜色 (RGBA32)
    /// </summary>
    public uint aura_color;
    
    /// <summary>
    /// 光晕透明度 (0-1)
    /// </summary>
    public float aura_alpha;
    
    /// <summary>
    /// 光晕大小倍率
    /// </summary>
    public float aura_scale;
    
    /// <summary>
    /// 是否显示金丹虚影
    /// </summary>
    public bool show_jindan;
    
    /// <summary>
    /// 是否显示元婴虚影
    /// </summary>
    public bool show_yuanying;
    
    /// <summary>
    /// 当前视觉状态
    /// 0=正常, 1=战斗, 2=修炼, 3=突破, 4=受伤
    /// </summary>
    public byte visual_state;
    
    /// <summary>
    /// 粒子效果强度 (0-1)
    /// </summary>
    public float particle_intensity;
}
```

#### RealmPressure 组件

```csharp
// Source/Content/Components/RealmPressure.cs
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 境界威压组件
/// 存储威压相关数据
/// </summary>
public struct RealmPressure : IComponent
{
    /// <summary>
    /// 威压值
    /// </summary>
    public float pressure_value;
    
    /// <summary>
    /// 威压抵抗值
    /// </summary>
    public float resistance_value;
    
    /// <summary>
    /// 被动威压范围 (tiles)
    /// </summary>
    public float passive_range;
    
    /// <summary>
    /// 是否正在主动释放威压
    /// </summary>
    public bool is_active_pressure;
    
    /// <summary>
    /// 当前承受的威压等级 (0-4)
    /// </summary>
    public byte pressure_effect_level;
    
    /// <summary>
    /// 威压效果剩余时间
    /// </summary>
    public float effect_remaining_time;
}
```

#### PressureRelation 关系组件

```csharp
// Source/Core/Components/PressureRelation.cs
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 威压关系
/// 表示一个实体正在被另一个实体威压
/// </summary>
public struct PressureRelation : ILinkRelation
{
    /// <summary>
    /// 威压来源实体
    /// </summary>
    public Entity source { get; set; }
    
    /// <summary>
    /// 有效威压值
    /// </summary>
    public float effective_pressure;
    
    public Entity GetRelationKey() => source;
}
```

### 4.2 资源库定义

#### RealmVisualAsset

```csharp
// Source/Content/Libraries/RealmVisualAsset.cs
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 境界视觉效果资源
/// 定义每个境界的视觉参数
/// </summary>
public class RealmVisualAsset : Asset
{
    /// <summary>
    /// 境界等级
    /// </summary>
    public int realm_level;
    
    /// <summary>
    /// 光晕颜色
    /// </summary>
    public Color aura_color = Color.white;
    
    /// <summary>
    /// 光晕透明度范围 (min, max)
    /// </summary>
    public Vector2 alpha_range = new(0.1f, 0.3f);
    
    /// <summary>
    /// 光晕大小倍率
    /// </summary>
    public float scale_multiplier = 1.0f;
    
    /// <summary>
    /// 粒子基础数量
    /// </summary>
    public int base_particle_count = 3;
    
    /// <summary>
    /// 光晕贴图路径
    /// </summary>
    public string aura_sprite_path;
    
    /// <summary>
    /// 呼吸动画速度
    /// </summary>
    public float breath_speed = 0.5f;
    
    /// <summary>
    /// 呼吸动画振幅
    /// </summary>
    public float breath_amplitude = 0.1f;
    
    /// <summary>
    /// 威压基础倍率
    /// </summary>
    public float pressure_multiplier = 1.0f;
    
    /// <summary>
    /// 威压范围倍率
    /// </summary>
    public float pressure_range_multiplier = 1.0f;
}
```

### 4.3 配置数据

#### realm_visual_config.json

```json
{
    "realm_visuals": [
        {
            "id": "mortal",
            "realm_level": 0,
            "aura_color": "#FFFFFF00",
            "alpha_range": [0, 0],
            "scale_multiplier": 0,
            "base_particle_count": 0,
            "pressure_multiplier": 0,
            "pressure_range_multiplier": 0
        },
        {
            "id": "qi_refining",
            "realm_level": 1,
            "aura_color": "#FFFFFFAA",
            "alpha_range": [0.1, 0.2],
            "scale_multiplier": 1.0,
            "base_particle_count": 2,
            "aura_sprite_path": "cultiway/special_effects/aura/qi_aura",
            "breath_speed": 0.4,
            "breath_amplitude": 0.05,
            "pressure_multiplier": 1.0,
            "pressure_range_multiplier": 1.0
        },
        {
            "id": "foundation",
            "realm_level": 1,
            "aura_color": "#87CEEBCC",
            "alpha_range": [0.2, 0.3],
            "scale_multiplier": 1.2,
            "base_particle_count": 3,
            "aura_sprite_path": "cultiway/special_effects/aura/foundation_aura",
            "breath_speed": 0.5,
            "breath_amplitude": 0.08,
            "pressure_multiplier": 2.0,
            "pressure_range_multiplier": 1.0
        },
        {
            "id": "jindan",
            "realm_level": 2,
            "aura_color": "#FFD700DD",
            "alpha_range": [0.3, 0.4],
            "scale_multiplier": 1.5,
            "base_particle_count": 5,
            "aura_sprite_path": "cultiway/special_effects/aura/jindan_aura",
            "breath_speed": 0.6,
            "breath_amplitude": 0.1,
            "pressure_multiplier": 5.0,
            "pressure_range_multiplier": 2.0
        },
        {
            "id": "yuanying",
            "realm_level": 3,
            "aura_color": "#9370DBEE",
            "alpha_range": [0.4, 0.5],
            "scale_multiplier": 1.8,
            "base_particle_count": 8,
            "aura_sprite_path": "cultiway/special_effects/aura/yuanying_aura",
            "breath_speed": 0.7,
            "breath_amplitude": 0.12,
            "pressure_multiplier": 15.0,
            "pressure_range_multiplier": 3.0
        },
        {
            "id": "huashen",
            "realm_level": 4,
            "aura_color": "#FFFFFFFF",
            "alpha_range": [0.5, 0.6],
            "scale_multiplier": 2.2,
            "base_particle_count": 12,
            "aura_sprite_path": "cultiway/special_effects/aura/huashen_aura",
            "breath_speed": 0.8,
            "breath_amplitude": 0.15,
            "pressure_multiplier": 50.0,
            "pressure_range_multiplier": 5.0
        }
    ],
    "pressure_effects": [
        {
            "level": 0,
            "name": "无效果",
            "min_pressure": 0,
            "max_pressure": 0
        },
        {
            "level": 1,
            "name": "威慑",
            "min_pressure": 1,
            "max_pressure": 49,
            "accuracy_reduction": 0.1,
            "dodge_reduction": 0.075,
            "speed_reduction": 0.05
        },
        {
            "level": 2,
            "name": "恐惧",
            "min_pressure": 50,
            "max_pressure": 149,
            "all_stats_reduction": 0.15,
            "attack_reduction": 0.15,
            "speed_reduction": 0.15,
            "flee_chance": 0.3,
            "recovery_time": 4.0
        },
        {
            "level": 3,
            "name": "威压崩溃",
            "min_pressure": 150,
            "max_pressure": 299,
            "all_stats_reduction": 0.4,
            "can_attack": false,
            "speed_reduction": 0.5,
            "flee_chance": 0.7,
            "dot_damage_percent": 0.01,
            "recovery_time": 12.0
        },
        {
            "level": 4,
            "name": "碾压",
            "min_pressure": 300,
            "max_pressure": 999999,
            "can_move": false,
            "dot_damage_percent": 0.05,
            "instant_kill_chance": 0.1,
            "permanent_fear": true
        }
    ]
}
```

---

## 渲染系统设计

### 5.1 系统架构

```
GeneralRenderSystems
├── RenderAnimFrameSystem (已有)
├── CloudRenderSystem (已有)
└── RealmVisualRenderSystemGroup (新增)
    ├── AuraRenderSystem         // 光晕渲染
    ├── ParticleRenderSystem     // 粒子渲染
    ├── RealmIndicatorSystem     // 境界标识渲染
    └── PressureEffectSystem     // 威压效果渲染
```

### 5.2 AuraRenderSystem

光晕渲染系统，负责绘制角色周围的光环效果。

```csharp
// Source/Content/Systems/Render/AuraRenderSystem.cs
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>
/// 光晕渲染系统
/// 根据角色境界渲染对应的光晕效果
/// </summary>
public class AuraRenderSystem : QuerySystem<ActorBinder, RealmVisual>
{
    private MonoObjPool<Aura> _pool;
    private Sprite[] _aura_sprites;
    
    public AuraRenderSystem()
    {
        // 初始化对象池和资源
        var obj = new GameObject("realm_auras");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;
        
        var prefab = ModClass.NewPrefabPreview("RealmAura").AddComponent<Aura>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sortingOrder = -1; // 在角色下方
        
        _pool = new(prefab, obj.transform,
            active_action: (aura) => { aura.transform.localScale = Vector3.one * 0.01f; });
        
        // 加载光晕贴图
        LoadAuraSprites();
    }
    
    private void LoadAuraSprites()
    {
        // TODO: 从资源库加载不同境界的光晕贴图
        _aura_sprites = new Sprite[5];
        // _aura_sprites[0] = SpriteTextureLoader.getSprite("cultiway/special_effects/aura/qi_aura");
        // ...
    }
    
    [Hotfixable]
    protected override void OnUpdate()
    {
        _pool.ResetToStart();
        if (MapBox.isRenderMiniMap()) return;
        
        Query.ForEachEntity([Hotfixable](ref ActorBinder actor_binder, ref RealmVisual visual, Entity e) =>
        {
            Actor a = actor_binder.Actor;
            if (a == null || !a.isAlive()) return;
            if (!a.is_visible) return;
            if (visual.aura_level <= 0) return;
            
            Aura aura = _pool.GetNext();
            var sprite_renderer = aura.sprite_renderer;
            var transform = aura.transform;
            
            // 设置位置和大小
            transform.localPosition = a.cur_transform_position;
            transform.localScale = Vector3.one * a.stats[S.scale] * visual.aura_scale;
            
            // 设置颜色和透明度
            Color color = ColorUtils.FromUInt32(visual.aura_color);
            color.a = visual.aura_alpha;
            
            // 呼吸动画
            float breathPhase = Time.time * 0.5f; // TODO: 从配置读取
            float breathScale = 1 + Mathf.Sin(breathPhase) * 0.1f;
            transform.localScale *= breathScale;
            
            sprite_renderer.color = color;
            
            // 设置贴图
            if (visual.aura_level <= _aura_sprites.Length && _aura_sprites[visual.aura_level - 1] != null)
            {
                sprite_renderer.sprite = _aura_sprites[visual.aura_level - 1];
            }
        });
        
        _pool.ClearUnsed();
    }
    
    [RequireComponent(typeof(SpriteRenderer))]
    class Aura : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}
```

### 5.3 ElementParticleSystem

元素粒子系统，根据灵根属性生成对应的粒子效果。

```csharp
// Source/Content/Systems/Render/ElementParticleSystem.cs
// 负责渲染五行元素粒子效果
// 参考现有的粒子系统实现

// 关键设计:
// 1. 使用对象池管理粒子
// 2. 根据灵根类型选择粒子贴图
// 3. 粒子围绕角色旋转
// 4. 支持粒子数量和速度配置
```

### 5.4 PressureVisualSystem

威压视觉效果系统，渲染受到威压时的视觉反馈。

```csharp
// Source/Content/Systems/Render/PressureVisualSystem.cs
// 渲染威压效果的视觉表现

// 关键效果:
// 1. 恐惧冷汗粒子
// 2. 身体颤抖效果 (通过修改SpriteRenderer的位置偏移实现)
// 3. 黑暗笼罩效果 (高等级威压)
// 4. 地面压痕效果
```

---

## AI行为交互

### 6.1 威压相关AI行为

#### BehReleasePressure

主动释放威压的行为。

```csharp
// Source/Content/Behaviours/BehReleasePressure.cs
// 使用场景:
// 1. 遇到敌人时主动释放威压
// 2. 保护同伴时释放威压
// 3. 威慑低境界修士

// 决策因素:
// - 当前灵力是否充足
// - 敌人数量和强度
// - 是否有需要保护的对象
```

#### BehResistPressure

抵抗威压的行为。

```csharp
// Source/Content/Behaviours/BehResistPressure.cs
// 使用场景:
// 1. 被威压时尝试抵抗
// 2. 使用法宝或丹药抵抗
// 3. 寻找安全区域

// 决策因素:
// - 当前威压等级
// - 自身抵抗能力
// - 逃跑路径是否可行
```

#### BehFleePressure

威压逃跑行为。

```csharp
// Source/Content/Behaviours/BehFleePressure.cs
// 当威压等级达到恐惧或以上时触发

// 行为逻辑:
// 1. 计算威压来源方向
// 2. 向相反方向逃跑
// 3. 寻找掩体或友军
```

### 6.2 Job修改

需要修改现有的修仙者Job，加入威压相关行为。

```csharp
// 在 XianJob 中添加威压相关行为节点
// 位置: Source/Content/ActorJobs/...

// 优先级建议:
// 1. 检测是否被高等级威压 -> 逃跑
// 2. 检测是否可以威压敌人 -> 释放威压
// 3. 正常战斗/修炼行为
```

---

## 技术实现

### 7.1 实现步骤

#### 第一阶段: 基础框架 ✅ (已完成)

1. ✅ 创建 `RealmVisual` 组件
2. ✅ 创建 `RealmVisualManager` 管理器（替代原计划的资源库）
3. ✅ 创建配置文件加载逻辑
4. ✅ 在 `ActorExtend` 中注册组件初始化（通过 `RegisterActionOnUpdateStats`）

**注意**: `RealmPressure` 组件暂缓实现（威压系统待开发）

#### 第二阶段: 视觉系统 ✅ (已完成)

1. ✅ 实现 `RealmAuraRenderSystem` 基础光晕渲染
2. ✅ 实现呼吸动画效果（基于正弦波的缩放和透明度变化）
3. ✅ 实现元素粒子系统（使用Unity ParticleSystem优化性能）
4. ✅ 实现金丹/元婴虚影渲染（支持强度透明度，兼容28x28图标）

#### 第三阶段: 威压系统 ⏸️ (暂缓)

1. ⏸️ 实现威压值计算逻辑
2. ⏸️ 实现威压效果判定
3. ⏸️ 实现威压状态效果 (减益)
4. ⏸️ 实现威压视觉效果

**状态**: 根据需求，威压系统暂时不实现

#### 第四阶段: AI交互 ⏸️ (暂缓)

1. ⏸️ 实现威压相关AI行为
2. ⏸️ 修改现有Job添加威压逻辑
3. ⏸️ 测试和调优

**状态**: 待威压系统实现后开发

#### 第五阶段: 资源制作 🔄 (部分完成)

1. ✅ 制作光晕贴图 (5个境界) - 已通过Python脚本自动生成
2. ⏸️ 制作元素粒子贴图 (5种元素) - 待制作专用贴图
3. ⏸️ 制作威压效果贴图 - 待威压系统实现
4. ✅ 制作金丹/元婴虚影贴图 - 已完成（28x28）

### 7.2 性能优化

#### 渲染优化

```csharp
// 1. 使用LOD系统
// 远距离时降低效果复杂度
float distance = Vector3.Distance(Camera.main.transform.position, actor.position);
if (distance > 50) {
    // 简化效果
    particleCount /= 2;
    auraQuality = Low;
}

// 2. 使用对象池
// 所有渲染对象都使用 MonoObjPool 管理

// 3. 批量渲染
// 相同材质的光晕使用批处理

// 4. 视锥裁剪
// 不在视野内的不渲染 (已有 is_visible 检查)
```

#### 逻辑优化

```csharp
// 1. 威压计算分帧
// 不是每帧都计算所有威压关系
private float _pressure_update_timer = 0;
private const float PRESSURE_UPDATE_INTERVAL = 0.5f;

// 2. 空间分区
// 使用格子系统快速查找范围内的单位
// 可复用现有的 tile 系统

// 3. 缓存计算结果
// 威压值在属性改变时才重新计算
```

### 7.3 配置开关

在 `default_config.json` 中添加:

```json
{
    "RealmVisual": {
        "enabled": true,
        "aura_enabled": true,
        "particles_enabled": true,
        "indicator_enabled": true,
        "quality": "high",
        "max_visible_auras": 50
    },
    "RealmPressure": {
        "enabled": true,
        "passive_pressure_enabled": true,
        "pressure_visual_enabled": true,
        "friendly_fire_enabled": false
    }
}
```

---

## 资源需求

### 8.1 贴图资源

| 资源名称 | 尺寸 | 数量 | 说明 |
|---------|-----|-----|------|
| 光晕贴图 | 64x64 | 5 | 各境界基础光晕 |
| 元素光晕 | 64x64 | 5 | 五行元素特色光晕 |
| 元素粒子 | 16x16 | 5x3 | 每种元素3帧动画 |
| 金丹虚影 | 32x32 | 5 | 不同类型金丹 |
| 元婴虚影 | 32x48 | 5 | 不同类型元婴 |
| 威压效果 | 32x32 | 4 | 各等级威压视觉 |
| 冷汗粒子 | 8x8 | 3 | 恐惧效果粒子 |
| 突破特效 | 128x128 | 8 | 突破动画序列帧 |

### 8.2 文件结构

```
GameResources/cultiway/special_effects/
├── aura/
│   ├── qi_aura.png
│   ├── foundation_aura.png
│   ├── jindan_aura.png
│   ├── yuanying_aura.png
│   ├── huashen_aura.png
│   └── sprites.json
├── element_particles/
│   ├── iron/
│   ├── wood/
│   ├── water/
│   ├── fire/
│   ├── earth/
│   └── sprites.json
├── realm_indicator/
│   ├── jindan/
│   ├── yuanying/
│   └── sprites.json
├── pressure/
│   ├── fear_sweat.png
│   ├── dark_aura.png
│   ├── ground_crack.png
│   └── sprites.json
└── breakthrough/
    ├── breakthrough_0.png
    ├── ...
    └── sprites.json
```

---

## 开发TODO

### 🔴 高优先级

#### Phase 1: 组件与数据结构

- [x] **TODO-RV-001**: 创建 `RealmVisual` 组件 ✅
  - 文件: `Source/Content/Components/RealmVisual.cs`
  - 参考: `Source/Content/Components/Xian.cs`
  - 状态: 已完成，包含定义索引、境界阶段、视觉状态、标识标志等字段
  
- [ ] **TODO-RV-002**: 创建 `RealmPressure` 组件
  - 文件: `Source/Content/Components/RealmPressure.cs`
  - 包含威压值、抵抗值、范围等字段
  - 状态: 暂缓（威压系统待实现）

- [ ] **TODO-RV-003**: 创建 `PressureRelation` 关系组件
  - 文件: `Source/Core/Components/PressureRelation.cs`
  - 参考: `Source/Core/Components/StatusRelation.cs`
  - 状态: 暂缓（威压系统待实现）

- [x] **TODO-RV-004**: 创建 `RealmVisualDefinition` 资源类 ✅
  - 文件: `Source/Content/RealmVisual/RealmVisualManager.cs` (内部类)
  - 包含各境界的视觉参数配置
  - 状态: 已完成，使用内部类实现

- [x] **TODO-RV-005**: 创建 `RealmVisualManager` 管理器 ✅
  - 文件: `Source/Content/RealmVisual/RealmVisualManager.cs`
  - 参考: `Source/Content/Libraries/JindanLibrary.cs`
  - 状态: 已完成，负责配置加载、组件更新、资源管理

#### Phase 2: 渲染系统

- [x] **TODO-RV-006**: 实现 `RealmAuraRenderSystem` ✅
  - 文件: `Source/Content/Systems/Render/RealmAuraRenderSystem.cs`
  - 参考: `Source/Content/Systems/Render/CloudRenderSystem.cs`
  - 功能: 基础光晕渲染、呼吸动画
  - 状态: 已完成，支持动态透明度和呼吸动画

- [x] **TODO-RV-007**: 实现 `RealmElementParticleRenderSystem` ✅
  - 文件: `Source/Content/Systems/Render/RealmElementParticleRenderSystem.cs`
  - 功能: 五行元素粒子效果
  - 状态: 已完成，使用Unity ParticleSystem实现，参考RenderStatusParticleSystem

- [x] **TODO-RV-008**: 实现 `RealmIndicatorRenderSystem` ✅
  - 文件: `Source/Content/Systems/Render/RealmIndicatorRenderSystem.cs`
  - 功能: 金丹虚影、元婴虚影渲染
  - 状态: 已完成，支持根据强度调整不透明度，兼容28x28图标

- [x] **TODO-RV-009**: 将渲染系统注册到 `GeneralRenderSystems` ✅
  - 文件: `Source/Content/Manager.cs`
  - 参考现有系统注册方式
  - 状态: 已完成，所有渲染系统已注册

### 🟠 中优先级

#### Phase 3: 威压逻辑

- [ ] **TODO-RV-010**: 实现威压值计算逻辑
  - 文件: `Source/Content/Extensions/PressureExtend.cs`
  - 包含 `CalculatePressureValue()` 和 `CalculateResistanceValue()`

- [ ] **TODO-RV-011**: 实现 `PressureUpdateSystem`
  - 文件: `Source/Content/Systems/Logic/PressureUpdateSystem.cs`
  - 功能: 定期更新威压关系、计算有效威压

- [ ] **TODO-RV-012**: 实现威压效果状态
  - 文件: `Source/Content/StatusEffects.cs` (扩展)
  - 添加 Intimidated, Feared, PressureCollapse, Crushed 状态

- [ ] **TODO-RV-013**: 实现威压效果应用
  - 文件: `Source/Content/Systems/Logic/PressureEffectSystem.cs`
  - 功能: 根据有效威压等级应用对应减益效果

- [ ] **TODO-RV-014**: 实现 `PressureVisualRenderSystem`
  - 文件: `Source/Content/Systems/Render/PressureVisualRenderSystem.cs`
  - 功能: 渲染恐惧冷汗、身体颤抖等视觉效果

#### Phase 4: 组件初始化与同步

- [x] **TODO-RV-015**: 在 `RealmVisualManager` 中注册视觉组件初始化 ✅
  - 文件: `Source/Content/RealmVisual/RealmVisualManager.cs`
  - 通过 `RegisterActionOnUpdateStats` 在属性更新时自动更新 `RealmVisual` 组件
  - 状态: 已完成，境界变化时自动同步视觉组件

- [ ] **TODO-RV-016**: 在 `ActorExtend` 中添加威压相关方法
  - 文件: `Source/Core/ActorExtend.cs`
  - 添加 `GetPressureValue()`, `GetResistanceValue()`, `ApplyPressure()` 等
  - 状态: 暂缓（威压系统待实现）

- [ ] **TODO-RV-017**: 修改 `OnUpdateStats` 添加威压属性同步
  - 文件: `Source/Content/Cultisyses.cs`
  - 在属性更新时重新计算威压相关值
  - 状态: 暂缓（威压系统待实现）

### 🟡 低优先级

#### Phase 5: AI行为

- [ ] **TODO-RV-018**: 实现 `BehReleasePressure` 行为
  - 文件: `Source/Content/Behaviours/BehReleasePressure.cs`
  - 功能: 主动释放威压

- [ ] **TODO-RV-019**: 实现 `BehResistPressure` 行为
  - 文件: `Source/Content/Behaviours/BehResistPressure.cs`
  - 功能: 尝试抵抗威压

- [ ] **TODO-RV-020**: 实现 `BehFleePressure` 行为
  - 文件: `Source/Content/Behaviours/BehFleePressure.cs`
  - 功能: 威压逃跑

- [ ] **TODO-RV-021**: 修改修仙者Job添加威压行为节点
  - 文件: `Source/Content/ActorJobs/...`
  - 在战斗决策中加入威压判断

#### Phase 6: 配置与UI

- [x] **TODO-RV-022**: 创建配置文件 ✅
  - 文件: `Content/RealmVisual/realm_visual_config.json`
  - 包含各境界视觉参数配置
  - 状态: 已完成，包含所有境界的视觉参数

- [x] **TODO-RV-023**: 添加配置开关到 `default_config.json` ✅
  - 文件: `default_config.json`
  - 添加视觉效果系统的开关
  - 状态: 已完成，包含 REALM_VISUAL_ENABLED, AURA_ENABLED, PARTICLE_ENABLED, INDICATOR_ENABLED

- [x] **TODO-RV-023-1**: 添加本地化文本 ✅
  - 文件: `Locales/config.csv`
  - 添加境界视觉表现设置的本地化文本
  - 状态: 已完成

- [ ] **TODO-RV-024**: 在角色信息窗口显示威压信息
  - 文件: `Source/UI/CreatureInfoPages/...`
  - 添加威压值显示
  - 状态: 暂缓（威压系统待实现）

### 🟢 资源制作

- [x] **TODO-RV-025**: 制作光晕贴图 (5个境界) ✅
  - 文件: `Scripts/generate_aura_sprites.py`
  - 状态: 已完成，已创建Python脚本自动生成所有境界光晕贴图
  - 生成文件: `qi_aura.png`, `foundation_aura.png`, `jindan_aura.png`, `yuanying_aura.png`, `huashen_aura.png`
  - 尺寸: 128x128，支持自定义尺寸和颜色

- [ ] **TODO-RV-026**: 制作元素粒子贴图 (5种元素)
  - 状态: 待制作，当前使用通用粒子贴图

- [x] **TODO-RV-027**: 制作金丹虚影贴图 ✅
  - 文件: `GameResources/cultiway/special_effects/aura/jindan_indicator.png`
  - 状态: 已完成，28x28尺寸

- [x] **TODO-RV-028**: 制作元婴虚影贴图 ✅
  - 文件: `GameResources/cultiway/special_effects/aura/yuanying_indicator.png`
  - 状态: 已完成，28x28尺寸

- [ ] **TODO-RV-029**: 制作威压效果贴图
  - 状态: 暂缓（威压系统待实现）

- [ ] **TODO-RV-030**: 制作突破特效序列帧
  - 状态: 待制作

---

## 附录

### A. 颜色常量参考

```csharp
// Source/Content/Const/RealmColors.cs
public static class RealmColors
{
    // 境界光晕颜色
    public static readonly Color QiRefining = new Color(1f, 1f, 1f, 0.15f);
    public static readonly Color Foundation = new Color(0.53f, 0.81f, 0.92f, 0.25f);
    public static readonly Color Jindan = new Color(1f, 0.84f, 0f, 0.35f);
    public static readonly Color Yuanying = new Color(0.58f, 0.44f, 0.86f, 0.45f);
    public static readonly Color Huashen = new Color(1f, 1f, 1f, 0.55f);
    
    // 元素颜色
    public static readonly Color Iron = new Color(1f, 0.84f, 0f, 1f);      // 金
    public static readonly Color Wood = new Color(0.13f, 0.55f, 0.13f, 1f); // 木
    public static readonly Color Water = new Color(0.25f, 0.41f, 0.88f, 1f); // 水
    public static readonly Color Fire = new Color(1f, 0.27f, 0f, 1f);       // 火
    public static readonly Color Earth = new Color(0.55f, 0.27f, 0.07f, 1f); // 土
}
```

### B. 相关现有代码路径

| 功能 | 文件路径 |
|-----|---------|
| 境界组件 | `Source/Content/Components/Xian.cs` |
| 金丹组件 | `Source/Content/Components/Jindan.cs` |
| 元婴组件 | `Source/Content/Components/Yuanying.cs` |
| 境界系统 | `Source/Content/Cultisyses.cs` |
| 力量等级 | `Source/Core/Components/PowerLevel.cs` |
| 云渲染参考 | `Source/Content/Systems/Render/CloudRenderSystem.cs` |
| 动画渲染参考 | `Source/Core/Systems/Render/RenderAnimFrameSystem.cs` |
| 状态效果参考 | `Source/Core/Libraries/StatusEffectAsset.cs` |
| 对象池 | `Source/Abstract/MonoObjPool.cs` |
| 颜色工具 | `Source/Utils/ColorUtils.cs` |

### C. 预估工时

| 阶段 | 任务 | 预估时间 |
|-----|------|---------|
| Phase 1 | 组件与数据结构 | 2天 |
| Phase 2 | 渲染系统 | 3天 |
| Phase 3 | 威压逻辑 | 3天 |
| Phase 4 | 组件初始化 | 1天 |
| Phase 5 | AI行为 | 2天 |
| Phase 6 | 配置与UI | 1天 |
| 资源制作 | 贴图制作 | 2天 |
| **合计** | | **14天** |

---

---

## 实现状态总结

### ✅ 已完成功能（境界视觉表现系统）

#### 核心组件与管理器
- ✅ `RealmVisual` 组件 - 存储视觉状态
- ✅ `RealmVisualManager` - 配置管理和组件同步
- ✅ `RealmVisualDefinition` - 视觉参数定义

#### 渲染系统
- ✅ `RealmAuraRenderSystem` - 光晕渲染（支持呼吸动画）
- ✅ `RealmElementParticleRenderSystem` - 元素粒子渲染（使用ParticleSystem）
- ✅ `RealmIndicatorRenderSystem` - 境界标识渲染（支持强度透明度）

#### 配置与资源
- ✅ 配置文件 `realm_visual_config.json`
- ✅ 配置开关 `default_config.json`
- ✅ 本地化文本 `Locales/config.csv`
- ✅ 颜色常量 `RealmColors.cs`
- ✅ 光晕生成脚本 `Scripts/generate_aura_sprites.py`
- ✅ 光晕贴图资源（5个境界，128x128）

#### 系统集成
- ✅ 渲染系统注册到 `GeneralRenderSystems`
- ✅ 组件自动初始化与同步
- ✅ 支持配置开关控制

### ⏸️ 待实现功能（境界威压系统）

威压系统相关功能暂缓实现，包括：
- `RealmPressure` 组件
- `PressureRelation` 关系组件
- 威压值计算与应用
- 威压效果状态
- 威压相关AI行为
- 威压视觉效果

### 📝 待完善功能

- 元素粒子专用贴图（当前使用通用贴图）
- 突破特效序列帧
- 威压信息UI显示（待威压系统实现后）

---

**文档维护**: 请在开发过程中同步更新此文档  
**最后更新**: 2025年11月（境界视觉表现系统已完成）

