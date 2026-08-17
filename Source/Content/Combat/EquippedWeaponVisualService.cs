using Cultiway.Core.Semantics;
using strings;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>解析当前真实武器的运行时有效性、精灵和材质色。</summary>
internal static class EquippedWeaponVisualService
{
    private const float ReferenceActorScale = 0.25f;

    public static bool IsCurrent(Actor owner, Item weapon)
    {
        return !owner.isRekt() && weapon != null && weapon.isAlive() && !weapon.isBroken() &&
               owner.hasWeapon() && ReferenceEquals(owner.getWeapon(), weapon);
    }

    public static Sprite ResolveSprite(Actor owner, EquipmentAsset weaponAsset)
    {
        Sprite sprite = ItemRendering.getItemMainSpriteFrame(weaponAsset);
        if (sprite == null || !weaponAsset.is_colored) return sprite;

        ColorAsset color = owner.kingdom.getColor();
        long spriteId = DynamicSprites.getItemSpriteID(sprite, color);
        return DynamicSprites.getCachedAtlasItemSprite(spriteId, sprite, color);
    }

    public static float ResolveSpriteAngle(Sprite sprite)
    {
        return sprite.rect.width >= sprite.rect.height ? 0f : -90f;
    }

    public static float ResolveWorldVisualScale(Actor owner)
    {
        float actorScale = Mathf.Max(0.1f, owner.stats[S.scale]);
        return Mathf.Clamp(actorScale / ReferenceActorScale, 0.4f, 4f);
    }

    public static void ResolveTrailColors(
        Actor owner,
        EquipmentAsset weaponAsset,
        out Color coreColor,
        out Color glowColor)
    {
        SemanticAsset materialSemantic = ResolveWeaponMaterialSemantic(weaponAsset.material);
        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        builder.Add(
            materialSemantic,
            1f,
            SemanticScope.Intrinsic,
            new SemanticSourceRef("content.equipped_weapon.material", weaponAsset.id));
        SemanticColorPalette palette = SemanticColorResolver.Resolve(builder.Build(), materialSemantic, 2);
        coreColor = SemanticColorResolver.ToVfxColor(
            palette.GetColor(0, new Color(0.78f, 0.82f, 0.88f)));
        glowColor = palette.Count > 1
            ? SemanticColorResolver.ToVfxColor(palette.Secondary)
            : Color.Lerp(coreColor, Color.white, 0.42f);

        ColorAsset kingdomColor = owner.kingdom?.getColor();
        if (!weaponAsset.is_colored || kingdomColor == null) return;
        Color tint = SemanticColorResolver.ToVfxColor(kingdomColor.getColorMain());
        coreColor = Color.Lerp(coreColor, tint, 0.78f);
        glowColor = Color.Lerp(glowColor, tint, 0.52f);
    }

    private static SemanticAsset ResolveWeaponMaterialSemantic(string material)
    {
        return material switch
        {
            "wood" => SkillSemantics.Element.Wood,
            "stone" => SkillSemantics.Element.Earth,
            "copper" or "bronze" or "silver" or "iron" or "steel" or "mythril" or "adamantine" =>
                SkillSemantics.Element.Iron,
            _ => SkillSemantics.Element.Generic,
        };
    }
}
