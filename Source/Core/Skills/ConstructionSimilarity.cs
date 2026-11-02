using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Skills;

/// <summary>创法时比较真实结构与参数；不保存名字、作者、染色或可变游戏对象。</summary>
public sealed class ConstructionSimilarity
{
    /// <summary>结构、材料、操作、作用和离散选择的内容索引。</summary>
    public readonly string Shape;

    /// <summary>结构及约百分之五参数阶梯的内容索引，用于世界目录查找。</summary>
    public readonly string Bucket;

    /// <summary>按稳定字段顺序排列的连续参数。</summary>
    private readonly double[] numbers;

    /// <summary>世界参数阶梯的相邻比例。</summary>
    private static readonly double Step = Math.Log(1.05d);

    /// <summary>只保留比较需要的小份数值。</summary>
    /// <param name="shape">已经归一的结构索引。</param>
    /// <param name="values">连续参数。</param>
    private ConstructionSimilarity(string shape, double[] values)
    {
        Shape = shape;
        numbers = values;
        var key = new StringBuilder(shape);
        foreach (double value in values)
            key.Append('|').Append(value == 0d
                ? "z"
                : (value < 0d ? "-" : "+") + Math.Round(Math.Log(Math.Abs(value)) / Step)
                    .ToString(CultureInfo.InvariantCulture));
        Bucket = Hash(key.ToString());
    }

    /// <summary>同一结构下所有连续参数均在百分之五以内；零和方向不得被近似掉。</summary>
    /// <param name="other">已经登记的比较资料。</param>
    public bool Near(ConstructionSimilarity other)
    {
        if (Shape != other.Shape || numbers.Length != other.numbers.Length) return false;
        for (int i = 0; i < numbers.Length; i++)
        {
            double a = numbers[i], b = other.numbers[i];
            if (a == b) continue;
            if (a == 0d || b == 0d || Math.Sign(a) != Math.Sign(b) ||
                Math.Max(Math.Abs(a), Math.Abs(b)) > Math.Min(Math.Abs(a), Math.Abs(b)) * 1.05d) return false;
        }

        return true;
    }

    /// <summary>正式编译后归一部位、事件和接触组，再提取结构与数值；中间计划不入长期缓存。</summary>
    /// <param name="definition">完整且自足的构造资料。</param>
    public static ConstructionSimilarity Describe(AbilityDefinition definition)
    {
        CompiledAbility plan = AbilityCompiler.Compile(definition, cache: false);
        return Describe(definition, plan);
    }

    /// <summary>正式入口已经编译时直接复用该次检查，不重复展开。</summary>
    /// <param name="definition">该次已经检查的资料。</param>
    /// <param name="plan">对应的实际计划。</param>
    internal static ConstructionSimilarity Describe(AbilityDefinition definition, CompiledAbility plan)
    {
        JObject data = JObject.Parse(JsonConvert.SerializeObject(definition));
        data["Use"] = new JObject { ["Channels"] = (int)plan.Use.Channels, ["Target"] = (int)plan.Use.Target };
        foreach (string key in new[] { "Id", "Revision", "Name", "Creator", "Seed", "Parent" }) data.Remove(key);
        data["Impact"] = Impact(plan.Impact);
        data["ExpandedForms"] = plan.FormCount;
        var forms = plan.Nodes.Where(node => node.DeclarationPath != null).GroupBy(node => node.DeclarationPath)
            .ToDictionary(group => group.Key, group => group.ToArray());

        void Condition(JObject condition, IEnumerable<int> sources)
        {
            if (condition == null) return;
            // 编译结果已经解析绝对、相对、父体和挂点实例，不重复解释路径文本。
            condition["Source"] = new JArray(sources);
            if (condition["Matter"]?.Type == JTokenType.String)
                condition["State"] = AbilityMatters
                    .Resolve((string)condition["Matter"], (string)condition["State"], "创法比较条件").State;
        }

        void Normalize(JObject node, string path)
        {
            if (node == null) return;
            CompiledForm[] actual = forms[path];
            node.Remove("Id");
            node.Remove("Surface");
            node.Remove("Tint");
            node["State"] = actual[0].Matter.State;
            node["Impact"] = Impact(actual[0].Impact ?? plan.Impact);
            node["ContactGroup"] = new JArray(actual.Select(form => form.ContactGroup).Distinct());
            CompiledContactPolicy contact = plan.ContactPolicies[actual[0].ContactGroup];
            node["Contact"] = Contact(contact);
            Condition(node["Activation"] as JObject, actual.Select(form => form.Activation.Source));
            int eventIndex = 0;
            foreach (JObject operation in (JArray)node["Operations"])
                if (operation["When"] is JObject condition)
                {
                    int index = eventIndex++;
                    Condition(condition, actual.Select(form => form.EventOperations[index].When.Source));
                }

            foreach (JObject child in (JArray)node["Children"])
                Normalize(child, path + "/" + (string)child["Id"]);
        }

        Normalize(data["Root"] as JObject, "root");
        if (data["Body"] is JObject body) body["Contact"] = Contact(plan.ContactPolicies[plan.FormCount]);
        var shape = new StringBuilder();
        var values = new List<double>();
        Extract(data, shape, values);
        return new ConstructionSimilarity(Hash(shape.ToString()), values.ToArray());
    }

    /// <summary>采用实际编译后的作用，缺省和明确写出同一规则具有相同含义。</summary>
    /// <param name="impact">实际作用。</param>
    private static JObject Impact(CompiledConstructionImpact impact) => new()
    {
        ["Targets"] = (int)impact.Targets, ["DamageScale"] = impact.DamageScale,
        ["Force"] = impact.Force, ["Lift"] = impact.SlideSeconds > 0 ? 0f : impact.Lift,
        ["Origin"] = (int)impact.Origin,
        ["Launch"] = impact.Force == 0 && impact.Launch,
        ["SlideSeconds"] = impact.Force == 0 ? 0f : impact.SlideSeconds,
        ["StaggerSeconds"] = impact.StaggerSeconds, ["BindSeconds"] = impact.BindSeconds,
        ["WorldEffects"] = new JArray(impact.WorldEffects.Select(effect => new JObject
        {
            ["Action"] = effect.Action.Id, ["MaximumTiles"] = effect.MaximumTiles, ["Duration"] = effect.Duration,
            ["TileCentreOnly"] = effect.TileCentreOnly,
            ["Contact"] = Contact(effect.Contact)
        }))
    };

    /// <summary>采用接触组实际规则，不把未使用的默认间隔当成差别。</summary>
    /// <param name="policy">已经统一的规则。</param>
    private static JObject Contact(CompiledContactPolicy policy) => new()
    {
        ["Mode"] = (int)policy.Mode, ["Interval"] = policy.Interval,
        ["MaximumPerTarget"] = policy.MaximumPerTarget, ["MaximumTotal"] = policy.MaximumTotal
    };

    /// <summary>保留字段名称、类型、顺序及全部离散值，仅将连续参数独立列出。</summary>
    /// <param name="token">完整归一资料。</param>
    /// <param name="shape">结构串。</param>
    /// <param name="values">按结构顺序排列的数值。</param>
    private static void Extract(JToken token, StringBuilder shape, List<double> values)
    {
        if (token is JObject obj)
        {
            shape.Append('{');
            foreach (JProperty property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                shape.Append(JsonConvert.SerializeObject(property.Name)).Append(':');
                Extract(property.Value, shape, values);
            }

            shape.Append('}');
        }
        else if (token is JArray array)
        {
            shape.Append('[');
            foreach (JToken child in array)
            {
                Extract(child, shape, values);
                shape.Append(',');
            }

            shape.Append(']');
        }
        else if (token.Type == JTokenType.Float)
        {
            shape.Append('F');
            values.Add((double)token);
        }
        else shape.Append('(').Append((int)token.Type).Append(':').Append(token.ToString(Formatting.None)).Append(')');
    }

    /// <summary>索引不长期保存完整结构串，不参与存档或能力身份。</summary>
    /// <param name="value">已经稳定排序的内容。</param>
    private static string Hash(string value)
    {
        using var algorithm = SHA256.Create();
        return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}