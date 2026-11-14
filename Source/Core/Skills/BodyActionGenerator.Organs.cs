using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>器官及真实骨段直接生成自己的局部姿态，不经过人形手路、肩点或腿长。</summary>
public static partial class BodyActionGenerator
{
    /// <summary>先解析真实局部三轴比例，再生成同一动作的起势、发力与回收，最后统一核对阶段和结果规则。</summary>
    /// <param name="request">已检查时长、幅度及发力方向的输入。</param>
    private static BodyActionDefinition CreatePartRotation(BodyActionRequest request)
    {
        if (request.WeaponForm != null || request.Contact != null)
            throw new InvalidDataException("器官或骨段动作使用自身实际几何，不能借用人形手内器物或指定其接触挂点");
        Vector3 axis = request.Kind is BodyActionKind.Sweep or BodyActionKind.Deflect or BodyActionKind.Twist
            ? Vector3.up : Vector3.right;
        if (request.OrganAxis != null)
        {
            if (request.OrganAxis.Length != 3 || request.OrganAxis.Any(value => !Finite(value)))
                throw new InvalidDataException("局部发力轴需要三个有限数值");
            axis = new Vector3(request.OrganAxis[0], request.OrganAxis[1], request.OrganAxis[2]);
            if (axis.sqrMagnitude < .000001f || !Finite(axis.sqrMagnitude))
                throw new InvalidDataException("局部发力轴不能为零或超出有限范围");
        }
        axis = Quaternion.AngleAxis(request.Plane, Vector3.forward) * axis.normalized;
        bool recovery = request.Responses?.Length > 0;
        float[] times = recovery ? new[] { 0f, .27f, .46f, .68f, .84f, 1f } : new[] { 0f, .27f, .46f, .68f, 1f };
        float[] angles = recovery ? new[] { 0f, -.3f, 1f, .55f, .15f, 0f } : new[] { 0f, -.3f, 1f, .55f, 0f };
        float duration = request.Seconds / request.Speed;
        if (!Positive(duration)) throw new InvalidDataException("器官动作历时超出有限范围");
        float amplitude = request.Arc * request.Amplitude *
            (request.Kind == BodyActionKind.Guard ? .2f : request.Kind == BodyActionKind.Deflect ? .35f : 1f);
        if (request.Kind is BodyActionKind.Lift or BodyActionKind.Pull) amplitude = -amplitude;
        var body = new BodyActionDefinition
        {
            Effector = request.Effector, ReleaseAt = duration * .3f, ActiveFrom = duration * .38f,
            ActiveUntil = duration * .7f, Impact = Exertion(request), Keys = new BodyMotionKey[times.Length],
            Responses = request.Responses?.Select(rule => rule == null ? null : new BodyResultResponse
                { Result = rule.Result, RecoverFrom = rule.RecoverFrom, Seconds = rule.Seconds / request.Speed }).ToArray(),
            Stages = BodyConstructionEdits.CopyStages(request.Stages, 1f / request.Speed)
        };
        for (int i = 0; i < times.Length; i++)
            body.Keys[i] = new BodyMotionKey
            {
                Id = i == 2 ? "strike" : i == times.Length - 1 ? "rest" : recovery && i == times.Length - 2 ? "recover.pose" : "pose." + i,
                At = times[i] * duration, PartRotation = Values(axis * (angles[i] * amplitude))
            };
        BodyConstructionEdits.AlignStageEnvelope(body);
        _ = new CompiledBodyAction(body, duration);
        return body;
    }
}
