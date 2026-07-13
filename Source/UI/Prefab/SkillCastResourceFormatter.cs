using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.UI.Prefab;

internal static class SkillCastResourceFormatter
{
    public static string Format(SkillCastResourceRequirement requirement)
    {
        if (!requirement.IsConfigured)
        {
            return "Cultiway.Wanfa.UI.State.NoCastResource".Localize();
        }

        var mode = GetModeName(requirement.Mode);
        var separator = requirement.Mode switch
        {
            SkillCastResourceRequirementMode.AnyOf => " > ",
            SkillCastResourceRequirementMode.AllOf => " + ",
            _ => string.Empty
        };
        var resources = string.Join(separator, requirement.ResourceAssetIds.Select(id => id.Localize()));
        return string.Format("Cultiway.Wanfa.UI.Format.CastResource".Localize(), mode, resources);
    }

    public static string GetModeName(SkillCastResourceRequirementMode mode)
    {
        return $"Cultiway.Wanfa.UI.CastResource.Mode.{mode}".Localize();
    }

    public static string FormatItemLevel(SkillCastResourceRequirement requirement, ItemLevel itemLevel)
    {
        var separator = requirement.Mode == SkillCastResourceRequirementMode.AllOf ? " + " : " / ";
        var names = requirement.ResourceAssetIds
            .Select(id => ModClass.I.SkillV3.CastResourceLib.get(id).ItemLevelFormatter(itemLevel))
            .Distinct()
            .ToArray();
        return string.Join(separator, names);
    }
}
