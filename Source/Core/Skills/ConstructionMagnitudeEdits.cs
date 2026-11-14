using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>数量、尺寸、运动速度与历时的共用改造，保存实际参数，施放不再重复叠加词条。</summary>
public static partial class ConstructionMagnitudeEdits
{
    /// <summary>放大生成主体及其附属几何，不放大真实身体、基础伤害或布局中心。</summary>
    /// <param name="source">已有构造。</param>
    /// <param name="factor">尺寸倍率。</param>
    /// <param name="reprice">当前命令计算需求；旧正式创法请求关闭此项以保持原结果。</param>
    public static AbilityDefinition Resize(AbilityDefinition source, float factor, bool reprice = true)
    {
        Factor(factor);
        AbilityCompiler.Compile(source, cache: false);
        if (source.Root == null) throw new InvalidDataException("真实身体不能凭空放大，请先显化主体或改变实际体型");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        result.Root.Scale *= factor;
        Visit(result.Root, node =>
        {
            if (node != result.Root && node.Activation?.Placement == ConstructionEventPlacement.Event)
                node.Scale *= factor;
        });
        foreach (var operation in AllOperations(result))
        {
            if (operation.Conduction != null)
            {
                operation.Conduction.Range *= factor;
                operation.Conduction.MaximumCells = (int)Math.Min(262144,
                    Math.Max(1, operation.Conduction.MaximumCells * (double)factor * factor));
                operation.Conduction.MaximumTargets = (int)Math.Min(65536,
                    Math.Max(1, operation.Conduction.MaximumTargets * (double)factor * factor));
            }

            if (operation.WorldMatter != null)
                operation.WorldMatter.MaximumVolume *= factor * factor * factor;
        }

        AbilityCompiler.Compile(result, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, result) : result;
    }

    /// <summary>提高独立运动速度和转向，路径按相同比例走快；纯身体动作压缩完整历时。</summary>
    /// <param name="source">已有构造。</param>
    /// <param name="factor">实际运动速度倍率。</param>
    /// <param name="reprice">是否按实际前后规模计算需求。</param>
    public static AbilityDefinition Haste(AbilityDefinition source, float factor, bool reprice = true)
    {
        Factor(factor);
        AbilityCompiler.Compile(source, cache: false);
        if (source.Root == null) return Duration(source, 1f / factor, reprice);
        AbilityDefinition result = ConstructionEditing.Copy(source);
        bool changed = false;
        foreach (ConstructionOperation operation in AllOperations(result))
        {
            if (operation.Ray != null)
            {
                operation.Ray.TurnRate *= factor;
                changed = true;
            }
            if (operation.Fracture != null)
            {
                changed = true;
                operation.Fracture.Speed *= factor;
                operation.Fracture.Spin *= factor;
                operation.Fracture.Gravity *= factor * factor;
            }

            if (operation.Kind is ConstructionOperationKind.Gather or ConstructionOperationKind.Recover)
            {
                changed = true;
                operation.Seconds /= factor;
                continue;
            }

            if (operation.Kind is not (ConstructionOperationKind.Fly or ConstructionOperationKind.Spin)) continue;
            changed = true;
            Multiply(operation.Vector, factor);
            operation.TurnRate *= factor;
            if (operation.Acceleration != null) Multiply(operation.Acceleration, factor * factor);
            if (operation.Path == null) continue;
            operation.Path.TurnRate *= factor;
            foreach (ConstructionPathKey key in operation.Path.Keys) key.At /= factor;
        }

        if (!changed) throw new InvalidDataException("此构造没有飞行、旋转或聚合运动，可用历时倍率改变其动作快慢");
        AbilityCompiler.Compile(result, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, result) : result;
    }

    /// <summary>伸缩完整世界历时并保持对应姿态和路径；接触规则保留，资源与恢复需求按实际规模计算。</summary>
    /// <param name="source">已有身体或生成构造。</param>
    /// <param name="factor">全部过程的历时倍率。</param>
    /// <param name="reprice">是否按实际前后规模计算需求。</param>
    public static AbilityDefinition Duration(AbilityDefinition source, float factor, bool reprice = true)
    {
        Factor(factor);
        CompiledAbility plan = AbilityCompiler.Compile(source, cache: false);
        AbilityDefinition result = ConstructionEditing.Copy(source);
        // 缺省制作时长先按各自实际资源固定，再同步变换原部位及命名步骤。
        Visit(result.Root, node =>
        {
            foreach (var operation in node.Operations.Where(o =>
                         o.Kind == ConstructionOperationKind.Animate && o.Seconds == 0f))
                operation.Seconds = FormResources.Load(node.Form).Rig.Motion(operation.Clip).Duration;
        });
        foreach (var step in result.Steps.Where(s =>
                     s.Operation.Kind == ConstructionOperationKind.Animate && s.Operation.Seconds == 0f))
            step.Operation.Seconds = plan.Nodes[plan.Steps.First(s => s.Id == step.Id).Node].Resource.Rig
                .Motion(step.Operation.Clip).Duration;
        result.Lifetime *= factor;
        if (result.Body != null)
        {
            result.Body.ActiveFrom *= factor;
            result.Body.ActiveUntil *= factor;
            result.Body.ReleaseAt *= factor;
            result.Body.RecoilSeconds *= factor;
            if (result.Body.Stages != null)
                foreach (BodyActionStage stage in result.Body.Stages)
                { stage.From *= factor; stage.Until *= factor; stage.Interval *= factor; }
            if (result.Body.Responses != null)
                foreach (BodyResultResponse response in result.Body.Responses) response.Seconds *= factor;
            if (result.Body.Hold != null)
            {
                result.Body.Hold.At *= factor;
                result.Body.Hold.Seconds *= factor;
            }

            foreach (BodyMotionKey key in result.Body.Keys) key.At *= factor;
            if (result.Body.Construction?.Footwork != null)
            {
                result.Body.Construction.Footwork.TurnRate /= factor;
                foreach (var key in result.Body.Construction.Footwork.Keys) key.At *= factor;
            }
        }

        Visit(result.Root, node =>
        {
            node.AppearAt *= factor;
            node.DisappearAt *= factor;
            if (node.Hold != null)
            {
                node.Hold.At *= factor;
                node.Hold.Seconds *= factor;
            }

            if (node.Activation != null) node.Activation.Delay *= factor;
            node.ActiveFrom *= factor;
            if (node.ActiveUntil >= 0) node.ActiveUntil *= factor;
        });
        foreach (ConstructionOperation operation in AllOperations(result))
        {
            operation.At *= factor;
            if (operation.Hold != null)
            {
                operation.Hold.At *= factor;
                operation.Hold.Seconds *= factor;
            }

            if (operation.Fracture != null)
            {
                operation.Fracture.Seconds *= factor;
                operation.Fracture.Speed /= factor;
                operation.Fracture.Spin /= factor;
                operation.Fracture.Gravity /= factor * factor;
            }

            if (operation.When != null) operation.When.Delay *= factor;
            if (operation.Conduction?.Pulses > 1) operation.Conduction.Interval *= factor;
            operation.Seconds *= factor;
            operation.BlendSeconds *= factor;
            if (operation.Ray != null) operation.Ray.TurnRate /= factor;
            if (operation.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Spin)
            {
                Multiply(operation.Vector, 1f / factor);
                operation.TurnRate /= factor;
                if (operation.Acceleration != null) Multiply(operation.Acceleration, 1f / (factor * factor));
            }

            if (operation.Path == null) continue;
            operation.Path.TurnRate /= factor;
            foreach (ConstructionPathKey key in operation.Path.Keys) key.At *= factor;
        }

        AbilityCompiler.Compile(result, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, result) : result;
    }

    /// <summary>复制同一生成构造，可齐射或依次显现，各份保留自己的完整寿命和接触规则，真实身体只发力一次。</summary>
    /// <param name="source">带有生成主体的已有构造。</param>
    /// <param name="count">整次生成的总份数，含原来的一份。</param>
    /// <param name="spacing">相邻主体的横向布局间隔；身体坐标沿X，地图坐标沿Y。</param>
    /// <param name="interval">相邻显现的世界秒间隔，零表示齐射。</param>
    /// <param name="reprice">当前命令计算实际需求；旧请求保留原总份数费用。</param>
    public static AbilityDefinition Repeat(AbilityDefinition source, int count, float spacing, float interval,
        bool reprice = true)
    {
        CompiledAbility plan = AbilityCompiler.Compile(source, cache: false);
        if (source.Root == null) throw new InvalidDataException("数量改造需要生成主体，真实身体不会因此多出肢体，请先显化");
        if (count < 1 || count > 64 || !Finite(spacing) || spacing < 0 || !Finite(interval) || interval < 0 ||
            count > 1 && (plan.FormCount * count + 1 > AbilityCompiler.MaxForms || plan.Steps.Length * count > 128))
            throw new InvalidDataException("数量应为1至64，间隔须为非负有限数，展开后不能超过128个部位或128项操作输出");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        if (count == 1) return result;
        float originalLifetime = source.Lifetime;
        result.Lifetime += interval * (count - 1);
        if (!reprice) result.Cost *= count;
        bool bodyAxes = result.Binding?.FaceTarget == true ||
                        (result.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector);
        var copies = new ConstructionNode[count];
        var steps = new List<ConstructionStep>();
        for (int i = 0; i < count; i++)
        {
            AbilityDefinition branch = ConstructionEditing.Copy(source);
            ConstructionNode copy = branch.Root;
            copies[i] = copy;
            copy.Id = "copy_" + i;
            copy.Offset[bodyAxes ? 0 : 1] += (i - (count - 1) * .5f) * spacing;
            float delay = interval * i;
            // 先按原步骤名称取得真实时钟，再为每份重定位路径与独立命名。
            foreach (ConstructionStep step in branch.Steps)
            {
                int node = plan.Steps.First(s => s.Id == step.Id).Node;
                bool sharedClock = plan.Nodes[node].FragmentSource < 0;
                for (int parent = node; parent >= 0; parent = plan.Nodes[parent].Parent)
                    sharedClock &= plan.Nodes[parent].Activation == null;
                if (sharedClock) DelayOperation(step.Operation, delay);
            }

            ConstructionEditing.RebaseReferences(branch, "root/" + copy.Id, "copy_" + i + "/");
            steps.AddRange(branch.Steps);
            DelayBranch(copy, true);
            if (copy.Activation != null) copy.Activation.Delay += delay;
            Visit(copy, node =>
            {
                if (string.IsNullOrEmpty(node.ContactGroup)) return;
                if (node.ContactGroup == "body" && i == 0) return;
                if (node.ContactGroup == "body" && node.Contact == null && source.Body?.Contact != null)
                {
                    ConstructionContactPolicy policy = source.Body.Contact;
                    node.Contact = new ConstructionContactPolicy
                    {
                        Mode = policy.Mode,
                        Interval = policy.Interval,
                        MaximumPerTarget = policy.MaximumPerTarget,
                        MaximumTotal = policy.MaximumTotal
                    };
                }

                node.ContactGroup = "copy_" + i + "/" + node.ContactGroup;
            });
            if (plan.Nodes[0].FlyAt >= 0 && !copy.Operations.Any(o => o.Kind == ConstructionOperationKind.Detach))
                copy.Operations = copy.Operations.Concat(new[]
                {
                    new ConstructionOperation
                    {
                        Kind = ConstructionOperationKind.Detach,
                        At = plan.Nodes[0].FlyAt + (copy.Activation == null ? delay : 0f)
                    }
                }).ToArray();

            // 事件分支的局部时间不随首批出手延迟再次移动。
            void DelayBranch(ConstructionNode node, bool sharedClock)
            {
                sharedClock &= node.Activation == null;
                if (sharedClock)
                {
                    node.AppearAt = (node.AppearAt ?? 0f) + delay;
                    node.DisappearAt = (node.DisappearAt ?? originalLifetime) + delay;
                    node.ActiveFrom += delay;
                    if (node.ActiveUntil >= 0) node.ActiveUntil += delay;
                    if (node.Hold != null) node.Hold.At += delay;
                    foreach (ConstructionOperation operation in node.Operations) DelayOperation(operation, delay);
                }

                foreach (ConstructionNode child in node.Children) DelayBranch(child, sharedClock);
            }
        }

        result.Steps = steps.ToArray();
        result.Root = new ConstructionNode
            { Id = "copies", Form = "pivot", ContactRole = ConstructionContactRole.Decoration, Children = copies };
        // 手姿态只借用原动作一次，之后这些显化主体沿同一次释放点的真实速度独立运行。
        if ((result.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector) && result.Binding.Follow &&
            plan.Nodes[0].ReleaseAt >= 0f)
            result.Root.Operations = new[]
                { new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = plan.Nodes[0].ReleaseAt } };
        AbilityCompiler.Compile(result, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, result) : result;
    }

    /// <summary>在资料入口拒绝无效倍率，具体动作及总预算仍由正式编译检查。</summary>
    /// <param name="factor">用户或创法传入的倍率。</param>
    private static void Factor(float factor)
    {
        if (!Finite(factor) || factor <= 0f) throw new InvalidDataException("倍率必须是大于零的有限数");
    }

    /// <summary>判断参数能否参加有限计算。</summary>
    /// <param name="value">传入数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>在原结构上逐层执行同一种资料变化。</summary>
    /// <param name="node">当前生成部位，纯身体没有此值。</param>
    /// <param name="edit">对每个实际部位的变化。</param>
    private static void Visit(ConstructionNode node, Action<ConstructionNode> edit)
    {
        if (node == null) return;
        edit(node);
        foreach (ConstructionNode child in node.Children) Visit(child, edit);
    }

    /// <summary>所有作者声明的实际操作只访问一次，不重复处理编译器生成的原操作观察项。</summary>
    /// <param name="definition">当前独立草稿。</param>
    private static IEnumerable<ConstructionOperation> AllOperations(AbilityDefinition definition)
    {
        foreach (ConstructionOperation operation in NodeOperations(definition.Root)) yield return operation;
        foreach (ConstructionStep step in definition.Steps) yield return step.Operation;

        IEnumerable<ConstructionOperation> NodeOperations(ConstructionNode node)
        {
            if (node == null) yield break;
            foreach (ConstructionOperation operation in node.Operations) yield return operation;
            foreach (ConstructionNode child in node.Children)
            foreach (ConstructionOperation operation in NodeOperations(child))
                yield return operation;
        }
    }

    /// <summary>身体只发出一次事件；延后的副本接受同一次事件后等待，不能等到门槛已过才订阅。</summary>
    /// <param name="operation">使用整次时钟的实际操作。</param><param name="delay">此副本的实际延迟。</param>
    private static void DelayOperation(ConstructionOperation operation, float delay)
    {
        if (operation.When?.Source == "body") operation.When.Delay += delay;
        else operation.At += delay;
    }

    /// <summary>改变普通三轴速度，保持原方向。</summary>
    /// <param name="vector">原三轴数值。</param>
    /// <param name="factor">倍率。</param>
    private static void Multiply(float[] vector, float factor)
    {
        for (int i = 0; i < vector.Length; i++) vector[i] *= factor;
    }
}
