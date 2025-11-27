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
    public LogicSkillTravelSystem()
    {
        Filter.AllTags(Tags.Get<TagHasOnTravel>());
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }
    
    protected override void OnUpdate()
    {
        Query.ForEach((skillEntities, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities.EntityAt(i);
                ref var skillEntity = ref skillEntities[i];
                skillEntity.SkillContainer.GetComponent<SkillContainer>().OnTravel?.Invoke(entity);
            }
        }).Run();
    }
}

