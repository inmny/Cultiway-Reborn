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

    /// <summary>躯干及落脚支撑；全身发力期间两侧局部动作均须等待。</summary>
    Stance = 4
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

    /// <summary>明确的出手时点；旧资料缺省沿用有效窗口起点。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? ReleaseAt;

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

    /// <summary>真实接触后向肩点收回的体高比例，零表示没有接触回弹。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float ImpactRecoil;

    /// <summary>一次接触回弹使用的动作秒数，随动作快慢一同缩放。</summary>
    public float RecoilSeconds = .09f;

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
        Foot = foot;
        Forward = forward;
        Height = height;
        Shoulder = shoulder;
        UpperLength = upperLength;
        LowerLength = lowerLength;
        BendSide = bendSide;
        AimHeight = aimHeight;
    }
}

/// <summary>一个已经确定的身体姿态，身体显示、持物及作用形状共同读取。</summary>
public readonly struct BodyPose
{
    /// <summary>脚下、肩、肘、手的实际地图空间位置。</summary>
    public readonly Vector3 Foot, Shoulder, Elbow, Hand;

    /// <summary>共同站立支撑中的两侧落脚位置，不随躯干转体在地上打转。</summary>
    public readonly Vector3 LeftFoot, RightFoot;

    /// <summary>与肩肘掌同刻的身体承重点，供躯干和支撑显示读取。</summary>
    public readonly Vector3 CenterOfMass;

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
        Foot = foot;
        Shoulder = shoulder;
        Elbow = elbow;
        Hand = hand;
        Forward = forward;
        Rotation = rotation;
        Height = height;
        Torso = torso;
        Turn = turn;
        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        LeftFoot = foot - side * height * .1f;
        RightFoot = foot + side * height * .1f;
        CenterOfMass = foot + torso + Vector3.forward * height * .46f;
    }
}

/// <summary>编译后只保存数值关键点，不在每步读取或修改原始资料。</summary>
public sealed class CompiledBodyAction
{
    /// <summary>当前真实手部绑定采用的模型与体高比例。</summary>
    public const float HandScale = .7f;

    /// <summary>当前真实器物绑定采用的模型与体高比例。</summary>
    public const float WeaponScale = .42f;

    /// <summary>当前人形肩点到中线的体高比例。</summary>
    public const float ShoulderSide = .16f;

    /// <summary>当前真实上臂与体高之比。</summary>
    public const float UpperArm = .24f;

    /// <summary>当前真实前臂与体高之比。</summary>
    public const float LowerArm = .27f;

    /// <summary>实际手路和躯干构成的单位体高可达包络。</summary>
    public float HandReach { get; }

    /// <summary>手路加真实掌面或握持器物作用形状的单位体高可达包络。</summary>
    public float ContactReach { get; }

    /// <summary>实际末端部位。</summary>
    public string Effector { get; }

    /// <summary>实际需要占用的身体及支撑。</summary>
    public BodyOccupancy Occupancy { get; }

    /// <summary>是否实际控制躯干与支撑；局部肢体动作不占公共恢复和身体朝向。</summary>
    public bool ControlsStance => (Occupancy & BodyOccupancy.Stance) != 0;

    /// <summary>已经编译的出手时点，与有效和收势采用同一世界时钟。</summary>
    public float ReleaseAt { get; }

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

    /// <summary>每处姿态的共同切线，创建时计算；取样时不再分配或解读资料。</summary>
    private readonly Key[] tangents;

    /// <summary>只由真实接触启动的回弹幅度及历时。</summary>
    private readonly float impactRecoil, recoilSeconds;

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
            definition.ActiveFrom < 0 || definition.ActiveFrom >= definition.ActiveUntil ||
            definition.ActiveUntil > lifetime ||
            definition.Occupancy == BodyOccupancy.None || (definition.Occupancy &
                                                           ~(BodyOccupancy.RightArm | BodyOccupancy.LeftArm |
                                                             BodyOccupancy.Stance)) != 0)
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
                Rotation = Vector(source.Rotation, "Body/Keys[" + i + "]/Rotation"),
                Turn = source.Turn
            };
            if (i > 0)
                for (int axis = 0; axis < 3; axis++)
                    keys[i].Rotation[axis] = keys[i - 1].Rotation[axis] +
                                             Mathf.DeltaAngle(keys[i - 1].Rotation[axis], keys[i].Rotation[axis]);
        }

        if (!Finite(definition.ImpactRecoil) || definition.ImpactRecoil < 0 || definition.ImpactRecoil > .25f ||
            !Finite(definition.RecoilSeconds) || definition.RecoilSeconds <= 0)
            throw new InvalidDataException("Body/ImpactRecoil / RecoilSeconds：回弹须为0至四分之一体高，历时须为正有限数");
        impactRecoil = definition.ImpactRecoil;
        recoilSeconds = definition.RecoilSeconds;
        tangents = BuildTangents();
        hold = new CompiledConstructionHold(definition.Hold, lifetime, "body/Hold");
        if (definition.Hold != null && (hold.At < definition.ActiveFrom || hold.At > definition.ActiveUntil))
            throw new InvalidDataException("Body/Hold：维持时点须位于实际有效阶段内");
        Duration = keys[keys.Length - 1].At + hold.Seconds;
        if (keys[0].At != 0f || Duration <= 0f || definition.ActiveUntil > keys[keys.Length - 1].At ||
            Duration > lifetime)
            throw new InvalidDataException("Body/Keys：动作必须从起势覆盖至有效阶段结束和收势，且不超过Lifetime");
        if (definition.Effector == "hand.right" && (definition.Occupancy & BodyOccupancy.RightArm) == 0 ||
            definition.Effector == "hand.left" && (definition.Occupancy & BodyOccupancy.LeftArm) == 0)
            throw new InvalidDataException("Body/Occupancy：没有占用实际发力部位" + definition.Effector);
        Effector = definition.Effector;
        ReleaseAt = definition.ReleaseAt ?? definition.ActiveFrom;
        if (!Finite(ReleaseAt) || ReleaseAt < 0f || ReleaseAt > definition.ActiveFrom)
            throw new InvalidDataException("Body/ReleaseAt：出手时点须从零开始且不晚于有效窗口起点");
        Occupancy = definition.Occupancy;
        // 实际资料带动躯干时必须取得全身控制，不能靠省略声明让两个动作同时扭动身体。
        if (keys.Any(key => key.Torso != Vector3.zero || key.Turn != 0f)) Occupancy |= BodyOccupancy.Stance;
        ActiveFrom = definition.ActiveFrom;
        actionUntil = definition.ActiveUntil;
        ActiveUntil = actionUntil + hold.Seconds;
        WeaponForm = definition.WeaponForm;
        Grip = definition.Grip;
        float maximum = 0f;
        for (int i = 1; i < keys.Length; i++)
            maximum = Mathf.Max(maximum, 3f * (Mathf.Abs(keys[i].Turn - keys[i - 1].Turn) +
                                               SumAbs(keys[i].Rotation - keys[i - 1].Rotation)) /
                                         (keys[i].At - keys[i - 1].At));
        if (!Finite(maximum) || maximum > 7200f) throw new InvalidDataException("身体姿态超过每秒7200度的动作预算");
        MaximumAngularRate = maximum;
        foreach (Key key in keys) MaximumTorsoOffset = Mathf.Max(MaximumTorsoOffset, key.Torso.magnitude);
        // 手路采用保守三维包络，夹紧骨长的最小半径余量也包含在内；器物范围读取真实握点。
        HandReach = Mathf.Max(keys.Max(key => new Vector2(key.Hand.x, key.Hand.y).magnitude),
                        keys.Max(key => new Vector2(key.Torso.x, key.Torso.y).magnitude) + ShoulderSide) +
                    Math.Abs(UpperArm - LowerArm);
        var hand = ConstructionReferences.Resolve("Body/Effector/hand",
            () => Cultiway.Core.Visuals.Forms.FormResources.Load("hand"));
        var resource = WeaponForm == null
            ? hand
            : ConstructionReferences.Resolve("Body/WeaponForm",
                () => Cultiway.Core.Visuals.Forms.FormResources.Load(WeaponForm));
        Matrix4x4 handMatrix =
            Matrix4x4.TRS(Vector3.zero, WeaponForm == null ? Quaternion.identity : Quaternion.Euler(0, -90, 0),
                Vector3.one * HandScale) * Matrix4x4.Translate(new Vector3(0, .04f, 0));
        Matrix4x4 contactMatrix = handMatrix;
        if (WeaponForm != null)
        {
            Vector3 handGrip = hand.Anchors.Single(anchor => anchor.Id == "grip").Transform.GetColumn(3);
            contactMatrix = ConstructionReferences.Resolve("Body/Grip", () => resource.Grip(Grip)).Align(
                Matrix4x4.TRS(handMatrix.MultiplyPoint3x4(handGrip), Quaternion.identity, Vector3.one), WeaponScale);
        }

        ContactReach = HandReach + resource.Contacts.Select(contact =>
                Mathf.Max(contactMatrix.MultiplyPoint3x4(contact.A).magnitude,
                    contactMatrix.MultiplyPoint3x4(contact.B).magnitude) + contact.Radius * contactMatrix.lossyScale.x)
            .DefaultIfEmpty(0f).Max();
    }

    /// <summary>取样连续手路与转体，再用真实骨长解出肘部；不会延长手臂追赶越界目标。</summary>
    /// <param name="age">能力世界时间。</param>
    /// <param name="frame">来源身体本步输入。</param>
    /// <param name="contactAt">首个真实接触时点，负值表示没有接触。</param>
    public BodyPose Sample(float age, in BodyFrame frame, float contactAt = -1f)
    {
        age = hold.Local(age);
        int next = 1;
        while (next < keys.Length - 1 && age > keys[next].At) next++;
        Key a = keys[next - 1], b = keys[next];
        float span = b.At - a.At, t = Mathf.InverseLerp(a.At, b.At, age);
        Key va = tangents[next - 1], vb = tangents[next];
        float turn = Curve(a.Turn, b.Turn, va.Turn, vb.Turn, span, t);
        Vector3 forward = frame.Forward.normalized;
        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        Quaternion basis = Quaternion.LookRotation(forward, Vector3.forward);
        Vector3 local = Curve(a.Hand, b.Hand, va.Hand, vb.Hand, span, t),
            torso = Curve(a.Torso, b.Torso, va.Torso, vb.Torso, span, t);
        Vector3 ToWorld(Vector3 value) => forward * value.x + side * value.y + Vector3.forward * value.z;
        Vector3 offset = ToWorld(torso) * frame.Height;
        Vector3 shoulderLocal = Quaternion.AngleAxis(turn, Vector3.forward) * ToWorld(frame.Shoulder);
        Vector3 shoulder = frame.Foot + offset + shoulderLocal * frame.Height;
        Vector3 desired = frame.Foot + ToWorld(local) * frame.Height;
        float aim = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Mathf.Max(.0001f, ActiveFrom))) *
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01((keys[keys.Length - 1].At - age) /
                                      Mathf.Max(.0001f, keys[keys.Length - 1].At - actionUntil)));
        desired.z += frame.AimHeight * aim;
        float recoil = 0f;
        if (contactAt >= 0f && impactRecoil > 0f)
        {
            float progress = Mathf.Clamp01((age - hold.Local(contactAt)) / recoilSeconds);
            float pulse = Mathf.Sin(Mathf.PI * progress);
            recoil = impactRecoil * frame.Height * pulse * pulse;
        }

        Vector3 direction = desired - shoulder;
        float reach = Mathf.Clamp(direction.magnitude, Mathf.Abs(frame.UpperLength - frame.LowerLength) + .00001f,
            frame.UpperLength + frame.LowerLength - .00001f);
        reach = Mathf.Max(Mathf.Abs(frame.UpperLength - frame.LowerLength) + .00001f, reach - recoil);
        direction = direction.sqrMagnitude < 1e-10f ? forward : direction.normalized;
        Vector3 hand = shoulder + direction * reach;
        Vector3 pole = side * frame.BendSide - Vector3.forward * .2f;
        pole -= direction * Vector3.Dot(pole, direction);
        if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(direction, forward);
        if (pole.sqrMagnitude < 1e-8f) pole = side;
        float along = (frame.UpperLength * frame.UpperLength - frame.LowerLength * frame.LowerLength + reach * reach) /
                      (2f * reach);
        float outward = Mathf.Sqrt(Mathf.Max(0f, frame.UpperLength * frame.UpperLength - along * along));
        Vector3 elbow = shoulder + direction * along + pole.normalized * outward;
        Quaternion rotation =
            basis * Quaternion.Euler(Curve(a.Rotation, b.Rotation, va.Rotation, vb.Rotation, span, t));
        return new BodyPose(frame.Foot, shoulder, elbow, hand, rotation, frame.Height, offset, turn, forward);
    }

    /// <summary>读取有限的三维姿态参数。</summary>
    /// <param name="values">外部坐标。</param>
    /// <param name="path">关键点的具体字段。</param>
    private static Vector3 Vector(float[] values, string path)
    {
        if (values?.Length != 3 || values.Any(value => !Finite(value)))
            throw new InvalidDataException(path + "：需要三个有限坐标");
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <summary>拒绝会污染整份姿态的无效外部数值。</summary>
    /// <param name="value">外部数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>三轴转角的保守变化总量，避免复合转动抵消后低估拆步预算。</summary>
    /// <param name="value">相邻姿态差。</param>
    private static float SumAbs(Vector3 value) => Mathf.Abs(value.x) + Mathf.Abs(value.y) + Mathf.Abs(value.z);

    /// <summary>相邻段同向时保留连续速度，真正反向或平台处才停下，不超出关键点包络。</summary>
    private Key[] BuildTangents()
    {
        var result = new Key[keys.Length];
        for (int i = 1; i < keys.Length - 1; i++)
        {
            float before = keys[i].At - keys[i - 1].At, after = keys[i + 1].At - keys[i].At;
            result[i].Hand = Slope(keys[i - 1].Hand, keys[i].Hand, keys[i + 1].Hand, before, after);
            result[i].Torso = Slope(keys[i - 1].Torso, keys[i].Torso, keys[i + 1].Torso, before, after);
            result[i].Rotation = Slope(keys[i - 1].Rotation, keys[i].Rotation, keys[i + 1].Rotation, before, after);
            result[i].Turn = Slope(keys[i - 1].Turn, keys[i].Turn, keys[i + 1].Turn, before, after);
        }

        return result;
    }

    /// <summary>按不等距姿态时刻计算不会越过两端的连续速度。</summary>
    /// <param name="a">前一点。</param><param name="b">本点。</param><param name="c">后一点。</param>
    /// <param name="before">前段秒数。</param><param name="after">后段秒数。</param>
    private static float Slope(float a, float b, float c, float before, float after)
    {
        float left = (b - a) / before, right = (c - b) / after;
        if (left == 0 || right == 0 || Mathf.Sign(left) != Mathf.Sign(right)) return 0f;
        float w1 = 2f * after + before, w2 = after + 2f * before;
        return (w1 + w2) / (w1 / left + w2 / right);
    }

    /// <summary>各轴使用相同时间和相同的单调切线规则。</summary>
    /// <param name="a">前一点。</param><param name="b">本点。</param><param name="c">后一点。</param>
    /// <param name="before">前段秒数。</param><param name="after">后段秒数。</param>
    private static Vector3 Slope(Vector3 a, Vector3 b, Vector3 c, float before, float after) => new(
        Slope(a.x, b.x, c.x, before, after), Slope(a.y, b.y, c.y, before, after),
        Slope(a.z, b.z, c.z, before, after));

    /// <summary>三次姿态曲线，两端的速度分别由相邻完整段共同决定。</summary>
    /// <param name="a">起点。</param><param name="b">终点。</param><param name="va">起点速度。</param>
    /// <param name="vb">终点速度。</param><param name="span">本段秒数。</param><param name="t">本段进度。</param>
    private static float Curve(float a, float b, float va, float vb, float span, float t)
    {
        float square = t * t, cube = square * t;
        return (2 * cube - 3 * square + 1) * a + (cube - 2 * square + t) * span * va +
               (-2 * cube + 3 * square) * b + (cube - square) * span * vb;
    }

    /// <summary>位置与转角均按同一段进度取样。</summary>
    /// <param name="a">起点。</param><param name="b">终点。</param><param name="va">起点速度。</param>
    /// <param name="vb">终点速度。</param><param name="span">本段秒数。</param><param name="t">本段进度。</param>
    private static Vector3 Curve(Vector3 a, Vector3 b, Vector3 va, Vector3 vb, float span, float t) => new(
        Curve(a.x, b.x, va.x, vb.x, span, t), Curve(a.y, b.y, va.y, vb.y, span, t),
        Curve(a.z, b.z, va.z, vb.z, span, t));

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
        internal Vector3 Rotation;
    }
}