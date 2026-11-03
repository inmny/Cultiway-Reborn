using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>身体动作的实际脚下与足底锚点；预测只计算数字，确认后才移动人物。</summary>
internal sealed class BodyLocomotion
{
    /// <summary>可选实际模型阻挡；只在主线程预测中查询，不依赖显示回调。</summary>
    internal static System.Func<Vector3, Vector3, float, bool> ObstacleAllowed;

    /// <summary>本次实际人物，只在主线程查询原版通行规则。</summary>
    private readonly Actor actor;

    /// <summary>共同动作和已经采集的固定身体。</summary>
    private readonly CompiledBodyAction action;

    /// <summary>另一套实际身体，空值只有原版参考身体。</summary>
    private readonly BodyFit fit;

    /// <summary>起势坐标系，路径不因人物转身被重复旋转。</summary>
    private readonly Vector3 forward, side;

    /// <summary>只在确认后替换的完整运动与落脚状态。</summary>
    private State current;

    /// <summary>同一时点的实际人物脚下及支撑输入。</summary>
    internal readonly struct Pose
    {
        /// <summary>实际人物脚下，含共同地表与离地高度。</summary>
        internal readonly Vector3 Root;

        /// <summary>已由世界锚点换算的足底，不把接地足底随人物一起平移。</summary>
        internal readonly BodySupportPose Support;

        /// <summary>保存同一时刻的数值。</summary>
        /// <param name="root">人物脚下。</param><param name="support">实际支撑。</param>
        internal Pose(Vector3 root, BodySupportPose support)
        {
            Root = root;
            Support = support;
        }
    }

    /// <summary>一份可复制的完整预测，不引用可变原始资料。</summary>
    private struct State
    {
        /// <summary>共同动作时间及该时刻实际体高。</summary>
        internal float Age, Height;

        /// <summary>腾起时的实际地面基准及本时刻离地比例，空中不跟着下方山坡突然升降。</summary>
        internal float FlightGround, Rise, RequestedZ;

        /// <summary>制作过程是否仍处于这次腾起中。</summary>
        internal bool Airborne;

        /// <summary>人物及两足的实际世界位置。</summary>
        internal Vector3 Root, Left, Right;

        /// <summary>足底是否正在提供地面支撑。</summary>
        internal bool LeftPlanted, RightPlanted;
    }

    /// <summary>从真实起势建立双足，不读取三维显示回调。</summary>
    /// <param name="actor">实际人物。</param><param name="action">本次共同动作。</param>
    /// <param name="frame">已采集的身体及起势脚下。</param><param name="terrain">共同地表。</param>
    internal BodyLocomotion(Actor actor, CompiledBodyAction action, in BodyFrame frame, AbilityHeightField terrain)
    {
        this.actor = actor;
        this.action = action;
        fit = frame.Fit;
        forward = frame.Forward;
        side = Vector3.Cross(forward, Vector3.forward);
        current = new State { Root = frame.Foot, Height = frame.Height, FlightGround = Ground(terrain, frame.Foot) };
        UpdateFeet(ref current, action.Support(0f), terrain);
    }

    /// <summary>外部搬运后不再让旧步法把人物拉回原路径。</summary>
    internal bool OwnsPosition => Vector2.Distance(actor.current_position, current.Root) < .0001f;

    /// <summary>预测任一共同时间，不修改人物或已经确认的锚点。</summary>
    /// <param name="age">施放世界秒数。</param><param name="height">本步体高。</param>
    /// <param name="terrain">本轮共同地表。</param>
    internal Pose Sample(float age, float height, AbilityHeightField terrain) =>
        Describe(Calculate(age, height, terrain));

    /// <summary>真实接触边界已经确认后，才一起提交脚下和落脚。</summary>
    /// <param name="age">已经确认的时间。</param><param name="height">本步实际体高。</param>
    /// <param name="terrain">本轮共同地表。</param>
    internal Pose Commit(float age, float height, AbilityHeightField terrain)
    {
        current = Calculate(age, height, terrain);
        return Describe(current);
    }

    /// <summary>依次推进局部曲线、原版阻挡和实际足底，失败的位移不会在下一步补跳过去。</summary>
    /// <param name="age">请求的共同时间。</param><param name="height">本步体高。</param>
    /// <param name="terrain">共同地表。</param>
    private State Calculate(float age, float height, AbilityHeightField terrain)
    {
        State value = current;
        if (height != value.Height)
        {
            value.Left = value.Root + (value.Left - value.Root) * (height / value.Height);
            value.Right = value.Root + (value.Right - value.Root) * (height / value.Height);
            value.Height = height;
        }

        age = Mathf.Clamp(age, value.Age, action.Duration);
        while (value.Age < age)
        {
            float next = Mathf.Min(age, value.Age + Mathf.Min(.02f,
                action.FootworkStep(value.Age) / Mathf.Max(1f, height)));
            if (next <= value.Age) next = age;
            action.Footwork(value.Age, out Vector3 before, out _);
            action.Footwork(next, out Vector3 after, out _);
            Vector3 requested = value.Root + Map(after - before) * height;
            BodySupportPose support = action.Support(next);
            State candidate = value;
            candidate.Age = next;
            candidate.Airborne = support.Rise > .000001f;
            if (candidate.Airborne && !value.Airborne) candidate.FlightGround = Ground(terrain, value.Root);
            candidate.Root = requested;
            SetHeight(ref candidate, support, terrain);
            UpdateFeet(ref candidate, support, terrain);
            if (!Allowed(value, candidate, terrain))
            {
                // 二分只处理这一小段，已受阻的剩余行程随时间流逝，不积成下一次瞬移。
                float low = 0f, high = 1f;
                for (int i = 0; i < 18; i++)
                {
                    float middle = (low + high) * .5f;
                    State trial = candidate;
                    trial.Root = Vector3.Lerp(value.Root, requested, middle);
                    SetHeight(ref trial, support, terrain);
                    UpdateFeet(ref trial, support, terrain, value);
                    if (Allowed(value, trial, terrain)) low = middle;
                    else high = middle;
                }

                candidate.Root = Vector3.Lerp(value.Root, requested, low);
                SetHeight(ref candidate, support, terrain);
                UpdateFeet(ref candidate, support, terrain, value);
            }

            value = candidate;
        }

        SetHeight(ref value, action.Support(age), terrain);
        UpdateFeet(ref value, action.Support(age), terrain);
        return value;
    }

    /// <summary>接地足保持世界锚点，抬起后跟随共同身体，重新落脚才取得新锚点。</summary>
    /// <param name="value">本时刻完整身体。</param><param name="support">制作的同刻足底。</param>
    /// <param name="terrain">实际地表。</param><param name="previous">试探阻挡时沿用原落脚状态。</param>
    private void UpdateFeet(ref State value, BodySupportPose support, AbilityHeightField terrain,
        State? previous = null)
    {
        State old = previous ?? value;
        Quaternion heading = Quaternion.AngleAxis(action.Heading(value.Age), Vector3.forward);
        Vector3 root = value.Root;
        float height = value.Height;
        bool airborne = value.Airborne;
        value.Left = Foot(old.Left, support.LeftFoot, old.LeftPlanted && support.LeftPlanted);
        value.Right = Foot(old.Right, support.RightFoot, old.RightPlanted && support.RightPlanted);
        value.LeftPlanted = support.LeftPlanted;
        value.RightPlanted = support.RightPlanted;

        Vector3 Foot(Vector3 anchor, Vector3 local, bool planted)
        {
            Vector3 point = planted ? anchor : root + heading * Map(local) * height;
            float floor = Ground(terrain, point);
            point.z = planted ? floor :
                airborne ? Mathf.Max(floor, root.z + local.z * height) : floor + local.z * height;
            return point;
        }
    }

    /// <summary>原版墙格、水域和熔岩规则，以及固定腿骨共同限制实际根点。</summary>
    /// <param name="from">已确认的起点。</param><param name="to">本次候选。</param>
    /// <param name="terrain">实际地表。</param>
    private bool Allowed(in State from, in State to, AbilityHeightField terrain)
    {
        if (ObstacleAllowed?.Invoke(from.Root, to.Root, to.Height) == false) return false;
        Vector2 point = to.Root;
        if (!Toolbox.inMapBorder(ref point)) return false;
        WorldTile start = World.world.GetTileSimple((int)from.Root.x, (int)from.Root.y),
            end = World.world.GetTileSimple((int)to.Root.x, (int)to.Root.y);
        float rise = to.Root.z - Ground(terrain, to.Root);
        if (to.Airborne && Ground(terrain, to.Root) >
            to.RequestedZ + .000001f &&
            Vector2.Distance(from.Root, to.Root) > .000001f) return false;
        if (end != start)
        {
            bool leavingBlock = start.Type.block && (!start.Type.mountains || end.Type.mountains);
            if (end.Type.block && !leavingBlock && rise <= end.Type.block_height) return false;
            if (rise <= .000001f && end.Type.ocean && !start.Type.liquid && !actor.isWaterCreature()) return false;
            if (rise <= .000001f && end.Type.lava && !start.Type.lava && !actor.isImmuneToFire()) return false;
        }

        if (!to.LeftPlanted && !to.RightPlanted) return true;
        return CanStand(to);
    }

    /// <summary>在最低合法髋高仍无法连到地面足底时停止根点，不能伸长腿或拖动已踩实的脚。</summary>
    /// <param name="value">候选身体及已固定的足底。</param>
    private bool CanStand(in State value)
    {
        float minimum = .125f - CompiledBodyAction.HipHeight;
        Vector3 left = Local(value.Left - value.Root) / value.Height,
            right = Local(value.Right - value.Root) / value.Height;
        if (value.LeftPlanted) minimum = Mathf.Max(minimum, left.z + .125f - CompiledBodyAction.HipHeight);
        if (value.RightPlanted) minimum = Mathf.Max(minimum, right.z + .125f - CompiledBodyAction.HipHeight);
        if (fit != null)
        {
            if (value.LeftPlanted) minimum = Mathf.Max(minimum, left.z + fit.LeftMinimum - fit.LeftHip.z);
            if (value.RightPlanted) minimum = Mathf.Max(minimum, right.z + fit.RightMinimum - fit.RightHip.z);
        }

        Quaternion turn =
            Quaternion.AngleAxis(action.Heading(value.Age) + action.StanceTurn(value.Age), Vector3.forward);
        return Leg(left, true, value.LeftPlanted) && Leg(right, false, value.RightPlanted);

        bool Leg(Vector3 foot, bool leftSide, bool planted)
        {
            if (!planted) return true;
            Vector3 centre = Vector3.forward * (CompiledBodyAction.HipHeight + minimum);
            if (Vector3.Distance(centre, foot) > CompiledBodyAction.LegReach - .00001f) return false;
            BodyLegFit native = BodyLegFit.Native(leftSide);
            if (!Fits(native.Hip, native.Upper + native.Lower)) return false;
            return fit == null || Fits(leftSide ? fit.LeftHip : fit.RightHip, leftSide ? fit.LeftLeg : fit.RightLeg);

            bool Fits(Vector3 hip, float length)
            {
                Vector3 rotated = Local(turn * Map(hip)) + Vector3.forward * minimum;
                return Vector3.Distance(rotated, foot) <= length * BodyJointSolver.Extension - .00001f;
            }
        }
    }

    /// <summary>最后才把世界足底换成固定起势方向中的数值输入，工作线程不访问人物。</summary>
    /// <param name="value">已经完成的同刻身体。</param>
    private Pose Describe(in State value) => new(value.Root, new BodySupportPose(
        Local(value.Left - value.Root) / value.Height, Local(value.Right - value.Root) / value.Height,
        value.Rise, value.LeftPlanted, value.RightPlanted));

    /// <summary>地面移动跟随真实地表，空中保留起跳基准，提前碰到地面时不穿入地表。</summary>
    /// <param name="value">本时刻完整运动。</param><param name="support">同刻制作高度。</param>
    /// <param name="terrain">共同地表。</param>
    private void SetHeight(ref State value, BodySupportPose support, AbilityHeightField terrain)
    {
        float floor = Ground(terrain, value.Root);
        float baseline = floor;
        if (value.Airborne)
        {
            action.Flight(value.Age, out float progress, out float until);
            action.Footwork(value.Age, out Vector3 here, out _);
            action.Footwork(until, out Vector3 landing, out _);
            float destination = Ground(terrain, value.Root + Map(landing - here) * value.Height);
            baseline = Mathf.Lerp(value.FlightGround, destination, Mathf.SmoothStep(0f, 1f, progress));
        }

        value.RequestedZ = baseline + support.Rise * value.Height;
        value.Root.z = Mathf.Max(floor, value.RequestedZ);
        value.Rise = (value.Root.z - floor) / value.Height;
    }

    /// <summary>制作坐标转地图方向。</summary><param name="value">前、侧、高。</param>
    private Vector3 Map(Vector3 value) => forward * value.x + side * value.y + Vector3.forward * value.z;

    /// <summary>地图方向转固定制作坐标。</summary><param name="value">地图位移。</param>
    private Vector3 Local(Vector3 value) => new(Vector3.Dot(value, forward), Vector3.Dot(value, side), value.z);

    /// <summary>只读已经采集的共同地表。</summary><param name="terrain">本轮地表。</param><param name="point">地图位置。</param>
    private static float Ground(AbilityHeightField terrain, Vector3 point) => terrain?.Sample(point.x, point.y) ?? 0f;
}