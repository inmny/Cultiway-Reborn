using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一身体按自己真实关节恢复，受力过程不再假定人形肢链数量。</summary>
internal interface IBodyRecoveryRig
{
    /// <summary>接手时的完整真实表面。</summary>
    BodySurfacePose Basis { get; }
    /// <summary>同一轮恢复保持的实际几何。</summary>
    BodySurfacePart[] Parts { get; }
    /// <summary>本身体实际前方，直立时不再猜测图片横轴就是身体前方。</summary>
    Vector3 Forward { get; }
    /// <summary>只计算本身体已有部位的姿态。</summary>
    /// <param name="motion">整体运动。</param><param name="upright">直立方向。</param>
    /// <param name="progress">起身进度。</param><param name="terrain">实际地表。</param>
    BodySurfaceTransform[] Sample(in BodySurfaceTransform motion, Quaternion upright, float progress, AbilityHeightField terrain);
    /// <summary>显示与接触共用同一恢复表面。</summary>
    /// <param name="motion">整体运动。</param><param name="bones">已计算姿态。</param>
    /// <param name="terrain">实际地表。</param><param name="maximum">允许的承托上界。</param>
    BodySurfacePose Surface(in BodySurfaceTransform motion, BodySurfaceTransform[] bones, AbilityHeightField terrain, float maximum);
    /// <summary>按真实顶点确定承托高度。</summary>
    /// <param name="bones">同刻姿态。</param><param name="terrain">实际地表。</param>
    /// <param name="maximum">承托上界。</param><param name="support">实际承托。</param>
    float Lift(BodySurfaceTransform[] bones, AbilityHeightField terrain, float maximum, out float support);
    /// <summary>各实际骨段检查共同环境，不改用整个人物包络。</summary>
    /// <param name="from">原姿态。</param><param name="to">候选姿态。</param><param name="terrain">实际地表。</param>
    /// <param name="obstacles">真实障碍。</param><param name="maximum">承托上界。</param>
    bool Blocked(BodySurfaceTransform[] from, BodySurfaceTransform[] to, AbilityHeightField terrain, IReadOnlyList<BodySurfaceObstacle> obstacles, float maximum);
    /// <summary>再次受击接续当前表面，不回到最初的冻结姿态。</summary>
    /// <param name="surface">已确认表面。</param><param name="bones">已确认骨段。</param>
    IBodyRecoveryRig Rebase(BodySurfacePose surface, BodySurfaceTransform[] bones);
}
