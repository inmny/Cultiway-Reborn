using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Visuals.Forms;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.CreatureCompositions.ActiveAbilities;

/// <summary>把器官提供的现有法术放在相应身体部位；不增加新的爪击或尾击伤害。</summary>
internal static class CreatureOrganCastingPosition
{
    /// <summary>每次施放重新读取当前身体与动画尺寸，换形、换器官后不会沿用旧挂点。</summary>
    /// <param name="caster">真实拥有肉身的施法者；离体载体由原有解析器处理。</param>
    /// <param name="container">实际施放的来源容器。</param>
    internal static Vector3? Resolve(ActorExtend caster, Entity container)
    {
        if (!caster.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid ||
            !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature,
                out CompiledCreaturePhenotype compiled)) return null;
        if (container.IsNull || !container.TryGetComponent(out SkillContainer skill)) return null;
        string skillId = skill.SkillEntityAssetID;
        if (!compiled.TryGetSkillSource(skillId, out var selected) ||
            !CreatureOrganSkillRegistry.TryGetContainer(skillId, out Entity source) || source != container)
            return null;
        string layerId = selected.Organ.Rank.CastingLayerId;
        if (!string.IsNullOrEmpty(layerId))
            return CreatureAppearanceService.TryGetAnchor(compiled, caster.Base, layerId, "cast", out Vector3 point)
                ? point : null;
        if (!TryResolveWorld(caster.Base, compiled, selected.Organ, out Vector3 world)) return null;
        world.z -= ConstructedAbilityService.BodyTerrain?.Sample(world.x, world.y) ?? 0f;
        return world;
    }

    /// <summary>使用真正可见的制作点，或当前共用身体表面。坐标已包含地表和实际承托。</summary>
    internal static bool TryResolveWorld(Actor actor, CompiledCreaturePhenotype body, in CompiledCreatureOrgan source,
        out Vector3 point)
    {
        string layerId = source.Rank.CastingLayerId;
        if (string.IsNullOrEmpty(layerId))
            return BodyCastOutlets.TryGet(actor, source.Rank.BodyOutlet, out point, out _);
        if (BodyOrganBinding.ModelProvider == null)
        {
            if (!CreatureAppearanceService.TryGetAnchor(body, actor, layerId, "cast", out point)) return false;
            point.z += ConstructedAbilityService.BodyFoot(actor).z - actor.position_height;
            return true;
        }

        point = default;
        BodySurfacePose pose = ConstructedAbilityService.BodyCastingSurface(actor);
        if (pose == null) return false;
        string organId = "organ:" + source.Organ.id;
        for (int i = 0; i < pose.Parts.Length; i++)
        {
            BodySurfacePart part = pose.Parts[i];
            if (part.Id != organId || part.Triangles.Length == 0) continue;
            var layer = Content.Libraries.Manager.CreatureVisualLayerLibrary.get(layerId);
            BodyOrganModel model = BodyOrganBinding.ModelProvider(actor, layer);
            if (model == null) return false;
            foreach (FormAnchorBinding anchor in model.Resource.Anchors)
            {
                if (anchor.Id != "cast") continue;
                Matrix4x4 matrix = pose.Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
                point = matrix.MultiplyPoint3x4(anchor.Frame().GetColumn(3));
                return true;
            }
            return false;
        }
        return false;
    }
}
