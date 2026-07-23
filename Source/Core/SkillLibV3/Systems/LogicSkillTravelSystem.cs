using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 技能实体移动时调用OnTravel回调的系统
/// </summary>
public class LogicSkillTravelSystem : QuerySystem<SkillEntity>
{
    private readonly List<Entity> _pendingEntities = new();

    public LogicSkillTravelSystem()
    {
        Filter.AllTags(Tags.Get<TagHasOnTravel>());
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoTravelEffects>());
    }
    
    protected override void OnUpdate()
    {
        _pendingEntities.Clear();
        Query.ForEach((skillEntities, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities.EntityAt(i);
                ref var skillEntity = ref skillEntities[i];
                if (skillEntity.SkillContainer.GetComponent<SkillContainer>().OnTravel != null)
                {
                    _pendingEntities.Add(entity);
                }
            }
        }).Run();

        for (int i = 0; i < _pendingEntities.Count; i++)
        {
            Entity entity = _pendingEntities[i];
            if (entity.IsNull) continue;

            SkillEntity skillEntity = entity.GetComponent<SkillEntity>();
            skillEntity.SkillContainer.GetComponent<SkillContainer>().OnTravel?.Invoke(entity);
        }
    }
}

