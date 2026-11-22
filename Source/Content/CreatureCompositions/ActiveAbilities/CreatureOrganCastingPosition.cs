using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Components;
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
        Actor actor = caster.Base;
        string layerId = selected.Organ.Rank.CastingLayerId;
        if (!string.IsNullOrEmpty(layerId))
            return CreatureAppearanceService.TryGetAnchor(compiled, actor, layerId, "cast", out Vector3 point)
                ? point : null;
        AnimationFrameData frame = actor.getAnimationFrameData();
        float scale = Mathf.Max(.03f, actor.actor_scale);
        float width = Mathf.Max(4f, frame?.size_unit.x ?? 6f) * scale;
        float height = Mathf.Max(4f, frame?.size_unit.y ?? 6f) * scale;
        float facing = actor.is_looking_left ? -1f : 1f;
        string slot = selected.Organ.Entry.SlotId;
        // 尚未声明制作出口的器官继续原来的部位近似，不冒称已经完成实际挂点接入。
        float forward = slot == YaoContent.Slots.Tail ? -.42f :
            slot == YaoContent.Slots.Head || slot == YaoContent.Slots.Breath ? .40f : .05f;
        float elevation = slot == YaoContent.Slots.Tail ? .3f :
            slot == YaoContent.Slots.Limbs ? .25f : .55f;
        return new Vector3(actor.current_position.x + facing * width * forward,
            actor.current_position.y, actor.position_height + height * elevation);
    }
}
