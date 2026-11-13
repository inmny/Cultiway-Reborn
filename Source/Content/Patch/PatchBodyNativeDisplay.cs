using Cultiway.Content.Skills;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Patch;

/// <summary>在原版人物图片池完成更新后，将共同上身动作交给图片变形绘制。</summary>
internal static class PatchBodyNativeDisplay
{
    /// <summary>身体和持物图片共用一轮姿态寿命，避免持物在身体发布结束时被提前回收。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(QuantumSpriteManager), nameof(QuantumSpriteManager.update))]
    private static void Begin()
    {
        FormNativeSpriteDeformation.BeginFrame();
        BodyOrganNativeDisplay.BeginFrame();
    }

    /// <summary>全部原版图片发布完成后才归还已经结束的网格。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(QuantumSpriteManager), nameof(QuantumSpriteManager.update))]
    private static void End()
    {
        FormNativeSpriteDeformation.EndFrame();
        BodyOrganNativeDisplay.EndFrame();
    }

    /// <summary>只遍历本次真正可见的原版人物，图片池换人后不会遗留上一人的姿态。</summary>
    /// <param name="__0">原版人物自己的图片池。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnits")]
    private static void Publish(QuantumSpriteAsset __0)
    {
        ActorManager manager = World.world.units;
        ActorRenderData data = manager.render_data;
        bool rotated = ConstructedAbilityService.HasNativeBodySurfaces;
        int count = 0;
        for (int i = 0; i < manager.visible_units.count; i++)
            if (data.has_normal_render[i])
                count++;
        if (count > 0)
        {
            QuantumSprite[] pictures = __0.group_system.getFastActiveList(count);
            int index = 0;
            for (int i = 0; i < manager.visible_units.count; i++)
            {
                if (!data.has_normal_render[i]) continue;
                SpriteRenderer renderer = pictures[index++].sprite_renderer;
                Actor actor = manager.visible_units.array[i];
                bool hasBody = ConstructedAbilityService.TryGetBodyRoot(actor.id, out BodyPose pose, out Vector3 foot);
                if (renderer.sprite != null && hasBody)
                {
                    Color actual = renderer.color; actual.a = actor.color.a; renderer.color = actual;
                    foreach (var organ in ConstructedAbilityService.GetBodyOrgans(actor.id))
                        BodyOrganNativeDisplay.Publish(renderer, actor, organ.Binding, organ.Pose, foot);
                }
                if (rotated && ConstructedAbilityService.TryGetNativeBodySurface(actor.id, out BodySurfacePose surface))
                {
                    Color tint = renderer.color;
                    tint.a = actor.color.a;
                    renderer.color = tint;
                    FormNativeSpriteDeformation.SetSurface(renderer, surface, false);
                    continue;
                }
                if (renderer.sprite == null || !hasBody)
                    continue;
                // 原版图片池只比较红绿蓝；接管身体时也同步实际透明度，避免画面仍不透明而接触已透过。
                Color color = renderer.color;
                if (color.a != actor.color.a)
                {
                    color.a = actor.color.a;
                    renderer.color = color;
                }

                bool stance = ConstructedAbilityService.TryGetBodyPose(actor.id, BodyOccupancy.Stance, out _);
                Vector2 offset = actor.move_jump_offset + actor.shake_offset;
                var root = new Vector3(foot.x + offset.x, foot.y + foot.z + offset.y, foot.z);
                float scale = pose.Height * Mathf.Max(.03f, actor.actor_scale) / SpatialActorPose.Height(actor);
                FormNativeSpriteDeformation.SetPose(renderer,
                    stance ? pose.Torso : Vector3.zero,
                    pose.Height * .62f, root, scale,
                    pose.LeftLeg.HasValue || pose.Head.HasValue || pose.Organ.HasValue ? pose : null);
            }
        }

    }

    /// <summary>被带动的实际持物沿同一完整表面绘制，不留一件仍直立的旧图片。</summary>
    /// <param name="__0">原版持物图片池。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitItems")]
    private static void Items(QuantumSpriteAsset __0)
    {
        if (!ConstructedAbilityService.HasNativeBodySurfaces) return;
        ActorManager manager = World.world.units;
        ActorRenderData data = manager.render_data;
        int count = 0;
        for (int i = 0; i < manager.visible_units.count; i++) if (data.has_item[i]) count++;
        if (count == 0) return;
        QuantumSprite[] pictures = __0.group_system.getFastActiveList(count);
        int index = 0;
        for (int i = 0; i < manager.visible_units.count; i++)
        {
            if (!data.has_item[i]) continue;
            SpriteRenderer renderer = pictures[index++].sprite_renderer;
            if (ConstructedAbilityService.TryGetNativeBodySurface(manager.visible_units.array[i].id,
                    out BodySurfacePose surface))
                FormNativeSpriteDeformation.SetSurface(renderer, surface, true);
        }
    }
}
