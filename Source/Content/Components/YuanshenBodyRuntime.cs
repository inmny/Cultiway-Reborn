using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>人物对一名无身化神作出的限时自愿承载声明。</summary>
public struct YuanshenBodyConsent : IComponent
{
    /// <summary>唯一获准采用肉身的化神人物编号。</summary>
    public long recipient_actor_id;

    /// <summary>同意到期世界时间。</summary>
    public double expires_at;
}

/// <summary>无身化神正在引导的一次身体转移。</summary>
public struct YuanshenPossessionState : IComponent
{
    /// <summary>宿主人物编号。</summary>
    public long target_actor_id;

    /// <summary>双方锁定使用的唯一提交编号。</summary>
    public long token;

    /// <summary>本次引导完成世界时间。</summary>
    public double completes_at;

    /// <summary>本次使用的冻结宿主肉身快照。</summary>
    public PhysicalBodySnapshot body;

    /// <summary>形成与宿主灵根相性。</summary>
    public float compatibility;

    /// <summary>本次神魂对抗成功率。</summary>
    public float success_chance;

    /// <summary>宿主是否明确自愿承载。</summary>
    public bool voluntary;
}

/// <summary>宿主在一次身体转移引导中的排他锁。</summary>
public struct YuanshenBodyTransferLock : IComponent
{
    /// <summary>发起转移的无身化神人物编号。</summary>
    public long source_actor_id;

    /// <summary>与发起者状态一致的提交编号。</summary>
    public long token;
}

/// <summary>化神夺舍和肉身重塑的冷却。</summary>
public struct YuanshenBodyRecoveryRuntime : IComponent
{
    /// <summary>下一次允许开始化神夺舍的世界时间。</summary>
    public double possession_ready_at;

    /// <summary>下一次允许开始肉身重塑的世界时间。</summary>
    public double reconstruction_ready_at;
}

/// <summary>无身元神正在进行的确定性本相肉身重塑。</summary>
public struct YuanshenReconstructionState : IComponent
{
    /// <summary>开始时冻结的本相肉身印记。</summary>
    public PhysicalBodySnapshot body;

    /// <summary>开始时冻结的元神形成快照。</summary>
    public CoreFormationSnapshot formation;

    /// <summary>已经完成的塑体世界秒数。</summary>
    public double progress;

    /// <summary>开始时冻结的法器实体编号。</summary>
    public int anchor_artifact_entity_id;

    /// <summary>开始时冻结的备用锚点绑定令牌。</summary>
    public int anchor_token;

    /// <summary>上次结算使用的世界时间。</summary>
    public double last_updated_at;

    /// <summary>上次按固定间隔结算战斗中断的世界时间。</summary>
    public double last_interrupted_at;

    /// <summary>本次已经支付的灵气总量。</summary>
    public float paid_wakan;

    /// <summary>本次塑体需要支付的灵气总量。</summary>
    public float required_wakan;
}

/// <summary>人物唯一的本命法器备用锚点。</summary>
public struct YuanshenArtifactAnchorState : IComponent
{
    /// <summary>作为备用锚点的法器实体编号。</summary>
    public int artifact_entity_id;

    /// <summary>本次绑定在当前世界内唯一的令牌。</summary>
    public int generation;

    /// <summary>锚点绑定世界时间。</summary>
    public double bound_at;
}

/// <summary>法器实体当前承担的元神备用锚点绑定。</summary>
public struct YuanshenArtifactAnchorBinding : IComponent
{
    /// <summary>绑定人物编号。</summary>
    public long owner_actor_id;

    /// <summary>与人物锚点状态双向校验的绑定令牌。</summary>
    public int token;
}

/// <summary>备用锚点完成一次致命转移后的重新绑定冷却。</summary>
public struct YuanshenArtifactRescueCooldown : IComponent
{
    /// <summary>再次允许绑定备用锚点的世界时间。</summary>
    public double expires_at;
}

/// <summary>命魂真正死亡的单次提交令牌。</summary>
public struct YuanshenTrueDeathState : IComponent
{
    /// <summary>死亡提交世界时间。</summary>
    public double submitted_at;

    /// <summary>导致提交的攻击人物编号；没有人物来源时为零。</summary>
    public long attacker_actor_id;
}
