using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 在通用回收链路之前将法术的首次回收请求转换为消散阶段。
/// </summary>
public sealed class PrepareSkillAnimationDissipationSystem :
    QuerySystem<SkillAnimationLifecycleState>
{
    private readonly List<Entity> _pending = new();

    public PrepareSkillAnimationDissipationSystem()
    {
        Filter.AllTags(Tags.Get<TagRecycle>());
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        _pending.Clear();
        Query.ForEachEntity((ref SkillAnimationLifecycleState _, Entity entity) => _pending.Add(entity));

        for (int i = 0; i < _pending.Count; i++)
        {
            Entity entity = _pending[i];
            if (entity.IsNull) continue;

            ref SkillAnimationLifecycleState state = ref entity.GetComponent<SkillAnimationLifecycleState>();
            if (state.Phase == SkillAnimationPhase.RecycleReady || !state.Animation.HasDissipation) continue;

            entity.RemoveTag<TagRecycle>();
            if (state.Phase != SkillAnimationPhase.Dissipation)
            {
                SkillAnimationLifecycle.EnterDissipation(entity);
            }
        }
    }
}
