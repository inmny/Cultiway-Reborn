namespace Cultiway.Core.Skills;

/// <summary>叠加在同一实际飞行上的普通曲线；共用参照和进度，各自保留周期。</summary>
public sealed class ConstructionPathTerm
{
    /// <summary>有序控制点；从零偏移开始，位置与柄使用主路径的坐标参照。</summary>
    public ConstructionPathKey[] Keys;

    /// <summary>本条曲线走完后是否独立循环；循环必须闭合。</summary>
    public bool Loop;
}
