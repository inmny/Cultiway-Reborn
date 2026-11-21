using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>与同一姿态关键点一起保存的足底路径及本体离地高度，均以体高为单位。</summary>
public sealed class BodySupportMotion
{
    /// <summary>实际命名足相对自身自然落点的前、侧、高请求；空值采用人形双足。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, float[]> Parts;

    /// <summary>左足相对本体脚下的前、侧、高位置。</summary>
    public float[] LeftFoot = [0, -.1f, 0];

    /// <summary>右足相对本体脚下的前、侧、高位置。</summary>
    public float[] RightFoot = [0, .1f, 0];

    /// <summary>本体相对实际地表的高度，零为落地；不代替躯干的屈伸。</summary>
    public float Rise;
}

/// <summary>一处真实命名足的同刻位置及是否请求接地；所在过程决定坐标系。</summary>
internal readonly struct BodyFootPose
{
    /// <summary>实际原骨段名称。</summary>
    internal readonly string Part;
    /// <summary>制作阶段是相对自然落点的位移，确认运动后是世界锚点。</summary>
    internal readonly Vector3 Point;
    /// <summary>请求接地；实际网格仍须通过地表和承托检查。</summary>
    internal readonly bool Planted;
    /// <summary>真实落地时原足面至世界的变换；空值尚未踩实，不写入长期资料。</summary>
    internal readonly Matrix4x4? Plant;
    /// <summary>保存已经冻结的同刻数值。</summary>
    /// <param name="part">真实足部。</param><param name="point">当前坐标系内的位置。</param><param name="planted">接地请求。</param>
    /// <param name="plant">真实落地足面的世界变换。</param>
    internal BodyFootPose(string part, Vector3 point, bool planted, Matrix4x4? plant = null)
    { Part = part; Point = point; Planted = planted; Plant = plant; }
}

/// <summary>本时刻的足底和离地高度，支撑、腿部姿态及人物运动共用。</summary>
public readonly struct BodySupportPose
{
    /// <summary>非人身体自己的命名足；不读取左右足替身。</summary>
    internal readonly BodyFootPose[] Parts;
    /// <summary>命名足已经换算到世界时采用的整体转向，单位为度。</summary>
    internal readonly float Heading;
    /// <summary>同轮命名足已经完成的实际身体，主线程与绘制不再重复求解。</summary>
    internal readonly BodyJointFrame Joint;
    /// <summary>相对本体脚下的左右足底。</summary>
    public readonly Vector3 LeftFoot, RightFoot;

    /// <summary>本体离地高度与体高之比。</summary>
    public readonly float Rise;

    /// <summary>足底实际接地时才提供支撑，空中不凭空钉住双脚。</summary>
    public bool LeftPlanted { get; }

    /// <summary>右足是否实际接地。</summary>
    public bool RightPlanted { get; }

    /// <summary>记录同一时间已经完成的支撑。</summary>
    /// <param name="left">左足。</param><param name="right">右足。</param><param name="rise">离地高度。</param>
    /// <param name="leftPlanted">实际地形中的左足接地情况，空值从制作高度判断。</param>
    /// <param name="rightPlanted">实际地形中的右足接地情况。</param>
    internal BodySupportPose(Vector3 left, Vector3 right, float rise, bool? leftPlanted = null,
        bool? rightPlanted = null)
    {
        Parts = null;
        Heading = 0f;
        Joint = null;
        LeftFoot = left;
        RightFoot = right;
        Rise = rise;
        LeftPlanted = leftPlanted ?? (rise <= .000001f && left.z <= .000001f);
        RightPlanted = rightPlanted ?? (rise <= .000001f && right.z <= .000001f);
    }

    /// <summary>保存实际身体的命名足过程，不建立人形双足。</summary>
    /// <param name="parts">已经冻结的各足。</param><param name="rise">身体离地比例。</param><param name="heading">共同整体转向。</param>
    /// <param name="joint">本轮实际身体姿态。</param>
    internal BodySupportPose(BodyFootPose[] parts, float rise, float heading = 0f, BodyJointFrame joint = null)
    { Parts = parts; Rise = rise; Heading = heading; Joint = joint; LeftFoot = RightFoot = Vector3.zero; LeftPlanted = RightPlanted = false; }
}

/// <summary>复制有限的支撑过程，取样不读取可编辑资料或游戏对象。</summary>
internal sealed class CompiledBodySupportMotion
{
    /// <summary>非人过程明确提供的实际名称，空值为人形过程。</summary>
    internal readonly string[] Parts;
    /// <summary>与身体关键点相同的时间及足底资料。</summary>
    private readonly (float At, BodySupportPose Pose)[] keys;

    /// <summary>该过程实际需要的离地包络。</summary>
    internal readonly float MaximumRise;

    /// <summary>移动足底及本体所需的拆步速度，随整段调速同步变化。</summary>
    internal readonly float MaximumRate;

    /// <summary>腾起与保持竖直运动所需的定义层控制预算。</summary>
    internal readonly double MovementWork;

    /// <summary>每次腾起的起地与触地时间，原地抬足不创建腾空段。</summary>
    private readonly (float From, float Until)[] flights;

    /// <summary>检查完整过程及落脚连续性，不接受地上滑足或结束时突然落地。</summary>
    /// <param name="source">同一身体动作关键点。</param><param name="left">起始左足。</param>
    /// <param name="right">起始右足。</param>
    /// <param name="named">按真实原骨段读取各足，不采用人形落点。</param>
    /// <param name="worldRise">根点使用世界速度预算，足底仍使用身体姿态预算。</param>
    internal CompiledBodySupportMotion(BodyMotionKey[] source, Vector3 left, Vector3 right, bool named = false,
        bool worldRise = false)
    {
        // 先固定真实部位集合，再复制各关键点，最后登记共同腾起区间。
        if (named)
        {
            if (source[0].Support?.Parts == null || source[0].Support.Parts.Count == 0 || source[0].Support.Parts.Count > 32)
                throw new InvalidDataException("Body/Keys/Support/Parts：需要一至32处实际命名足");
            Parts = source[0].Support.Parts.Keys.OrderBy(part => part, StringComparer.Ordinal).ToArray();
            if (Parts.Any(part => !part.StartsWith("part:", StringComparison.Ordinal) || part.Length <= 5))
                throw new InvalidDataException("Body/Keys/Support/Parts：须使用实际原骨段名称part:名称");
        }
        keys = new (float, BodySupportPose)[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            BodySupportMotion motion = source[i].Support;
            if (motion == null || float.IsNaN(motion.Rise) || float.IsInfinity(motion.Rise) || motion.Rise < 0f)
                throw new InvalidDataException("Body/Keys/Support：须逐点提供有限且不低于地表的支撑过程");
            Vector3 l = named ? Vector3.zero : Read(motion.LeftFoot), r = named ? Vector3.zero : Read(motion.RightFoot);
            if (named)
            {
                if (motion.Parts == null || !Parts.SequenceEqual(motion.Parts.Keys.OrderBy(part => part, StringComparer.Ordinal)))
                    throw new InvalidDataException("Body/Keys/Support/Parts：每个关键点须提供同一组实际足");
                var feet = Parts.Select(part => new BodyFootPose(part, Read(motion.Parts[part]),
                    motion.Rise == 0f && motion.Parts[part][2] == 0f)).ToArray();
                if ((i == 0 || i == source.Length - 1) && feet.Any(foot => foot.Point != Vector3.zero))
                    throw new InvalidDataException("Body/Keys/Support/Parts：起势和收势须接回各足自己的自然落点");
                keys[i] = (source[i].At, new BodySupportPose(feet, motion.Rise));
            }
            else
            {
                if (motion.Parts != null) throw new InvalidDataException("Body/Keys/Support/Parts：人形过程不能夹带非人足部");
                keys[i] = (source[i].At, new BodySupportPose(l, r, motion.Rise));
            }
            MaximumRise = Mathf.Max(MaximumRise, motion.Rise);
            if (i == 0 || i == source.Length - 1)
            {
                if (motion.Rise != 0f || !named && (l != left || r != right))
                    throw new InvalidDataException("Body/Keys/Support：起势与收势须回到声明的双足地面站姿");
            }

            if (i == 0) continue;
            BodySupportPose before = keys[i - 1].Pose;
            float climb = Mathf.Max(0f, motion.Rise - before.Rise);
            MovementWork += climb * (1d + 1.5d * climb / (source[i].At - source[i - 1].At)) * .1d;
            CheckPlant(before.LeftFoot, l);
            CheckPlant(before.RightFoot, r);
            float travel = Mathf.Max(Vector3.Distance(before.LeftFoot, l), Vector3.Distance(before.RightFoot, r));
            if (named)
                for (int foot = 0; foot < Parts.Length; foot++)
                {
                    Vector3 a = before.Parts[foot].Point, b = keys[i].Pose.Parts[foot].Point;
                    CheckPlant(a, b); travel = Mathf.Max(travel, Vector3.Distance(a, b));
                }
            float seconds = source[i].At - source[i - 1].At;
            float rise = Mathf.Abs(motion.Rise - before.Rise);
            if (worldRise && 1.5f * rise / seconds > 64f)
                throw new InvalidDataException("Body/Keys/Support：世界离地速度超过每秒64格");
            MaximumRate = Mathf.Max(MaximumRate, 810f * (worldRise ? travel : Mathf.Max(travel, rise)) / seconds);

            // 两端均在地面时落脚不能平移；抬足或腾起后才允许换落点。
            void CheckPlant(Vector3 a, Vector3 b)
            {
                if (before.Rise == 0f && motion.Rise == 0f && a.z == 0f && b.z == 0f && a != b)
                    throw new InvalidDataException("Body/Keys/Support：接地足底不能滑动，换落点须先抬足");
            }
        }

        if (float.IsNaN(MaximumRate) || float.IsInfinity(MaximumRate) || MaximumRate > 7200f)
            throw new InvalidDataException("Body/Keys/Support：支撑过程超过动作速度预算");
        var intervals = new System.Collections.Generic.List<(float, float)>();
        for (int i = 0; i < keys.Length - 1; i++)
            if (keys[i].Pose.Rise == 0f && keys[i + 1].Pose.Rise > 0f)
            {
                int end = i + 1;
                while (keys[end].Pose.Rise > 0f) end++;
                intervals.Add((keys[i].At, keys[end].At));
                i = end - 1;
            }

        flights = intervals.ToArray();
    }

    /// <summary>读取当前腾起段，以实际落地高度连续衔接下降。</summary>
    /// <param name="age">动作局部时间。</param><param name="progress">该段进度。</param>
    /// <param name="until">落地局部时点。</param>
    internal void Flight(float age, out float progress, out float until)
    {
        foreach (var flight in flights)
            if (age >= flight.From && age <= flight.Until)
            {
                progress = Mathf.InverseLerp(flight.From, flight.Until, age);
                until = flight.Until;
                return;
            }

        progress = 1f;
        until = age;
    }

    /// <summary>无超调地连接足底与本体高度，腾起、顶点、触地均连续衔接。</summary>
    /// <param name="age">已经过共同维持处理的动作时间。</param>
    internal BodySupportPose Sample(float age)
    {
        int next = 1;
        while (next < keys.Length - 1 && age > keys[next].At) next++;
        var a = keys[next - 1];
        var b = keys[next];
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.At, b.At, age));
        if (Parts != null)
        {
            float rise = Mathf.Lerp(a.Pose.Rise, b.Pose.Rise, t);
            var feet = new BodyFootPose[Parts.Length];
            for (int i = 0; i < feet.Length; i++)
            {
                Vector3 point = Vector3.Lerp(a.Pose.Parts[i].Point, b.Pose.Parts[i].Point, t);
                feet[i] = new BodyFootPose(Parts[i], point, rise <= .000001f && point.z <= .000001f);
            }
            return new BodySupportPose(feet, rise);
        }
        return new BodySupportPose(Vector3.Lerp(a.Pose.LeftFoot, b.Pose.LeftFoot, t),
            Vector3.Lerp(a.Pose.RightFoot, b.Pose.RightFoot, t), Mathf.Lerp(a.Pose.Rise, b.Pose.Rise, t));
    }

    /// <summary>在资料入口读取有限且不低于本体脚下的足底坐标。</summary>
    /// <param name="value">保存的三个坐标。</param>
    private static Vector3 Read(float[] value)
    {
        if (value == null || value.Length != 3 || value.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ||
            value[2] < 0f)
            throw new InvalidDataException("Body/Keys/Support：足底需要三个有限坐标，高度不得为负");
        return new Vector3(value[0], value[1], value[2]);
    }
}
