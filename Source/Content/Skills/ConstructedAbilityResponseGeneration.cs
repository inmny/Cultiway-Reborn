using System;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>自主创法使用已有实际姿态改造结果恢复，不编排固定多招组合。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>已确认有收势姿态时选择真实结果、姿态和合法转入秒数，保存实际选择。</summary>
    /// <param name="source">受理时固定的完整身体构造。</param><param name="random">同次创作的确定随机源。</param>
    private static AbilityDefinition GenerateBodyResponse(AbilityDefinition source, Random random)
    {
        int[] points = BodyConstructionEdits.RecoveryPoints(source);
        int pose = points[random.Next(points.Length)];
        float available = source.Body.Keys[pose].At - source.Body.ActiveUntil;
        return BodyConstructionEdits.Response(source, (BodyActionResult)random.Next(4), pose,
            available * (.2f + (float)random.NextDouble() * .5f));
    }
}
