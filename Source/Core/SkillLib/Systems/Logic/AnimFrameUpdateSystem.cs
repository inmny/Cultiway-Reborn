using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class AnimFrameUpdateSystem : QuerySystem<SkillAnimData, SkillEntityComponent>
{
    private readonly ArchetypeQuery<SkillAnimData, OverAnimFrames, SkillEntityComponent> over_frames_query;

    public AnimFrameUpdateSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<PrefabTag>());
        over_frames_query = world.Query<SkillAnimData, OverAnimFrames, SkillEntityComponent>(Filter);
    }

    protected override void OnUpdate()
    {
        Query.WithoutAllComponents(ComponentTypes.Get<OverAnimFrames>());
        Query.ForEachEntity((ref SkillAnimData        anim_data,
                             ref SkillEntityComponent skill_entity_asset, Entity e) =>
        {
            var anim_setting = skill_entity_asset.asset.anim_setting;
            if (anim_setting == null) return;
            anim_data.timer += Tick.deltaTime;
            if (anim_data.timer < anim_setting.interval) return;
            anim_data.idx++;
            anim_data.timer -= anim_setting.interval;
            if (anim_data.idx < anim_setting.frames.Length) return;
            if (e.HasComponent<AnimLoopEndTrigger>())
            {
                e.GetComponent<AnimLoopEndTrigger>().loop_times++;
            }

            if (!anim_setting.loop)
            {
                anim_data.idx--;
                return;
            }

            anim_data.idx = 0;
        });
        over_frames_query.ForEachEntity((ref SkillAnimData        anim_data,          ref OverAnimFrames over_frames,
                                         ref SkillEntityComponent skill_entity_asset, Entity             e) =>
        {
            SkillEntityAsset.AnimSetting anim_setting = skill_entity_asset.asset.anim_setting;
            if (anim_setting == null) return;
            anim_data.timer += Tick.deltaTime;
            if (anim_data.timer < anim_setting.interval) return;
            anim_data.idx++;
            anim_data.timer -= anim_setting.interval;
            if (anim_data.idx < over_frames.frames.Length) return;
            if (e.HasComponent<AnimLoopEndTrigger>()) e.GetComponent<AnimLoopEndTrigger>().loop_times++;

            if (!anim_setting.loop)
            {
                anim_data.idx--;
                return;
            }

            anim_data.idx = 0;
        });
    }
}