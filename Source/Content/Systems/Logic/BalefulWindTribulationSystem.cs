using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进当前世界中正在运行的煞风劫会话。</summary>
public sealed class BalefulWindTribulationSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        BalefulWindTribulationSkillService.UpdateAll();
    }
}
