using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 法器装备后的调度模式，决定自动调度器是否以及如何令其运转。
/// </summary>
public enum ArtifactEquipMode
{
    /// <summary>
    /// 由调度器根据场景、用途权重、优先级和神识负荷自动决定状态。
    /// </summary>
    Automatic,

    /// <summary>
    /// 仅保持装备与准备状态，不参与自动运转选择。
    /// </summary>
    Standby,

    /// <summary>
    /// 优先尝试运转，仍受分念容量和强制运转负荷上限约束。
    /// </summary>
    ForcedOperating,
}

/// <summary>
/// 法器在当前一轮调度后的实际控制状态。
/// </summary>
public enum ArtifactControlState
{
    /// <summary>
    /// 冷待命，不占用准备负荷或运转负荷。
    /// </summary>
    Cold,

    /// <summary>
    /// 已准备但未运转，仅占用准备负荷。
    /// </summary>
    Ready,

    /// <summary>
    /// 正常运转，占用运转负荷和相应分念。
    /// </summary>
    Operating,

    /// <summary>
    /// 在总负荷超过神识容量时继续运转。
    /// </summary>
    Overloaded,
}

/// <summary>
/// 法器自身的操控参数。专属法器可以直接覆盖这些值。
/// </summary>
public struct ArtifactControlProfile : IComponent
{
    /// <summary>
    /// 法器结构复杂度，是基础运转负荷的乘数。
    /// </summary>
    public float complexity;

    /// <summary>
    /// 准备负荷占运转负荷的比例。
    /// </summary>
    public float prepared_load_ratio;

    /// <summary>
    /// 运转时占用的分念数量。
    /// </summary>
    public int thread_cost;

    /// <summary>
    /// 是否能够自主运转；自主法器不占用角色分念。
    /// </summary>
    public bool autonomous;
}

/// <summary>
/// 法器能力向自动调度器提供的用途权重。该值由能力资产即时汇总，不作为实体组件保存。
/// </summary>
public struct ArtifactUseProfile
{
    /// <summary>
    /// 进攻用途权重。
    /// </summary>
    public float offensive;

    /// <summary>
    /// 防御用途权重。
    /// </summary>
    public float defensive;

    /// <summary>
    /// 辅助用途权重。
    /// </summary>
    public float support;

    /// <summary>
    /// 修炼用途权重。
    /// </summary>
    public float cultivate;

    /// <summary>
    /// 生产用途权重。
    /// </summary>
    public float production;
}

/// <summary>
/// 法器当前的祭炼归属和熟练程度。
/// </summary>
public struct ArtifactAttunement : IComponent
{
    /// <summary>
    /// 当前祭炼者的角色 ID。
    /// </summary>
    public long owner_actor_id;

    /// <summary>
    /// 当前祭炼熟练度，取值范围为 0 至 100。
    /// </summary>
    public float mastery;

    /// <summary>
    /// 是否已与祭炼者完成本命绑定。
    /// </summary>
    public bool life_bound;

    /// <summary>
    /// 是否禁止自动装备；手动重新装备会清除此标记。
    /// </summary>
    public bool auto_equip_disabled;
}

/// <summary>
/// 角色法器调度的最近一次结果。
/// </summary>
public struct ArtifactLoadoutState : IComponent
{
    /// <summary>
    /// 当前所有已准备法器占用的准备负荷总和。
    /// </summary>
    public float prepared_load;

    /// <summary>
    /// 当前所有运转法器占用的运转负荷总和。
    /// </summary>
    public float operating_load;

    /// <summary>
    /// 当前运转法器占用的分念总数。
    /// </summary>
    public int used_threads;
}
