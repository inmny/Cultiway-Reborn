using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class ActorTools
{
    public static ActorExtend GetExtend(this Actor actor)
    {
        return ModClass.I.ActorExtendManager.Get(actor.data.id, true);
    }
}