using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>独立周期的曲线相加，只合并数值，不增加主体、施放或另一个运动时钟。</summary>
internal sealed partial class CompiledConstructionPath
{
    /// <summary>独立复制的有限附加曲线，叶曲线不允许再次嵌套。</summary>
    private readonly CompiledConstructionPath[] additions;

    /// <summary>先核对附加数量及静态用途，再按相同参照编译各条并核对合计预算。</summary>
    /// <param name="source">完整可编辑路径。</param><param name="path">具体错误位置。</param>
    /// <param name="layout">是否静态排布；当前叠加只提供给实际飞行。</param>
    private CompiledConstructionPath[] CompileAdditions(ConstructionPath source, string path, bool layout)
    {
        if (source.Additions == null || source.Additions.Length == 0) return Array.Empty<CompiledConstructionPath>();
        if (layout || source.Additions.Length > 7)
            throw new InvalidDataException(path + "/Additions：实际飞行最多叠加七条附加曲线，静态排布暂不采用叠加");
        var result = new CompiledConstructionPath[source.Additions.Length];
        int count = segments.Length;
        for (int i = 0; i < result.Length; i++)
        {
            ConstructionPathTerm term = source.Additions[i];
            if (term == null) throw new InvalidDataException(path + "/Additions[" + i + "]：附加曲线不能为空");
            result[i] = new CompiledConstructionPath(new ConstructionPath
            {
                Space = source.Space, Keys = term.Keys, Loop = term.Loop, Forward = null
            }, path + "/Additions[" + i + "]");
            count += result[i].Count;
        }
        if (count > 64) throw new InvalidDataException(path + "/Additions：全部曲线合计超过64曲段");
        return result;
    }

    /// <summary>所有曲线采用同一累计进度，下一实际边界取最先抵达的一条。</summary>
    /// <param name="age">本次施放时间或换算后的路程进度。</param><param name="startedAt">开始时点。</param>
    internal float UntilBoundary(float age, float startedAt)
    {
        float result = UntilOwnBoundary(age, startedAt);
        foreach (CompiledConstructionPath addition in additions)
            result = Mathf.Min(result, addition.UntilOwnBoundary(age, startedAt));
        return result;
    }
}
