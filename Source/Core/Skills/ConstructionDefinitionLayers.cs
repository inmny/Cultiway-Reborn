using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Cultiway.Core.Skills;

/// <summary>构造在创建和修改边界统一合成，运行只使用已经确定的结果。</summary>
public static class ConstructionDefinitionLayers
{
    /// <summary>完整定义中的零、空值和关闭选项都是已有选择，不因保存属性省略而消失。</summary>
    private sealed class CompleteContract : DefaultContractResolver
    {
        /// <summary>只用于完整资料快照，不改变游戏正式保存格式。</summary>
        /// <param name="member">构造资料成员。</param><param name="serialization">原序列化规则。</param>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
        {
            var property = base.CreateProperty(member, serialization);
            property.NullValueHandling = NullValueHandling.Include;
            property.DefaultValueHandling = DefaultValueHandling.Include;
            return property;
        }
    }

    /// <summary>只在构造边界使用的共享只读规则，序列化器按次创建。</summary>
    private static readonly CompleteContract contract = new();

    /// <summary>从完整定义保留全部已有选择；返回的对象由调用方独占。</summary>
    /// <param name="definition">正式定义或独立草稿。</param>
    public static JObject Snapshot(AbilityDefinition definition) => JObject.FromObject(definition, Serializer());

    /// <summary>基础、来源、玩家依次覆盖，默认只填其余缺项；输入从不被改写。</summary>
    /// <param name="basis">基础构造的明确字段。</param><param name="source">来源变化，空值表示没有该层。</param>
    /// <param name="player">玩家明确变化，空值表示没有该层。</param><param name="defaults">默认补全，空值表示使用资料类型原有默认值。</param>
    public static AbilityDefinition Resolve(JObject basis, JObject source = null, JObject player = null, JObject defaults = null)
    {
        var values = new JObject();
        Overlay(values, basis); Overlay(values, source); Overlay(values, player); Fill(values, defaults);
        return values.ToObject<AbilityDefinition>(Serializer());
    }

    /// <summary>来源已形成完整结果时提取实际变化，按同一优先次序合成。</summary>
    /// <param name="basis">固定的基础版本。</param><param name="changed">来源产生的独立变化结果。</param>
    public static AbilityDefinition FromSource(AbilityDefinition basis, AbilityDefinition changed)
    {
        var original = Snapshot(basis);
        return Resolve(original, source: Difference(original, Snapshot(changed)));
    }

    /// <summary>只记录实际字段变化，数组整份替换，空列表也不会被合并回来。</summary>
    /// <param name="before">变化前的完整快照。</param><param name="after">变化后的完整快照。</param>
    internal static JObject Difference(JObject before, JObject after)
    {
        var result = new JObject();
        foreach (var property in after.Properties())
        {
            var previous = before.Property(property.Name);
            if (previous != null && JToken.DeepEquals(previous.Value, property.Value)) continue;
            if (previous?.Value is JObject oldObject && property.Value is JObject newObject)
                result[property.Name] = Difference(oldObject, newObject);
            else result[property.Name] = property.Value.DeepClone();
        }
        return result;
    }

    /// <summary>仅在明确对象之间逐字段覆盖，空值、数值、布尔及列表直接替换。</summary>
    /// <param name="target">本轮独占的合成对象。</param><param name="layer">更高优先级的变化。</param>
    private static void Overlay(JObject target, JObject layer)
    {
        if (layer == null) return;
        foreach (var property in layer.Properties())
        {
            var existing = target.Property(property.Name, StringComparison.OrdinalIgnoreCase);
            if (property.Value is JObject change)
            {
                var current = existing?.Value as JObject;
                if (current == null)
                {
                    current = new JObject();
                    if (existing == null) target.Add(property.Name, current); else existing.Value = current;
                }
                Overlay(current, change);
            }
            else if (existing == null) target.Add(property.Name, property.Value.DeepClone());
            else existing.Value = property.Value.DeepClone();
        }
    }

    /// <summary>默认只写不存在的字段，明确空值和空列表仍是存在的选择。</summary>
    /// <param name="target">已经应用玩家变化的资料。</param><param name="defaults">可用默认字段。</param>
    private static void Fill(JObject target, JObject defaults)
    {
        if (defaults == null) return;
        foreach (var property in defaults.Properties())
        {
            var existing = target.Property(property.Name, StringComparison.OrdinalIgnoreCase);
            if (existing == null) target[property.Name] = property.Value.DeepClone();
            else if (existing.Value is JObject current && property.Value is JObject fallback) Fill(current, fallback);
        }
    }

    /// <summary>每次合成使用独立读取状态，拼错的字段不能被当作已经生效的选择。</summary>
    private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = contract,
        MissingMemberHandling = MissingMemberHandling.Error
    });
}
