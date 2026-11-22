using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Effects;

namespace Cultiway.Content.Skills;

/// <summary>把暂时节奏交给选中麦田自身的生长更新。</summary>
internal static class CropGrowthPacing
{
    internal const string ActionId = "crop.pace";

    private static readonly Dictionary<long, (Building Crop, CompiledProcessTime Schedule, double StartedAt)> paces = new();

    /// <summary>只在真实地块根部接收尚会生长的原版麦田。</summary>
    internal static bool CanApply(WorldTile tile) =>
        tile?.building is { component_wheat: { } wheat } crop && crop.current_tile == tile &&
        crop.asset.wheat && crop.isNormal() && !wheat.isMaxLevel();

    /// <summary>从本次正式提交开始调整后续有效时长，之前的积累不变。</summary>
    internal static SkillEffectResult Apply(ConstructionWorldContext context)
    {
        Building crop = context.Tile.building;
        CompiledProcessTime time = context.ProcessTime;
        if (time == null || time.AdjustmentWork(time.LastWindowEnd) == 0d)
            return Forget(crop) ? new SkillEffectResult(SkillEffectOutcomeFlags.CropPaceChanged) : default;
        paces[crop.id] = (crop, time, World.world.getCurWorldTime());
        return new SkillEffectResult(SkillEffectOutcomeFlags.CropPaceChanged);
    }

    /// <summary>原版每次从上一次更新到本次的秒数中，只换算与已受理时间窗相交的部分。</summary>
    internal static bool Advance(Building crop, ref float elapsed)
    {
        if (paces.Count == 0 || crop?.data == null || !paces.TryGetValue(crop.id, out var pace)) return true;
        if (!ReferenceEquals(pace.Crop, crop) || crop.current_tile?.building != crop || !CanApply(crop.current_tile))
        {
            paces.Remove(crop.id);
            return true;
        }

        double now = World.world.getCurWorldTime() - pace.StartedAt;
        CompiledProcessTime time = pace.Schedule;
        double earlier = now - elapsed;
        elapsed = (float)Math.Max(0d, time.WorkAt(now) - time.WorkAt(earlier));
        if (now >= time.LastWindowEnd) paces.Remove(crop.id);
        // 即使上批进度已达到阈值，暂停时也不跨入下一阶段。
        return elapsed > 0f;
    }

    /// <summary>按原建筑身份解除，换田不接管上一棵的暂时节奏。</summary>
    internal static bool Forget(Building crop) => crop?.data != null && paces.Remove(crop.id);

    /// <summary>临时节奏归当前世界，原版生长进度仍由麦田存档。</summary>
    internal static void Clear() => paces.Clear();
}
