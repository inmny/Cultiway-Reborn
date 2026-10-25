using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>先收集待结束施放，再于遍历之外释放本体关系；原物不进入删除路径。</summary>
public sealed class ReleaseSkillExecutionBodiesSystem : QuerySystem<SkillExecution>
{
    /// <summary>本轮等待释放的实际施放。</summary>
    private readonly List<Entity> ending = new();

    /// <summary>只处理已经进入统一回收的执行体。</summary>
    public ReleaseSkillExecutionBodiesSystem() => Filter.AllTags(Tags.Get<TagRecycle>());

    /// <summary>收集后再改动关系及本体，避免回收过程修改正在遍历的实体集合。</summary>
    protected override void OnUpdate()
    {
        ending.Clear();
        Query.ForEachEntity((ref SkillExecution _, Entity execution) => ending.Add(execution));
        foreach (Entity execution in ending) SkillExecutionLifecycle.ReleaseBodies(execution);
        ending.Clear();
    }
}