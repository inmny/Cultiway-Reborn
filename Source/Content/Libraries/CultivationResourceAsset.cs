using Cultiway.Abstract;
using Cultiway.Core;

namespace Cultiway.Content.Libraries;

/// <summary>读取或消耗修炼资源时所需的角色和地块上下文。</summary>
public readonly struct CultivationResourceContext
{
    /// <summary>创建一次修炼资源访问上下文。</summary>
    public CultivationResourceContext(ActorExtend actor, int tileX = -1, int tileY = -1)
    {
        Actor = actor;
        TileX = tileX;
        TileY = tileY;
    }

    /// <summary>支付修炼消耗的角色。</summary>
    public ActorExtend Actor { get; }

    /// <summary>指定来源地块横坐标；负数表示使用角色当前地块。</summary>
    public int TileX { get; }

    /// <summary>指定来源地块纵坐标；负数表示使用角色当前地块。</summary>
    public int TileY { get; }
}

/// <summary>从一项修炼资源中原子地扣除不超过请求值的数量。</summary>
public delegate float CultivationResourceWithdrawer(in CultivationResourceContext context, float requestedAmount);

/// <summary>只读查询一项修炼资源的当前可用量。</summary>
public delegate float CultivationResourceReader(in CultivationResourceContext context);

/// <summary>可由内容模块注册并供修炼规则消耗的资源资产。</summary>
public class CultivationResourceAsset : Asset
{
    /// <summary>只读返回当前可支付量，不得在查询中修改来源状态。</summary>
    public CultivationResourceReader GetAvailable;

    /// <summary>读取并扣除实际可支付量；返回值必须位于 0 到请求值之间。</summary>
    public CultivationResourceWithdrawer WithdrawUpTo;
}
