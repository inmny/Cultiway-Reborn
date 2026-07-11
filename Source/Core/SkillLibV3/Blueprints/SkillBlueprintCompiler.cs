using System;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Blueprints;

public enum SkillBlueprintCompileMode
{
    Preview,
    Runtime
}

public sealed class SkillBlueprintCompileResult
{
    public Entity Container;
    public SkillCompatibilityResult Validation = new();
    public bool Success => !Container.IsNull && Validation.IsCompatible;
}

public sealed class SkillBlueprintCompiler
{
    private readonly SkillBlueprintValidator _validator = new();

    public SkillBlueprintCompileResult Compile(SkillBlueprint blueprint, SkillBlueprintCompileMode mode)
    {
        var result = new SkillBlueprintCompileResult();
        result.Validation.Merge(_validator.Validate(blueprint));
        if (!result.Validation.IsCompatible) return result;

        var entityAsset = ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
        var builder = new SkillContainerBuilder(entityAsset);
        builder.UseAnimation(blueprint.AnimationIndex);
        builder.UseCastResources(blueprint.CastResourceRequirement);
        var defaultTrajectoryId = SkillBlueprintTrajectory.ResolveDefaultId(entityAsset);
        if (!string.Equals(defaultTrajectoryId, blueprint.TrajectoryAssetId, StringComparison.Ordinal))
        {
            builder.AddModifier(new Trajectory { ID = blueprint.TrajectoryAssetId });
        }

        foreach (var spec in blueprint.Modifiers)
        {
            var modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
            modifier.Materialize(builder, spec);
        }
        foreach (var spec in blueprint.Modifiers)
        {
            var modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
            modifier.EditorNormalize?.Invoke(builder, spec);
        }

        // 编译器统一掌管名称，Runtime 也使用规则命名构建，避免 Builder 额外发起一次 AI 命名。
        var buildMode = mode == SkillBlueprintCompileMode.Preview
            ? SkillContainerBuildMode.Preview
            : SkillContainerBuildMode.RuleOnly;
        var container = builder.Build(buildMode);
        container.AddComponent(new Components.SkillBlueprintOrigin
        {
            BlueprintId = blueprint.Id,
            Revision = blueprint.Revision,
            Signature = SkillBlueprintSignature.Build(blueprint)
        });
        ApplyName(container, blueprint);

        result.Container = container;
        return result;
    }

    public static void Recycle(Entity container)
    {
        if (container.IsNull) return;
        if (container.Tags.Has<TagOccupied>())
        {
            container.RemoveTag<TagOccupied>();
        }
        if (!container.Tags.Has<TagRecycle>()) container.AddTag<TagRecycle>();
    }

    private static void ApplyName(Entity container, SkillBlueprint blueprint)
    {
        string name;
        if (blueprint.NameMode == SkillBlueprintNameMode.Custom)
        {
            name = blueprint.CustomName;
        }
        else if (!string.IsNullOrWhiteSpace(blueprint.GeneratedName))
        {
            name = blueprint.GeneratedName;
        }
        else if (!string.IsNullOrWhiteSpace(blueprint.RuleName))
        {
            name = blueprint.RuleName;
        }
        else
        {
            name = SkillNameGenerator.Instance.GenerateRuleFor(container);
        }

        if (container.HasName)
        {
            container.GetComponent<EntityName>().value = name;
        }
        else
        {
            container.AddComponent(new EntityName(name));
        }
    }
}
