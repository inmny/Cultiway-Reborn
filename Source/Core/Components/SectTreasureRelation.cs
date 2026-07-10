using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 表示一件特殊物品的所有权属于宗门；物品外借时该关系仍然保留。
/// </summary>
public struct SectTreasureRelation : ILinkRelation
{
    public Entity item;

    public Entity GetRelationKey()
    {
        return item;
    }
}
