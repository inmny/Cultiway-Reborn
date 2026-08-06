using System.Collections.Generic;

namespace Cultiway.Core.Coordination;

/// <summary>领域会话每次更新后的状态。</summary>
public enum CoordinationSessionResult
{
    /// <summary>继续当前阶段。</summary>
    Continue,

    /// <summary>正常完成当前行动。</summary>
    Complete,

    /// <summary>以会话失败结束行动。</summary>
    Fail
}

/// <summary>单个参与者执行领域逻辑后的结果。</summary>
public enum CoordinationParticipantResult
{
    /// <summary>继续参加行动。</summary>
    Continue,

    /// <summary>主动离开当前行动，但不直接终止其他成员。</summary>
    Leave,

    /// <summary>当前成员失败会使整个行动失败。</summary>
    FailActivity
}

/// <summary>行动会话可使用的受限控制面。</summary>
public interface ICoordinatedActivityController
{
    /// <summary>当前行动的只读视图。</summary>
    CoordinatedActivityView View { get; }

    /// <summary>当前参与者状态是否满足定义中的全部到场门槛。</summary>
    bool MeetsReadinessRequirements { get; }

    /// <summary>为指定参与者设置或替换位置订单。</summary>
    bool SetPlacement(long actorId, in CoordinationPlacementOrder order);

    /// <summary>设置领域层附加的到场状态；位置要求仍由服务统一验证。</summary>
    bool SetDomainReady(long actorId, bool ready);

    /// <summary>从行动中释放一个参与者。</summary>
    bool RemoveParticipant(long actorId);
}

/// <summary>领域会话每次主线程更新时接收的上下文。</summary>
public readonly struct CoordinationUpdateContext
{
    /// <summary>创建更新上下文。</summary>
    internal CoordinationUpdateContext(ICoordinatedActivityController controller, double now)
    {
        Controller = controller;
        Now = now;
    }

    /// <summary>受限行动控制器。</summary>
    public ICoordinatedActivityController Controller { get; }

    /// <summary>当前世界时间。</summary>
    public double Now { get; }
}

/// <summary>领域会话处理单个参与者时接收的上下文。</summary>
public readonly struct CoordinationParticipantContext
{
    /// <summary>创建参与者执行上下文。</summary>
    internal CoordinationParticipantContext(
        CoordinatedActivityView activity,
        CoordinationParticipantView participant,
        Actor actor,
        bool placementReady,
        double now)
    {
        Activity = activity;
        Participant = participant;
        Actor = actor;
        PlacementReady = placementReady;
        Now = now;
    }

    /// <summary>当前行动视图。</summary>
    public CoordinatedActivityView Activity { get; }

    /// <summary>当前参与者视图。</summary>
    public CoordinationParticipantView Participant { get; }

    /// <summary>当前角色实时对象；只允许在主线程使用。</summary>
    public Actor Actor { get; }

    /// <summary>角色是否已经满足位置订单。</summary>
    public bool PlacementReady { get; }

    /// <summary>当前世界时间。</summary>
    public double Now { get; }
}

/// <summary>每类协调行动的强类型运行时策略。</summary>
public interface ICoordinatedActivitySession
{
    /// <summary>按席位提供候选成员；服务负责群组归属、冲突和人数上限。</summary>
    void CollectCandidates(
        in CoordinatedActivityView activity,
        CoordinationRoleDefinition role,
        IList<CoordinationCandidate> output);

    /// <summary>重新验证一个已经分配的参与者。</summary>
    bool IsParticipantValid(
        in CoordinatedActivityView activity,
        in CoordinationParticipantView participant,
        Actor actor);

    /// <summary>行动进入招募、集合或执行阶段后更新领域订单与内部计时。</summary>
    void OnStageChanged(in CoordinationUpdateContext context);

    /// <summary>按行动心跳执行领域级逻辑。</summary>
    CoordinationSessionResult Update(in CoordinationUpdateContext context);

    /// <summary>角色 AI 实际执行该行动时处理领域级成员逻辑。</summary>
    CoordinationParticipantResult TickParticipant(in CoordinationParticipantContext context);

    /// <summary>返回任务栏使用的本地化键。</summary>
    string ResolvePresentationLocaleKey(
        in CoordinatedActivityView activity,
        in CoordinationParticipantView participant);

    /// <summary>行动结束且成员已释放后接收最终结果。</summary>
    void OnEnded(in CoordinatedActivityResult result);
}
