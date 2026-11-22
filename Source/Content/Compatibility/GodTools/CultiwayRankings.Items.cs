using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api;
using UnityEngine;

namespace Cultiway.Content.Compatibility.GodTools;

internal static partial class CultiwayRankings
{
    private const string Materials = "cultiway:materials";
    private const string Talismans = "cultiway:talismans";
    private const string MaterialIcon = "cultiway/icons/item_shapes/stone/灵石";
    private const string TalismanIcon = "cultiway/icons/item_shapes/talisman/jinfl";

    private sealed class InventoryRow
    {
        internal string Key;
        internal string Name;
        internal Sprite Icon;
        internal ItemLevel? Grade;
        internal string Holder;
        internal string Shape;
        internal string Creator;
        internal string Spell;
        internal float Strength;
        internal float CastingLevel;
    }

    private static void RegisterMaterials(GodToolsRankingApi api)
    {
        bool IsMaterial(Entity item) => item.Tags.Has<TagIngredient>() && !item.HasComponent<Artifact>() &&
            !item.HasComponent<Talisman>();
        RegisterInventory(api, Materials, MaterialIcon, () => ModClass.I.W.Query<SpecialItem>().Entities,
            IsMaterial);
        api.Field<InventoryRow>(Materials, "shape", Locale + "material_shape", "Category",
            row => api.Choice(row.Shape, row.Shape), icon: MaterialIcon);
        api.Board(Materials, Locale + "material_board", Group, 500, MaterialIcon, "level", "shape", "holder");
    }

    private static void RegisterTalismans(GodToolsRankingApi api)
    {
        RegisterInventory(api, Talismans, TalismanIcon, () => ModClass.I.W.Query<SpecialItem, Talisman>().Entities,
            item => item.HasComponent<Talisman>(), (item, row) =>
            {
                var talisman = item.GetComponent<Talisman>();
                var spell = talisman.SkillContainer;
                if (!spell.IsNull && spell.TryGetComponent(out SkillContainer container))
                    row.Spell = EntityName(spell, container.SkillEntityAssetID);
                row.Strength = talisman.Strength;
                row.CastingLevel = talisman.PowerLevel;
            });
        api.Field<InventoryRow>(Talismans, "spell", Locale + "sealed_spell", "Text", row => api.Text(row.Spell));
        api.Field<InventoryRow>(Talismans, "strength", Locale + "talisman_strength", "Number", row => api.Number(row.Strength));
        api.Field<InventoryRow>(Talismans, "power", Locale + "talisman_power", "Number", row => api.Number(row.CastingLevel));
        api.Board(Talismans, Locale + "talisman_board", Group, 600, TalismanIcon, "level", "spell", "strength");
    }

    private static void RegisterInventory(GodToolsRankingApi api, string id, string icon,
        Func<IEnumerable<Entity>> query, Func<Entity, bool> matches, Action<Entity, InventoryRow> populate = null)
    {
        api.Source(id, Read, row => row.Key, row => row.Name, row => row.Icon, key =>
        {
            if (Find(key, out var item))
                Tooltip.show(Anchor, WorldboxGame.Tooltips.SpecialItem.id, new TooltipData { tip_name = Key(item.Id) });
        }, key =>
        {
            lock (EntityStoreLock.GlobalLock)
                return Find(key, out var item) ? GetHolder(item).Tile : null;
        });
        api.Field<InventoryRow>(id, "name", Locale + "name", "Text", row => api.Text(row.Name));
        api.Field<InventoryRow>(id, "level", Locale + "grade", "Ordinal",
            row => row.Grade is ItemLevel grade ? Grade(api, grade) : null, GradeOptions, icon);
        api.Field<InventoryRow>(id, "holder", Locale + "holder", "Text", row => api.Text(row.Holder));
        api.Field<InventoryRow>(id, "creator", Locale + "creator", "Text", row => api.Text(row.Creator));

        bool Find(string key, out Entity item)
        {
            item = default;
            return ParseKey(key, out long pid) && ModClass.I.W.TryGetEntityByPid(pid, out item) &&
                IsRankableArtifact(item) && item.HasComponent<SpecialItem>() && matches(item);
        }

        IEnumerable<InventoryRow> Read()
        {
            var rows = new List<InventoryRow>();
            lock (EntityStoreLock.GlobalLock)
            {
                foreach (var item in query())
                {
                    if (!IsRankableArtifact(item) || !matches(item) || !item.TryGetComponent(out ItemShape shape)) continue;
                    var row = new InventoryRow
                    {
                        Key = Key(item.Pid), Name = EntityName(item, shape.shape_id), Shape = shape.shape_id,
                        Icon = shape.Type == null ? SpriteTextureLoader.getSprite(icon) : item.GetComponent<SpecialItem>().GetSprite(),
                        Grade = item.TryGetComponent(out ItemLevel grade) ? grade : null,
                        Holder = GetHolder(item).Name,
                        Creator = item.TryGetComponent(out ItemCreation creation) ? creation.creator : null
                    };
                    populate?.Invoke(item, row);
                    rows.Add(row);
                }
            }
            return rows;
        }
    }
}
