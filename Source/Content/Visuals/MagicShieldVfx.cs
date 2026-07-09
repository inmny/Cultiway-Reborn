using Cultiway.Content.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>
///     魔法师常驻护盾特效：遍历可见的魔法修炼者，用 QuantumSprite 画护盾贴图（复用原版 fx_status_shield_t）。
/// </summary>
internal static class MagicShieldVfx
{
    private const float AnimationFrameInterval = 6f;

    private static Sprite[] _shieldSprites;
    private static bool    _loadAttempted;

    public static void Draw(QuantumSpriteAsset asset)
    {
        if (asset?.group_system == null) return;

        EnsureSpritesLoaded();

        var frame = _shieldSprites[Mathf.FloorToInt(Time.frameCount / AnimationFrameInterval) % _shieldSprites.Length];
        var material = LibraryMaterials.instance.mat_world_object;
        var visible = World.world.units.visible_units;

        for (int i = 0; i < visible.count; i++)
        {
            var actor = visible.array[i];
            if (actor == null || !actor.isAlive() || !actor.is_visible) continue;
            if (!actor.GetExtend().E.HasComponent<Magic>()) continue;

            var qs = asset.group_system.getNext();
            qs.setSprite(frame);
            qs.setSharedMat(material);
            var pos = actor.cur_transform_position;
            qs.setPosOnly(ref pos);
            qs.setScale(actor.current_scale.y * asset.base_scale);
        }
    }

    private static void EnsureSpritesLoaded()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;
        _shieldSprites = SpriteTextureLoader.getSpriteList("effects/fx_status_shield_t");
    }
}
