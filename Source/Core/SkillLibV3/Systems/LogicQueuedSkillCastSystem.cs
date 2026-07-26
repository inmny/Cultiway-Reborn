using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>在无 ECS 查询锁的技能阶段启动来自战斗回调的标准施法请求。</summary>
public sealed class LogicQueuedSkillCastSystem : BaseSystem
{
    /// <summary>让技能管理器消费当前已经提交的全部请求。</summary>
    protected override void OnUpdateGroup()
    {
        ModClass.I.SkillV3.FlushQueuedSkillSequences();
    }
}
