using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>一次身体动作需要占用的部位；独立离手法术不自动占用这些部位。</summary>
[Flags]
public enum BodyOccupancy
{
    /// <summary>没有身体占用。</summary>
    None = 0,
    /// <summary>右侧前肢。</summary>
    RightArm = 1,
    /// <summary>左侧前肢。</summary>
    LeftArm = 2,
    /// <summary>躯干及落脚支撑。</summary>
    Stance = 4
}

/// <summary>主线程维护的实际身体占用，供不同技能来源共用，不以流派名字互相排斥。</summary>
public static class BodyControlState
{
    /// <summary>当前实际占用的部位。</summary>
    private static readonly Dictionary<long, BodyOccupancy> occupied = new();
    /// <summary>查询需要的部位是否正被其他动作使用。</summary>
    /// <param name="actor">人物稳定编号。</param>
    /// <param name="parts">需要的部位。</param>
    public static bool IsBusy(long actor, BodyOccupancy parts = BodyOccupancy.RightArm | BodyOccupancy.LeftArm | BodyOccupancy.Stance) =>
        occupied.TryGetValue(actor, out BodyOccupancy current) && (current & parts) != 0;
    /// <summary>只在动作正式受理后取得没有冲突的部位。</summary>
    /// <param name="actor">人物稳定编号。</param>
    /// <param name="parts">实际需要的部位。</param>
    public static bool TryAcquire(long actor, BodyOccupancy parts)
    {
        occupied.TryGetValue(actor, out BodyOccupancy current);
        if ((current & parts) != 0) return false;
        occupied[actor] = current | parts; return true;
    }
    /// <summary>结束只解除本次取得的部位，保留其他不冲突动作。</summary>
    /// <param name="actor">人物稳定编号。</param>
    /// <param name="parts">本次占用的部位。</param>
    public static void Release(long actor, BodyOccupancy parts)
    {
        if (!occupied.TryGetValue(actor, out BodyOccupancy current)) return;
        current &= ~parts;
        if (current == BodyOccupancy.None) occupied.Remove(actor); else occupied[actor] = current;
    }
    /// <summary>世界结束时不保留任何身体控制。</summary>
    public static void Clear() => occupied.Clear();
}

/// <summary>可保存、可改造的身体动作；路径坐标按真实体高缩放。</summary>
public sealed class BodyActionDefinition
{
    /// <summary>实际需要的身体部位，目前内容适配提供hand.right及hand.left。</summary>
    public string Effector = "hand.right";
    /// <summary>空值使用手掌；非空时借用实际持有器物，并用指定形体描述握点及刃部。</summary>
    public string WeaponForm;
    /// <summary>器物上实际采用的握持挂点。</summary>
    public string Grip = "grip";
    /// <summary>实际允许接触的开始时点。</summary>
    public float ActiveFrom;
    /// <summary>实际允许接触的结束时点。</summary>
    public float ActiveUntil;
    /// <summary>动作需要的部位及支撑。</summary>
    public BodyOccupancy Occupancy = BodyOccupancy.RightArm | BodyOccupancy.Stance;
    /// <summary>身体与同名body组共用的接触规则；缺省沿用每人一次。</summary>
    public ConstructionContactPolicy Contact;
    /// <summary>在原身体动作的有效阶段维持姿态；缺省没有额外维持。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionHold Hold;
    /// <summary>按原身体动作时钟排列的连续姿态关键点。</summary>
    public BodyMotionKey[] Keys;
}

/// <summary>一项动作中的姿态关键点；各项均参与共同身体姿态计算。</summary>
public sealed class BodyMotionKey
{
    /// <summary>世界秒数。</summary>
    public float At;
    /// <summary>末端相对脚下的位置，依次为前方、侧方和高度，以体高为单位。</summary>
    public float[] Hand = new float[3];
    /// <summary>肩及躯干相对站立姿态的位移。</summary>
    public float[] Torso = new float[3];
    /// <summary>围绕竖直支撑的转体角度。</summary>
    public float Turn;
    /// <summary>掌面或握持轴相对出手方向的三个旋转角度。</summary>
    public float[] Rotation = new float[3];
}

/// <summary>从实际身体采集的数值输入，不持有人物、网格或外部模组对象。</summary>
public readonly struct BodyFrame
{
    /// <summary>真实脚下位置。</summary>
    public readonly Vector3 Foot;
    /// <summary>受理时锁定的水平朝向。</summary>
    public readonly Vector3 Forward;
    /// <summary>实际体高。</summary>
    public readonly float Height;
    /// <summary>站立肩点，按体高归一化。</summary>
    public readonly Vector3 Shoulder;
    /// <summary>两段实际肢体长度。</summary>
    public readonly float UpperLength, LowerLength;
    /// <summary>确定肘部朝向的侧向系数。</summary>
    public readonly float BendSide;
    /// <summary>受理时确定的高低调整，按实际目标身体而非显示镜头计算。</summary>
    public readonly float AimHeight;

    /// <summary>绑定已经由来源核对过的真实身体尺寸。</summary>
    /// <param name="foot">脚下位置。</param>
    /// <param name="forward">水平朝向。</param>
    /// <param name="height">体高。</param>
    /// <param name="shoulder">归一化肩点。</param>
    /// <param name="upperLength">第一段肢体实际长度。</param>
    /// <param name="lowerLength">第二段肢体实际长度。</param>
    /// <param name="bendSide">肘部外侧方向。</param>
    /// <param name="aimHeight">实际末端需要的高低调整。</param>
    public BodyFrame(Vector3 foot, Vector3 forward, float height, Vector3 shoulder,
        float upperLength, float lowerLength, float bendSide, float aimHeight = 0f)
    {
        Foot = foot; Forward = forward; Height = height; Shoulder = shoulder;
        UpperLength = upperLength; LowerLength = lowerLength; BendSide = bendSide;
        AimHeight = aimHeight;
    }
}

/// <summary>一个已经确定的身体姿态，身体显示、持物及作用形状共同读取。</summary>
public readonly struct BodyPose
{
    /// <summary>脚下、肩、肘、手的实际地图空间位置。</summary>
    public readonly Vector3 Foot, Shoulder, Elbow, Hand;
    /// <summary>出手时锁定的水平前方，身体显示不从手掌旋转反推朝向。</summary>
    public readonly Vector3 Forward;
    /// <summary>本时刻的手部坐标系。</summary>
    public readonly Quaternion Rotation;
    /// <summary>实际体高。</summary>
    public readonly float Height;
    /// <summary>躯干偏移及转角，用于人物及身体作用部位对齐。</summary>
    public readonly Vector3 Torso;
    /// <summary>本时刻的支撑转体角度。</summary>
    public readonly float Turn;

    /// <summary>记录一次完整姿态。</summary>
    /// <param name="foot">脚下位置。</param>
    /// <param name="shoulder">肩点。</param>
    /// <param name="elbow">肘点。</param>
    /// <param name="hand">手点。</param>
    /// <param name="rotation">手的朝向。</param>
    /// <param name="height">体高。</param>
    /// <param name="torso">躯干偏移。</param>
    /// <param name="turn">转体角度。</param>
    /// <param name="forward">身体锁定的水平前方。</param>
    internal BodyPose(Vector3 foot, Vector3 shoulder, Vector3 elbow, Vector3 hand,
        Quaternion rotation, float height, Vector3 torso, float turn, Vector3 forward)
    {
        Foot = foot; Shoulder = shoulder; Elbow = elbow; Hand = hand; Forward = forward;
        Rotation = rotation; Height = height; Torso = torso; Turn = turn;
    }
}

/// <summary>编译后只保存数值关键点，不在每步读取或修改原始资料。</summary>
public sealed class CompiledBodyAction
{
    /// <summary>实际末端部位。</summary>
    public string Effector { get; }
    /// <summary>实际需要占用的身体及支撑。</summary>
    public BodyOccupancy Occupancy { get; }
    /// <summary>作用开始和结束的世界秒数。</summary>
    public float ActiveFrom { get; }
    /// <summary>作用结束的世界秒数。</summary>
    public float ActiveUntil { get; }
    /// <summary>身体完成收势的时点，之后独立形体仍可继续。</summary>
    public float Duration { get; }
    /// <summary>需要借用的器物形体。</summary>
    public string WeaponForm { get; }
    /// <summary>实际握点。</summary>
    public string Grip { get; }
    /// <summary>由姿态关键点推导的最大角速度，用于高速持物动作拆步。</summary>
    public float MaximumAngularRate { get; }
    /// <summary>重心相对脚下的最大归一化位移，供真实运动的空间筛选使用。</summary>
    public float MaximumTorsoOffset { get; }
    /// <summary>从编辑资料复制的连续取样点。</summary>
    private readonly Key[] keys;
    /// <summary>独立于动作快慢的姿态维持。</summary>
    private readonly CompiledConstructionHold hold;
    /// <summary>原动作的作用结束，用于相同姿态上的高低调整。</summary>
    private readonly float actionUntil;
    /// <summary>身体开始维持的世界时点。</summary>
    internal float HoldAt => hold.At;
    /// <summary>身体结束维持的世界时点。</summary>
    internal float HoldUntil => hold.At + hold.Seconds;

    /// <summary>在资料边界检查动作时段、部位和完整姿态。</summary>
    /// <param name="definition">要编译的动作。</param>
    /// <param name="lifetime">能力总时长。</param>
    internal CompiledBodyAction(BodyActionDefinition definition, float lifetime)
    {
        if (string.IsNullOrWhiteSpace(definition.Effector)) throw new InvalidDataException("Body/Effector：缺少实际发力部位编号");
        if (definition.Keys == null || definition.Keys.Length < 2 || definition.Keys.Length > 128)
            throw new InvalidDataException("Body/Keys：需要2至128个实际动作关键点");
        if (!Finite(definition.ActiveFrom) || !Finite(definition.ActiveUntil) ||
            definition.ActiveFrom < 0 || definition.ActiveFrom >= definition.ActiveUntil || definition.ActiveUntil > lifetime ||
            definition.Occupancy == BodyOccupancy.None || (definition.Occupancy & ~(BodyOccupancy.RightArm | BodyOccupancy.LeftArm | BodyOccupancy.Stance)) != 0)
            throw new InvalidDataException("Body/ActiveFrom / ActiveUntil / Occupancy：需要有实际部位占用、且在整次寿命内递增的有效时段");
        keys = new Key[definition.Keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            BodyMotionKey source = definition.Keys[i];
            if (source == null || !Finite(source.At) || !Finite(source.Turn) || source.At < 0 || source.At > lifetime ||
                i > 0 && source.At <= keys[i - 1].At)
                throw new InvalidDataException("Body/Keys[" + i + "]/At：姿态时间须为有限数且严格递增");
            keys[i] = new Key
            {
                At = source.At,
                Hand = Vector(source.Hand, "Body/Keys[" + i + "]/Hand"),
                Torso = Vector(source.Torso, "Body/Keys[" + i + "]/Torso"),
                Rotation = Quaternion.Euler(Vector(source.Rotation, "Body/Keys[" + i + "]/Rotation")),
                Turn = source.Turn
            };
        }
        hold = new CompiledConstructionHold(definition.Hold, lifetime, "body/Hold");
        if (definition.Hold != null && (hold.At < definition.ActiveFrom || hold.At > definition.ActiveUntil))
            throw new InvalidDataException("Body/Hold：维持时点须位于实际有效阶段内");
        Duration = keys[keys.Length - 1].At + hold.Seconds;
        if (keys[0].At != 0f || Duration <= 0f || definition.ActiveUntil > keys[keys.Length - 1].At || Duration > lifetime)
            throw new InvalidDataException("Body/Keys：动作必须从起势覆盖至有效阶段结束和收势，且不超过Lifetime");
        if (definition.Effector == "hand.right" && (definition.Occupancy & BodyOccupancy.RightArm) == 0 ||
            definition.Effector == "hand.left" && (definition.Occupancy & BodyOccupancy.LeftArm) == 0)
            throw new InvalidDataException("Body/Occupancy：没有占用实际发力部位" + definition.Effector);
        Effector = definition.Effector; Occupancy = definition.Occupancy;
        ActiveFrom = definition.ActiveFrom; actionUntil = definition.ActiveUntil; ActiveUntil = actionUntil + hold.Seconds;
        WeaponForm = definition.WeaponForm; Grip = definition.Grip;
        float maximum = 0f;
        for (int i = 1; i < keys.Length; i++)
            maximum = Mathf.Max(maximum, 1.5f * (Mathf.Abs(keys[i].Turn - keys[i - 1].Turn) +
                Quaternion.Angle(keys[i - 1].Rotation, keys[i].Rotation)) / (keys[i].At - keys[i - 1].At));
        if (!Finite(maximum) || maximum > 7200f) throw new InvalidDataException("身体姿态超过每秒7200度的动作预算");
        MaximumAngularRate = maximum;
        foreach (Key key in keys) MaximumTorsoOffset = Mathf.Max(MaximumTorsoOffset, key.Torso.magnitude);
    }

    /// <summary>取样连续手路与转体，再用真实骨长解出肘部；不会延长手臂追赶越界目标。</summary>
    /// <param name="age">能力世界时间。</param>
    /// <param name="frame">来源身体本步输入。</param>
    public BodyPose Sample(float age, in BodyFrame frame)
    {
        age = hold.Local(age);
        int next = 1;
        while (next < keys.Length - 1 && age > keys[next].At) next++;
        Key a = keys[next - 1], b = keys[next];
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.At, b.At, age));
        float turn = Mathf.Lerp(a.Turn, b.Turn, t);
        Vector3 forward = frame.Forward.normalized;
        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        Quaternion basis = Quaternion.LookRotation(forward, Vector3.forward);
        Vector3 local = Vector3.Lerp(a.Hand, b.Hand, t), torso = Vector3.Lerp(a.Torso, b.Torso, t);
        Vector3 ToWorld(Vector3 value) => forward * value.x + side * value.y + Vector3.forward * value.z;
        Vector3 offset = ToWorld(torso) * frame.Height;
        Vector3 shoulderLocal = Quaternion.AngleAxis(turn, Vector3.forward) * ToWorld(frame.Shoulder);
        Vector3 shoulder = frame.Foot + offset + shoulderLocal * frame.Height;
        Vector3 desired = frame.Foot + ToWorld(local) * frame.Height;
        float aim = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Mathf.Max(.0001f, ActiveFrom))) *
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((keys[keys.Length - 1].At - age) / Mathf.Max(.0001f, keys[keys.Length - 1].At - actionUntil)));
        desired.z += frame.AimHeight * aim;
        Vector3 direction = desired - shoulder;
        float reach = Mathf.Clamp(direction.magnitude, Mathf.Abs(frame.UpperLength - frame.LowerLength) + .00001f,
            frame.UpperLength + frame.LowerLength - .00001f);
        direction = direction.sqrMagnitude < 1e-10f ? forward : direction.normalized;
        Vector3 hand = shoulder + direction * reach;
        Vector3 pole = side * frame.BendSide - Vector3.forward * .2f;
        pole -= direction * Vector3.Dot(pole, direction);
        if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(direction, forward);
        if (pole.sqrMagnitude < 1e-8f) pole = side;
        float along = (frame.UpperLength * frame.UpperLength - frame.LowerLength * frame.LowerLength + reach * reach) / (2f * reach);
        float outward = Mathf.Sqrt(Mathf.Max(0f, frame.UpperLength * frame.UpperLength - along * along));
        Vector3 elbow = shoulder + direction * along + pole.normalized * outward;
        Quaternion rotation = basis * Quaternion.Slerp(a.Rotation, b.Rotation, t);
        return new BodyPose(frame.Foot, shoulder, elbow, hand, rotation, frame.Height, offset, turn, forward);
    }

    /// <summary>读取有限的三维姿态参数。</summary>
    /// <param name="values">外部坐标。</param>
    /// <param name="path">关键点的具体字段。</param>
    private static Vector3 Vector(float[] values, string path)
    {
        if (values?.Length != 3 || values.Any(value => !Finite(value))) throw new InvalidDataException(path + "：需要三个有限坐标");
        return new Vector3(values[0], values[1], values[2]);
    }
    /// <summary>拒绝会污染整份姿态的无效外部数值。</summary>
    /// <param name="value">外部数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    /// <summary>已经复制并解析的单个姿态。</summary>
    private struct Key
    {
        /// <summary>世界时间。</summary>
        internal float At;
        /// <summary>末端路径及躯干偏移。</summary>
        internal Vector3 Hand, Torso;
        /// <summary>转体角度。</summary>
        internal float Turn;
        /// <summary>手部朝向。</summary>
        internal Quaternion Rotation;
    }
}
