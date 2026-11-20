using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Utils.Extension;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>同一份普通构造可由已学法术或封存它的真实符箓发射。</summary>
internal static class SealRelayConstruction
{
    /// <summary>原连发及可映射到普通构造的设置。</summary>
    private static readonly HashSet<Type> DeliveryModifiers = new()
    {
        typeof(Trajectory), typeof(BurstCount), typeof(SalvoCount), typeof(ComboModifier), typeof(VolleyModifier),
        typeof(EmpowerModifier), typeof(HugeModifier), typeof(HasteModifier), typeof(ProficiencyModifier)
    };

    /// <summary>实际运行独立复制，原始资料可安全复用。</summary>
    private static readonly Lazy<AbilityDefinition> Authored = new(() =>
        JsonConvert.DeserializeObject<AbilityDefinition>(File.ReadAllText(Path.Combine(
            ModClass.I.GetDeclaration().FolderPath, "Content/Skills/Attack/SealRelay.json"))));

    /// <summary>保留实际来源、目标、费用及本次伤害，交给共同运行。</summary>
    /// <param name="emission">已经到达出手时点的实际封存或已学请求。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        if (skill.GetComponent<SkillContainer>().Asset != SkillEntities.SealRelay) return SkillEmissionResult.Unhandled;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        foreach (Type type in skill.GetComponentTypes())
            if (typeof(IModifier).IsAssignableFrom(type) && !DeliveryModifiers.Contains(type) &&
                !ContactEffectMigration.Supports(type)) return SkillEmissionResult.Rejected;
        if (!NatureConstructionMigration.Supports(skill) || !ContactEffectMigration.MatchesCallbacks(skill) ||
            !ContactEffectMigration.MatchesTravelCallbacks(skill)) return SkillEmissionResult.Rejected;

        var caster = emission.Source.a.GetExtend();
        AbilityDefinition definition = ConstructionEditing.Copy(Authored.Value);
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        var firstFlight = definition.Root.Operations[1];
        bool straight = trajectory == SkillTrajectories.TowardsDirection.id || trajectory == SkillTrajectories.TowardsDirectionNoRot.id;
        if (straight) firstFlight.Goal = null;
        else if (trajectory == SkillTrajectories.TowardsTarget.id) firstFlight.Goal.Follow = true;
        else if (trajectory != SkillTrajectories.TowardsPosition.id) return SkillEmissionResult.Rejected;

        // 实际尺寸及时间都按同一份外部设置展开，选敌距离与缩放一起变。
        float effect = emission.RuntimeData.ResolveEffectScale();
        float area = effect, power = 1f, time = 1f;
        bool enabled = true;
        if (skill.TryGetComponent(out SkillSettings settings))
        {
            area *= settings.AreaMultiplier;
            power = settings.PowerMultiplier;
            if (settings.Duration > 0f) time = settings.Duration / definition.Lifetime;
            enabled = settings.ContactEnabled;
        }
        float strength = ContactEffectMigration.SetupStrengthScale(skill, out float angle, out float speed);
        if (skill.TryGetComponent(out EmpowerModifier empower)) power *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) power *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        if (skill.HasComponent<HugeModifier>()) area *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (skill.TryGetComponent(out HasteModifier haste)) speed *= Mathf.Clamp(1f + haste.SpeedMultiplier, .1f, 10f);
        if (!(area > 0f && area <= 12f && time >= .20f && time <= 20f)) return SkillEmissionResult.Rejected;
        definition.Lifetime *= time;
        Tune(definition.Root, area, time, speed, enabled);

        var damage = new GetHitEvent
        {
            Damage = emission.Strength * effect * power * strength,
            Element = NatureConstructionMigration.Element(skill, emission.RuntimeData),
            AttackType = AttackType.Other, AttackerPowerLevel = emission.PowerLevel,
            DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        damage.BindAttacker(emission.Source);
        if (enabled)
            ContactEffectMigration.Bind(definition, skill, effect, caster: caster, runtimeData: emission.RuntimeData);
        var target = new ActiveAbilityTarget(emission.Target, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        Vector3 direction = emission.TargetPos - emission.SourcePos;
        direction.z = 0f;
        direction = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees + angle, Vector3.forward) * direction;
        return ConstructedAbilityService.StartPrepaid(caster, definition, target, emission.SourcePos, emission.Payment,
            damage, direction, spatialSource: emission.SpatialSource) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>沿普通部位同步展开体积、时点和飞行速度，关闭接触时只保留首张纸。</summary>
    /// <param name="node">原资料复制的部位。</param><param name="area">同一几何尺度。</param>
    /// <param name="time">世界时间倍率。</param><param name="speed">运动速度倍率。</param>
    /// <param name="contact">当前容器是否允许实际接触。</param>
    private static void Tune(ConstructionNode node, float area, float time, float speed, bool contact)
    {
        node.Scale *= area;
        node.DisappearAt *= time;
        if (node.Activation != null)
        {
            node.Activation.Delay *= time;
            if (node.Activation.Target != null) node.Activation.Target.Radius *= area;
        }
        foreach (var operation in node.Operations)
        {
            operation.At *= time;
            operation.Seconds *= time;
            if (operation.Kind == ConstructionOperationKind.Fly) operation.Vector[2] *= speed / time;
        }
        if (!contact)
        {
            node.ContactRole = ConstructionContactRole.Decoration;
            node.Children = Array.Empty<ConstructionNode>();
        }
        foreach (var child in node.Children) Tune(child, area, time, speed, contact);
    }
}
