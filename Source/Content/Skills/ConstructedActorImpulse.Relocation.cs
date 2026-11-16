namespace Cultiway.Content.Skills;

internal static partial class ConstructedActorImpulse
{
    /// <summary>位置被明确重新放置前归还旧运动，旧记录不能在下次原版更新中把人物拖回。</summary>
    /// <param name="actor">真实被重新放置的人物。</param><param name="generation">本次接触所属的世界。</param>
    internal static bool EndForRelocation(Actor actor, long generation)
    {
        long actorId = actor.id;
        if (motions.TryRemove(actorId, out Motion motion)) CancelBodyOwnership(motion, "Relocated");
        if (generation != ConstructedAbilityService.BodyWorldGeneration || actor.data?.id != actorId) return false;
        actor.stopForce();
        actor.batch.c_action_landed.Remove(actor);
        return true;
    }
}
