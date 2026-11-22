using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

/// <summary>逐条恢复万法阁蓝图，损坏的字段只影响所属蓝图。</summary>
internal sealed class WanfaBlueprintListReader : JsonConverter<List<SkillBlueprint>>
{
    /// <summary>写出继续使用普通蓝图字段。</summary>
    public override bool CanWrite => false;

    /// <summary>读取数组后逐条还原，记录有问题的编号及原因，再继续读下一条。</summary>
    /// <param name="reader">当前蓝图数组的读取位置。</param>
    /// <param name="objectType">保存文件中对应的字段类型。</param>
    /// <param name="existingValue">读取之前的默认列表。</param>
    /// <param name="hasExistingValue">是否有默认列表。</param>
    /// <param name="serializer">用于还原单份蓝图的读取器。</param>
    public override List<SkillBlueprint> ReadJson(JsonReader reader, Type objectType,
        List<SkillBlueprint> existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var blueprints = new List<SkillBlueprint>();
        if (reader.TokenType == JsonToken.Null) return blueprints;
        var records = JArray.Load(reader);
        for (int index = 0; index < records.Count; index++)
        {
            var record = records[index] as JObject;
            JToken id = record?.GetValue("Id", StringComparison.OrdinalIgnoreCase);
            string name = id?.Type == JTokenType.String ? id.Value<string>() : $"第 {index + 1} 条";
            try
            {
                if (record == null) throw new JsonSerializationException("蓝图记录应为对象");
                blueprints.Add(record.ToObject<SkillBlueprint>(serializer));
            }
            catch (JsonException error)
            {
                ModClass.LogWarning($"万法阁读取：跳过蓝图 {name}，{error.Message}");
            }
        }
        return blueprints;
    }

    /// <summary>此读取器没有自定义写出流程。</summary>
    /// <param name="writer">序列化器提供的写入位置。</param>
    /// <param name="value">待写出的蓝图列表。</param>
    /// <param name="serializer">序列化器。</param>
    public override void WriteJson(JsonWriter writer, List<SkillBlueprint> value, JsonSerializer serializer)
        => throw new NotSupportedException();
}
