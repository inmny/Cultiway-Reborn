using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
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
        SkillCastPlanner.RegisterSourcePositionResolver(ResolveSourcePosition);
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
        return AllowsSkillFromNode(skill, node);
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

    /// <summary>在元神至少四层且聚焦节点稳定时提供技能出生位置。</summary>
    /// <param name="caster">技能资源、冷却和归属人物。</param>
    /// <param name="skill">准备释放的技能容器。</param>
    /// <returns>合法节点坐标；条件不符时返回空。</returns>
    private static Vector3? ResolveSourcePosition(ActorExtend caster, Entity skill)
    {
        if (caster == null || caster.HasComponent<YuanshenBodilessTransitState>() || skill.IsNull ||
            !skill.HasComponent<SkillContainer>() ||
            !caster.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 4 ||
            !YuanshenThoughtService.TryGetFocused(caster, out YuanshenNodeHandle handle, out Vector2 position) ||
            !YuanshenNodeLockService.TryResolve(handle, out Entity node) ||
            !node.TryGetComponent(out YuanshenNodeIntegrity integrity) || integrity.Ratio < 0.35f ||
            !AllowsSkillFromNode(skill, node))
            return null;
        DivineSenseBudget budget = DivineSenseBudgetService.Resolve(caster);
        if (budget.AutomaticPreparedLimit < budget.TotalLoadCapacity * 0.1f) return null;
        return new Vector3(position.x, position.y, 0.35f);
    }
}
