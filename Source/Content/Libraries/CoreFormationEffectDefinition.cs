using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>核心形成效果能够接收的运行时事件。</summary>
[Flags]
public enum CoreFormationEffectTrigger : ushort
{
    /// <summary>不接收自动事件。</summary>
    None = 0,

    /// <summary>持有者对目标造成最终伤害后触发。</summary>
    DamageDealt = 1 << 0,

    /// <summary>持有者承受最终伤害前触发，可修改伤害。</summary>
    FinalDamageIncoming = 1 << 1,

    /// <summary>持有者承受最终伤害后触发。</summary>
    DamageTaken = 1 << 2,

    /// <summary>持有者完成一次技能施放序列后触发。</summary>
    SkillCastCompleted = 1 << 3,

    /// <summary>持有者完成击杀后触发。</summary>
    Kill = 1 << 4,

    /// <summary>由固定逻辑帧推进资源池、延迟恢复和持续形态。</summary>
    Tick = 1 << 5,
}

/// <summary>传递给核心形成效果处理委托的具体事件类型。</summary>
public enum CoreFormationEffectEventKind : byte
{
    /// <summary>造成最终伤害。</summary>
    DamageDealt,

    /// <summary>最终伤害修正。</summary>
    FinalDamageIncoming,

    /// <summary>承受最终伤害。</summary>
    DamageTaken,

    /// <summary>技能施放序列完成。</summary>
    SkillCastCompleted,

    /// <summary>完成击杀。</summary>
    Kill,

    /// <summary>固定逻辑帧推进。</summary>
    Tick,
}

/// <summary>核心形成特效信号所对应的表现阶段。</summary>
public enum CoreFormationVisualChannel : byte
{
    /// <summary>效果在施法者身上触发。</summary>
    Trigger,

    /// <summary>持续状态或印记被施加到受影响目标。</summary>
    Apply,

    /// <summary>效果在受影响目标处命中。</summary>
    Hit,

    /// <summary>效果进入蓄力或资源充盈状态。</summary>
    Charge,

    /// <summary>主动形态开始。</summary>
    Activate,

    /// <summary>权威状态存续期间的循环表现。</summary>
    Loop,

    /// <summary>主动形态或蓄力状态结束。</summary>
    End,

    /// <summary>保命或重生被成功结算。</summary>
    Rebirth,
}

/// <summary>帧动画在世界中的空间跟随方式。</summary>
public enum CoreFormationVisualMotion : byte
{
    /// <summary>固定在触发点。</summary>
    Stationary,

    /// <summary>跟随效果持有者。</summary>
    FollowOwner,

    /// <summary>跟随受影响目标。</summary>
    FollowTarget,

    /// <summary>从持有者向目标做直线移动。</summary>
    Linear,

    /// <summary>在触发点逐步放大。</summary>
    Expand,

    /// <summary>在触发点逐步缩小。</summary>
    Contract,
}

/// <summary>单个核心形成帧动画通道的资源和播放参数。</summary>
public sealed class CoreFormationEffectVisualCue
{
    /// <summary>按帧命名的资源目录。</summary>
    public string path;

    /// <summary>世界渲染缩放。</summary>
    public float scale = 0.1f;

    /// <summary>相邻帧的播放间隔。</summary>
    public float frame_interval = 0.1f;

    /// <summary>循环实例的最长兜底寿命。</summary>
    public float life_time = 1f;

    /// <summary>空间跟随方式。</summary>
    public CoreFormationVisualMotion motion;

    /// <summary>是否允许帧序列自身循环。</summary>
    public bool loop;

    /// <summary>是否始终保持贴图竖直，不继承移动方向。</summary>
    public bool fixed_upright = true;

    /// <summary>可选的统一染色。</summary>
    public Color tint = Color.white;

    /// <summary>是否应用统一染色。</summary>
    public bool use_tint;
}

/// <summary>一个效果在触发、命中、激活和循环阶段使用的表现资源。</summary>
public sealed class CoreFormationEffectVisualProfile
{
    /// <summary>效果在施法者处触发时的表现。</summary>
    public CoreFormationEffectVisualCue trigger;

    /// <summary>持续状态或印记施加到目标时的表现。</summary>
    public CoreFormationEffectVisualCue apply;

    /// <summary>效果在目标处命中时的表现。</summary>
    public CoreFormationEffectVisualCue hit;

    /// <summary>进入蓄力或资源充盈状态时的表现。</summary>
    public CoreFormationEffectVisualCue charge;

    /// <summary>主动形态开始时的表现。</summary>
    public CoreFormationEffectVisualCue activate;

    /// <summary>权威状态存续期间的循环表现。</summary>
    public CoreFormationEffectVisualCue loop;

    /// <summary>主动形态结束时的表现。</summary>
    public CoreFormationEffectVisualCue end;

    /// <summary>保命或重生成功时的表现。</summary>
    public CoreFormationEffectVisualCue rebirth;

    /// <summary>取得指定表现阶段的播放配置。</summary>
    public CoreFormationEffectVisualCue Get(CoreFormationVisualChannel channel)
    {
        return channel switch
        {
            CoreFormationVisualChannel.Trigger => trigger,
            CoreFormationVisualChannel.Apply => apply,
            CoreFormationVisualChannel.Hit => hit,
            CoreFormationVisualChannel.Charge => charge,
            CoreFormationVisualChannel.Activate => activate,
            CoreFormationVisualChannel.Loop => loop,
            CoreFormationVisualChannel.End => end,
            CoreFormationVisualChannel.Rebirth => rebirth,
            _ => null,
        };
    }
}

/// <summary>主动核心形成能力的固定消耗、作用范围和执行委托。</summary>
public sealed class CoreFormationActiveProfile
{
    /// <summary>主动能力显示名称的本地化键。</summary>
    public string name_key;

    /// <summary>主动能力图标路径。</summary>
    public string icon_path;

    /// <summary>每次成功释放扣除的固定灵气。</summary>
    public float wakan_cost;

    /// <summary>主动形态的持续秒数；瞬发能力为零。</summary>
    public float duration;

    /// <summary>成功释放后的冷却秒数。</summary>
    public float cooldown;

    /// <summary>可选择目标的最远距离。</summary>
    public float range;

    /// <summary>落点处实际影响半径。</summary>
    public float radius;

    /// <summary>统一主动能力系统使用的目标模式。</summary>
    public ActiveAbilityTargetMode target_mode;

    /// <summary>统一主动能力系统使用的激活模式。</summary>
    public ActiveAbilityActivationMode activation_mode = ActiveAbilityActivationMode.Instant;

    /// <summary>通过基本资格检查后参与 AI 抽取的权重。</summary>
    public int ai_weight = 20;

    /// <summary>判断当前战斗环境是否值得准备该能力。</summary>
    internal CoreFormationActivePrepareAction CanPrepare;

    /// <summary>执行能力并写回运行时状态。</summary>
    internal CoreFormationActiveUseAction Use;

    /// <summary>取得主动能力的本地化显示名称。</summary>
    public string GetName()
    {
        return !string.IsNullOrEmpty(name_key) && LM.Has(name_key) ? LM.Get(name_key) : name_key;
    }
}

/// <summary>一次核心形成效果事件携带的运行时数据。</summary>
public sealed class CoreFormationEffectEvent
{
    /// <summary>事件类型。</summary>
    public CoreFormationEffectEventKind Kind;

    /// <summary>攻击者、受击者或其他事件关联对象。</summary>
    public BaseSimObject Other;

    /// <summary>当前最终伤害；最终伤害阶段允许处理器改写。</summary>
    public float Damage;

    /// <summary>伤害元素构成。</summary>
    public ElementComposition Composition;

    /// <summary>原版攻击类型。</summary>
    public AttackType AttackType;

    /// <summary>已完成施放的技能容器。</summary>
    public Entity SkillContainer;

    /// <summary>本次技能序列实际发射数量。</summary>
    public int EmittedCount;

    /// <summary>本次技能序列的资源出资方式。</summary>
    public SkillCastFundingSource FundingSource;

    /// <summary>本次逻辑推进的秒数。</summary>
    public float DeltaTime;

    /// <summary>当前同步伤害链是否属于二次反应伤害。</summary>
    public bool IsReaction;
}

/// <summary>效果定义、原子贡献和最终倍率组成的不可变解析结果。</summary>
public readonly struct CoreFormationResolvedEffect
{
    /// <summary>被选中的效果定义。</summary>
    public readonly CoreFormationEffectDefinition Definition;

    /// <summary>提供该效果的形成原子。</summary>
    public readonly CoreFormationAtomAsset Atom;

    /// <summary>原子在角色快照中的贡献状态。</summary>
    public readonly CoreFormationAtomState AtomState;

    /// <summary>经过境界、品质、强度和原子权重约束后的效果倍率。</summary>
    public readonly float Potency;

    /// <summary>创建一个完整的核心形成效果解析结果。</summary>
    public CoreFormationResolvedEffect(
        CoreFormationEffectDefinition definition,
        CoreFormationAtomAsset atom,
        CoreFormationAtomState atomState,
        float potency)
    {
        Definition = definition;
        Atom = atom;
        AtomState = atomState;
        Potency = potency;
    }

    /// <summary>根据定义基础概率和倍率计算本次实际触发概率。</summary>
    public float ProcChance => Mathf.Min(Definition.max_chance,
        Definition.base_chance * Mathf.Sqrt(Potency));
}

/// <summary>处理一次被动或状态推进事件的委托。</summary>
public delegate void CoreFormationEffectHandler(
    in CoreFormationResolvedEffect effect,
    ActorExtend owner,
    ref CoreFormationEffectRuntimeEntry runtime,
    CoreFormationEffectEvent evt);

/// <summary>判断 AI 是否应准备一个主动形成能力的委托。</summary>
public delegate bool CoreFormationActivePrepareAction(
    in CoreFormationResolvedEffect effect,
    ActorExtend owner,
    in CoreFormationEffectRuntimeEntry runtime,
    BaseSimObject target);

/// <summary>执行一个主动形成能力的委托。</summary>
public delegate bool CoreFormationActiveUseAction(
    in CoreFormationResolvedEffect effect,
    ActorExtend owner,
    ref CoreFormationEffectRuntimeEntry runtime,
    in ActiveAbilityTarget target,
    ActiveAbilityUseOrigin origin);

/// <summary>由形成原子提供、可按效果族合并升级的一项实际机制。</summary>
public sealed class CoreFormationEffectDefinition
{
    /// <summary>跨原子合并状态和执行升级覆盖的稳定效果族 ID。</summary>
    public string family_id;

    /// <summary>同一效果族中的覆盖等级，数值较高者生效。</summary>
    public int rank = 1;

    /// <summary>该定义接收的自动事件集合。</summary>
    public CoreFormationEffectTrigger triggers;

    /// <summary>触发判定的基础概率。</summary>
    public float base_chance;

    /// <summary>倍率修正后的概率硬上限。</summary>
    public float max_chance = 1f;

    /// <summary>普通触发成功后写入的内部冷却秒数。</summary>
    public float cooldown;

    /// <summary>计算权重贡献时采用的分类基准值。</summary>
    public float reference_weight = 1f;

    /// <summary>效果显示名称的本地化键。</summary>
    public string name_key;

    /// <summary>效果详细说明的本地化键。</summary>
    public string description_key;

    /// <summary>最终伤害规则所属的固定执行阶段。</summary>
    public FinalDamageStage final_damage_stage;

    /// <summary>效果的帧动画与粒子表现配置。</summary>
    public CoreFormationEffectVisualProfile visual;

    /// <summary>存在时把该效果暴露为统一主动能力。</summary>
    public CoreFormationActiveProfile active;

    /// <summary>处理自动事件的委托。</summary>
    internal CoreFormationEffectHandler Handle;

    /// <summary>取得效果的本地化显示名称。</summary>
    public string GetName()
    {
        return !string.IsNullOrEmpty(name_key) && LM.Has(name_key) ? LM.Get(name_key) : family_id;
    }

    /// <summary>取得效果的本地化基础说明。</summary>
    public string GetDescription()
    {
        return !string.IsNullOrEmpty(description_key) && LM.Has(description_key)
            ? LM.Get(description_key)
            : string.Empty;
    }
}
