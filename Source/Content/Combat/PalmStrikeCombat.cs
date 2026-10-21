using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.KnightCombat;
using Cultiway.Core;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>独立的原地徒手掌击，使用原版攻击恢复及近战伤害，不替换所有徒手普攻。</summary>
internal sealed class PalmStrikeCombat : BaseSystem, IActiveAbilityProvider, IWorldStateClearable
{
    /// <summary>主动能力的稳定来源编号。</summary>
    internal const string ProviderId = "content.palm_strike";
    /// <summary>单次动作的世界秒数。</summary>
    internal const float Duration = 0.4f;
    /// <summary>收掌及前送后发生接触的世界秒数。</summary>
    internal const float ContactAt = 0.18f;
    /// <summary>由同一逻辑系统拥有的动作，可供两个视角读取。</summary>
    internal static readonly Dictionary<long, Action> Actions = new();
    /// <summary>本次更新结束后才移除的动作编号，避免改动遍历中的集合。</summary>
    private readonly List<long> finished = new();
    /// <summary>统一主动能力来源。</summary>
    string IActiveAbilityProvider.Id => ProviderId;

    /// <summary>徒手人形角色获得独立掌击入口，装备武器时隐藏。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="output">已有的能力集合。</param>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (GeneralSettings.EnableSkillSystems && HasBody(caster))
            output.Add(new ActiveAbilityHandle(ProviderId, caster.E));
    }

    /// <summary>此招只参与战斗。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) => ActiveAbilityChannel.Combat;

    /// <summary>提供现有能力栏所需的名称、目标和原地动作约束。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle) => new(
        "Cultiway.PalmStrike".Localize(), SpriteTextureLoader.getSprite("cultiway/icons/skills/knight/repulse"),
        ActiveAbilityChannel.Combat, ActiveAbilityTargetMode.Object, ActiveAbilityActivationMode.Instant,
        ActiveAbilityCastMobility.StationaryDuringRecovery);

    /// <summary>徒手攻击共用原版恢复时间，不另设一份技能冷却。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!HasBody(caster) || Actions.ContainsKey(caster.Base.getID()))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        return caster.Base.attack_timer > 0f
            ? new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Cooldown, caster.Base.attack_timer)
            : ActiveAbilityControlState.Ready;
    }

    /// <summary>受理时检查身体、公共攻击恢复和近战动作条件。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">原目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        handle.ProviderId == ProviderId && handle.Source == caster.E && GeneralSettings.EnableSkillSystems &&
        HasBody(caster) && caster.Base.isAttackPossible() &&
        !Actions.ContainsKey(caster.Base.getID()) && !KnightTechniqueRuntimeService.IsBusy(caster);

    /// <summary>只接受手臂实际可达的活体敌人。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">此次选择。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanPrepare(caster, handle, target.Object) && CanContact(caster.Base, target.Object);

    /// <summary>在近身时以普通攻击尺度参与现有动作选择。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">原目标。</param>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 5;

    /// <summary>按一次徒手普攻向战术层报告威力，无额外资源和范围效果。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">原目标。</param>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(ActorExtend caster, ActiveAbilityHandle handle,
        BaseSimObject target) => new(1f, 0f, 0f, 0f, caster.Base.stats[S.damage], 0f, 1f);

    /// <summary>手臂可达距离加上目标身体的近侧表面，受原版近战上限约束。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">用于确定身体表面的目标。</param>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        Reach(caster.Base) + (target?.isActor() == true ? SpatialActorPose.Height(target.a) * 0.22f : 0f);

    /// <summary>单目标接触没有范围预览。</summary>
    /// <param name="caster">当前角色。</param>
    /// <param name="handle">能力句柄。</param>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;

    /// <summary>只记录一次动作并启动公共恢复，不移动人物或立即造成伤害。</summary>
    /// <param name="caster">出掌者。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">锁定的原目标。</param>
    /// <param name="origin">玩家、脚本或自主选择。</param>
    public bool TryUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        Actor actor = caster.Base;
        Vector3 direction = target.Object.GetSimPos() - actor.GetSimPos();
        direction.z = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        Actions.Add(actor.getID(), new Action(caster, target.Object, direction, World.world.getCurWorldTime()));
        actor.startAttackCooldown();
        actor.attack_timer = Mathf.Max(Duration, actor.attack_timer);
        CombatWorldService.HoldMovementForAction(actor, Duration);
        actor.punchTargetAnimation(actor.GetSimPos() + direction, pAngle: -8f);
        return true;
    }

    /// <summary>沿世界时间先取消失效动作，再跨过唯一接触点结算，最后完成回收。</summary>
    protected override void OnUpdateGroup()
    {
        finished.Clear();
        double now = World.world.getCurWorldTime();
        foreach (var pair in Actions)
        {
            Action action = pair.Value;
            Actor actor = action.Caster.Base;
            float age = (float)(now - action.Started);
            if (!GeneralSettings.EnableSkillSystems || !HasBody(action.Caster))
            {
                if (!actor.isRekt()) actor.target_angle.z = 0f;
                finished.Add(pair.Key);
                continue;
            }
            float extension = Extension(age);
            actor.target_angle.z = age >= Duration ? 0f : Mathf.Lerp(-8f, 16f, extension);
            if (!action.Resolved && age >= ContactAt)
            {
                action.Resolved = true;
                Vector3 offset = !action.Target.isRekt() ? action.Target.GetSimPos() - actor.GetSimPos() : Vector3.zero;
                offset.z = 0f;
                if (CanContact(actor, action.Target) && Vector3.Dot(offset.normalized, action.Direction) >= 0.85f)
                {
                    action.Contact = SpatialActorPose.Hand(actor, action.Direction, Reach(actor));
                    action.AttemptCount++;
                    AttackDataResult result = KnightWeaponStrikeResolver.StrikeUnarmed(action.Caster, action.Target,
                        (_, damage) => { action.Damage = damage; action.FeedbackAt = World.world.getCurWorldTime(); },
                        1f);
                    action.Blocked = result.state == ApplyAttackState.Block || result.state == ApplyAttackState.Deflect;
                    if (action.Blocked) action.FeedbackAt = now;
                }
            }
            // 即使一个逻辑步跨过整个动作，也先处理唯一接触点，再结束动作。
            if (age >= Duration) finished.Add(pair.Key);
        }
        foreach (long id in finished) Actions.Remove(id);
    }

    /// <summary>手臂按体型缩放，并保留原版徒手攻击的距离上限。</summary>
    /// <param name="actor">出掌者。</param>
    internal static float Reach(Actor actor) => Mathf.Min(actor.getAttackRange(), SpatialActorPose.Height(actor) * 0.65f);

    /// <summary>蓄势、快速前送、短暂停势、回收共用的局部动作曲线。</summary>
    /// <param name="age">已经经过的世界时间。</param>
    internal static float Extension(float age)
    {
        if (age < 0.12f) return Mathf.Lerp(0.32f, 0.18f, age / 0.12f);
        if (age < ContactAt) return Mathf.Lerp(0.18f, 1f, 1f - Mathf.Pow(1f - (age - 0.12f) / 0.06f, 3f));
        if (age < 0.22f) return 1f;
        return Mathf.Lerp(1f, 0.32f, Mathf.SmoothStep(0f, 1f, (age - 0.22f) / 0.18f));
    }

    /// <summary>身体失效、飞起、受冻结或换装都会取消徒手动作。</summary>
    /// <param name="caster">实际肉身角色。</param>
    private static bool HasBody(ActorExtend caster) => caster != null && !caster.Base.isRekt() &&
        caster.Base.isAlive() && caster.Base.asset.is_humanoid && !caster.Base.hasWeapon() &&
        SkillCasterContextService.Resolve(caster).Kind == SkillCarrierKind.Physical &&
        KnightTechniqueRuntimeService.CanAct(caster);

    /// <summary>每次接触前重新检查距离和高度，不追踪逃离手掌的目标。</summary>
    /// <param name="actor">出掌者。</param>
    /// <param name="target">锁定的原目标。</param>
    private static bool CanContact(Actor actor, BaseSimObject target)
    {
        if (target.isRekt() || !target.isActor() || !target.isAlive() || target == actor ||
            target.a.isFlying() || !SkillTargetRelationResolver.IsHostile(actor, target)) return false;
        float radius = SpatialActorPose.Height(target.a) * 0.22f;
        return Vector2.Distance(actor.current_position, target.current_position) <= Reach(actor) + radius &&
               Mathf.Abs(SpatialActorPose.Foot(actor).z - SpatialActorPose.Foot(target.a).z) <= SpatialActorPose.Height(actor) * 0.4f;
    }

    /// <summary>世界切换即放弃尚未发生的接触，不带到新世界。</summary>
    void IWorldStateClearable.ClearWorldState() => Actions.Clear();

    /// <summary>一招的实际目标、方向与接触结果，两个视角读取同一份记录。</summary>
    internal sealed class Action
    {
        /// <summary>出掌者与原目标。</summary>
        internal readonly ActorExtend Caster;
        /// <summary>本次锁定的原目标。</summary>
        internal readonly BaseSimObject Target;
        /// <summary>受理时锁定的出掌方向。</summary>
        internal readonly Vector3 Direction;
        /// <summary>开始的世界时间。</summary>
        internal readonly double Started;
        /// <summary>一次接触是否已经尝试，包含挥空。</summary>
        internal bool Resolved;
        /// <summary>已经调用的正式近战结算次数，供实机检查。</summary>
        internal int AttemptCount;
        /// <summary>目标是否实际挡住或拨开。</summary>
        internal bool Blocked;
        /// <summary>正数最终伤害，不把未造成伤害的接触画成受伤。</summary>
        internal float Damage;
        /// <summary>接触位置的身体表面近似。</summary>
        internal Vector3 Contact;
        /// <summary>实际结果反馈时刻，未产生结果时保持负数。</summary>
        internal double FeedbackAt = -1d;

        /// <summary>建立一次动作，不保存精灵或外部模组对象。</summary>
        /// <param name="caster">出掌者。</param>
        /// <param name="target">原目标。</param>
        /// <param name="direction">锁定方向。</param>
        /// <param name="started">世界开始时间。</param>
        internal Action(ActorExtend caster, BaseSimObject target, Vector3 direction, double started)
        { Caster = caster; Target = target; Direction = direction; Started = started; }
    }
}
