using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts.ActiveAbilities;

/// <summary>符箓与卷轴锁定制作时的法术版次，由成品持有自己的封存容器。</summary>
internal static class ConsumableSpellSnapshot
{
    /// <summary>保留轨迹、词条及效果，复制可独立编辑的资源需求。</summary>
    /// <param name="learned">制作者已经掌握的原法术。</param>
    internal static Entity Capture(Entity learned)
    {
        Entity sealedSpell = learned.Store.CloneEntity(learned);
        ref var container = ref sealedSpell.GetComponent<SkillContainer>();
        container.CastResourceRequirement = container.CastResourceRequirement?.DeepClone();
        if (sealedSpell.Tags.Has<TagOccupied>()) sealedSpell.RemoveTag<TagOccupied>();
        return sealedSpell;
    }
}
