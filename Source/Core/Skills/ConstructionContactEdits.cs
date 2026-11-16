using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>编辑和创作共同修改实际接触组，不制造相互冲突的部位规则。</summary>
public static class ConstructionContactEdits
{
    /// <summary>读取选中声明第一组的实际规则，返回独立数值副本。</summary>
    /// <param name="source">当前完整资料。</param><param name="node">实际声明；空值表示身体。</param>
    public static ConstructionContactPolicy Read(AbilityDefinition source, ConstructionNode node = null)
    {
        CompiledAbility plan = AbilityCompiler.Compile(source, false);
        int[] groups = Groups(source, plan, node);
        CompiledContactPolicy policy = plan.ContactPolicies[groups[0]];
        return new ConstructionContactPolicy
        {
            Mode = policy.Mode, Interval = policy.Interval > 0f ? policy.Interval : .25f,
            MaximumPerTarget = policy.MaximumPerTarget, MaximumTotal = policy.MaximumTotal
        };
    }

    /// <summary>先确定实际共用范围，再修改完整副本；候选无效时原资料保持不变。</summary>
    /// <param name="source">当前完整资料。</param><param name="policy">新规则；空值恢复每人一次。</param>
    /// <param name="node">当前资料中的真实声明；空值表示身体。</param>
    public static AbilityDefinition Set(AbilityDefinition source, ConstructionContactPolicy policy, ConstructionNode node = null)
    {
        // 同一声明可能展开成多组；沿共用声明和共用组展开有限闭包，不能只改一个输出。
        var plan = AbilityCompiler.Compile(source, false);
        _ = new CompiledContactPolicy(policy, "编辑接触规则");
        var groups = new HashSet<int>(Groups(source, plan, node));
        var paths = new HashSet<string>(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (CompiledForm form in plan.Nodes)
                if (form.DeclarationPath != null && (groups.Contains(form.ContactGroup) || paths.Contains(form.DeclarationPath)))
                {
                    changed |= groups.Add(form.ContactGroup);
                    changed |= paths.Add(form.DeclarationPath);
                }
        } while (changed);

        // 每个被影响的声明独占规则数组，身体及body组继续共享相同的数值含义。
        AbilityDefinition result = ConstructionEditing.Copy(source);
        foreach (var part in Parts(result.Root))
            if (paths.Contains(part.Path)) part.Node.Contact = Copy(policy);
        if (result.Body != null && groups.Contains(plan.Nodes.Length)) result.Body.Contact = Copy(policy);
        AbilityCompiler.Compile(result, false);
        return result;
    }

    /// <summary>独立候选通过完整编译才交付；切换用途不删除原作用或改变共享组。</summary>
    /// <param name="source">完整原资料。</param><param name="node">本资料中的实际声明。</param>
    /// <param name="role">攻击、屏障或装饰用途。</param><param name="interception">屏障的吸收或反射。</param>
    /// <param name="blocksGroundActors">是否阻挡敌方地面人物。</param><param name="reflectionLimit">本部位允许反射的次数。</param>
    public static AbilityDefinition SetClash(AbilityDefinition source, ConstructionNode node,
        ConstructionContactRole role, ConstructionInterception interception, bool blocksGroundActors, int reflectionLimit)
    {
        string path = Parts(source.Root).Where(part => ReferenceEquals(part.Node, node)).Select(part => part.Path).SingleOrDefault();
        if (path == null) throw new InvalidDataException("交锋规则找不到本资料中的实际部位");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        ConstructionNode target = Parts(result.Root).Single(part => part.Path == path).Node;
        target.ContactRole = role;
        target.Interception = interception;
        target.BlocksGroundActors = blocksGroundActors;
        target.ReflectionLimit = reflectionLimit;
        AbilityCompiler.Compile(result, false);
        return result;
    }

    /// <summary>只接受本资料里的实际声明，不以重复的局部名字猜测目标。</summary>
    /// <param name="source">完整资料。</param><param name="plan">同份资料的编译结果。</param>
    /// <param name="node">当前声明，空值表示身体。</param>
    private static int[] Groups(AbilityDefinition source, CompiledAbility plan, ConstructionNode node)
    {
        if (node == null)
        {
            if (source.Body == null) throw new InvalidDataException("接触规则缺少实际身体");
            return new[] { plan.Nodes.Length };
        }
        string path = Parts(source.Root).Where(part => ReferenceEquals(part.Node, node)).Select(part => part.Path).SingleOrDefault();
        int[] groups = plan.Nodes.Where(form => form.DeclarationPath == path).Select(form => form.ContactGroup).Distinct().ToArray();
        if (path == null || groups.Length == 0) throw new InvalidDataException("接触规则找不到本资料中的实际部位");
        return groups;
    }

    /// <summary>按完整声明路径遍历，不展开运行时实例。</summary>
    /// <param name="node">当前声明。</param><param name="parent">父声明路径。</param>
    private static IEnumerable<(ConstructionNode Node, string Path)> Parts(ConstructionNode node, string parent = null)
    {
        if (node == null) yield break;
        string path = parent == null ? "root" : parent + "/" + node.Id;
        yield return (node, path);
        foreach (ConstructionNode child in node.Children)
            foreach (var part in Parts(child, path)) yield return part;
    }

    /// <summary>多个声明不持有同一个可编辑对象。</summary><param name="policy">允许恢复缺省的规则。</param>
    private static ConstructionContactPolicy Copy(ConstructionContactPolicy policy) => policy == null ? null : new ConstructionContactPolicy
    {
        Mode = policy.Mode, Interval = policy.Interval,
        MaximumPerTarget = policy.MaximumPerTarget, MaximumTotal = policy.MaximumTotal
    };
}
