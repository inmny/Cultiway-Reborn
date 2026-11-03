using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>与同一姿态关键点一起保存的足底路径及本体离地高度，均以体高为单位。</summary>
public sealed class BodySupportMotion
{
    /// <summary>左足相对本体脚下的前、侧、高位置。</summary>
    public float[] LeftFoot = [0, -.1f, 0];

    /// <summary>右足相对本体脚下的前、侧、高位置。</summary>
    public float[] RightFoot = [0, .1f, 0];

    /// <summary>本体相对实际地表的高度，零为落地；不代替躯干的屈伸。</summary>
    public float Rise;
}

/// <summary>本时刻的足底和离地高度，支撑、腿部姿态及人物运动共用。</summary>
public readonly struct BodySupportPose
{
    /// <summary>相对本体脚下的左右足底。</summary>
    public readonly Vector3 LeftFoot, RightFoot;

    /// <summary>本体离地高度与体高之比。</summary>
    public readonly float Rise;

    /// <summary>足底实际接地时才提供支撑，空中不凭空钉住双脚。</summary>
    public bool LeftPlanted => Rise <= .000001f && LeftFoot.z <= .000001f;

    /// <summary>右足是否实际接地。</summary>
    public bool RightPlanted => Rise <= .000001f && RightFoot.z <= .000001f;

    /// <summary>记录同一时间已经完成的支撑。</summary>
    /// <param name="left">左足。</param><param name="right">右足。</param><param name="rise">离地高度。</param>
    internal BodySupportPose(Vector3 left, Vector3 right, float rise)
    {
        LeftFoot = left;
        RightFoot = right;
        Rise = rise;
    }
}

/// <summary>复制有限的支撑过程，取样不读取可编辑资料或游戏对象。</summary>
internal sealed class CompiledBodySupportMotion
{
    /// <summary>与身体关键点相同的时间及足底资料。</summary>
    private readonly (float At, BodySupportPose Pose)[] keys;

    /// <summary>该过程实际需要的离地包络。</summary>
    internal readonly float MaximumRise;

    /// <summary>移动足底及本体所需的拆步速度，随整段调速同步变化。</summary>
    internal readonly float MaximumRate;

    /// <summary>检查完整过程及落脚连续性，不接受地上滑足或结束时突然落地。</summary>
    /// <param name="source">同一身体动作关键点。</param><param name="left">起始左足。</param>
    /// <param name="right">起始右足。</param>
    internal CompiledBodySupportMotion(BodyMotionKey[] source, Vector3 left, Vector3 right)
    {
        keys = new (float, BodySupportPose)[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            BodySupportMotion motion = source[i].Support;
            if (motion == null || float.IsNaN(motion.Rise) || float.IsInfinity(motion.Rise) || motion.Rise < 0f)
                throw new InvalidDataException("Body/Keys/Support：须逐点提供有限且不低于地表的支撑过程");
            Vector3 l = Read(motion.LeftFoot), r = Read(motion.RightFoot);
            keys[i] = (source[i].At, new BodySupportPose(l, r, motion.Rise));
            MaximumRise = Mathf.Max(MaximumRise, motion.Rise);
            if (i == 0 || i == source.Length - 1)
            {
                if (motion.Rise != 0f || l != left || r != right)
                    throw new InvalidDataException("Body/Keys/Support：起势与收势须回到声明的双足地面站姿");
            }

            if (i == 0) continue;
            BodySupportPose before = keys[i - 1].Pose;
            CheckPlant(before.LeftFoot, l);
            CheckPlant(before.RightFoot, r);
            MaximumRate = Mathf.Max(MaximumRate,
                810f * Mathf.Max(Vector3.Distance(before.LeftFoot, l), Vector3.Distance(before.RightFoot, r),
                    Mathf.Abs(motion.Rise - before.Rise)) / (source[i].At - source[i - 1].At));

            // 两端均在地面时落脚不能平移；抬足或腾起后才允许换落点。
            void CheckPlant(Vector3 a, Vector3 b)
            {
                if (before.Rise == 0f && motion.Rise == 0f && a.z == 0f && b.z == 0f && a != b)
                    throw new InvalidDataException("Body/Keys/Support：接地足底不能滑动，换落点须先抬足");
            }
        }

        if (float.IsNaN(MaximumRate) || float.IsInfinity(MaximumRate) || MaximumRate > 7200f)
            throw new InvalidDataException("Body/Keys/Support：支撑过程超过动作速度预算");
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