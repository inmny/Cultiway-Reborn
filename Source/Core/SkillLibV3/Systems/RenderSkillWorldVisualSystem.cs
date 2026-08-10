using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>消费结构化技能结果并更新池化世界法阵与局部反馈。</summary>
public sealed class RenderSkillWorldVisualSystem : BaseSystem, IWorldStateClearable
{
    void IWorldStateClearable.ClearWorldState()
    {
        SkillWorldVisualService.ClearWorldState();
    }

    protected override void OnUpdateGroup()
    {
        SkillWorldVisualRuntime.Update(Tick.deltaTime);
    }
}
