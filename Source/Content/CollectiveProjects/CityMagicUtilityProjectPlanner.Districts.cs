using System.Collections.Generic;
using System.Linq;
using static Cultiway.Content.CollectiveProjects.CityMagicUtilityProjectPlannerSearch;

namespace Cultiway.Content.CollectiveProjects;

internal sealed partial class CityRoutineMagicProjectPlanner
{
    /// <summary>同一座城内按实际连通的多个问题节点安排净化，只有一个节点时沿用原工程。</summary>
    private static bool TryPlanDistricts(City city, IReadOnlyList<MagicUtilitySpellOption> options,
        IReadOnlyList<WorldTile> polluted, CityMagicUtilityProjectPayload payload, HashSet<int> allowed,
        out MagicUtilityPlanCandidate first)
    {
        first = default;
        var source = city.getBuildingOfType("type_bonfire");
        if (source?.current_tile?.zone is not { } root || root.city != city || !source.isUsable()) return false;
        var groups = polluted.Where(tile => tile.zone?.city == city).GroupBy(tile => tile.zone).ToArray();
        if (groups.Length < 2) return false;
        var routes = CityDistrictPlan.Routes(city, root);
        var candidates = new List<(MagicUtilityPlanCandidate Plan, int[] Route)>();
        foreach (var group in groups)
        {
            if (!routes.TryGetValue(group.Key, out int[] route) ||
                !TryFindBestArea(options, group.ToArray(), payload, allowed, null, out var candidate)) continue;
            candidates.Add((candidate, route));
        }
        if (candidates.Count < 2) return false;

        var order = candidates.OrderBy(candidate => candidate.Route.Length).ThenBy(candidate => candidate.Plan.Target.tile_id).ToArray();
        payload.Districts = new CityDistrictPlan
        {
            SourceBuilding = source.id,
            Nodes = order.Select(candidate => new CityDistrictNode
                { Target = candidate.Plan.Target.tile_id, Route = candidate.Route }).ToArray()
        };
        first = order[0].Plan;
        return true;
    }
}
