using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public sealed class LogicSkillVfxTravelSystem : QuerySystem<Position, Rotation, SkillVfxRuntime>
{
    public LogicSkillVfxTravelSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var now = Tick.time;
        Query.ForEachComponents((ref Position pos, ref Rotation rot, ref SkillVfxRuntime runtime) =>
        {
            ModClass.I.SkillV3.Vfx.QueueTrail(ref pos, ref rot, ref runtime, now);
        });
    }
}
