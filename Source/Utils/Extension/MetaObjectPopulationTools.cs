using System.Collections.Generic;

namespace Cultiway.Utils.Extension;

public static class MetaObjectPopulationTools
{
    public static int CountValidUnits(this IEnumerable<Actor> units)
    {
        int count = 0;
        foreach (Actor actor in units)
        {
            if (!IsPopulationActor(actor)) continue;
            count++;
        }

        return count;
    }

    public static int CountValidAdults(this IEnumerable<Actor> units)
    {
        int count = 0;
        foreach (Actor actor in units)
        {
            if (!IsPopulationActor(actor) || !actor.isAdult()) continue;
            count++;
        }

        return count;
    }

    public static int CountValidChildren(this IEnumerable<Actor> units)
    {
        int count = 0;
        foreach (Actor actor in units)
        {
            if (!IsPopulationActor(actor) || !actor.isBaby()) continue;
            count++;
        }

        return count;
    }

    private static bool IsPopulationActor(Actor actor)
    {
        return !actor.isRekt()
               && actor.asset != null
               && !actor.asset.is_boat;
    }
}
