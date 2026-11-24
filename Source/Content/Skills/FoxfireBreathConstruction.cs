using System;
using System.IO;
using System.Linq;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>活着的真实头部与狐火腺同时可用，才允许从吻端喷吐。</summary>
internal static class FoxfireBreathConstruction
{
    /// <summary>共同火息资料从内容目录读取，发射不改写其动作与几何。</summary>
    private static readonly Lazy<AbilityDefinition> Definition = new(() =>
        JsonConvert.DeserializeObject<AbilityDefinition>(File.ReadAllText(Path.Combine(
            ModClass.I.GetDeclaration().FolderPath, "Content/Skills/Attack/FoxfireBreath.json"))));

    /// <summary>在实际器官受理前拒绝缺少出口或尚未朝向目标的请求。</summary>
    /// <param name="caster">当前有实际肉身的来源。</param><param name="target">本次预定目标。</param>
    internal static bool CanBreathe(ActorExtend caster, BaseSimObject target) =>
        TryMuzzle(caster, out var origin, out var forward) && AimsAt(origin, forward, target.GetSimPos());

    /// <summary>在已受理的真正发射时重新取样，保持当前妖力支付和来源归属。</summary>
    /// <param name="emission">已到实际出手时点的步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        if (emission.SkillContainer.GetComponent<SkillContainer>().Asset != SkillEntities.FoxfireBreath)
            return SkillEmissionResult.Unhandled;
        if (emission.Sourceless || emission.Source?.isActor() != true || emission.Source != emission.SpatialSource ||
            emission.Source.isRekt()) return SkillEmissionResult.Rejected;
        ActorExtend caster = emission.Source.a.GetExtend();
        if (!TryMuzzle(caster, out var mouth, out var forward) || !AimsAt(mouth, forward, emission.TargetPos))
            return SkillEmissionResult.Rejected;
        var damage = new GetHitEvent
        {
            Damage = emission.Strength, Element = ElementComposition.Static.Fire, AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        damage.BindAttacker(emission.Source);
        var target = new ActiveAbilityTarget(emission.Target, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(caster, Definition.Value, target, mouth, emission.Payment,
            damage, direction: forward, spatialSource: emission.SpatialSource, sourceIsWorld: true,
            reportPreview: emission.ReportPreview)
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>按实际头部三角面的顶点定位下部前缘。装饰性的雷角和装备表面不参与选择。</summary>
    /// <param name="caster">当前有实际肉身的来源。</param>
    /// <param name="point">绝对世界坐标中的真实吻端顶点。</param>
    /// <param name="forward">这帧实际身体朝向。</param>
    internal static bool TryMuzzle(ActorExtend caster, out Vector3 point, out Vector3 forward)
    {
        point = forward = default;
        Actor actor = caster.Base;
        if (actor.isRekt() || !actor.isAlive() || !caster.TryGetComponent(out CreaturePhenotype phenotype) ||
            !phenotype.IsValid || !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex,
                phenotype.Signature, out var current) ||
            !current.OrderedOrgans.Any(organ => organ.Organ.id == "yao.gland.foxfire")) return false;
        return BodyCastOutlets.TryGet(actor, CreatureOrganBodyOutlet.Mouth, out point, out forward);
    }

    /// <summary>喷口与真实朝向保持一致，不能向已经在脑后的目标发火。</summary>
    private static bool AimsAt(Vector3 mouth, Vector3 forward, Vector3 target)
    {
        Vector3 planar = target - mouth;
        planar.z = 0f;
        return Vector3.Dot(planar.normalized, forward) > .68f;
    }
}
