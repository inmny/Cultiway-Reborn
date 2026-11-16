using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>创作普通飞行资料，各部位各自选取曲线、目标与后续过程。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只选择连续自由飞行；流散、射线、加速下落及间隔转移仍保留各自原理。</summary>
    /// <param name="draft">待改造的独立完整资料。</param><param name="plan">原术已经核对的实际部位及命名步骤。</param>
    private static ConstructionOperation[] CreationFlights(AbilityDefinition draft, CompiledAbility plan)
    {
        bool Usable(ConstructionOperation flight) => flight.Kind == ConstructionOperationKind.Fly &&
            !flight.WorldVelocity && flight.Transfer == null &&
            (flight.Acceleration == null || flight.Acceleration.All(value => value == 0f));
        // 同一原部位展开出的每份实例都须是普通飞行，命名步骤中的流散也参与判断。
        var nodes = plan.Nodes.Where(node => node.DeclarationPath != null).GroupBy(node => node.DeclarationPath)
            .Where(group => group.All(node => node.FlowSettings == null)).Select(group =>
            {
                ConstructionNode node = draft.Root;
                foreach (string id in group.Key.Split('/').Skip(1)) node = node.Children.Single(child => child.Id == id);
                return node;
            });
        return nodes.SelectMany(node => node.Operations).Where(Usable)
            .Concat(draft.Steps.Where(step => Usable(step.Operation) &&
                plan.Steps.Where(compiled => compiled.Id == step.Id).All(compiled => plan.Nodes[compiled.Node].FlowSettings == null))
                .Select(step => step.Operation)).ToArray();
    }

    /// <summary>以原实际前向建立横向平面，独立选择普通控制点；不改名称、时点或触发引用。</summary>
    /// <param name="flight">本次独立草稿中的普通飞行操作。</param><param name="random">受理时固定的随机源。</param>
    private static void GenerateFlightPath(ConstructionOperation flight, System.Random random)
    {
        // 先取得实际前向，再生成偏移，最后固定朝向和进度；有限目标弧线不叠加中心前进。
        Vector3 velocity = new(flight.Vector[0], flight.Vector[1], flight.Vector[2]);
        Vector3 forward = velocity.sqrMagnitude > 0f ? velocity.normalized :
            flight.Path?.Forward is { Length: 3 } axis ? new Vector3(axis[0], axis[1], axis[2]).normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .9f ? Vector3.right : Vector3.up).normalized;
        Vector3 lift = Vector3.Cross(side, forward).normalized;
        float period = Between(1.5f, 2.5f), radius = Between(.35f, 1.3f), phase = Between(0f, Mathf.PI * 2f);
        // 已有明确目标的改造保留其语义，不以固定目标弧线取代持续跟随或返回。
        int shape = random.Next(flight.Goal == null ? 7 : 6);
        ConstructionPath path = shape switch
        {
            0 => null,
            1 => ConstructionPathCurves.Wave(period, side * radius + lift * Between(-.5f, .5f), phase),
            2 => ConstructionPathCurves.Zigzag(period, side * radius, phase),
            3 => ConstructionPathCurves.Spiral(3f, period, side * radius, lift * radius, phase: phase),
            4 => ConstructionPathCurves.Spiral(3f, period, side * radius, lift * radius, Between(.2f, .7f), phase),
            5 => ConstructionPathCurves.ContractingSpiral(3f, period, side * radius, lift * radius, Between(.1f, .3f), phase),
            _ => ConstructionPathCurves.Arc(Between(1.2f, 2f), Between(1f, 4f))
        };
        if (path?.Space == ConstructionPathSpace.TargetLine)
        {
            flight.Vector = new float[3];
            flight.TurnRate = 0f;
        }
        else if (velocity.sqrMagnitude == 0f)
        {
            velocity = forward * Between(5f, 9f);
            flight.Vector = Array(velocity);
        }
        if (path != null)
        {
            path.Forward = random.Next(4) == 0 ? null : Array(forward);
            path.TurnRate = Between(240f, 600f);
            if (path.Space == ConstructionPathSpace.Local)
            {
                if (random.Next(3) == 0) path.DistancePerSecond = velocity.magnitude;
                if (random.Next(3) == 0)
                {
                    ConstructionPath extra = ConstructionPathCurves.Wave(period * Between(.8f, 1.4f), lift * Between(.15f, .5f), phase);
                    path.Additions = new[] { new ConstructionPathTerm { Keys = extra.Keys, Loop = extra.Loop } };
                }
            }
        }
        flight.Path = path;
        if (path != null && flight.Goal?.Arrival == ConstructionFlightArrival.Continue) flight.Goal.DirectArrival = false;

        float Between(float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);
        float[] Array(Vector3 value) => new[] { value.x, value.y, value.z };
    }

    /// <summary>独立选择固定方向、目标更新、转向和真实到达；返回条件上的来源目标保持为来源。</summary>
    /// <param name="flight">已有局部曲线或直行的真实操作。</param><param name="random">本次固定随机源。</param>
    private static void GenerateFlightGoal(ConstructionOperation flight, System.Random random)
    {
        bool returning = flight.Goal?.Anchor == ConstructionFlightAnchor.Source;
        var goal = new ConstructionFlightGoal
        {
            Anchor = returning ? ConstructionFlightAnchor.Source : random.Next(4) == 0 ? ConstructionFlightAnchor.Direction : ConstructionFlightAnchor.Target,
            Follow = random.Next(2) == 0,
            Arrival = (ConstructionFlightArrival)random.Next(1, 4), Radius = .1f
        };
        if (goal.Anchor == ConstructionFlightAnchor.Direction)
        {
            goal.Follow = false;
            goal.Arrival = ConstructionFlightArrival.None;
        }
        else
        {
            goal.Approach = random.Next(2) == 0;
            goal.DirectArrival = !goal.Approach && (flight.Path == null || goal.Arrival != ConstructionFlightArrival.Continue) && random.Next(2) == 0;
            if (random.Next(3) == 0)
            {
                ConstructionPath steering = ConstructionPathCurves.Wave(1.5f + (float)random.NextDouble(), new Vector3(.4f, .2f, 0f));
                steering.Forward = null;
                goal.Steering = new ConstructionFlightSteering { Side = (float)random.NextDouble() - .5f, Decay = .5f + (float)random.NextDouble(), Path = steering };
            }
        }
        flight.TurnRate = 180f + (float)random.NextDouble() * 360f;
        flight.Goal = goal;
    }

    /// <summary>达到真实离开起点距离后改向来源，继续使用原部位，不重新发射或重置命中。</summary>
    /// <param name="outward">已经选定的投出操作。</param><param name="random">固定输入的随机源。</param>
    private static ConstructionOperation ReturnFlight(ConstructionOperation outward, System.Random random) => new()
    {
        Kind = ConstructionOperationKind.Fly, At = outward.At, Vector = (float[])outward.Vector.Clone(),
        InheritVelocity = false, TurnRate = 540f, Ground = outward.Ground,
        When = new ConstructionCondition { Trigger = ConstructionChangeTrigger.DistanceFromFlightStart, Threshold = 2f + (float)random.NextDouble() * 3f },
        Goal = new ConstructionFlightGoal { Anchor = ConstructionFlightAnchor.Source, Follow = true, Arrival = ConstructionFlightArrival.End, Radius = .1f, Approach = true }
    };
}

/// <summary>旧请求保留原来的随机序列，新生成规则只用于升级后受理的请求。</summary>
internal sealed class CreationV11ToV12 : ISaveMigration
{
    /// <summary>只支持前十版生成规则的正式请求区段。</summary>
    public int FromVersion => 11;

    /// <summary>原输入已经完整，不补抽曲线、目标或资源。</summary>
    /// <param name="data">保存层独立复制的旧资料。</param>
    public JObject Apply(JObject data) => data;
}
