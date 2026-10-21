using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Settings;

/// <summary>在蓝图入口检查单招设置，并在容器与蓝图之间转换。</summary>
public static class SkillSettingsCompiler
{
    /// <summary>检查外部设置是否适合模板已有的作用方式和材质。</summary>
    /// <param name="blueprint">待编译的单招蓝图。</param>
    /// <param name="result">累计检查结果。</param>
    public static void Validate(SkillBlueprint blueprint, SkillCompatibilityResult result)
    {
        if (blueprint.NatureId != null && !SkillNatureCatalog.Natures.ContainsKey(blueprint.NatureId) ||
            !FiniteRange(blueprint.AreaMultiplier, .1f, 8f) || !FiniteRange(blueprint.PowerMultiplier, .05f, 8f) ||
            !FiniteRange(blueprint.Duration, 0f, 30f)) result.AddError("settings.values");
        var asset = ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
        if (blueprint.ConductiveWater && (!SkillNatureCatalog.Natures.TryGetValue(blueprint.NatureId ?? "", out var nature) ||
            !nature.CanConduct || asset?.ImpactProfile.IsField != true)) result.AddError("settings.water_field");
        if (blueprint.NatureId != null && asset?.CarrierRequirement == SkillCarrierRequirement.PhysicalBody)
            result.AddError("settings.physical_nature");
        if (blueprint.NatureId != null && asset != null && !asset.SupportsNature(blueprint.NatureId))
            result.AddError("settings.material");
        if (blueprint.Duration > 0 && asset != null && blueprint.Duration < asset.MinimumOverrideDuration)
            result.AddError("settings.duration_short");
    }

    /// <summary>将已检查的设置放入容器，刷新显示和强度。</summary>
    /// <param name="container">当前技能容器。</param>
    /// <param name="blueprint">已通过检查的蓝图。</param>
    public static void Attach(Entity container, SkillBlueprint blueprint)
    {
        container.AddComponent(new SkillSettings
        {
            NatureId = blueprint.NatureId, AreaMultiplier = blueprint.AreaMultiplier,
            PowerMultiplier = blueprint.PowerMultiplier, Duration = blueprint.Duration,
            ConductiveWater = blueprint.ConductiveWater, ContactEnabled = blueprint.ContactEnabled
        });
        SkillContainerUtils.RefreshVfxElement(container);
        SkillContainerEvaluator.Refresh(container);
    }

    /// <summary>导出当前单招设置，用于保存、复制和签名。</summary>
    /// <param name="container">当前技能容器。</param>
    /// <param name="blueprint">接收设置的蓝图。</param>
    public static void Export(Entity container, SkillBlueprint blueprint)
    {
        if (!container.TryGetComponent(out SkillSettings settings)) return;
        blueprint.NatureId = settings.NatureId; blueprint.AreaMultiplier = settings.AreaMultiplier;
        blueprint.PowerMultiplier = settings.PowerMultiplier; blueprint.Duration = settings.Duration;
        blueprint.ConductiveWater = settings.ConductiveWater; blueprint.ContactEnabled = settings.ContactEnabled;
    }

    /// <summary>取得登记的实际元素组成。</summary>
    /// <param name="natureId">性质编号。</param>
    public static ElementComposition Element(string natureId) => SkillNatureCatalog.Natures[natureId].Element;

    /// <summary>取得登记的显示性质。</summary>
    /// <param name="natureId">性质编号。</param>
    public static Visuals.SkillVfxElementAsset Visual(string natureId) => SkillNatureCatalog.Natures[natureId].Visual;

    /// <summary>在外部读取入口排除非有限或越界数值。</summary>
    /// <param name="value">外部数值。</param>
    /// <param name="minimum">允许的最小值。</param>
    /// <param name="maximum">允许的最大值。</param>
    private static bool FiniteRange(float value, float minimum, float maximum)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
}
