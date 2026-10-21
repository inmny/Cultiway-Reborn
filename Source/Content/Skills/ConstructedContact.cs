using Cultiway.Core.SkillLibV3.ActiveAbilities;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次真实施放受理的记录，保留来源与调用方式，供实际交战验证使用。</summary>
public readonly struct ConstructedCastStarted
{
    /// <summary>本次施放编号。</summary>
    public readonly long CastId;
    /// <summary>完整构造编号。</summary>
    public readonly string AbilityId;
    /// <summary>人物的稳定编号。</summary>
    public readonly long SourceId;
    /// <summary>本次为玩家、脚本或真正自主出手。</summary>
    public readonly ActiveAbilityUseOrigin Origin;
    /// <summary>真实受理的世界时点，接触回调内不借用整轮末端时间。</summary>
    public readonly double WorldTime;
    /// <summary>记录已经受理的一次施放。</summary>
    /// <param name="cast">施放编号。</param>
    /// <param name="ability">构造编号。</param>
    /// <param name="source">人物编号。</param>
    /// <param name="origin">实际调用方式。</param>
    /// <param name="worldTime">真实受理时点。</param>
    internal ConstructedCastStarted(long cast, string ability, long source, ActiveAbilityUseOrigin origin, double worldTime)
    { CastId = cast; AbilityId = ability; SourceId = source; Origin = origin; WorldTime = worldTime; }
}

/// <summary>由共同作用形状确认的真实接触；原版伤害仍按正常抗性和防御结算。</summary>
public readonly struct ConstructedContact
{
    /// <summary>本次施放编号。</summary>
    public readonly long CastId;
    /// <summary>构造及实际来源。</summary>
    public readonly string AbilityId;
    /// <summary>实际来源人物。</summary>
    public readonly long SourceId;
    /// <summary>反射前最初的来源，不随控制权改变。</summary>
    public readonly long OriginalSourceId;
    /// <summary>本部位实际经过的反射次数。</summary>
    public readonly int Reflections;
    /// <summary>当前控制权下对该人物的次数，以及整次施放该组的累计次数。</summary>
    public readonly int TargetContactNumber, GroupContactNumber;
    /// <summary>实际接触人物。</summary>
    public readonly long TargetId;
    /// <summary>发生接触的展开部位，负一为实际身体。</summary>
    public readonly int Node;
    /// <summary>地图空间的真实接触位置。</summary>
    public readonly Vector3 Position;
    /// <summary>由攻击作用面指向目标的实际接触方向，初始深度重叠时可为零。</summary>
    public readonly Vector3 Normal;
    /// <summary>单次施放已经过去的世界秒数。</summary>
    public readonly float Age;
    /// <summary>原版近战结果；生成法术使用Hit表示已经同步执行正常伤害规则，不保证突破免疫。</summary>
    public readonly ApplyAttackState Result;
    /// <summary>实际伤害采用的材料和物态；身体作用没有生成材料时为空。</summary>
    public readonly string MatterBefore, StateBefore;
    /// <summary>真实作用后该生成部位的材料和物态。</summary>
    public readonly string MatterAfter, StateAfter;

    /// <summary>记录已经确认的接触，不把目标选择或动画时点冒充接触。</summary>
    /// <param name="cast">施放编号。</param>
    /// <param name="ability">构造编号。</param>
    /// <param name="source">来源。</param>
    /// <param name="target">目标。</param>
    /// <param name="node">作用部位。</param>
    /// <param name="position">实际接触位置。</param>
    /// <param name="normal">实际接触方向。</param>
    /// <param name="age">世界年龄。</param>
    /// <param name="result">原版实际受理结果。</param>
    /// <param name="originalSource">最初来源，零表示与当前来源相同。</param>
    /// <param name="reflections">实际反射次数。</param>
    /// <param name="targetContactNumber">当前归属对该人物的累计次数。</param>
    /// <param name="groupContactNumber">该组全程累计次数。</param>
    /// <param name="matterBefore">伤害时的材料。</param>
    /// <param name="stateBefore">伤害时的物态。</param>
    /// <param name="matterAfter">作用后的材料。</param>
    /// <param name="stateAfter">作用后的物态。</param>
    internal ConstructedContact(long cast, string ability, long source, long target, int node,
        Vector3 position, Vector3 normal, float age, ApplyAttackState result, long originalSource = 0, int reflections = 0,
        int targetContactNumber = 1, int groupContactNumber = 1, string matterBefore = null, string stateBefore = null, string matterAfter = null, string stateAfter = null)
    {
        CastId = cast; AbilityId = ability; SourceId = source; TargetId = target; Node = node;
        Position = position; Normal = normal; Age = age; Result = result;
        OriginalSourceId = originalSource == 0 ? source : originalSource; Reflections = reflections;
        TargetContactNumber = targetContactNumber; GroupContactNumber = groupContactNumber;
        MatterBefore = matterBefore; StateBefore = stateBefore; MatterAfter = matterAfter; StateAfter = stateAfter;
    }
}

/// <summary>真实反射已经改变的运动、归属和屏障余量。</summary>
public readonly struct ConstructedReflection
{
    /// <summary>原施放编号，反射不创建另一施放。</summary>
    public readonly long CastId;
    /// <summary>原构造编号。</summary>
    public readonly string AbilityId;
    /// <summary>发生反射的实际部位。</summary>
    public readonly int Node;
    /// <summary>最初来源、原控制者与新控制者。</summary>
    public readonly long OriginalSource, PreviousOwner, Owner;
    /// <summary>累计实际反射数。</summary>
    public readonly int Count;
    /// <summary>原施放的真实进度。</summary>
    public readonly float Age;
    /// <summary>接触位置、表面方向及前后世界速度。</summary>
    public readonly Vector3 Position, Normal, IncomingVelocity, OutgoingVelocity;
    /// <summary>保留的攻击作用量，同时为屏障本次支付量。</summary>
    public readonly float Force;
    /// <summary>实际参与交锋的屏障施放。</summary>
    public readonly long BarrierCast;
    /// <summary>实际屏障部位。</summary>
    public readonly int BarrierNode;
    /// <summary>屏障支付后的剩余量。</summary>
    public readonly float BarrierRemaining;

    /// <summary>记录已确认的反射，不把预计接触当作结果。</summary>
    /// <param name="cast">原施放。</param>
    /// <param name="ability">原构造。</param>
    /// <param name="node">原部位。</param>
    /// <param name="original">原始来源。</param>
    /// <param name="previous">上一控制者。</param>
    /// <param name="owner">新控制者。</param>
    /// <param name="count">累计反射数。</param>
    /// <param name="age">原施放进度。</param>
    /// <param name="position">接触位置。</param>
    /// <param name="normal">屏障表面方向。</param>
    /// <param name="incoming">原速度。</param>
    /// <param name="outgoing">新速度。</param>
    /// <param name="force">保留能量及支付量。</param>
    /// <param name="barrier">屏障施放。</param>
    /// <param name="barrierNode">屏障部位。</param>
    /// <param name="remaining">屏障余量。</param>
    internal ConstructedReflection(long cast, string ability, int node, long original, long previous, long owner,
        int count, float age, Vector3 position, Vector3 normal, Vector3 incoming, Vector3 outgoing, float force,
        long barrier, int barrierNode, float remaining)
    {
        CastId = cast; AbilityId = ability; Node = node; OriginalSource = original; PreviousOwner = previous; Owner = owner;
        Count = count; Age = age; Position = position; Normal = normal; IncomingVelocity = incoming; OutgoingVelocity = outgoing;
        Force = force; BarrierCast = barrier; BarrierNode = barrierNode; BarrierRemaining = remaining;
    }
}

/// <summary>真实形体相撞后已经发生的消耗；显示及测试只读取结果。</summary>
public readonly struct ConstructedClash
{
    /// <summary>交锋双方的实际施放编号。</summary>
    public readonly long LeftCast, RightCast;
    /// <summary>交锋双方的实际来源。</summary>
    public readonly long LeftSource, RightSource;
    /// <summary>实际相撞的展开部位。</summary>
    public readonly int LeftNode, RightNode;
    /// <summary>真实作用面位置及从左指向右的方向。</summary>
    public readonly Vector3 Position, Normal;
    /// <summary>同一交锋时点下双方各自已经经过的时间。</summary>
    public readonly float LeftAge, RightAge;
    /// <summary>本次双方各自损耗的作用量。</summary>
    public readonly float Absorbed;
    /// <summary>交锋后双方的实际剩余量。</summary>
    public readonly float LeftRemaining, RightRemaining;

    /// <summary>保存已经确认的实际交锋结果。</summary>
    /// <param name="leftCast">左施放。</param>
    /// <param name="rightCast">右施放。</param>
    /// <param name="leftSource">左来源。</param>
    /// <param name="rightSource">右来源。</param>
    /// <param name="leftNode">左部位。</param>
    /// <param name="rightNode">右部位。</param>
    /// <param name="position">真实接触点。</param>
    /// <param name="normal">真实作用方向。</param>
    /// <param name="leftAge">左施放进度。</param>
    /// <param name="rightAge">右施放进度。</param>
    /// <param name="absorbed">双方各自消耗量。</param>
    /// <param name="leftRemaining">左剩余。</param>
    /// <param name="rightRemaining">右剩余。</param>
    internal ConstructedClash(long leftCast, long rightCast, long leftSource, long rightSource, int leftNode, int rightNode,
        Vector3 position, Vector3 normal, float leftAge, float rightAge, float absorbed, float leftRemaining, float rightRemaining)
    {
        LeftCast = leftCast; RightCast = rightCast; LeftSource = leftSource; RightSource = rightSource;
        LeftNode = leftNode; RightNode = rightNode; Position = position; Normal = normal;
        LeftAge = leftAge; RightAge = rightAge; Absorbed = absorbed; LeftRemaining = leftRemaining; RightRemaining = rightRemaining;
    }
}
