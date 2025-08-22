using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class ActorTools
{
    private static readonly ActorExtendManager ActorExtendManager = ModClass.I.ActorExtendManager;
    public static ActorExtend GetExtend(this Actor actor)
    {
        return ActorExtendManager.Get(actor);
    }

    public static bool HasExtend(this Actor actor)
    {
        return ActorExtendManager.Has(actor);
    }
    public static bool HasSect(this Actor actor)
    {
        return actor.GetExtend().sect != null;
    }
}