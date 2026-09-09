using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.WorldTools;
using Cultiway.Utils.Extension;
using NeoModLoader.api;

namespace Cultiway.Content.YaoBeasts;

/// <summary>启灵雨：雨滴命中的凡兽立刻启灵，不做物种过滤，也不要求启灵积累。</summary>
public static class YaoAwakeningRainService
{
    /// <summary>绑定世界神力与雨滴落地回调；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        WorldboxGame.GodPowers.YaoAwakeningRain.click_action = TrySpawn;
        WorldboxGame.GodPowers.YaoAwakeningRain.click_brush_action = TrySpawn;
        WorldboxGame.Drops.YaoAwakeningRain.action_landed_drop = OnDropLanded;
    }

    /// <summary>格子中存在可启灵的动物时才落雨，避免在空地上空刷雨滴。</summary>
    [ClickActionCaller]
    public static bool TrySpawn(WorldTile tile, string powerId)
    {
        foreach (Actor actor in WorldToolDropTargets.SnapshotAliveActors(tile))
        {
            if (!CanAwaken(actor)) continue;
            World.world.drop_manager.spawn(tile, WorldboxGame.Drops.YaoAwakeningRain);
            return true;
        }

        return false;
    }

    /// <summary>雨滴落地：格子内所有未修炼的动物当场启灵。</summary>
    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId)
    {
        foreach (Actor actor in WorldToolDropTargets.SnapshotAliveActors(tile))
        {
            if (!CanAwaken(actor)) continue;
            YaoAwakeningService.TryAwakenByRain(actor.GetExtend());
        }
    }

    /// <summary>已经拥有任意修炼体系的单位不再参与启灵。</summary>
    private static bool CanAwaken(Actor actor)
    {
        if (actor == null || !actor.isAlive()) return false;
        ActorExtend extend = actor.GetExtend();
        return !extend.HasCultisys<Yao>() && !extend.HasCultisys<Xian>() &&
               !extend.HasCultisys<Knight>() && !extend.HasCultisys<Magic>();
    }
}
