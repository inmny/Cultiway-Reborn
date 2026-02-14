namespace Cultiway.Core;

/// <summary>
/// Primary 层水体细分类。
/// </summary>
public enum PrimaryWaterKind
{
    /// <summary>
    /// 非水体或未指定。
    /// </summary>
    None = 0,
    /// <summary>
    /// 触边的大水体。
    /// </summary>
    Sea = 1,
    /// <summary>
    /// 非触边且非狭长河道的水体。
    /// </summary>
    Lake = 2,
    /// <summary>
    /// 非触边、狭长的水体。
    /// </summary>
    River = 3
}
