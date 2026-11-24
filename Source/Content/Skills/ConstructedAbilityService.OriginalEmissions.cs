using System;
using System.Collections.Generic;
using Cultiway.Content.WeaponControl;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>内容登记完成后为每项原来源绑定唯一的正式发射。失败直接交还此发的费用。</summary>
    private static Func<SkillEmission, SkillEmissionResult> BindOriginalEmissions()
    {
        var handlers = new Dictionary<SkillEntityAsset, Func<SkillEmission, SkillEmissionResult>>
        {
            { WeaponControlSkills.WeaponControl, WeaponControlSkills.Emit },
            { SkillEntities.FoxfireBreath, FoxfireBreathConstruction.Emit },
            { SkillEntities.SealRelay, SealRelayConstruction.Emit },
            { SkillEntities.GroundSpike, GroundSpikeConstructionMigration.Emit },
            { SkillEntities.Rift, RiftConstructionMigration.Emit },
            { SkillEntities.ChainLightning, ChainConstructionMigration.Emit }
        };
        foreach (SkillEntityAsset asset in ModClass.I.SkillV3.SkillLib.list)
        {
            if (UtilityConstructionMigration.Supports(asset)) handlers.Add(asset, UtilityConstructionMigration.Emit);
            if (ProjectileConstructionMigration.Supports(asset)) handlers.Add(asset, ProjectileConstructionMigration.Emit);
            if (FieldConstructionMigration.Supports(asset)) handlers.Add(asset, FieldConstructionMigration.Emit);
            if (BarrierConstructionMigration.Supports(asset)) handlers.Add(asset, BarrierConstructionMigration.Emit);
            if (SupportConstructionMigration.Supports(asset)) handlers.Add(asset, SupportConstructionMigration.Emit);
            if (BeamConstructionMigration.Supports(asset)) handlers.Add(asset, BeamConstructionMigration.Emit);
        }

        foreach (Entity skill in CoreFormationSkills.RegisteredContainers)
        {
            SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
            if (FormationSelfConstruction.Supports(skill)) handlers.Add(asset, FormationSelfConstruction.Emit);
            if (FormationAreaConstruction.Supports(skill)) handlers.Add(asset, FormationAreaConstruction.Emit);
            if (FormationCueConstruction.Supports(skill)) handlers.Add(asset, FormationCueConstruction.Emit);
            if (FormationTargetConstruction.Supports(skill)) handlers.Add(asset, FormationTargetConstruction.Emit);
        }

        return emission => handlers.TryGetValue(emission.SkillContainer.GetComponent<SkillContainer>().Asset,
            out var handler) ? handler(emission) : SkillEmissionResult.Rejected;
    }
}
