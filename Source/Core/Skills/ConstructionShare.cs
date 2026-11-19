using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Skills;

/// <summary>构造分享文本的读取错误，带可本地化原因。</summary>
internal sealed class ConstructionShareException : FormatException
{
    public readonly string Reason;

    public ConstructionShareException(string reason, Exception inner = null) : base(reason, inner) => Reason = reason;
}

/// <summary>带格式版本的完整构造资料；不转换成本、不取样、不带人物状态。</summary>
internal static class ConstructionShare
{
    private const string Format = "Cultiway.Construction";
    private const int Version = 1;
    private const int MaximumTextLength = 4 * 1024 * 1024;

    /// <summary>导出完整事实，世界内对人物编号的绑定不随文本分享。</summary>
    internal static string Write(AbilityDefinition definition)
    {
        var data = new JObject
        {
            ["Format"] = Format,
            ["Version"] = Version,
            ["Definition"] = Snapshot(definition)
        };
        string text = data.ToString(Formatting.None);
        if (text.Length > MaximumTextLength) throw new ConstructionShareException("ShareTooLarge");
        return text;
    }

    /// <summary>只读现行分享格式；创法补全和计价不参与导入。</summary>
    internal static AbilityDefinition Read(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ConstructionShareException("ShareInvalidText");
        if (text.Length > MaximumTextLength) throw new ConstructionShareException("ShareTooLarge");
        try
        {
            using var reader = new JsonTextReader(new StringReader(text)) { MaxDepth = 128, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read() || (string)root["Format"] != Format)
                throw new ConstructionShareException("ShareInvalidText");
            if (root["Version"]?.Type != JTokenType.Integer || (int)root["Version"] != Version)
                throw new ConstructionShareException("ShareUnsupportedVersion");
            if (root["Definition"] is not JObject data || data.Property(nameof(AbilityDefinition.Version)) == null ||
                data.Property(nameof(AbilityDefinition.Revision)) == null || data.Property(nameof(AbilityDefinition.Cost)) == null ||
                data.Property(nameof(AbilityDefinition.Cooldown)) == null)
                throw new ConstructionShareException("ShareInvalidText");
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });
            var definition = data.ToObject<AbilityDefinition>(serializer);
            if (definition.Version != 1) throw new ConstructionShareException("ShareUnsupportedVersion");
            if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Name) ||
                definition.Name.Length > 128 || definition.Revision < 1)
                throw new ConstructionShareException("ShareInvalidText");
            return definition;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or OverflowException)
        { throw new ConstructionShareException("ShareInvalidText", error); }
    }

    /// <summary>两边实际构造完全相同才可复用原编号，当前世界的作者绑定不改变构造本身。</summary>
    internal static bool Matches(AbilityDefinition current, AbilityDefinition incoming) =>
        JToken.DeepEquals(Snapshot(current), Snapshot(incoming));

    private static JObject Snapshot(AbilityDefinition definition)
    {
        JObject data = ConstructionDefinitionLayers.Snapshot(definition);
        data.Remove(nameof(AbilityDefinition.ExternalCreator));
        return data;
    }
}
