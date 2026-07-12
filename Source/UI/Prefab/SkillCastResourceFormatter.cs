using System.Linq;
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
}
