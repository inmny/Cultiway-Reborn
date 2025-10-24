using Cultiway.Const;
using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class ActorTools
{
    private static readonly ActorExtendManager ActorExtendManager = ModClass.I.ActorExtendManager;
    public static ActorExtend GetExtend(this Actor actor)
    {
        return ActorExtendManager.Get(actor);
    }
    public static bool HasSect(this Actor actor)
    {
        return actor.GetExtend().sect != null;
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