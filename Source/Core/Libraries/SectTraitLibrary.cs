using System.Collections.Generic;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门特质资产库。
/// </summary>
public class SectTraitLibrary : BaseTraitLibrary<SectTrait>
{
    public override string icon_path => "cultiway/icons/sect_traits/";

    public override List<string> getDefaultTraitsForMeta(ActorAsset pAsset)
    {
        return null;
    }

    public override void linkAssets()
    {
        LinkExclusiveGroupsToOppositeList();
        base.linkAssets();
    }

    private void LinkExclusiveGroupsToOppositeList()
    {
        for (int i = 0; i < list.Count; i++)
        {
            SectTrait trait = list[i];
            if (!IsExclusiveGroupTrait(trait)) continue;

            for (int j = 0; j < list.Count; j++)
            {
                SectTrait other = list[j];
                if (i != j && IsExclusiveGroupTrait(other) && other.group_id == trait.group_id)
                {
                    AddOppositeId(trait, other.id);
                }
            }
        }
    }

    private static void AddOppositeId(SectTrait trait, string oppositeId)
    {
        if (trait.opposite_list != null && trait.opposite_list.Contains(oppositeId)) return;
        trait.addOpposite(oppositeId);
    }

    public List<SectTrait> GetResidenceStrategies()
    {
        var result = new List<SectTrait>();
        for (int i = 0; i < list.Count; i++)
        {
            SectTrait trait = list[i];
            if (trait.isResidenceStrategy)
            {
                result.Add(trait);
            }
        }

        return result;
    }

    public List<SectTrait> GetFoundingPolicies(string groupId)
    {
        var result = new List<SectTrait>();
        for (int i = 0; i < list.Count; i++)
        {
            SectTrait trait = list[i];
            if (trait.isFoundingPolicy && trait.group_id == groupId)
            {
                result.Add(trait);
            }
        }

        return result;
    }

    private static bool IsExclusiveGroupTrait(SectTrait trait)
    {
        return trait.isResidenceStrategy || trait.isFoundingPolicy;
    }
}
