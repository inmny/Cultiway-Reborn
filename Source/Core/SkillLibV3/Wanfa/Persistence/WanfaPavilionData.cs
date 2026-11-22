using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

[Serializable]
internal sealed class WanfaPavilionData
{
    /// <summary>旧资料转换时的局部丢弃提示，读取后清空。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<string> MigrationWarnings;
    /// <summary>本地蓝图，每条独立读取。</summary>
    [JsonConverter(typeof(WanfaBlueprintListReader))]
    public List<SkillBlueprint> Blueprints = new();
    public List<string> SelectedBlueprintIds = new();
}
