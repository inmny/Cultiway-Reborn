using System;
using System.IO;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>结果恢复沿用实际姿态、完整构造与正式编译，编辑和创作调用同一入口。</summary>
public static partial class BodyConstructionEdits
{
    /// <summary>实际接触窗口后的姿态可作为收势，不要求玩家先填写内部标记。</summary>
    /// <param name="source">当前完整构造。</param>
    public static int[] RecoveryPoints(AbilityDefinition source) => source.Body == null ? Array.Empty<int>() :
        Enumerable.Range(0, source.Body.Keys.Length).Where(i => source.Body.Keys[i].At > source.Body.ActiveUntil).ToArray();

    /// <summary>独立修改一种结果的姿态和转入时长，必要时给已有姿态分配稳定标记。</summary>
    /// <param name="source">完整草稿或正式资料。</param><param name="result">真实结果。</param>
    /// <param name="pose">原动作中的姿态序号。</param><param name="seconds">实际转入秒数。</param>
    public static AbilityDefinition Response(AbilityDefinition source, BodyActionResult result, int pose, float seconds)
    {
        if (source.Body == null || !RecoveryPoints(source).Contains(pose))
            throw new InvalidDataException("结果恢复需要选择实际接触窗口后的姿态");
        AbilityDefinition copy = ConstructionEditing.Copy(source);
        BodyMotionKey key = copy.Body.Keys[pose];
        if (string.IsNullOrWhiteSpace(key.Id))
        {
            string name = "recover." + pose;
            for (int suffix = 1; copy.Body.Keys.Any(k => k.Id == name); suffix++) name = "recover." + pose + "." + suffix;
            key.Id = name;
        }
        copy.Body.Responses = (copy.Body.Responses ?? Array.Empty<BodyResultResponse>()).Where(rule => rule.Result != result)
            .Concat(new[] { new BodyResultResponse { Result = result, RecoverFrom = key.Id, Seconds = seconds } }).ToArray();
        AbilityCompiler.Compile(copy, cache: false);
        return copy;
    }

    /// <summary>移除一种显式恢复，其他结果及姿态原样保留。</summary>
    /// <param name="source">完整构造。</param><param name="result">要移除的结果。</param>
    public static AbilityDefinition RemoveResponse(AbilityDefinition source, BodyActionResult result)
    {
        if (source.Body == null) throw new InvalidDataException("结果恢复需要实际身体动作");
        AbilityDefinition copy = ConstructionEditing.Copy(source);
        copy.Body.Responses = (copy.Body.Responses ?? Array.Empty<BodyResultResponse>()).Where(rule => rule.Result != result).ToArray();
        AbilityCompiler.Compile(copy, cache: false);
        return copy;
    }
}
