using System;
using System.IO;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>实际三轴部位与命名足的独立草稿改造，不保存人物或运行中的落脚锚点。</summary>
public static partial class BodyConstructionEdits
{
    /// <summary>同一三轴曲线改用另一个明确器官或原骨段，保留实际关系引用。</summary>
    /// <param name="source">可编译的原动作。</param><param name="effector">新的实际部位。</param>
    public static AbilityDefinition RetargetPart(AbilityDefinition source, string effector)
    {
        CompiledBodyAction original = AbilityCompiler.Compile(source, cache: false).Body;
        if (original?.Part.HasPartRotation != true || !new BodyPartSelection(effector).HasPartRotation)
            throw new InvalidDataException("本次部位切换需要实际器官或原骨段的三轴动作");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        if (result.Body.Construction == null) result.Body.Effector = effector;
        else result.Body.Construction.Parts.Single(part => part.Id == result.Body.Effector).Part = effector;
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>完整替换本过程的实际足集合，已有足保留曲线，新增足从自己的自然落点开始。</summary>
    /// <param name="source">原实际身体动作。</param><param name="feet">同一身体明确提供的足名。</param>
    public static AbilityDefinition NamedFeet(AbilityDefinition source, string[] feet)
    {
        if (AbilityCompiler.Compile(source, cache: false).Body?.Part.HasPartRotation != true || feet == null ||
            feet.Length is < 1 or > 32 || feet.Distinct(StringComparer.Ordinal).Count() != feet.Length ||
            feet.Any(part => !new BodyPartSelection(part).IsJoint))
            throw new InvalidDataException("命名足需要实际三轴身体动作与1至32个不重复原骨段名称");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        foreach (BodyMotionKey key in result.Body.Keys)
        {
            key.Support ??= new BodySupportMotion();
            var old = key.Support.Parts;
            key.Support.Parts = feet.ToDictionary(part => part, part => old != null && old.TryGetValue(part, out var point) ? point : new float[3], StringComparer.Ordinal);
        }
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>新增姿态独立复制所有身体通道和制作落脚，原姿态不随新输入变化。</summary>
    /// <param name="source">完整原动作。</param>
    public static AbilityDefinition AppendPose(AbilityDefinition source)
    {
        if (source?.Body == null || source.Body.Keys.Length >= 128)
            throw new InvalidDataException("新增姿态需要实际身体动作，且原关键点少于128个");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        BodyMotionKey last = result.Body.Keys.Last();
        var next = new BodyMotionKey
        {
            At = last.At + .1f, Hand = (float[])last.Hand.Clone(), Torso = (float[])last.Torso.Clone(),
            Rotation = (float[])last.Rotation.Clone(), Turn = last.Turn, Foot = (float[])last.Foot?.Clone(),
            HeadRotation = (float[])last.HeadRotation?.Clone(), PartRotation = (float[])last.PartRotation?.Clone(),
            Support = last.Support == null ? null : new BodySupportMotion
            {
                Rise = last.Support.Rise, LeftFoot = (float[])last.Support.LeftFoot.Clone(), RightFoot = (float[])last.Support.RightFoot.Clone(),
                Parts = last.Support.Parts?.ToDictionary(pair => pair.Key, pair => (float[])pair.Value.Clone(), StringComparer.Ordinal)
            }
        };
        result.Body.Keys = result.Body.Keys.Concat(new[] { next }).ToArray();
        result.Lifetime = Math.Max(result.Lifetime, next.At);
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>将末段曲线对半细分，保持原路径和动作时间，新增点可以继续自由改动。</summary>
    /// <param name="source">已有平面步法的完整动作。</param>
    public static AbilityDefinition InsertFootworkPoint(AbilityDefinition source)
    {
        AbilityDefinition result = ConstructionEditing.Copy(source);
        ConstructionPath path = result.Body?.Construction?.Footwork;
        if (path == null || path.Keys.Length >= 65) throw new InvalidDataException("新增步法点需要已有曲线，且原曲线少于65个点");
        ConstructionPathKey start = path.Keys[path.Keys.Length - 2], end = path.Keys.Last();
        // 按原三次曲线分出两段，原端点、切线和完整历时保持不变。
        UnityEngine.Vector3 p = Point(start.Point), q = Point(end.Point);
        UnityEngine.Vector3 a = p + Point(start.Out) * .5f, b = (p + Point(start.Out) + q + Point(end.In)) * .5f,
            c = q + Point(end.In) * .5f, d = (a + b) * .5f, e = (b + c) * .5f, middle = (d + e) * .5f;
        start.Out = Values(a - p); end.In = Values(c - q);
        path.Keys = path.Keys.Take(path.Keys.Length - 1).Concat(new[]
        {
            new ConstructionPathKey { At = (start.At + end.At) * .5f, Point = Values(middle), In = Values(d - middle), Out = Values(e - middle) }, end
        }).ToArray();
        AbilityCompiler.Compile(result, cache: false);
        return result;

        /// <summary>本次制作边界的三个有限坐标。</summary><param name="value">完整向量。</param>
        UnityEngine.Vector3 Point(float[] value) => new(value[0], value[1], value[2]);
        /// <summary>只保存数值，不保存Unity对象。</summary><param name="value">制作结果。</param>
        float[] Values(UnityEngine.Vector3 value) => new[] { value.x, value.y, value.z };
    }
}
