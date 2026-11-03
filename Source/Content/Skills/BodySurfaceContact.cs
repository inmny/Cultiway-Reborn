namespace Cultiway.Content.Skills;

/// <summary>一次数值表面查询的稳定建筑身份与绝对高度，不携带游戏对象。</summary>
internal readonly struct BodySurfaceContact
{
    /// <summary>稳定建筑编号，无表面时为零。</summary>
    internal readonly long Owner;

    /// <summary>共同绝对高度，无表面时为负无穷。</summary>
    internal readonly float Height;

    /// <summary>表面在所属建筑模型坐标中的高度，不随地基的世界高度改变。</summary>
    internal readonly float LocalHeight;

    /// <summary>保存同一次查询的完整结果。</summary>
    /// <param name="owner">稳定建筑编号。</param><param name="height">绝对高度。</param>
    /// <param name="localHeight">所属模型中的表面高度。</param>
    internal BodySurfaceContact(long owner, float height, float localHeight = 0)
    {
        Owner = owner;
        Height = height;
        LocalHeight = localHeight;
    }
}