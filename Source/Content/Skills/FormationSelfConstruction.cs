using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;

using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>从修炼来源的真实出手开始自身法相，状态继续由修炼规则维护。</summary>
internal static class FormationSelfConstruction
{
    internal static bool Supports(Entity skill) => ShapeFor(skill).Family != null;

    private static (string Family, string form, string matter, float height, float scale, float spin) ShapeFor(Entity skill) => skill switch
    {
        var entry when entry == CoreFormationSkills.SwordEmbryoAura =>
            (CoreFormationEffectFamilies.Sword, "sword_curtain", "iron", .21f, .55f, 84f),
        var entry when entry == CoreFormationSkills.SpiritPlatform =>
            (CoreFormationEffectFamilies.Spiritual, "spirit_aegis", "spirit", .66f, .8f, -56f),
        var entry when entry == CoreFormationSkills.PrimalBody =>
            (CoreFormationEffectFamilies.Body, "guard_seal", "earth", .12f, 1.15f, 0f),
        var entry when entry == CoreFormationSkills.FivePhase =>
            (CoreFormationEffectFamilies.FivePhase, "five_phase_mantle", "earth", .05f, .85f, 42f),
        _ => default
    };

    /// <summary>将授予容器的真实出手转换成绑定施法者的法相启动。</summary>
    /// <param name="emission">原主动能力已经确认的单次出手。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        (string family, string form, string matter, float height, float scale, float spin) = ShapeFor(skill);
        if (family == null) return SkillEmissionResult.Rejected;
        if (emission.Sourceless || emission.Source.isRekt() || !emission.Source.isActor() ||
            !emission.Source.a.isAlive() || emission.Source != emission.Target ||
            !TryResolve(emission.Source.a.GetExtend(), family, out CoreFormationResolvedEffect effect) ||
            effect.Definition.active.SkillContainer != skill)
            return SkillEmissionResult.Rejected;

        var definition = new AbilityDefinition
        {
            Id = skill.GetComponent<SkillContainer>().Asset.id,
            Name = effect.Definition.active.GetName(), Resource = "none", Cost = 0f, Cooldown = 0f, Lifetime = .62f,
            // 修炼者是自身状态的归属，离身载体与肉身的距离不改变自身能力的受理。
            TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat, Target = ActiveAbilityTargetMode.Self },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, Follow = true, Height = height },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Self, DamageScale = 0f },
            Root = new ConstructionNode
            {
                Id = "manifestation", Form = form, Matter = matter, Scale = scale, Volume = 0f,
                ContactRole = ConstructionContactRole.Decoration, ActiveFrom = .08f, ActiveUntil = .25f,
                Operations = new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .16f },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f, 0f, spin } },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .30f,
                        When = new ConstructionCondition
                            { Trigger = ConstructionChangeTrigger.SizeAbove, Threshold = .99f, Delay = .14f },
                        TargetScale = .01f }
                }
            }
        };
        var target = new ActiveAbilityTarget(emission.Target, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        var force = new GetHitEvent { AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin };
        force.BindAttacker(emission.Source);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target, emission.SourcePos,
            emission.Payment, force, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
            formationFamily: family,
            reportPreview: emission.ReportPreview) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>只解析当前修炼成果仍然授予的自身效果。</summary>
    /// <param name="owner">实际修炼者。</param><param name="family">原授予的效果族。</param>
    /// <param name="effect">当前有效的主动效果。</param>
    internal static bool TryResolve(ActorExtend owner, string family, out CoreFormationResolvedEffect effect) =>
        CoreFormationEffectResolver.TryResolveFamily(owner, family, out effect) &&
        effect.Definition.active?.target_mode == ActiveAbilityTargetMode.Self;
}
