using System;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>保持原人物资源与归属，仅把允许技能的出生位置改为聚焦节点。</summary>
public static class YuanshenNodeCastingService
{
    /// <summary>防止技能出生位置解析器重复注册。</summary>
    private static bool initialized;

    /// <summary>注册节点施法位置解析器。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        SkillCastPlanner.RegisterCastValidator(CanCastFromFocusedNode);
        SkillCastPlanner.RegisterSourcePlacementResolver(ResolveSkillPlacement);
    }

    /// <summary>显圣投影聚焦时只允许非敌对、非攻击型技能。</summary>
    /// <param name="caster">技能归属人物。</param>
    /// <param name="skill">准备释放的技能。</param>
    /// <returns>当前聚焦不受显圣限制，或技能属于护持用途时返回真。</returns>
    public static bool CanCastFromFocusedNode(ActorExtend caster, Entity skill)
    {
        if (caster != null && caster.HasComponent<YuanshenBodilessTransitState>()) return false;
        if (caster == null ||
            !YuanshenThoughtService.TryGetFocused(caster, out YuanshenNodeHandle handle, out _) ||
            !YuanshenNodeLockService.TryResolve(handle, out Entity node)) return true;
        if (!AllowsSkillFromNode(skill, node)) return false;
        return skill.IsNull || !skill.TryGetComponent(out SkillContainer container) ||
               container.Asset?.CarrierRequirement != SkillCarrierRequirement.PhysicalBody ||
               !IsReady(caster, node);
    }

    /// <summary>显圣投影聚焦时拒绝带任何进攻用途的法器主动能力。</summary>
    /// <param name="caster">法器能力归属人物。</param>
    /// <param name="profile">法器能力用途画像。</param>
    /// <returns>当前未聚焦显圣，或能力没有进攻权重时返回真。</returns>
    public static bool CanUseArtifactFromFocusedNode(ActorExtend caster, in ArtifactUseProfile profile)
    {
        if (caster == null || caster.HasComponent<YuanshenBodilessTransitState>()) return false;
        if (!YuanshenThoughtService.TryGetFocused(caster, out YuanshenNodeHandle handle, out _) ||
            !YuanshenNodeLockService.TryResolve(handle, out Entity node) ||
            !node.TryGetComponent(out YuanshenAdvancedNodeState advanced) || !advanced.support_only) return true;
        return profile.offensive <= 0f;
    }

    /// <summary>判断技能是否符合当前节点的护持限制。</summary>
    /// <param name="skill">准备释放的技能。</param>
    /// <param name="node">已经解析的聚焦节点。</param>
    /// <returns>节点没有限制，或技能不是进攻用途时返回真。</returns>
    private static bool AllowsSkillFromNode(Entity skill, Entity node)
    {
        if (!node.TryGetComponent(out YuanshenAdvancedNodeState advanced) || !advanced.support_only) return true;
        if (skill.IsNull || !skill.TryGetComponent(out SkillContainer container)) return false;
        SkillEntityAsset asset = container.Asset;
        return asset != null && asset.Type != SkillEntityType.Attack && asset.UseProfile != null &&
               asset.UseProfile.TargetRelation != SkillUseTargetRelation.Hostile;
    }

    /// <summary>已学技能发射时保留原节点及它的当前位置。</summary>
    /// <param name="caster">技能资源、冷却和归属人物。</param>
    /// <param name="skill">准备释放的技能容器。</param>
    /// <returns>合法的节点来源，条件不符时沿用普通出手。</returns>
    private static SkillCastPlacement? ResolveSkillPlacement(ActorExtend caster, Entity skill)
    {
        if (caster == null || caster.HasComponent<YuanshenBodilessTransitState>() || skill.IsNull ||
            !skill.TryGetComponent(out SkillContainer container) ||
            container.Asset?.CarrierRequirement == SkillCarrierRequirement.PhysicalBody ||
            !YuanshenThoughtService.TryGetFocused(caster, out YuanshenNodeHandle handle, out Vector2 position) ||
            !YuanshenNodeLockService.TryResolve(handle, out Entity node) ||
            !IsReady(caster, node) ||
            !AllowsSkillFromNode(skill, node))
            return null;
        return new SkillCastPlacement(SpellPosition(position), TrackNode(handle));
    }

    /// <summary>按实际作用检查显圣权限，再决定构造是否借用当前聚焦节点。</summary>
    /// <param name="context">归属和人物载体，借来的实际人物优先。</param>
    /// <param name="plan">当前构造。</param>
    /// <param name="handle">有效的原节点，缺省时用人物。</param>
    /// <param name="position">节点的实际起手位置。</param>
    internal static bool TryResolveConstruction(in SkillCasterContext context, CompiledAbility plan,
        out YuanshenNodeHandle handle, out Vector3 position)
    {
        handle = default;
        position = default;
        ActorExtend caster = context.Owner;
        if (context.Carrier != caster ||
            !YuanshenThoughtService.TryGetFocused(caster, out YuanshenNodeHandle focused, out Vector2 point) ||
            !YuanshenNodeLockService.TryResolve(focused, out Entity node)) return true;
        if (node.TryGetComponent(out YuanshenAdvancedNodeState advanced) && advanced.support_only &&
            (plan.Impact.Targets == SkillUseTargetRelation.Hostile || plan.ActorImpacts.Any(Harms))) return false;
        if (!IsReady(caster, node)) return true;
        if (plan.Body != null) return false;
        handle = focused;
        position = SpellPosition(point);
        return true;
    }

    /// <summary>显圣的护持要求对实际作用生效，部位覆盖也会被检查。</summary>
    /// <param name="impact">可接触人物的已编译作用。</param>
    private static bool Harms(CompiledConstructionImpact impact) =>
        impact.Targets == SkillUseTargetRelation.Hostile || impact.DamageScale > 0f || impact.HasAgingEffect ||
        impact.Force != 0f || impact.Impulse.HasValue || impact.Launch || impact.Knockback?.HasEffect == true ||
        impact.StaggerSeconds > 0f || impact.BindSeconds > 0f || impact.StrainSeconds > 0f || impact.ExposureSeconds > 0f ||
        impact.AdditionalDamage.Count > 0 ||
        impact.RandomContact?.Outcomes.Any(Harms) == true ||
        impact.ConditionalContacts.Any(rule => Harms(rule.Outcome) || Harms(rule.Otherwise)) ||
        impact.Spreads.Any(spread => Harms(spread.Outcome));

    /// <summary>检查接触分支是否会主动攻击。</summary>
    /// <param name="outcome">实际分支结果。</param>
    private static bool Harms(CompiledConstructionContactOutcome outcome) =>
        outcome != null && (outcome.Damage.Count > 0 || outcome.Knockback?.HasEffect == true);

    /// <summary>共用原元神层数、完整度与心神余量条件。</summary>
    /// <param name="caster">技能归属人物。</param>
    /// <param name="node">已经解析的当前节点。</param>
    private static bool IsReady(ActorExtend caster, Entity node)
    {
        if (!caster.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 4 ||
            !node.TryGetComponent(out YuanshenNodeState state) || state.IntegrityRatio < 0.35f) return false;
        DivineSenseBudget budget = DivineSenseBudgetService.Resolve(caster);
        return budget.AutomaticPreparedLimit >= budget.TotalLoadCapacity * 0.1f;
    }

    /// <summary>让已发出的法术跟随原节点。聚焦切换时始终读取受理的那一个。</summary>
    /// <param name="handle">原节点所属会话及代次。</param>
    internal static Func<Vector3?> TrackNode(YuanshenNodeHandle handle) => () =>
        YuanshenNodeLockService.TryResolve(handle, out Entity node) &&
        node.TryGetComponent(out YuanshenNodeState state) && state.IntegrityRatio > 0f &&
        node.TryGetComponent(out Position position) ? SpellPosition(position.v2) : null;

    /// <summary>节点施法沿用原本相对地面的高度。</summary>
    /// <param name="position">实际节点地面位置。</param>
    private static Vector3 SpellPosition(Vector2 position) => new(position.x, position.y, 0.35f);
}
