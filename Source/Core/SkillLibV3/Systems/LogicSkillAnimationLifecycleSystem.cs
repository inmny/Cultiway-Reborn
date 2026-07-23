using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 在非循环的出现或消散片段完整显示最后一帧后推进生命周期。
/// </summary>
public sealed class LogicSkillAnimationLifecycleSystem :
    QuerySystem<SkillAnimationLifecycleState, AnimData>
{
    private readonly List<Entity> _appearanceCompleted = new();
    private readonly List<Entity> _dissipationCompleted = new();

    public LogicSkillAnimationLifecycleSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
    }

    protected override void OnUpdate()
    {
        _appearanceCompleted.Clear();
        _dissipationCompleted.Clear();
        Query.ForEachEntity((ref SkillAnimationLifecycleState state, ref AnimData animData, Entity entity) =>
        {
            if (state.Phase is not (SkillAnimationPhase.Appearance or SkillAnimationPhase.Dissipation)) return;
            if (state.Phase == SkillAnimationPhase.Appearance && entity.Tags.Has<TagRecycle>()) return;

            float frameInterval = SkillAnimationLifecycle.ResolveCurrentFrameInterval(entity);
            if (animData.frame_idx != animData.frames.Length - 1 || animData.frame_timer < frameInterval) return;

            if (state.Phase == SkillAnimationPhase.Appearance)
            {
                _appearanceCompleted.Add(entity);
            }
            else
            {
                _dissipationCompleted.Add(entity);
            }
        });

        for (int i = 0; i < _appearanceCompleted.Count; i++)
        {
            Entity entity = _appearanceCompleted[i];
            if (entity.IsNull ||
                entity.GetComponent<SkillAnimationLifecycleState>().Phase != SkillAnimationPhase.Appearance) continue;
            SkillAnimationLifecycle.EnterRuntime(entity);
        }

        for (int i = 0; i < _dissipationCompleted.Count; i++)
        {
            Entity entity = _dissipationCompleted[i];
            if (entity.IsNull ||
                entity.GetComponent<SkillAnimationLifecycleState>().Phase != SkillAnimationPhase.Dissipation) continue;
            SkillAnimationLifecycle.MarkRecycleReady(entity);
        }
    }
}
