using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cultiway.Core.Skills;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>制作中的命名控制点，父级必须先于子级。</summary>
public sealed class FormControlDescription
{
    /// <summary>稳定控制名称。</summary>
    public string Id;

    /// <summary>父控制名称，空值为根。</summary>
    public string Parent;

    /// <summary>局部绑定位置。</summary>
    public float[] Position;

    /// <summary>局部绑定四元数，依次为x、y、z、w。</summary>
    public float[] Rotation;

    /// <summary>本控制影响顶点的绑定空间边界。</summary>
    public float[] Minimum, Maximum;
}

/// <summary>制作动作的完整局部姿态，不含单次施放状态。</summary>
public sealed class FormMotionDescription
{
    /// <summary>同一形体内的动作名称。</summary>
    public string Id;

    /// <summary>动作世界秒数。</summary>
    public float Duration;

    /// <summary>是否循环，循环端点必须相同。</summary>
    public bool Loop;

    /// <summary>有序的完整姿态。</summary>
    public FormMotionKeyDescription[] Keys;

    /// <summary>影响构造阶段的命名时点。</summary>
    public FormMotionMarkerDescription[] Markers;
}

/// <summary>制作动作中可以绑定真实有效时段的阶段时点。</summary>
public sealed class FormMotionMarkerDescription
{
    /// <summary>同一动作内的唯一名称。</summary>
    public string Id;

    /// <summary>动作内世界秒数。</summary>
    public float At;
}

/// <summary>一个制作关键点，位置及四元数按控制顺序紧密排列。</summary>
public sealed class FormMotionKeyDescription
{
    /// <summary>本时刻已经建立的主体进度，普通动作保持完整。</summary>
    public float Build = 1f;

    /// <summary>动作内世界秒数。</summary>
    public float At;

    /// <summary>每控制三个局部位置数值。</summary>
    public float[] Positions;

    /// <summary>每控制四个局部四元数数值。</summary>
    public float[] Rotations;
}

/// <summary>已复制的纯数值动作，计算不会访问网格、材质或可编辑资料。</summary>
public sealed class FormMotion
{
    /// <summary>所属资源内的稳定动作名。</summary>
    public readonly string Id;

    /// <summary>按时点及名称排序的不可变制作标记，只由共同动作时钟读取。</summary>
    internal readonly KeyValuePair<string, float>[] StageMarkers;

    /// <summary>完整动作时长。</summary>
    public readonly float Duration;

    /// <summary>循环方式。</summary>
    public readonly bool Loop;

    /// <summary>保守的整条骨架角速度，用于共同推进的转动分段。</summary>
    public readonly float MaximumAngularRate;

    /// <summary>全部控制平移的连续速度上界，供附着点真正脱落时预留运动。</summary>
    public readonly float MaximumTranslationRate;

    /// <summary>关键点时间。</summary>
    internal readonly float[] Times;

    /// <summary>与姿态同一时钟采样的成形进度。</summary>
    internal readonly float[] Builds;

    /// <summary>此动作是否确实包含逐段成形阶段。</summary>
    internal readonly bool FormsOverTime;

    /// <summary>各关键点的局部位置。</summary>
    internal readonly Vector3[][] Positions;

    /// <summary>各关键点的局部四元数。</summary>
    internal readonly Quaternion[][] Rotations;

    /// <summary>只在加载及编译边界查找的命名时点。</summary>
    private readonly Dictionary<string, float> markers = new(StringComparer.Ordinal);

    /// <summary>将明确阶段名称解析为动作内部时间。</summary>
    /// <param name="id">已有制作标记。</param>
    public float MarkerTime(string id) => markers.TryGetValue(id, out float time)
        ? time
        : throw new InvalidDataException("动作缺少阶段标记：" + id);

    /// <summary>在制作资料边界检查并复制。</summary>
    /// <param name="source">制作动作。</param>
    /// <param name="count">骨架控制数量。</param>
    /// <param name="path">具体资源位置。</param>
    internal FormMotion(FormMotionDescription source, int count, string path)
    {
        if (!FormRig.Finite(source.Duration) || source.Duration <= 0f || source.Keys == null ||
            source.Keys.Length < 2 || source.Keys.Length > 256) throw new InvalidDataException(path + "：动作时长或关键点无效");
        Id = source.Id;
        Duration = source.Duration;
        Loop = source.Loop;
        Times = new float[source.Keys.Length];
        Builds = new float[Times.Length];
        Positions = new Vector3[Times.Length][];
        Rotations = new Quaternion[Times.Length][];
        var rates = new float[count];
        var translations = new float[count];
        for (int key = 0; key < Times.Length; key++)
        {
            FormMotionKeyDescription data = source.Keys[key];
            if (data == null || !FormRig.Finite(data.Build) || data.Build < 0f || data.Build > 1f)
                throw new InvalidDataException(path + "：成形进度须在0至1之间");
            Builds[key] = data.Build;
            FormsOverTime |= data.Build < 1f;
            if (data == null || !FormRig.Finite(data.At) || data.At < 0 || data.At > Duration ||
                key > 0 && data.At <= Times[key - 1] || data.Positions?.Length != count * 3 ||
                data.Rotations?.Length != count * 4)
                throw new InvalidDataException(path + "：姿态时间或数量无效");
            Times[key] = data.At;
            Positions[key] = new Vector3[count];
            Rotations[key] = new Quaternion[count];
            for (int control = 0; control < count; control++)
            {
                Positions[key][control] = FormRig.Vector(data.Positions, control * 3, path);
                Rotations[key][control] = FormRig.Rotation(data.Rotations, control * 4, path);
                if (key > 0)
                {
                    rates[control] = Mathf.Max(rates[control],
                        Quaternion.Angle(Rotations[key - 1][control], Rotations[key][control]) /
                        (Times[key] - Times[key - 1]));
                    translations[control] = Mathf.Max(translations[control],
                        1.5f * Vector3.Distance(Positions[key - 1][control], Positions[key][control]) /
                        (Times[key] - Times[key - 1]));
                }
            }
        }

        if (Times[0] != 0f || Times[Times.Length - 1] != Duration) throw new InvalidDataException(path + "：动作必须覆盖起止时间");
        if (Loop && Builds[0] != Builds[Builds.Length - 1]) throw new InvalidDataException(path + "：循环成形进度不连续");
        for (int control = 0; control < count; control++)
        {
            MaximumAngularRate += rates[control];
            MaximumTranslationRate += translations[control];
            if (Loop && (Vector3.Distance(Positions[0][control], Positions[Times.Length - 1][control]) > .00001f ||
                         Quaternion.Angle(Rotations[0][control], Rotations[Times.Length - 1][control]) > .01f))
                throw new InvalidDataException(path + "：循环动作端点不连续");
        }

        foreach (FormMotionMarkerDescription marker in source.Markers ?? Array.Empty<FormMotionMarkerDescription>())
        {
            if (marker == null || string.IsNullOrWhiteSpace(marker.Id) || markers.ContainsKey(marker.Id) ||
                !FormRig.Finite(marker.At) || marker.At < 0f || marker.At > Duration)
                throw new InvalidDataException(path + "：阶段标记名称或时点无效");
            markers.Add(marker.Id, marker.At);
        }

        StageMarkers = markers.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
    }
}

/// <summary>一份形体共用的绑定骨架及动作，只有值资料和已经解析的名称。</summary>
public sealed class FormRig
{
    /// <summary>与两个显示方式的固定矩阵数组上限一致。</summary>
    public const int MaximumControls = 32;

    /// <summary>实际控制数量。</summary>
    public int Count => parents.Length;

    /// <summary>名称到控制索引。</summary>
    private readonly Dictionary<string, int> indices = new(StringComparer.Ordinal);

    /// <summary>名称到不可变动作。</summary>
    private readonly Dictionary<string, FormMotion> motions = new(StringComparer.Ordinal);

    /// <summary>错误提示所用的实际资源编号。</summary>
    private readonly string formId;

    /// <summary>父级索引。</summary>
    private readonly int[] parents;

    /// <summary>局部绑定姿态。</summary>
    private readonly Vector3[] positions;

    /// <summary>局部绑定旋转。</summary>
    private readonly Quaternion[] rotations;

    /// <summary>模型空间绑定逆矩阵。</summary>
    private readonly Matrix4x4[] inverseBind;

    /// <summary>每个控制影响顶点的绑定范围。</summary>
    private readonly Bounds[] influenceBounds;

    /// <summary>该控制是否拥有实际影响范围。</summary>
    private readonly bool[] hasBounds;

    /// <summary>混合资源中没有控制绑定的固定几何范围。</summary>
    private readonly Bounds? staticBounds;

    /// <summary>控制原点沿父链的最大位移，加绑定点到该控制的实际距离。</summary>
    /// <param name="control">已解析控制。</param><param name="point">绑定空间中的点。</param><param name="used">实际采用的动作。</param>
    internal float RadiusBound(int control, Vector3 point, FormMotion[] used)
    {
        float radius = inverseBind[control].MultiplyPoint3x4(point).magnitude;
        for (int i = control; i >= 0; i = parents[i])
        {
            float offset = positions[i].magnitude;
            foreach (FormMotion motion in used)
            foreach (Vector3[] key in motion.Positions)
                offset = Mathf.Max(offset, key[i].magnitude);
            radius += offset;
        }

        return radius;
    }

    /// <summary>在主线程加载边界复制骨架与动作，运行计算不再读取原始描述。</summary>
    /// <param name="source">已经解析的形体资料。</param>
    /// <param name="fixedBounds">在加载线程取得的固定部件范围。</param>
    internal FormRig(FormDescription source, Bounds? fixedBounds)
    {
        staticBounds = fixedBounds;
        formId = source.Id;
        FormControlDescription[] controls = source.Controls;
        if (controls == null || controls.Length > MaximumControls || source.Motions == null ||
            source.Motions.Length > 64)
            throw new InvalidDataException(source.Id + "：控制骨架或动作数量无效");
        parents = new int[controls.Length];
        positions = new Vector3[Count];
        rotations = new Quaternion[Count];
        inverseBind = new Matrix4x4[Count];
        influenceBounds = new Bounds[Count];
        hasBounds = new bool[Count];
        var bind = new Matrix4x4[Count];
        for (int i = 0; i < Count; i++)
        {
            FormControlDescription control = controls[i];
            if (control == null || string.IsNullOrWhiteSpace(control.Id) || indices.ContainsKey(control.Id))
                throw new InvalidDataException(source.Id + "：控制名称为空或重复");
            parents[i] = string.IsNullOrEmpty(control.Parent) ? -1 : Control(control.Parent);
            indices.Add(control.Id, i);
            if (control.Position?.Length != 3 || control.Rotation?.Length != 4)
                throw new InvalidDataException(source.Id + "/" + control.Id + "：绑定姿态数量无效");
            positions[i] = Vector(control.Position, 0, source.Id);
            rotations[i] = Rotation(control.Rotation, 0, source.Id);
            bind[i] = (parents[i] < 0 ? Matrix4x4.identity : bind[parents[i]]) *
                      Matrix4x4.TRS(positions[i], rotations[i], Vector3.one);
            inverseBind[i] = bind[i].inverse;
            if (control.Minimum == null && control.Maximum == null) continue;
            if (control.Minimum?.Length != 3 || control.Maximum?.Length != 3)
                throw new InvalidDataException(source.Id + "：影响范围不完整");
            Vector3 min = Vector(control.Minimum, 0, source.Id), max = Vector(control.Maximum, 0, source.Id);
            if (min.x > max.x || min.y > max.y || min.z > max.z) throw new InvalidDataException(source.Id + "：影响范围倒置");
            influenceBounds[i].SetMinMax(min, max);
            hasBounds[i] = true;
        }

        foreach (FormMotionDescription motion in source.Motions)
        {
            if (Count == 0 || motion == null || string.IsNullOrWhiteSpace(motion.Id) || motions.ContainsKey(motion.Id))
                throw new InvalidDataException(source.Id + "：动作缺少骨架或名称重复");
            motions.Add(motion.Id, new FormMotion(motion, Count, source.Id + "/" + motion.Id));
        }
    }

    /// <summary>在加载或编译边界解析控制名称，空值表示固定模型空间。</summary>
    /// <param name="id">控制名称。</param>
    public int Control(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        if (indices.TryGetValue(id, out int index)) return index;
        throw new InvalidDataException("形体缺少控制：" + id);
    }

    /// <summary>在编译边界解析一个实际制作动作。</summary>
    /// <param name="id">动作名称。</param>
    public FormMotion Motion(string id) => motions.TryGetValue(id, out FormMotion value)
        ? value
        : throw new InvalidDataException(
            "形体" + formId + "缺少制作动作：" + id + "；可用动作：" + string.Join("、", motions.Keys));

    /// <summary>为单次施放分配可复用姿态，刚性资源没有骨架姿态。</summary>
    public FormPose CreatePose() => Count == 0 ? null : new FormPose(Count);

    /// <summary>按共同时间采样一次局部姿态、层级矩阵、蒙皮及保守边界。</summary>
    /// <param name="motion">已编译动作，空值使用绑定姿态。</param>
    /// <param name="time">动作内部世界时间。</param>
    /// <param name="pose">本次独占输出。</param>
    /// <param name="from">过渡开始时的实际完整局部姿态，空值表示直接采样。</param>
    /// <param name="transition">向当前动作过渡的零至一进度。</param>
    public void Sample(FormMotion motion, float time, FormPose pose, FormPose from = null, float transition = 1f)
    {
        int key = 0;
        float blend = 0f;
        if (motion != null)
        {
            time = motion.Loop ? Mathf.Repeat(time, motion.Duration) : Mathf.Clamp(time, 0f, motion.Duration);
            int lower = 0, upper = motion.Times.Length - 1;
            while (upper - lower > 1)
            {
                int middle = (lower + upper) / 2;
                if (motion.Times[middle] <= time) lower = middle;
                else upper = middle;
            }

            key = lower;
            blend = Mathf.InverseLerp(motion.Times[key], motion.Times[key + 1], time);
        }

        pose.BuildProgress = motion == null ? 1f : Mathf.Lerp(motion.Builds[key], motion.Builds[key + 1], blend);
        if (from != null) pose.BuildProgress = Mathf.Lerp(from.BuildProgress, pose.BuildProgress, transition);
        for (int i = 0; i < Count; i++)
        {
            Vector3 position = motion == null
                ? positions[i]
                : Vector3.Lerp(motion.Positions[key][i], motion.Positions[key + 1][i], blend);
            Quaternion rotation = motion == null
                ? rotations[i]
                : Quaternion.Slerp(motion.Rotations[key][i], motion.Rotations[key + 1][i], blend);
            if (from != null)
            {
                position = Vector3.Lerp(from.Positions[i], position, transition);
                rotation = Quaternion.Slerp(from.Rotations[i], rotation, transition);
            }

            pose.Positions[i] = position;
            pose.Rotations[i] = rotation;
            pose.World[i] = (parents[i] < 0 ? Matrix4x4.identity : pose.World[parents[i]]) *
                            Matrix4x4.TRS(position, rotation, Vector3.one);
            pose.Skin[i] = pose.World[i] * inverseBind[i];
        }

        RebuildBounds(pose);
    }

    /// <summary>柔性表面的控制点共同读取地表；挂点、作用端点与网格使用同一矩阵。</summary>
    /// <param name="pose">已经采样的局部动作。</param>
    /// <param name="transform">实际世界变换。</param>
    /// <param name="terrain">本轮只读地表。</param>
    internal void Conform(FormPose pose, Matrix4x4 transform, AbilityHeightField terrain)
    {
        if (terrain == null) return;
        Matrix4x4 inverse = transform.inverse;
        Vector3 origin = transform.GetColumn(3);
        float reference = terrain.Sample(origin.x, origin.y);
        for (int i = 0; i < Count; i++)
        {
            Vector3 point = transform.MultiplyPoint3x4(pose.World[i].GetColumn(3));
            Vector3 shift = inverse.MultiplyVector(Vector3.forward * (terrain.Sample(point.x, point.y) - reference));
            Matrix4x4 offset = Matrix4x4.Translate(shift);
            pose.World[i] = offset * pose.World[i];
            pose.Skin[i] = offset * pose.Skin[i];
        }

        RebuildBounds(pose);
    }

    /// <summary>由实际控制矩阵重新建立相同的保守几何范围。</summary>
    /// <param name="pose">本次完整姿态。</param>
    internal void RebuildBounds(FormPose pose)
    {
        bool first = !staticBounds.HasValue;
        if (staticBounds.HasValue) pose.Bounds = staticBounds.Value;
        for (int i = 0; i < Count; i++)
        {
            if (!hasBounds[i]) continue;
            Bounds bounds = influenceBounds[i];
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = pose.Skin[i].MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (first)
                {
                    pose.Bounds = new Bounds(point, Vector3.zero);
                    first = false;
                }
                else pose.Bounds.Encapsulate(point);
            }
        }
    }

    /// <summary>资源数字必须有限。</summary>
    /// <param name="value">外部数字。</param>
    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>读取资源中的有限三维坐标。</summary>
    /// <param name="data">已经检查长度的资料。</param>
    /// <param name="offset">开始位置。</param>
    /// <param name="path">资源位置。</param>
    internal static Vector3 Vector(float[] data, int offset, string path)
    {
        var value = new Vector3(data[offset], data[offset + 1], data[offset + 2]);
        if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z)) throw new InvalidDataException(path + "：坐标不是有限值");
        return value;
    }

    /// <summary>读取资源中的单位四元数。</summary>
    /// <param name="data">已经检查长度的资料。</param>
    /// <param name="offset">开始位置。</param>
    /// <param name="path">资源位置。</param>
    internal static Quaternion Rotation(float[] data, int offset, string path)
    {
        var value = new Quaternion(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
        float length = Quaternion.Dot(value, value);
        if (!Finite(length) || Mathf.Abs(length - 1f) > .001f) throw new InvalidDataException(path + "：旋转不是单位四元数");
        return value.normalized;
    }
}

/// <summary>一次形体的可复用数值姿态；发布显示前复制矩阵，不让计算改写旧画面。</summary>
public sealed class FormPose
{
    /// <summary>与控制姿态一同确认的主体成形进度。</summary>
    public float BuildProgress = 1f;

    /// <summary>当前完整局部位置与四元数，用于连续切换而不混合世界矩阵。</summary>
    internal readonly Vector3[] Positions;

    /// <summary>当前局部旋转。</summary>
    internal readonly Quaternion[] Rotations;

    /// <summary>当前模型空间控制矩阵。</summary>
    public readonly Matrix4x4[] World;

    /// <summary>固定上限的当前蒙皮矩阵。</summary>
    public readonly Matrix4x4[] Skin = new Matrix4x4[FormRig.MaximumControls];

    /// <summary>已变形顶点的保守模型空间边界。</summary>
    public Bounds Bounds;

    /// <summary>为独占姿态分配一次必要矩阵。</summary>
    /// <param name="count">已经检查的控制数量。</param>
    internal FormPose(int count)
    {
        Positions = new Vector3[count];
        Rotations = new Quaternion[count];
        World = new Matrix4x4[count];
        for (int i = 0; i < Skin.Length; i++) Skin[i] = Matrix4x4.identity;
    }

    /// <summary>预测及恢复时复制到已分配的缓冲，不分配顶点或新姿态。</summary>
    /// <param name="destination">相同骨架的独占目标。</param>
    internal void CopyTo(FormPose destination)
    {
        Array.Copy(Positions, destination.Positions, Positions.Length);
        Array.Copy(Rotations, destination.Rotations, Rotations.Length);
        Array.Copy(World, destination.World, World.Length);
        Array.Copy(Skin, destination.Skin, Skin.Length);
        destination.Bounds = Bounds;
        destination.BuildProgress = BuildProgress;
    }
}