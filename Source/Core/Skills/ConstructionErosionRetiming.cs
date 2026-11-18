namespace Cultiway.Core.Skills;

/// <summary>选中已有世界材料过程，只替换时间安排；空数组恢复常速。</summary>
internal sealed class CompiledErosionRetiming
{
    internal readonly CompiledWorldMatter World;
    internal readonly CompiledProcessTime Pace;

    internal CompiledErosionRetiming(ConstructionOperation source, string path, int order)
    {
        World = new CompiledWorldMatter(source.WorldMatter, path + "/WorldMatter", order);
        if (source.TimeWindows.Length > 0)
            Pace = new CompiledProcessTime(source.TimeWindows, 1000000000, path + "/TimeWindows");
    }
}
