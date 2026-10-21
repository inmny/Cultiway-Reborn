using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

[Serializable]
internal sealed class WanfaPavilionData
{
    /// <summary>旧资料转换时的局部丢弃提示，读取后清空。</summary>
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public List<string> MigrationWarnings;
    public List<SkillBlueprint> Blueprints = new();
    public List<string> SelectedBlueprintIds = new();
}
