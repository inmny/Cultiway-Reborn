using Cultiway.Const;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class ActorTools
{
    private static readonly ActorExtendManager ActorExtendManager = ModClass.I.ActorExtendManager;
    public static ActorExtend GetExtend(this Actor actor)
    {
        return ActorExtendManager.Get(actor);
    }
    public static bool CheckExtend(this Actor actor)
    {
        return ActorExtendManager.Has(actor);
    }
    public static bool HasSect(this Actor actor)
    {
        return actor.GetExtend().sect != null;
    }

    public static SectRank GetSectRank(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SectRank_Int, out int rank, (int)SectRank.None);
        return (SectRank)rank;
    }

    public static void SetSectRank(this Actor actor, SectRank rank)
    {
        actor.data.set(ActorDataKeys.SectRank_Int, (int)rank);
    }

    public static void ClearSectRank(this Actor actor)
    {
        actor.data.removeInt(ActorDataKeys.SectRank_Int);
    }

    public static float GetSectJoinTime(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SectJoinTime_Float, out float joinTime, -1f);
        return joinTime;
    }

    public static void SetSectJoinTime(this Actor actor, float joinTime)
    {
        actor.data.set(ActorDataKeys.SectJoinTime_Float, joinTime);
    }

    public static void ClearSectJoinTime(this Actor actor)
    {
        actor.data.removeFloat(ActorDataKeys.SectJoinTime_Float);
    }

    public static int GetSectTenureYears(this Actor actor)
    {
        float joinTime = actor.GetSectJoinTime();
        if (joinTime < 0f) return 0;

        float elapsed = (float)World.world.getCurWorldTime() - joinTime;
        return Mathf.Max(0, Mathf.FloorToInt(elapsed / TimeScales.SecPerYear));
    }

    public static int GetSectContribution(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SectContribution_Int, out int contribution, 0);
        return contribution;
    }

    public static void AddSectContribution(this Actor actor, int contribution)
    {
        if (contribution <= 0) return;

        actor.data.set(ActorDataKeys.SectContribution_Int, actor.GetSectContribution() + contribution);
    }

    public static void ClearSectContribution(this Actor actor)
    {
        actor.data.removeInt(ActorDataKeys.SectContribution_Int);
    }

    public static string GetSourceSpawnerAssetId(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SourceSpawnerId_String, out string result);
        return result;
    }

    public static long GetSourceSpawnerId(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SourceSpawnerId_Long, out long result, -1);
        return result;
    }

    public static void SetSourceSpawnerId(this Actor actor, long id)
    {
        actor.data.set(ActorDataKeys.SourceSpawnerId_Long, id);
    }

    public static void SetSourceSpawnerAssetId(this Actor actor, string assetId)
    {
        actor.data.set(ActorDataKeys.SourceSpawnerId_String, assetId);
    }
}
