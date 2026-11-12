using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>由实际攻击确认的结果，不按播放时间猜测。</summary>
public enum BodyActionResult
{
    /// <summary>身体攻击实际击中。</summary>
    Contact = 0,
    /// <summary>攻击被实际架挡、格挡或拨开。</summary>
    Blocked = 1,
    /// <summary>原动作实际承托消失。</summary>
    SupportLost = 2,
    /// <summary>实际停顿或束缚已经受理，旧攻击立即停止。</summary>
    Interrupted = 3
}

/// <summary>同一动作遇到结果后选择的恢复过程，可随完整构造保存和改造。</summary>
public sealed class BodyResultResponse
{
    /// <summary>触发本规则的真实结果。</summary>
    public BodyActionResult Result;
    /// <summary>原动作有效窗口后的命名收势姿态。</summary>
    public string RecoverFrom;
    /// <summary>从当刻姿态连续转入所选收势的世界秒数。</summary>
    public float Seconds = .12f;
}

/// <summary>已经解析标记的结果规则，运行中不再读取原资料。</summary>
internal readonly struct CompiledBodyResponse
{
    /// <summary>实际结果。</summary>
    internal readonly BodyActionResult Result;
    /// <summary>原制作姿态中的恢复起点及转入历时。</summary>
    internal readonly float RecoverAt, Seconds;

    /// <summary>只复制已经检查的数值。</summary>
    /// <param name="source">完整规则。</param><param name="at">已解析的原姿态时间。</param>
    internal CompiledBodyResponse(BodyResultResponse source, float at)
    { Result = source.Result; RecoverAt = at; Seconds = source.Seconds; }
}

/// <summary>本次真实结果的纯数值状态，随共同预测保存和回退。</summary>
internal readonly struct BodyResponseState
{
    /// <summary>已经确认的结果。</summary>
    internal readonly BodyActionResult Result;
    /// <summary>施放时点、原姿态时点、恢复姿态和转入历时。</summary>
    internal readonly float At, From, To, Seconds;
    /// <summary>本次身体完成恢复的实际施放时点。</summary>
    internal readonly float Until;
    /// <summary>当刻实际输入，已有收势被中断时不能反查另一段旧曲线。</summary>
    internal readonly CompiledBodyAction.Key FromKey;

    /// <summary>真实结果确认后才创建，不停止整次施放时间。</summary>
    /// <param name="rule">已编译规则。</param><param name="at">真实施放时点。</param>
    /// <param name="from">当刻实际姿态时间。</param><param name="end">原恢复终点。</param>
    /// <param name="key">真实中断时的实际输入。</param>
    internal BodyResponseState(in CompiledBodyResponse rule, float at, float from, float end, in CompiledBodyAction.Key key)
    {
        FromKey = key;
        Result = rule.Result; At = at; From = from; To = rule.RecoverAt; Seconds = rule.Seconds;
        Until = at + Seconds + (end - To);
    }

    /// <summary>转入阶段采用平滑权重，之后继续所选恢复段。</summary>
    /// <param name="age">本次实际施放时间。</param>
    internal float PoseAge(float age) => age < At + Seconds
        ? Mathf.Lerp(From, To, Mathf.SmoothStep(0f, 1f, (age - At) / Seconds))
        : To + (age - At - Seconds);
}

/// <summary>结果过程复用同一姿态输入及骨长求解，不插值拉长关节。</summary>
public sealed partial class CompiledBodyAction
{
    /// <summary>没有结果规则的旧动作复用空数组。</summary>
    private readonly CompiledBodyResponse[] responses;
    /// <summary>供构造比较读取已经解析的规则。</summary>
    internal IReadOnlyList<CompiledBodyResponse> Responses => responses;
    /// <summary>恢复终点不再次插入原维持时长。</summary>
    internal float RecoveryEnd => keys[keys.Length - 1].At;

    /// <summary>只在资料边界检查实际标记、有限结果和可用恢复时段。</summary>
    /// <param name="source">完整原身体资料。</param>
    private CompiledBodyResponse[] CompileResponses(BodyActionDefinition source)
    {
        if (source.Responses == null || source.Responses.Length == 0) return Array.Empty<CompiledBodyResponse>();
        if (source.Responses.Length > 4)
            throw new InvalidDataException("Body/Responses：每种实际结果只能指定一条恢复规则");
        var seen = new HashSet<BodyActionResult>();
        var result = new CompiledBodyResponse[source.Responses.Length];
        for (int i = 0; i < result.Length; i++)
        {
            BodyResultResponse rule = source.Responses[i];
            if (rule == null || rule.Result is < BodyActionResult.Contact or > BodyActionResult.Interrupted ||
                !seen.Add(rule.Result) || string.IsNullOrWhiteSpace(rule.RecoverFrom) ||
                !Finite(rule.Seconds) || rule.Seconds <= 0f)
                throw new InvalidDataException("Body/Responses：结果不能重复，且须有命名恢复姿态和正有限转入历时");
            BodyMotionKey[] found = source.Keys.Where(key => key.Id == rule.RecoverFrom).ToArray();
            if (found.Length != 1 || found[0].At <= source.ActiveUntil || rule.Seconds > found[0].At - source.ActiveUntil)
                throw new InvalidDataException("Body/Responses/RecoverFrom：需要有效窗口后唯一的原姿态，转入历时不能超过可用恢复时段");
            result[i] = new CompiledBodyResponse(rule, found[0].At);
        }
        return result;
    }

    /// <summary>有限规则按真实结果选择，不按技能名称编排。</summary>
    /// <param name="result">已经确认的结果。</param><param name="rule">实际规则。</param>
    internal bool TryResponse(BodyActionResult result, out CompiledBodyResponse rule)
    {
        foreach (var candidate in responses)
            if (candidate.Result == result) { rule = candidate; return true; }
        rule = default;
        return false;
    }

    /// <summary>转入时只混合动作输入，完整身体仍由原求解器处理。</summary>
    /// <param name="age">扣除落地等待的恢复时钟。</param><param name="frame">当刻身体输入。</param>
    /// <param name="response">真实结果状态。</param><param name="previous">上次确认的实际姿态。</param>
    /// <param name="worldAge">仍在继续的实际施放世界时间。</param>
    internal BodyPose SampleResponse(float age, in BodyFrame frame, in BodyResponseState response, BodyPose? previous, float worldAge)
    {
        float at = Mathf.Min(RecoveryEnd, response.PoseAge(age));
        Key key = ResponseKey(age, in response);
        BodyFrame resolved = frame.WithHeading(key.Heading);
        return SampleResolved(at, in resolved, -1f, previous, worldAge, key);
    }

    /// <summary>支撑与骨长检查使用同一恢复朝向和躯干转角。</summary>
    /// <param name="age">扣除落地等待的时钟。</param><param name="response">真实结果。</param>
    /// <param name="heading">身体朝向。</param><param name="turn">躯干转角。</param>
    internal void ResponseAngles(float age, in BodyResponseState response, out float heading, out float turn)
    {
        Key key = ResponseKey(age, in response);
        heading = key.Heading; turn = key.Turn;
    }

    /// <summary>足底转向指定恢复段，已踩实的脚由实际运动保持世界锚点。</summary>
    /// <param name="age">扣除落地等待的时钟。</param><param name="response">真实结果。</param>
    /// <param name="from">结果确认时的实际双足，采用同一局部朝向。</param>
    internal BodySupportPose ResponseSupport(float age, in BodyResponseState response, in BodySupportPose from)
    {
        if (age >= response.At + response.Seconds) return At(response.PoseAge(age));
        float t = Mathf.SmoothStep(0f, 1f, (age - response.At) / response.Seconds);
        BodySupportPose to = At(response.To);
        return new BodySupportPose(Vector3.Lerp(from.LeftFoot, to.LeftFoot, t),
            Vector3.Lerp(from.RightFoot, to.RightFoot, t), 0f,
            from.LeftPlanted && to.LeftPlanted, from.RightPlanted && to.RightPlanted);

        BodySupportPose At(float at) => supportMotion?.Sample(at) ??
            new BodySupportPose(Construction.LeftFoot, Construction.RightFoot, 0f);
    }

    /// <summary>输入曲线直接连接真实结果与所选恢复段，跳过原攻击后续。</summary>
    /// <param name="age">扣除落地等待的时钟。</param><param name="response">真实结果。</param>
    private Key ResponseKey(float age, in BodyResponseState response) => age < response.At + response.Seconds
        ? BlendResponse(response.FromKey, KeyAt(response.To), Mathf.SmoothStep(0f, 1f, (age - response.At) / response.Seconds))
        : KeyAt(Mathf.Min(RecoveryEnd, response.PoseAge(age)));

    /// <summary>从最近确认的普通姿态或正在进行的收势保存输入，不把混合姿态映射回旧攻击。</summary>
    /// <param name="age">当前收势时钟。</param><param name="poseAge">当刻制作时点。</param>
    /// <param name="previous">当前实际收势，普通动作为空。</param>
    internal Key ResponseOrigin(float age, float poseAge, BodyResponseState? previous) =>
        previous.HasValue ? ResponseKey(age, previous.Value) : KeyAt(poseAge);

    /// <summary>未指定中断规则时连续回到本动作正式收势终点，不要求旧资料新增字段。</summary>
    internal CompiledBodyResponse InterruptionResponse => new(new BodyResultResponse
        { Result = BodyActionResult.Interrupted, Seconds = Mathf.Min(.16f, Duration) }, RecoveryEnd);

    /// <summary>读取原曲线的同刻输入，时间已经排除维持。</summary>
    /// <param name="age">原制作姿态时点。</param>
    private Key KeyAt(float age)
    {
        int next = 1;
        while (next < keys.Length - 1 && age > keys[next].At) next++;
        Key a = keys[next - 1], b = keys[next], va = tangents[next - 1], vb = tangents[next];
        float span = b.At - a.At, t = Mathf.InverseLerp(a.At, b.At, age);
        return new Key
        {
            At = age, Hand = Curve(a.Hand, b.Hand, va.Hand, vb.Hand, span, t),
            Torso = Curve(a.Torso, b.Torso, va.Torso, vb.Torso, span, t),
            Foot = Curve(a.Foot, b.Foot, va.Foot, vb.Foot, span, t),
            HeadRotation = Curve(a.HeadRotation, b.HeadRotation, va.HeadRotation, vb.HeadRotation, span, t),
            Rotation = Curve(a.Rotation, b.Rotation, va.Rotation, vb.Rotation, span, t),
            Turn = Curve(a.Turn, b.Turn, va.Turn, vb.Turn, span, t),
            Heading = Curve(a.Heading, b.Heading, va.Heading, vb.Heading, span, t),
            PartAngle = Curve(a.PartAngle, b.PartAngle, va.PartAngle, vb.PartAngle, span, t)
        };
    }

    /// <summary>两处输入间直接连续转入，不再经过被跳过的攻击关键点。</summary>
    /// <param name="a">真实接触姿态输入。</param><param name="b">实际恢复输入。</param><param name="t">平滑权重。</param>
    private static Key BlendResponse(in Key a, in Key b, float t) => new()
    {
        At = Mathf.Lerp(a.At, b.At, t), Hand = Vector3.Lerp(a.Hand, b.Hand, t),
        Torso = Vector3.Lerp(a.Torso, b.Torso, t), Foot = Vector3.Lerp(a.Foot, b.Foot, t),
        HeadRotation = Vector3.Lerp(a.HeadRotation, b.HeadRotation, t), Rotation = Vector3.Lerp(a.Rotation, b.Rotation, t),
        Turn = Mathf.Lerp(a.Turn, b.Turn, t), Heading = Mathf.Lerp(a.Heading, b.Heading, t),
        PartAngle = Mathf.Lerp(a.PartAngle, b.PartAngle, t)
    };
}
