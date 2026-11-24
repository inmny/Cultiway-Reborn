using System;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>一次发射的实际空间来源，归属和扣费仍由原技能负责。</summary>
internal readonly struct SkillCastPlacement
{
    /// <summary>本次解析时的出生位置。</summary>
    internal readonly Vector3 Position;

    /// <summary>可选的原载体位置。返回空表示原载体消失。</summary>
    internal readonly Func<Vector3?> Track;

    /// <summary>建立一次出生位置及可选的随动方式。</summary>
    /// <param name="position">出生位置。</param>
    /// <param name="track">原载体位置的读取方式。</param>
    internal SkillCastPlacement(Vector3 position, Func<Vector3?> track = null)
    {
        Position = position;
        Track = track;
    }
}

/// <summary>一个已经受理的发射步骤；收费和步骤数由原来源维护，执行入口只能接管一次。</summary>
public struct SkillEmission
{
    /// <summary>本步骤尚未提交的实际扣费，执行入口成功时提交，失败时由来源撤销。</summary>
    internal SkillCastPayment Payment;

    /// <summary>仅由万法试放传入，读取同次共同执行是否还在运行。</summary>
    internal Action<Func<bool>> ReportPreview;

    /// <summary>实际已学或来源授予的容器。</summary>
    public Entity SkillContainer;

    /// <summary>没有施法人物的世界来源。</summary>
    public bool Sourceless;

    /// <summary>实际归属者与空间载体。</summary>
    public BaseSimObject Source, SpatialSource;

    /// <summary>已经确定的出生位置。</summary>
    public Vector3 SourcePos;

    /// <summary>是否已有步骤固定坐标或实际释放时解析出的身体出口。</summary>
    public bool HasSourcePosition;

    /// <summary>实际出口已包含世界高度，由接管世界坐标的执行入口使用。</summary>
    internal bool SourceIsWorld;

    /// <summary>实际跟踪对象及本步骤确定的目标位置。</summary>
    public BaseSimObject Target;

    /// <summary>本步骤确定的目标位置。</summary>
    public Vector3 TargetPos;

    /// <summary>来源给出的实际强度与功率。</summary>
    public float Strength, PowerLevel;

    /// <summary>同一施放内各次实际发射的种子，用于保持不同的散落位置。</summary>
    internal int StepSeed;

    /// <summary>齐射的实际偏角。</summary>
    public float InitialAngleOffsetDegrees;

    /// <summary>本次行动的攻击归属。</summary>
    public Kingdom AttackKingdom;

    /// <summary>本次关联、来源方式和世界任务边界。</summary>
    public SkillCastRuntimeData RuntimeData;
}

/// <summary>执行接管与实际受理分别说明，拒绝不能落回另一套执行。</summary>
public enum SkillEmissionResult
{
    /// <summary>当前实现不能处理，由通用来源继续执行。</summary>
    Unhandled,
    /// <summary>已经识别但实际条件失效，不生成另一个执行体。</summary>
    Rejected,
    /// <summary>真实执行已经受理。</summary>
    Accepted
}
