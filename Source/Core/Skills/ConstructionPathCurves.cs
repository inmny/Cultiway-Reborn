using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>把常用数学曲线展开成普通控制点；运行、保存和玩家后续修改都只使用原路径。</summary>
public static class ConstructionPathCurves
{
    /// <summary>建立连续波形；幅度可沿任意局部方向，初始相位不造成出手瞬移。</summary>
    /// <param name="period">完整往复的世界秒数。</param><param name="amplitude">局部三轴的实际幅度。</param>
    /// <param name="phase">初始相位，单位为弧度。</param>
    public static ConstructionPath Wave(float period, Vector3 amplitude, float phase = 0f)
        => Harmonic(period, period, amplitude, Vector3.zero, 0f, phase);

    /// <summary>展开有明确终点的正弦片段，结束后保持末偏移，不继续下一周期。</summary>
    /// <param name="seconds">完整片段的进度长度。</param><param name="period">正弦完整一周的进度长度。</param>
    /// <param name="amplitude">局部三轴的实际幅度。</param><param name="phase">初始相位，不造成开始瞬移。</param>
    public static ConstructionPath WaveSpan(float seconds, float period, Vector3 amplitude, float phase = 0f)
        => Harmonic(seconds, period, amplitude, Vector3.zero, 0f, phase, false);

    /// <summary>两条独立局部方向构成圆或椭圆，可与中心追踪及前进同时使用。</summary>
    /// <param name="seconds">衰减曲线完整时长；不衰减时只保存一周并循环。</param>
    /// <param name="period">一周的世界秒数。</param><param name="first">正弦方向及半径。</param>
    /// <param name="second">余弦方向及半径。</param><param name="decay">半径每秒指数衰减率。</param>
    /// <param name="phase">初始相位，单位为弧度。</param>
    public static ConstructionPath Spiral(float seconds, float period, Vector3 first, Vector3 second,
        float decay = 0f, float phase = 0f) => Harmonic(seconds, period, first, second, decay, phase);

    /// <summary>半径按时间线性收缩的普通盘旋曲线，只展开完整有限行程，仍可逐点修改。</summary>
    /// <param name="seconds">收缩行程的完整世界秒数。</param><param name="period">旋转一周的世界秒数。</param>
    /// <param name="first">正弦方向与初始半径。</param><param name="second">余弦方向与初始半径。</param>
    /// <param name="shrink">每秒减少的初始半径比例。</param><param name="phase">初始相位，单位为弧度。</param>
    public static ConstructionPath ContractingSpiral(float seconds, float period, Vector3 first, Vector3 second, float shrink, float phase = 0f)
    {
        Check(seconds, first); Check(period, second);
        if (!Finite(shrink) || shrink < 0f || shrink * seconds > 1.000001f || !Finite(phase))
            throw new InvalidDataException("线性收缩率须非负，行程不能越过零半径，相位须有限");
        double count = Math.Ceiling((double)seconds / period * 16d);
        if (count > 64d) throw new InvalidDataException("收缩盘旋超过64曲段，请延长周期或缩短行程");
        int segments = Math.Max(16, (int)count);
        if (seconds / segments < .001f) throw new InvalidDataException("曲线关键点相邻至少需要0.001秒");
        float omega = Mathf.PI * 2f / period;
        Vector3 origin = first * Mathf.Sin(phase) + second * Mathf.Cos(phase);
        var keys = new ConstructionPathKey[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float at = seconds * i / segments, angle = omega * at + phase, radius = Mathf.Max(0f, 1f - shrink * at);
            Vector3 harmonic = first * Mathf.Sin(angle) + second * Mathf.Cos(angle);
            Vector3 tangent = (first * Mathf.Cos(angle) - second * Mathf.Sin(angle)) * (omega * radius) - harmonic * shrink;
            float left = i == 0 ? 0f : at - keys[i - 1].At;
            float right = i == segments ? 0f : seconds * (i + 1) / segments - at;
            keys[i] = new ConstructionPathKey
            {
                At = at, Point = i == 0 ? new float[3] : Array(harmonic * radius - origin),
                In = Array(-tangent * (left / 3f)), Out = Array(tangent * (right / 3f))
            };
        }
        return new ConstructionPath { Forward = null, Keys = keys };
    }

    /// <summary>半径脉动的盘旋，展开成三条不同周期的普通曲线，不展开完整合成周期。</summary>
    /// <param name="period">旋转一周的进度。</param><param name="pulsePeriod">半径脉动一周的进度。</param>
    /// <param name="first">正弦方向及半径。</param><param name="second">余弦方向及半径。</param>
    /// <param name="pulseRatio">半径变化相对原半径的比例。</param><param name="phase">旋转和脉动的初始相位。</param>
    public static ConstructionPath PulsingSpiral(float period, float pulsePeriod, Vector3 first, Vector3 second,
        float pulseRatio, float phase = 0f)
    {
        Check(pulsePeriod, Vector3.zero);
        if (!Finite(pulseRatio)) throw new InvalidDataException("脉动比例须为有限数字");
        ConstructionPath result = Spiral(period, period, first, second, phase: phase);
        if (pulseRatio == 0f) return result;
        // 正弦乘积展开为和频、差频；每条仍由普通三次控制点表达。
        float half = pulseRatio * .5f;
        var terms = new List<ConstructionPathTerm>();
        Add(Spiral(1f, 1f / (1f / period + 1f / pulsePeriod), second * half, -first * half, phase: 2f * phase));
        float difference = 1f / period - 1f / pulsePeriod;
        if (difference != 0f)
            Add(Spiral(1f, 1f / Mathf.Abs(difference), -second * (half * Mathf.Sign(difference)), first * half));
        result.Additions = terms.ToArray();
        return result;

        /// <summary>只保存普通叶曲线的控制点与周期，不带另一份运动参照或时钟。</summary>
        /// <param name="curve">本次独占的参数展开结果。</param>
        void Add(ConstructionPath curve) => terms.Add(new ConstructionPathTerm { Keys = curve.Keys, Loop = curve.Loop });
    }

    /// <summary>建立有明确折点的往复运动，首尾位置连续，不用圆滑曲线掩盖实际转折。</summary>
    /// <param name="period">完整往复的世界秒数。</param><param name="amplitude">三轴折返幅度。</param>
    /// <param name="phase">初始相位，单位为弧度，不造成首帧跳跃。</param>
    public static ConstructionPath Zigzag(float period, Vector3 amplitude, float phase = 0f)
    {
        Check(period, amplitude);
        if (!Finite(phase)) throw new InvalidDataException("折线相位必须为有限数字");
        float cycle = Mathf.Repeat(phase / (2f * Mathf.PI), 1f);
        float origin = Triangle(cycle);
        var points = new List<ConstructionPathKey> { new() };
        for (float corner = .25f; corner < cycle + 1f; corner += .5f)
            if (corner > cycle)
                points.Add(new ConstructionPathKey { At = (corner - cycle) * period,
                    Point = Array(amplitude * (Triangle(corner) - origin)) });
        points.Add(new ConstructionPathKey { At = period });
        var keys = points.ToArray();
        for (int i = 0; i < keys.Length - 1; i++)
        {
            Vector3 delta = (new Vector3(keys[i + 1].Point[0], keys[i + 1].Point[1], keys[i + 1].Point[2]) -
                             new Vector3(keys[i].Point[0], keys[i].Point[1], keys[i].Point[2])) / 3f;
            keys[i].Out = Array(delta);
            keys[i + 1].In = Array(-delta);
        }
        return new ConstructionPath { Loop = true, Keys = keys };

        float Triangle(float value) => 1f - 4f * Mathf.Abs(Mathf.Repeat(value + .25f, 1f) - .5f);
    }

    /// <summary>目标相对弧线，经过指定真实弧顶高度，抵达后保持末端。</summary>
    /// <param name="seconds">抵达目标的世界秒数。</param><param name="height">相对直连路径的离地弧顶。</param>
    public static ConstructionPath Arc(float seconds, float height)
    {
        Check(seconds, new Vector3(0f, 0f, height));
        return new ConstructionPath
        {
            Space = ConstructionPathSpace.TargetLine,
            Keys = new[]
            {
                new ConstructionPathKey { Out = new[] { 1f / 3f, 0f, height * 4f / 3f } },
                new ConstructionPathKey { At = seconds, Point = new[] { 1f, 0f, 0f }, In = new[] { -1f / 3f, 0f, height * 4f / 3f } }
            }
        };
    }

    /// <summary>按解析位置与速度生成三次曲段，先检查边界，再展开控制点；不截断超预算输入。</summary>
    /// <param name="seconds">完整时长。</param><param name="period">周期。</param>
    /// <param name="first">正弦幅度。</param><param name="second">余弦幅度。</param>
    /// <param name="decay">指数衰减率。</param><param name="phase">初始相位。</param>
    /// <param name="looping">明确覆盖是否循环，空值沿用无衰减时循环的原规则。</param>
    private static ConstructionPath Harmonic(float seconds, float period, Vector3 first, Vector3 second, float decay, float phase, bool? looping = null)
    {
        Check(period, first); Check(seconds, second);
        if (!Finite(decay) || decay < 0f || !Finite(phase)) throw new InvalidDataException("曲线衰减必须非负，相位必须为有限数字");
        bool loop = looping ?? decay == 0f;
        float duration = loop ? period : seconds;
        double count = Math.Ceiling((double)duration / period * 8d);
        if (count > 64d) throw new InvalidDataException("有限曲线超过64段，请延长周期或缩短时长");
        int segments = Math.Max(8, (int)count);
        if (duration / segments < .001f) throw new InvalidDataException("曲线关键点相邻至少需要0.001秒");
        double omega = Math.PI * 2d / period;
        Vector3 origin = first * (float)Math.Sin(phase) + second * (float)Math.Cos(phase);
        var keys = new ConstructionPathKey[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float at = duration * i / segments;
            double angle = omega * at + phase, envelope = Math.Exp(-decay * at);
            Vector3 harmonic = first * (float)Math.Sin(angle) + second * (float)Math.Cos(angle);
            Vector3 point = harmonic * (float)envelope - origin;
            Vector3 tangent = ((first * (float)Math.Cos(angle) - second * (float)Math.Sin(angle)) * (float)omega - harmonic * decay) * (float)envelope;
            float left = i == 0 ? 0f : at - keys[i - 1].At;
            float right = i == segments ? 0f : duration * (i + 1) / segments - at;
            keys[i] = new ConstructionPathKey
            {
                At = at, Point = i == 0 || loop && i == segments ? new float[3] : Array(point),
                In = Array(-tangent * (left / 3f)), Out = Array(tangent * (right / 3f))
            };
        }
        return new ConstructionPath { Loop = loop, Keys = keys };
    }

    /// <summary>只在外部参数进入时核对实际范围，编译仍负责完整曲率与速度预算。</summary>
    /// <param name="seconds">明确的世界时长。</param><param name="amplitude">三轴实际幅度。</param>
    private static void Check(float seconds, Vector3 amplitude)
    {
        if (!Finite(seconds) || seconds < .008f || seconds > 3600f ||
            !Finite(amplitude.sqrMagnitude) || amplitude.magnitude > 10000f)
            throw new InvalidDataException("曲线时长须为0.008至3600秒，幅度须为一万格内的有限坐标");
    }

    /// <summary>数值边界的有限检查。</summary><param name="value">实际输入。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>输出可保存的独立三轴资料。</summary><param name="value">实际数值。</param>
    private static float[] Array(Vector3 value) => new[] { value.x, value.y, value.z };
}
