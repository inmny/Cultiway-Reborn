using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicCheckCasterSystem : QuerySystem<SkillCaster>
{
    public LogicCheckCasterSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagRecycle>());
    }
    protected override void OnUpdate()
    {
        var cmd_buf = CommandBuffer;
        Query.ForEachEntity(((ref SkillCaster caster, Entity entity) =>
        {
            if (caster.value == null || caster.value.E.IsNull || caster.AsActor == null || !caster.AsActor.isAlive())
            {
                cmd_buf.AddTag<TagRecycle>(entity.Id);
            }
        }));
        cmd_buf.Playback();
    }
}