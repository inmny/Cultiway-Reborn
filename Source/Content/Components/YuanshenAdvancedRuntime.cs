using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>元神设施锚点的物质类型。</summary>
public enum YuanshenAnchorKind : byte
{
    /// <summary>依托宗门现有建筑设立的元神台。</summary>
    SectPlatform,

    /// <summary>依托城市现有庙宇设立的香火坛。</summary>
    CityAltar
}

/// <summary>运行世界内唯一指向一处元神设施锚点的稳定句柄。</summary>
public readonly struct YuanshenAnchorHandle : IEquatable<YuanshenAnchorHandle>
{
    /// <summary>锚点实体当前编号。</summary>
    public readonly int EntityId;

    /// <summary>锚点创建代次。</summary>
    public readonly int Generation;

    /// <summary>发起设立并承担反噬的人物编号。</summary>
    public readonly long OwnerActorId;

    /// <summary>从锚点身份创建稳定句柄。</summary>
    /// <param name="entityId">锚点实体编号。</param>
    /// <param name="identity">锚点身份。</param>
    public YuanshenAnchorHandle(int entityId, in YuanshenAnchorIdentity identity)
    {
        EntityId = entityId;
        Generation = identity.generation;
        OwnerActorId = identity.owner_actor_id;
    }

    /// <summary>判断两枚句柄是否完全相同。</summary>
    /// <param name="other">另一枚句柄。</param>
    public bool Equals(YuanshenAnchorHandle other)
    {
        return EntityId == other.EntityId && Generation == other.Generation && OwnerActorId == other.OwnerActorId;
    }

    /// <summary>判断对象是否为同一枚句柄。</summary>
    /// <param name="obj">待比较对象。</param>
    public override bool Equals(object obj)
    {
        return obj is YuanshenAnchorHandle other && Equals(other);
    }

    /// <summary>返回稳定哈希值。</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(EntityId, Generation, OwnerActorId);
    }

    /// <summary>判断两枚句柄是否相同。</summary>
    public static bool operator ==(YuanshenAnchorHandle left, YuanshenAnchorHandle right) => left.Equals(right);

    /// <summary>判断两枚句柄是否不同。</summary>
    public static bool operator !=(YuanshenAnchorHandle left, YuanshenAnchorHandle right) => !left.Equals(right);

    /// <summary>句柄是否包含可解析的实体与人物编号。</summary>
    public bool IsValid => EntityId > 0 && Generation > 0 && OwnerActorId > 0L;
}

/// <summary>元神设施锚点的稳定归属和物质载体。</summary>
public struct YuanshenAnchorIdentity : IComponent
{
    /// <summary>发起设立并承担反噬的人物编号。</summary>
    public long owner_actor_id;

    /// <summary>承载锚点的原版建筑编号。</summary>
    public long building_id;

    /// <summary>锚点创建代次。</summary>
    public int generation;

    /// <summary>锚点类型。</summary>
    public YuanshenAnchorKind kind;

    /// <summary>获准使用该设施的宗门或城市编号。</summary>
    public long collective_id;

    /// <summary>锚点设立世界时间。</summary>
    public double established_at;
}

/// <summary>锚点的容量与香火愿力状态。</summary>
public struct YuanshenAnchorState : IComponent
{
    /// <summary>当前已经承担的心神份额。</summary>
    public float current_load;

    /// <summary>允许承担的心神份额上限。</summary>
    public float load_capacity;

    /// <summary>香火坛当前可用于显圣和元神恢复的愿力。</summary>
    public float incense;

    /// <summary>香火坛愿力容量。</summary>
    public float incense_capacity;

    /// <summary>最近一次物质建筑受损的世界时间。</summary>
    public double last_attacked_at;

    /// <summary>最近一次观察到的建筑生命值。</summary>
    public float last_building_health;

}

/// <summary>一处锚点已经明确建立的有界双向连接。</summary>
public struct YuanshenAnchorLinks : IComponent
{
    /// <summary>与本锚点直接连通的其他设施句柄。</summary>
    public List<YuanshenAnchorHandle> handles;
}

/// <summary>一名人物持有的有界设施锚点网络和点选状态。</summary>
public struct YuanshenAnchorNetworkRuntime : IComponent
{
    /// <summary>人物设立并仍存续的设施锚点。</summary>
    public List<YuanshenAnchorHandle> owned_anchors;

    /// <summary>下一处设施锚点使用的创建代次。</summary>
    public int next_generation;

    /// <summary>玩家建立连接时明确选中的第一处锚点。</summary>
    public YuanshenAnchorHandle selected_anchor;
}

/// <summary>高阶元神节点共用的持续、锚点依赖和战斗状态。</summary>
public struct YuanshenAdvancedNodeState : IComponent
{
    /// <summary>节点自然结束世界时间，零表示没有固定期限。</summary>
    public double expires_at;

    /// <summary>每秒消耗人物最大灵气的比例。</summary>
    public float upkeep_ratio;

    /// <summary>按整秒结算维持消耗的累计时间。</summary>
    public float upkeep_elapsed;

    /// <summary>节点依赖的设施锚点；法相可以为空。</summary>
    public YuanshenAnchorHandle anchor;

    /// <summary>当前明确攻击目标人物编号。</summary>
    public long target_actor_id;

    /// <summary>距离下一次节点神魂攻击的累计秒数。</summary>
    public float attack_elapsed;

    /// <summary>显圣只能使用护持能力时为真。</summary>
    public bool support_only;
}

/// <summary>法相有限主体模板。</summary>
public enum YuanshenDharmaTemplate : byte
{
    /// <summary>通用元神人形。</summary>
    General,

    /// <summary>多层精神结构组成的灵台。</summary>
    SpiritPlatform,

    /// <summary>锋锐集中的剑胎。</summary>
    SwordEmbryo,

    /// <summary>大型龙魂轮廓。</summary>
    DragonAspect,

    /// <summary>接近本相的真身。</summary>
    PrimalBody
}

/// <summary>由元神形成快照只读解析出的法相视觉与动作倾向。</summary>
public struct YuanshenDharmaAppearance : IComponent
{
    /// <summary>有限主体模板。</summary>
    public YuanshenDharmaTemplate template;

    /// <summary>形成主元素覆盖色。</summary>
    public Color element_color;

    /// <summary>剑道倾向。</summary>
    public bool sword_path;

    /// <summary>炼体倾向。</summary>
    public bool body_path;

    /// <summary>幻道倾向。</summary>
    public bool illusion_path;

    /// <summary>灵渊倾向。</summary>
    public bool reservoir_path;
}

/// <summary>一枚节点当前通过哪处设施锚点维持远距牵引。</summary>
public struct YuanshenAnchorResidence : IComponent
{
    /// <summary>当前承载节点的设施锚点。</summary>
    public YuanshenAnchorHandle anchor;

    /// <summary>本节点在锚点容量中实际预留的份额。</summary>
    public float reserved_load;
}

/// <summary>一枚元神节点沿明确锚点连接进行的远距迁移。</summary>
public struct YuanshenAnchorTransitState : IComponent
{
    /// <summary>本次迁移起点锚点。</summary>
    public YuanshenAnchorHandle source;

    /// <summary>本次迁移终点锚点。</summary>
    public YuanshenAnchorHandle destination;

    /// <summary>迁移完成世界时间。</summary>
    public double completes_at;

    /// <summary>本次迁移是否回到人物命魂主位置而非另一设施。</summary>
    public bool return_to_root;

    /// <summary>开始迁移时的节点完整度，用于发现明确中断。</summary>
    public float starting_integrity;
}

/// <summary>无身命魂本体沿明确锚点连接进行的远距迁移。</summary>
public struct YuanshenBodilessTransitState : IComponent
{
    /// <summary>迁移开始时可以退回的起点锚点。</summary>
    public YuanshenAnchorHandle source;

    /// <summary>明确点选的终点锚点。</summary>
    public YuanshenAnchorHandle destination;

    /// <summary>迁移开始时的世界位置。</summary>
    public Vector2 source_position;

    /// <summary>迁移完成世界时间。</summary>
    public double completes_at;

    /// <summary>迁移开始时的人物生命，用于发现明确魂伤中断。</summary>
    public float starting_health;
}

/// <summary>元神九层人物正在锚点准备的稳定化身载体。</summary>
public struct YuanshenAvatarPreparationState : IComponent
{
    /// <summary>承载准备过程和最终化身的设施锚点。</summary>
    public YuanshenAnchorHandle anchor;

    /// <summary>已经完成的准备世界秒数。</summary>
    public double progress;

    /// <summary>上次结算世界时间。</summary>
    public double last_updated_at;

    /// <summary>上次按固定间隔结算战斗扰动的世界时间。</summary>
    public double last_interrupted_at;

    /// <summary>已经支付的灵气。</summary>
    public float paid_wakan;

    /// <summary>完成载体需要支付的灵气总量。</summary>
    public float required_wakan;
}
