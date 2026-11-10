using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Patch;

/// <summary>在原版完成主体动画和着色后合入器官，覆盖普通批量绘制及受击等单独绘制。</summary>
public static class PatchCreatureAppearance
{
    /// <summary>只读取已经存在的完整身体，不在绘制过程中创建或改写游戏状态。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), "checkSpriteToRender")]
    private static void Compose(Actor __instance, ref Sprite __result)
    {
        __result = ComposeActor(__instance, __result, null);
    }

    /// <summary>普通单位使用准备好的图片数组，合成必须在原版并行准备结束后回到主线程执行。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.calculateVisibleActors))]
    private static void ComposeVisible(ActorManager __instance)
    {
        ActorRenderData data = __instance.render_data;
        for (int i = 0; i < __instance.visible_units.count; i++)
        {
            if (!data.has_normal_render[i]) continue;
            data.main_sprite_colored[i] = ComposeActor(__instance.visible_units.array[i],
                data.main_sprite_colored[i], data.main_sprites[i]);
        }
    }

    /// <summary>装备及器官共用最终合成入口，不在绘制中创建身体或改变穿戴。</summary>
    private static Sprite ComposeActor(Actor owner, Sprite body, Sprite nativeFrame)
    {
        if (body == null) return body;
        string excluded = ConstructedAbilityService.TryGetBodyOrgan(owner.id, out var organ) ? organ.Layer.id : null;
        return CreatureAppearanceService.ComposeActorFrame(owner, body, nativeFrame ?? owner.calculateMainSprite(), excluded)?.Sprite ?? body;
    }
}
