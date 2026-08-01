using ai.behaviours;
using Cultiway.Core;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

/// <summary>认领或重新校验集体工程，并由具体执行器设置角色应前往的执行地块。</summary>
public sealed class BehPrepareCollectiveProject : BehaviourActionActor
{
    private readonly bool acquireEmergency;

    /// <summary>创建常规工程重新准备行为，或创建会先认领应急工程的行为。</summary>
    public BehPrepareCollectiveProject(bool acquireEmergency)
    {
        this.acquireEmergency = acquireEmergency;
    }

    /// <summary>确保角色拥有一个可执行项目，并把失败认领转换为任务停止。</summary>
    public override BehResult execute(Actor pActor)
    {
        ActorExtend actor = pActor.GetExtend();
        bool prepared = acquireEmergency
            ? CollectiveProjectService.TryAcquireEmergencyProject(actor)
            : CollectiveProjectService.TryPrepareAssignedProject(actor);
        return prepared ? BehResult.Continue : BehResult.Stop;
    }
}

/// <summary>把已经到达执行位置的角色行动提交给项目定义所绑定的执行器。</summary>
public sealed class BehExecuteCollectiveProject : BehaviourActionActor
{
    /// <summary>提交实际行动；失败时服务会释放认领，行为树随即结束当前任务。</summary>
    public override BehResult execute(Actor pActor)
    {
        return CollectiveProjectService.TryExecuteAssignedProject(pActor.GetExtend())
            ? BehResult.Continue
            : BehResult.Stop;
    }
}
