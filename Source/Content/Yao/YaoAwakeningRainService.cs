using Cultiway.Content.Components;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Skills;
using Cultiway.Core.WorldTools;
using Cultiway.Utils.Extension;
using NeoModLoader.api;

namespace Cultiway.Content.YaoBeasts;

/// <summary>启灵雨：雨滴命中的凡兽立刻启灵，不做物种过滤，也不要求启灵积累。</summary>
public static class YaoAwakeningRainService
{
    /// <summary>原版启灵雨实际落地时使用的地块操作。</summary>
    private static readonly ConstructionWorldAction awakeningContact = new(
        "nature.awaken_by_rain", tile => tile.hasUnits(), AwakenAt);

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

    /// <summary>以实际雨滴落地为触发，把该格交给共用地块结算。</summary>
    /// <param name="drop">原版落下的雨滴。</param>
    /// <param name="tile">雨滴实际接触的格子。</param>
    /// <param name="dropId">原雨滴资料编号。</param>
    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId) =>
        ConstructedAbilityService.ConfirmEnvironmentContact(tile, WorldboxGame.GodPowers.YaoAwakeningRain.id,
            awakeningContact);

    /// <summary>格内仍符合原资格的角色当场启灵，按实际成功人数提交结果。</summary>
    /// <param name="tile">实际落点。</param>
    private static SkillEffectResult AwakenAt(WorldTile tile)
    {
        int count = 0;
        foreach (Actor actor in WorldToolDropTargets.SnapshotAliveActors(tile))
        {
            if (!CanAwaken(actor)) continue;
            if (YaoAwakeningService.TryAwakenByRain(actor.GetExtend())) count++;
        }

        return count == 0 ? SkillEffectResult.None : new SkillEffectResult(SkillEffectOutcomeFlags.CreatureAwakened, count);
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
