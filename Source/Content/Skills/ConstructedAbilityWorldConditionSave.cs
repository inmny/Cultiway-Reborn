using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private const string WorldConditionSectionName = "构造维持的世界条件";

    /// <summary>长期条件从世界区段恢复，原施放和资源扣费各自处理。</summary>
    private static void RegisterWorldConditionSave() =>
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, ConditionArchive>(
            "constructed_world_conditions", WorldConditionSectionName, 1,
            _ => CaptureWorldConditions(), RestoreWorldConditions,
            data =>
            {
                if (data.Records == null) throw new InvalidDataException("缺少世界条件记录");
            }));

    /// <summary>收集已经确立且仍有效的条件。普通短期条件及任务边界不进入此区段。</summary>
    private static ConditionArchive CaptureWorldConditions()
    {
        double now = World.world.getCurWorldTime();
        var records = new List<ConditionRecord>();
        foreach (var running in casts) Add(running.WorldCondition);
        foreach (var condition in restoredFireConditions) Add(condition);
        return records.Count == 0 ? null : new ConditionArchive { Records = JArray.FromObject(records) };

        void Add(ActiveWorldCondition condition)
        {
            if (condition == null || !condition.Rule.ContinueAfterLoad || condition.HasScope || !condition.IsLive(now)) return;
            records.Add(new ConditionRecord
            {
                Source = condition.SourceId,
                X = condition.Center.x,
                Y = condition.Center.y,
                Z = condition.Center.z,
                Radius = condition.Rule.Radius,
                EntireWorld = condition.Rule.EntireWorld,
                Remaining = condition.EndsAt - now
            });
        }
    }

    /// <summary>等原版人物完成读取再按原归属接回条件，只重建有效范围索引。</summary>
    private static void RestoreWorldConditions(MapBox world, ConditionArchive archive)
    {
        foreach (var condition in restoredFireConditions) UnindexWorldCondition(condition);
        restoredFireConditions.Clear();
        double now = world.getCurWorldTime();
        for (int index = 0; index < archive.Records.Count; index++)
        {
            ConditionRecord row;
            try
            {
                row = archive.Records[index].ToObject<ConditionRecord>();
            }
            catch (JsonException error)
            {
                ModClass.I.WorldSaves.LastReport.Skip(WorldConditionSectionName, "记录 " + index, error.Message);
                continue;
            }

            if (row == null || row.Source <= 0 || !(row.X >= 0f && row.X < MapBox.width && row.Y >= 0f && row.Y < MapBox.height) ||
                float.IsNaN(row.Z) || float.IsInfinity(row.Z) || !(row.Radius >= .5f && row.Radius <= 256f) ||
                !(row.Remaining > 0d && row.Remaining <= 3600d))
            {
                ModClass.I.WorldSaves.LastReport.Skip(WorldConditionSectionName, "记录 " + index, "范围、来源或剩余时间无效");
                continue;
            }
            Actor owner = world.units.get(row.Source);
            if (owner?.data?.id != row.Source || !owner.isAlive())
            {
                ModClass.I.WorldSaves.LastReport.Skip(WorldConditionSectionName, "记录 " + index, "原维持者未能恢复");
                continue;
            }

            var rule = new CompiledWorldCondition(new ConstructionWorldCondition
            {
                EntireWorld = row.EntireWorld,
                Radius = row.Radius,
                ContinueAfterLoad = true
            });
            var restored = new ActiveWorldCondition(owner, row.Source, rule, new Vector3(row.X, row.Y, row.Z), now + row.Remaining);
            restoredFireConditions.Add(restored);
            IndexWorldCondition(restored);
        }
    }

    private sealed class ConditionArchive
    {
        public JArray Records;
    }

    private sealed class ConditionRecord
    {
        public long Source;
        public float X, Y, Z, Radius;
        public double Remaining;
        public bool EntireWorld;
    }
}
