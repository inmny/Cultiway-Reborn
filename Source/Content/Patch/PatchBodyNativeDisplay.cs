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
    /// <summary>只遍历本次真正可见的原版人物，图片池换人后不会遗留上一人的姿态。</summary>
    /// <param name="__0">原版人物自己的图片池。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnits")]
    private static void Publish(QuantumSpriteAsset __0)
    {
        FormNativeSpriteDeformation.BeginFrame();
        ActorManager manager = World.world.units;
        ActorRenderData data = manager.render_data;
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
                if (renderer.sprite == null ||
                    !ConstructedAbilityService.TryGetBodyRoot(actor.id, out BodyPose pose, out Vector3 foot))
                    continue;
                bool stance = ConstructedAbilityService.TryGetBodyPose(actor.id, BodyOccupancy.Stance, out _);
                Vector2 offset = actor.move_jump_offset + actor.shake_offset;
                var root = new Vector3(foot.x + offset.x, foot.y + foot.z + offset.y, foot.z);
                float scale = pose.Height * Mathf.Max(.03f, actor.actor_scale) / SpatialActorPose.Height(actor);
                FormNativeSpriteDeformation.SetPose(renderer,
                    stance ? new Vector2(pose.Torso.x, pose.Torso.y + pose.Torso.z) : Vector2.zero,
                    pose.Height * .62f, root, scale, pose.LeftLeg.HasValue ? pose : null);
            }
        }

        FormNativeSpriteDeformation.EndFrame();
    }
}