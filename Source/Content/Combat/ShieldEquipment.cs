using System.Collections.Generic;
using Cultiway.Abstract;

namespace Cultiway.Content.Combat;

/// <summary>盾使用原有的武器槽、物品登记和城市打造。</summary>
internal sealed class ShieldEquipment : ICanInit
{
    /// <summary>与正式世界存档共用的铁盾物品编号。</summary>
    internal const string IronId = "cultiway_iron_shield";

    /// <summary>注册实际装备，并加入原版已有的物品列表。</summary>
    public void Init()
    {
        const string group = "shield";
        const string path = "cultiway/items/iron_shield";
        var sprite = SpriteTextureLoader.getSprite(path);
        DynamicSprites.preloadItemSprite(sprite);
        ItemLibrary items = AssetManager.items;
        if (AssetManager.item_groups.get(group) == null)
            AssetManager.item_groups.add(new ItemGroupAsset { id = group, color = "#849cad", name = "item_cultiway_shield" });

        EquipmentAsset shield = items.clone(IronId, "hammer_iron");
        shield.group_id = group;
        shield.equipment_subtype = IronId;
        shield.translation_key = "item_cultiway_shield";
        shield.needs_to_be_explored = false;
        shield.colored = false;
        shield.path_gameplay_sprite = path;
        shield.path_icon = path;
        shield.gameplay_sprites = new[] { sprite };
        shield.path_slash_animation = "effects/slashes/slash_base";
        shield.name_templates = new List<string> { "weapon_name_city", "weapon_name_kingdom" };
        shield.equipment_value = 26;
        shield.base_stats["damage"] = 2f;
        shield.base_stats["critical_chance"] = 0f;
        items.equipment_by_subtypes.Add(IronId, new List<EquipmentAsset> { shield });
        items.pot_weapon_assets_all.Add(shield);
        items.pot_weapon_assets_unlocked.Add(shield);
    }
}
