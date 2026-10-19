using System.IO;
using Cultiway.Content.CollectiveProjects;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Persistence;

/// <summary>工程目标采用明确记录，语义只保存稳定编号。</summary>
internal static class CollectiveProjectSaveContent
{
    /// <summary>登记当前玩法的工程目标与历史。</summary>
    internal static void Initialize()
        => ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, CollectiveProjectWorldSaveData>(
            "collective-projects", "集体工程与完成历史", 1,
            _ => CollectiveProjectService.CaptureWorldSave(Capture),
            (_, data) => CollectiveProjectService.RestoreWorldSave(data, Restore, ModClass.I.WorldSaves.LastReport)));

    /// <summary>只保存当前实际存在的工程目标类型。</summary>
    /// <param name="payload">玩法当前维护的工程目标。</param>
    private static JObject Capture(object payload)
    {
        if (payload == null) return null;
        if (payload is not CityMagicUtilityProjectPayload value) throw new System.InvalidOperationException("工程目标尚未登记存档");
        return JObject.FromObject(new PayloadRecord
        {
            Semantic = value.EffectSemantic?.id,
            Goal = value.Goal,
            MinimumUtility = value.MinimumUtility,
            MinimumNetGain = value.MinimumNetGain,
            PlannedRadius = value.PlannedRadius,
            ExpectedUtility = value.ExpectedUtility,
            ExecutionFootprintTileIds = value.ExecutionFootprintTileIds,
            BaselineUsefulTileIds = value.BaselineUsefulTileIds,
            BaselineMetric = value.BaselineMetric,
            HasExecutionBaseline = value.HasExecutionBaseline,
        }, JsonSerializer.Create(StableSaveJson.Settings));
    }

    /// <summary>用当前语义库重新连接工程目标。</summary>
    /// <param name="source">原始目标记录。</param>
    private static object Restore(JObject source)
    {
        if (source == null) return null;
        PayloadRecord value = source.ToObject<PayloadRecord>(JsonSerializer.Create(StableSaveJson.Settings));
        if (value.Version != 1 || !ModClass.L.SemanticLibrary.TryResolve(value.Semantic, out var semantic))
            throw new InvalidDataException("工程目标版本或语义已不支持");
        return new CityMagicUtilityProjectPayload
        {
            EffectSemantic = semantic,
            Goal = value.Goal,
            MinimumUtility = value.MinimumUtility,
            MinimumNetGain = value.MinimumNetGain,
            PlannedRadius = value.PlannedRadius,
            ExpectedUtility = value.ExpectedUtility,
            ExecutionFootprintTileIds = value.ExecutionFootprintTileIds,
            BaselineUsefulTileIds = value.BaselineUsefulTileIds,
            BaselineMetric = value.BaselineMetric,
            HasExecutionBaseline = value.HasExecutionBaseline,
        };
    }

    /// <summary>城市工程目标的第一版记录。</summary>
    private sealed class PayloadRecord
    {
        /// <summary>目标记录版本。</summary>
        public int Version = 1;
        /// <summary>效果语义编号。</summary>
        public string Semantic;
        /// <summary>目标的 Goal。</summary>
        public CityMagicUtilityProjectGoal Goal;
        /// <summary>目标的 MinimumUtility。</summary>
        public float MinimumUtility;
        /// <summary>目标的 MinimumNetGain。</summary>
        public int MinimumNetGain;
        /// <summary>目标的 PlannedRadius。</summary>
        public float PlannedRadius;
        /// <summary>目标的 ExpectedUtility。</summary>
        public float ExpectedUtility;
        /// <summary>目标的 ExecutionFootprintTileIds。</summary>
        public int[] ExecutionFootprintTileIds;
        /// <summary>目标的 BaselineUsefulTileIds。</summary>
        public int[] BaselineUsefulTileIds;
        /// <summary>目标的 BaselineMetric。</summary>
        public float BaselineMetric;
        /// <summary>目标的 HasExecutionBaseline。</summary>
        public bool HasExecutionBaseline;
    }
}
