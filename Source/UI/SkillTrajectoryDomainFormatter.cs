using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils;

namespace Cultiway.UI;

internal static class SkillTrajectoryDomainFormatter
{
    private static readonly SkillTrajectoryDomain[] OrderedDomains =
    [
        SkillTrajectoryDomain.FlyingBody,
        SkillTrajectoryDomain.FlyingWave,
        SkillTrajectoryDomain.Ballistic,
        SkillTrajectoryDomain.Skyfall,
        SkillTrajectoryDomain.GroundTravel,
        SkillTrajectoryDomain.GroundManifest,
        SkillTrajectoryDomain.TargetManifest,
        SkillTrajectoryDomain.Beam,
        SkillTrajectoryDomain.Chain,
        SkillTrajectoryDomain.StationaryField,
        SkillTrajectoryDomain.MobileField,
        SkillTrajectoryDomain.Barrier,
        SkillTrajectoryDomain.Aura,
        SkillTrajectoryDomain.Melee
    ];

    public static string Format(SkillTrajectoryDomain domains)
    {
        var names = new List<string>();
        for (int i = 0; i < OrderedDomains.Length; i++)
        {
            SkillTrajectoryDomain domain = OrderedDomains[i];
            if ((domains & domain) != domain) continue;
            names.Add(GetName(domain));
        }
        return names.Count == 0
            ? "Cultiway.SkillTrajectoryDomain.None".Localize()
            : string.Join("、", names);
    }

    public static SkillTrajectoryDomain GetPrimary(SkillTrajectoryDomain domains)
    {
        for (int i = 0; i < OrderedDomains.Length; i++)
        {
            if ((domains & OrderedDomains[i]) == OrderedDomains[i]) return OrderedDomains[i];
        }
        return SkillTrajectoryDomain.None;
    }

    public static int GetSortOrder(SkillTrajectoryDomain domain)
    {
        for (int i = 0; i < OrderedDomains.Length; i++)
        {
            if (OrderedDomains[i] == domain) return i;
        }
        return OrderedDomains.Length;
    }

    public static string GetName(SkillTrajectoryDomain domain)
    {
        return $"Cultiway.SkillTrajectoryDomain.{domain}".Localize();
    }

    public static string GetDescription(SkillTrajectoryDomain domain)
    {
        return $"Cultiway.SkillTrajectoryDomain.{domain}.Description".Localize();
    }
}
