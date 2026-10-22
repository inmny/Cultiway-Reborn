using System;
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
    public static AbilityDefinition Resize(AbilityDefinition source, float factor)
    {
        Factor(factor); AbilityCompiler.Compile(source);
        if (source.Root == null) throw new InvalidDataException("真实身体不能凭空放大，请先显化主体或改变实际体型");
        AbilityDefinition result = ConstructionEditing.Copy(source); result.Root.Scale *= factor;
        Visit(result.Root, node =>
        {
            if (node != result.Root && node.Activation?.Placement == ConstructionEventPlacement.Event)
                node.Scale *= factor;
            foreach (var operation in node.Operations)
                if (operation.WorldMatter != null)
                    operation.WorldMatter.MaximumVolume *= factor * factor * factor;
        });
        AbilityCompiler.Compile(result);
        return result;
    }

    /// <summary>提高独立运动速度和转向，路径按相同比例走快；纯身体动作压缩完整历时。</summary>
    /// <param name="source">已有构造。</param>
    /// <param name="factor">实际运动速度倍率。</param>
    public static AbilityDefinition Haste(AbilityDefinition source, float factor)
    {
        Factor(factor); AbilityCompiler.Compile(source);
        if (source.Root == null) return Duration(source, 1f / factor);
        AbilityDefinition result = ConstructionEditing.Copy(source); bool changed = false;
        Visit(result.Root, node =>
        {
            foreach (ConstructionOperation operation in node.Operations)
            {
                if (operation.Fracture != null)
                { changed = true; operation.Fracture.Speed *= factor; operation.Fracture.Spin *= factor; operation.Fracture.Gravity *= factor * factor; }
                if (operation.Kind is ConstructionOperationKind.Gather or ConstructionOperationKind.Recover)
                { changed = true; operation.Seconds /= factor; continue; }
                if (operation.Kind is not (ConstructionOperationKind.Fly or ConstructionOperationKind.Spin)) continue;
                changed = true; Multiply(operation.Vector, factor); operation.TurnRate *= factor;
                if (operation.Acceleration != null) Multiply(operation.Acceleration, factor * factor);
                if (operation.Path == null) continue;
                operation.Path.TurnRate *= factor;
                foreach (ConstructionPathKey key in operation.Path.Keys) key.At /= factor;
            }
        });
        if (!changed) throw new InvalidDataException("此构造没有飞行、旋转或聚合运动，可用历时倍率改变其动作快慢");
        AbilityCompiler.Compile(result); return result;
    }

    /// <summary>伸缩完整过程的世界历时，保持同一阶段的姿态和路径；接触频率、次数上限、消耗和冷却不变。</summary>
    /// <param name="source">已有身体或生成构造。</param>
    /// <param name="factor">全部过程的历时倍率。</param>
    public static AbilityDefinition Duration(AbilityDefinition source, float factor)
    {
        Factor(factor); AbilityCompiler.Compile(source);
        AbilityDefinition result = ConstructionEditing.Copy(source); result.Lifetime *= factor;
        if (result.Body != null)
        {
            result.Body.ActiveFrom *= factor; result.Body.ActiveUntil *= factor;
            if (result.Body.Hold != null) { result.Body.Hold.At *= factor; result.Body.Hold.Seconds *= factor; }
            foreach (BodyMotionKey key in result.Body.Keys) key.At *= factor;
        }
        Visit(result.Root, node =>
        {
            node.AppearAt *= factor; node.DisappearAt *= factor;
            if (node.Hold != null) { node.Hold.At *= factor; node.Hold.Seconds *= factor; }
            if (node.Activation != null) node.Activation.Delay *= factor;
            node.ActiveFrom *= factor; if (node.ActiveUntil >= 0) node.ActiveUntil *= factor;
            foreach (ConstructionOperation operation in node.Operations)
            {
                operation.At *= factor;
                if (operation.Fracture != null)
                { operation.Fracture.Seconds *= factor; operation.Fracture.Speed /= factor; operation.Fracture.Spin /= factor; operation.Fracture.Gravity /= factor * factor; }
                if (operation.When != null) operation.When.Delay *= factor;
                if (operation.Kind == ConstructionOperationKind.Animate && operation.Seconds == 0)
                    operation.Seconds = FormResources.Load(node.Form).Rig.Motion(operation.Clip).Duration;
                operation.Seconds *= factor; operation.BlendSeconds *= factor;
                if (operation.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Spin)
                { Multiply(operation.Vector, 1f / factor); operation.TurnRate /= factor; if (operation.Acceleration != null) Multiply(operation.Acceleration, 1f / (factor * factor)); }
                if (operation.Path == null) continue;
                operation.Path.TurnRate /= factor;
                foreach (ConstructionPathKey key in operation.Path.Keys) key.At *= factor;
            }
        });
        AbilityCompiler.Compile(result); return result;
    }

    /// <summary>复制同一生成构造，可齐射或依次显现，各份保留自己的完整寿命和接触规则，真实身体只发力一次。</summary>
    /// <param name="source">带有生成主体的已有构造。</param>
    /// <param name="count">整次生成的总份数，含原来的一份。</param>
    /// <param name="spacing">相邻主体的横向布局间隔；身体坐标沿X，地图坐标沿Y。</param>
    /// <param name="interval">相邻显现的世界秒间隔，零表示齐射。</param>
    public static AbilityDefinition Repeat(AbilityDefinition source, int count, float spacing, float interval)
    {
        CompiledAbility plan = AbilityCompiler.Compile(source);
        if (source.Root == null) throw new InvalidDataException("数量改造需要生成主体，真实身体不会因此多出肢体，请先显化");
        if (count < 1 || count > 64 || !Finite(spacing) || spacing < 0 || !Finite(interval) || interval < 0 ||
            count > 1 && plan.FormCount * count + 1 > AbilityCompiler.MaxForms)
            throw new InvalidDataException("数量应为1至64，间隔须为非负有限数，展开后不能超过128个部位");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        if (count == 1) return result;
        float originalLifetime = source.Lifetime;
        result.Lifetime += interval * (count - 1); result.Cost *= count;
        bool bodyAxes = result.Binding?.FaceTarget == true || result.Binding?.Anchor == AbilityAnchor.Hand;
        var copies = new ConstructionNode[count];
        for (int i = 0; i < count; i++)
        {
            ConstructionNode copy = ConstructionEditing.Copy(source).Root;
            copies[i] = copy; copy.Id = "copy_" + i;
            copy.Offset[bodyAxes ? 0 : 1] += (i - (count - 1) * .5f) * spacing;
            float delay = interval * i;
            ConstructionEditing.RebaseEventSources(copy, "root/" + copy.Id);
            DelayBranch(copy, true);
            if (copy.Activation != null) copy.Activation.Delay += delay;
            Visit(copy, node =>
            {
                if (string.IsNullOrEmpty(node.ContactGroup)) return;
                if (node.ContactGroup == "body" && i == 0) return;
                if (node.ContactGroup == "body" && node.Contact == null && source.Body?.Contact != null)
                {
                    ConstructionContactPolicy policy = source.Body.Contact;
                    node.Contact = new ConstructionContactPolicy { Mode = policy.Mode, Interval = policy.Interval,
                        MaximumPerTarget = policy.MaximumPerTarget, MaximumTotal = policy.MaximumTotal };
                }
                node.ContactGroup = "copy_" + i + "/" + node.ContactGroup;
            });
            if (plan.Nodes[0].FlyAt >= 0 && !copy.Operations.Any(o => o.Kind == ConstructionOperationKind.Detach))
                copy.Operations = copy.Operations.Concat(new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = plan.Nodes[0].FlyAt + (copy.Activation == null ? delay : 0f) } }).ToArray();
            // 事件分支的局部时间不随首批出手延迟再次移动。
            void DelayBranch(ConstructionNode node, bool sharedClock)
            {
                sharedClock &= node.Activation == null;
                if (sharedClock)
                {
                    node.AppearAt = (node.AppearAt ?? 0f) + delay;
                    node.DisappearAt = (node.DisappearAt ?? originalLifetime) + delay;
                    node.ActiveFrom += delay; if (node.ActiveUntil >= 0) node.ActiveUntil += delay;
                    if (node.Hold != null) node.Hold.At += delay;
                    foreach (ConstructionOperation operation in node.Operations) operation.At += delay;
                }
                foreach (ConstructionNode child in node.Children) DelayBranch(child, sharedClock);
            }
        }
        result.Root = new ConstructionNode { Id = "copies", Form = "pivot", ContactRole = ConstructionContactRole.Decoration, Children = copies };
        // 手姿态只借用原动作一次，之后这些显化主体沿同一次释放点的真实速度独立运行。
        if (result.Binding?.Anchor == AbilityAnchor.Hand && result.Binding.Follow && plan.Nodes[0].ReleaseAt >= 0f)
            result.Root.Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = plan.Nodes[0].ReleaseAt } };
        AbilityCompiler.Compile(result); return result;
    }

    /// <summary>在资料入口拒绝无效倍率，具体动作及总预算仍由正式编译检查。</summary>
    /// <param name="factor">用户或创法传入的倍率。</param>
    private static void Factor(float factor)
    { if (!Finite(factor) || factor <= 0f) throw new InvalidDataException("倍率必须是大于零的有限数"); }

    /// <summary>判断参数能否参加有限计算。</summary>
    /// <param name="value">传入数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>在原结构上逐层执行同一种资料变化。</summary>
    /// <param name="node">当前生成部位，纯身体没有此值。</param>
    /// <param name="edit">对每个实际部位的变化。</param>
    private static void Visit(ConstructionNode node, Action<ConstructionNode> edit)
    { if (node == null) return; edit(node); foreach (ConstructionNode child in node.Children) Visit(child, edit); }

    /// <summary>改变普通三轴速度，保持原方向。</summary>
    /// <param name="vector">原三轴数值。</param>
    /// <param name="factor">倍率。</param>
    private static void Multiply(float[] vector, float factor)
    { for (int i = 0; i < vector.Length; i++) vector[i] *= factor; }
}
