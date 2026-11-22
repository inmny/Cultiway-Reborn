using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>双方先核对再解除旧控制，最后在同一次确认中交换连续位置并通知空间索引。</summary>
    /// <param name="source">实际参与换位的人物。</param><param name="target">实际接触人物。</param>
    /// <param name="exchange">已经编译的换位资料。</param><param name="result">真实接触结果。</param>
    /// <param name="strength">传播到实际接触的作用倍率。</param>
    /// <param name="useTiles">旧落地词条交换原地块，普通构造保留连续坐标。</param>
    private static void ApplyContactExchange(Actor source, Actor target, CompiledConstructionExchange exchange,
        ApplyAttackState result, float strength, bool useTiles = false)
    {
        if (exchange == null || exchange.Chance <= 0f || strength <= 0f || result != ApplyAttackState.Hit || source == null)
            return;
        if (source == target || !ExchangeEligible(source, target)) return;
        float chance = Mathf.Clamp01(exchange.Chance * strength);
        if (chance < 1f && !Randy.randomChance(chance)) return;

        Vector2 sourcePosition = useTiles ? (Vector2)source.current_tile.posV3 : source.current_position;
        Vector2 targetPosition = useTiles ? (Vector2)target.current_tile.posV3 : target.current_position;
        long sourceId = source.id, targetId = target.id, generation = worldGeneration;
        if (!ReleaseForExchange(source, sourceId, generation) || !ReleaseForExchange(target, targetId, generation)) return;
        if (source.data?.id != sourceId || target.data?.id != targetId ||
            !ExchangeEligible(source, target)) return;

        // 两端都有效后再写入；只放置终点，不向两点之间提供步法、受力或伤害路径。
        PlaceAfterExchange(source, targetPosition, exchange.WaitSeconds);
        PlaceAfterExchange(target, sourcePosition, exchange.WaitSeconds);
        NotifyActorChanged(source);
        NotifyActorChanged(target);
    }

    /// <summary>原词条只允许两个仍存活、落地且处于不同地块的人物换位。</summary>
    /// <param name="source">当前控制人物。</param><param name="target">实际接触人物。</param>
    private static bool ExchangeEligible(Actor source, Actor target) =>
        source.isAlive() && target.isAlive() && source.hasHealth() && target.hasHealth() &&
        source.current_tile != null && target.current_tile != null && source.current_tile != target.current_tile &&
        source.position_height <= 0f && target.position_height <= 0f;

    /// <summary>归还旧身体、双方关系和受力，避免换位后仍由旧动作拉回原处。</summary>
    /// <param name="actor">即将换位的人物。</param><param name="actorId">接触时的真实人物编号。</param>
    /// <param name="generation">本次接触所属的世界。</param>
    /// <param name="keepTask">本次搬运是原出行任务的一步时继续该任务。</param>
    private static bool ReleaseForExchange(Actor actor, long actorId, long generation, bool keepTask = false)
    {
        if (generation != worldGeneration || actor.data?.id != actorId) return false;
        InterruptBodyConnections(actor);
        if (generation != worldGeneration || actor.data?.id != actorId) return false;
        for (int i = 0; i < casts.Count; i++)
        {
            RunningCast running = casts[i];
            if (running.SourceId != actor.id || !running.BodyControlled) continue;
            running.Cast.InterruptBody();
            ReleaseBody(running);
            if (generation != worldGeneration || actor.data?.id != actorId) return false;
        }
        if (!ConstructedActorImpulse.EndForRelocation(actor, generation)) return false;
        if (!keepTask) actor.cancelAllBeh();
        return generation == worldGeneration && actor.data?.id == actorId;
    }

    /// <summary>原版地块身份和连续坐标一起更新；显示通过现有人物更新同步，不创建传送烟雾。</summary>
    /// <param name="actor">实际人物。</param><param name="position">另一端换位前的连续位置。</param>
    /// <param name="wait">换位后的停步世界秒数。</param>
    private static void PlaceAfterExchange(Actor actor, Vector2 position, float wait)
    {
        actor.spawnOn(World.world.GetTileSimple((int)position.x, (int)position.y), 0f);
        float ground = simulation.terrain?.Sample(position.x, position.y) ?? 0f;
        PublishBodyPosition(actor, new Vector3(position.x, position.y, ground));
        actor.makeWait(wait);
        Persistence.ActorPositionSaveService.Remember(actor.GetExtend());
    }
}
