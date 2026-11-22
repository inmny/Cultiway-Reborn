using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.UI.Prefab;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;
using ContentLibraries = Cultiway.Content.Libraries.Manager;

namespace Cultiway.Content.Compatibility.GodTools;

/// <summary>用 Cultiway 当前世界的数据扩展天榜，并复用各对象的原有详情入口。</summary>
internal static partial class CultiwayRankings
{
    private const string Group = "cultiway:rankings";
    private const string Actors = "godtools:actors";
    private const string Books = "cultiway:cultibooks";
    private const string Spells = "cultiway:spells";
    private const string Regions = "cultiway:regions";
    private const string Artifacts = "cultiway:artifacts";
    private const string Locale = "inmny.godtools.ranking.cultiway.";
    private const string BookIcon = "books/custom_book_covers/cultibook/01";
    private const string SpellIcon = "cultiway/icons/iconMagic";
    private const string RegionIcon = "cultiway/icons/iconGeoRegion";
    private const string ArtifactIcon = "cultiway/icons/world_tools/iconBaibaoPavilion";

    private sealed class BookRow
    {
        internal CultibookAsset Book;
        internal int Learners;
        internal long? ReaderId;
        internal float Mastery;
    }

    private readonly struct RegionRow(GeoRegion region, int tiles, int population)
    {
        internal readonly GeoRegion Region = region;
        internal readonly int Tiles = tiles;
        internal readonly int Population = population;
    }

    private readonly struct ArtifactRow(string key, string name, Sprite icon, ItemLevel? grade, int abilities,
        string shape, string holder)
    {
        internal readonly string Key = key;
        internal readonly string Name = name;
        internal readonly Sprite Icon = icon;
        internal readonly ItemLevel? Grade = grade;
        internal readonly int Abilities = abilities;
        internal readonly string Shape = shape;
        internal readonly string Holder = holder;
    }

    private static readonly GodToolsRankingApi.Option[] GradeOptions = Enumerable.Range(0, 36).Select(value =>
    {
        var grade = ItemLevel.FromValue(value);
        return new GodToolsRankingApi.Option(Key(value), grade.GetName, value, Key(grade.Stage),
            $"Cultiway.Stage.{grade.Stage}", -grade.Stage);
    }).ToArray();

    private static object Grade(GodToolsRankingApi api, ItemLevel grade) =>
        api.Choice(Key((int)grade), grade.GetName(), (int)grade);

    private static Dictionary<string, BookRow> bookRows = new();
    private static GameObject Anchor => ScrollWindow.get("WindowRankings").gameObject;

    /// <summary>在天榜的内置来源就绪后登记 Cultiway 分组、榜单和人物指标。</summary>
    internal static void Register() => GodToolsRankingApi.WhenReady(api =>
    {
        api.Group(Group, "Cultiway", 600);
        RegisterActorFields(api);
        RegisterBooks(api);
        RegisterSpells(api);
        RegisterRegions(api);
        RegisterArtifacts(api);
        RegisterMaterials(api);
        RegisterTalismans(api);
    });

    private static void RegisterActorFields(GodToolsRankingApi api)
    {
        RegisterRealm<Xian>(api, "realm", "mortal");
        RegisterRealm<Magic>(api, "magic", "no_magic");
        RegisterRealm<Knight>(api, "knight", "no_knight");
        api.Field<Actor>(Actors, "cultiway:talent", Locale + "talent", "Number", actor =>
        {
            var extend = actor.GetExtend();
            return extend.HasElementRoot() ? api.Number(extend.GetElementRoot().GetStrength() * 100d) : null;
        });
        api.Field<Actor>(Actors, "cultiway:skill_count", Locale + "skill_count", "Number",
            actor => api.Number(actor.GetExtend().all_skills.Count));
        api.Field<Actor>(Actors, "cultiway:book_grade", Locale + "book_grade", "Ordinal", actor =>
        {
            var book = actor.GetExtend().GetMainCultibook();
            return book == null ? null : Grade(api, book.Level);
        }, GradeOptions, BookIcon);
        api.Field<Actor>(Actors, "cultiway:apprentices", Locale + "apprentices", "Number", actor =>
            api.Number(actor.GetExtend().TryGetComponent(out MasterApprenticeState state) ? state.ApprenticeCount : 0));
    }

    private static void RegisterRealm<T>(GodToolsRankingApi api, string id, string noRealm) where T : struct, ICultisysComponent
    {
        api.Field<Actor>(Actors, "cultiway:" + id, Locale + id, "Ordinal", actor =>
        {
            var extend = actor.GetExtend();
            if (!extend.HasCultisys<T>()) return api.Choice(noRealm, LM.Get(Locale + noRealm), -1);
            var realm = extend.GetCultisys<T>();
            return api.Choice(Key(realm.CurrLevel), realm.Asset.GetLevelName(realm.CurrLevel), extend.GetCultisysLevelForSort<T>());
        });
    }

    private static void RegisterBooks(GodToolsRankingApi api)
    {
        api.Source(Books, ReadBooks, row => row.Book.id, row => row.Book.Name,
            _ => SpriteTextureLoader.getSprite(BookIcon), key => InspectBook(api, key));
        api.Field<BookRow>(Books, "name", Locale + "name", "Text", row => api.Text(row.Book.Name));
        api.Field<BookRow>(Books, "level", Locale + "grade", "Ordinal",
            row => Grade(api, row.Book.Level), GradeOptions, BookIcon);
        api.Field<BookRow>(Books, "learners", Locale + "learners", "Number", row => api.Number(row.Learners));
        api.Field<BookRow>(Books, "spells", Locale + "spells", "Number",
            row => api.Number(row.Book.SkillPool.Count + row.Book.ConstructionPool.Count));
        api.Board(Books, Locale + "books", Group, 100, BookIcon, "learners", "level", "spells");
    }

    private static IEnumerable<BookRow> ReadBooks()
    {
        var books = ContentLibraries.CultibookLibrary.list.Where(book => book != null)
            .ToDictionary(book => book.id, book => new BookRow { Book = book });
        foreach (var actor in World.world.units.getSimpleList())
        {
            if (actor?.data == null || !actor.isAlive() || actor.asset?.can_be_inspected != true) continue;
            foreach (var (book, mastery) in actor.GetExtend().GetAllMaster<CultibookAsset>())
            {
                if (book == null || !books.TryGetValue(book.id, out var row) || mastery <= 0) continue;
                row.Learners++;
                if (row.ReaderId.HasValue && row.Mastery >= mastery) continue;
                row.ReaderId = actor.data.id;
                row.Mastery = mastery;
            }
        }
        bookRows = books;
        return books.Values;
    }

    private static void InspectBook(GodToolsRankingApi api, string key)
    {
        var book = ContentLibraries.CultibookLibrary.get(key);
        if (book == null) return;
        if (bookRows.GetValueOrDefault(key)?.ReaderId is long id &&
            World.world.units.get(id) is { } actor && actor.data?.id == id && actor.isAlive())
        {
            var reader = actor.GetExtend();
            float mastery = reader.GetMaster(book);
            if (mastery > 0)
            {
                CultibookTooltip.Show(Anchor, book, reader, mastery);
                return;
            }
        }
        api.ShowDescription(book.Name, book.Description);
    }

    private static void RegisterSpells(GodToolsRankingApi api)
    {
        api.Source(Spells, ReadSpells, row => row.Signature, SpellName, row =>
        {
            var skill = row.Container.GetComponent<SkillContainer>();
            return skill.Asset.ResolveIcon(skill.AnimationIndex) ?? SpriteTextureLoader.getSprite(SpellIcon);
        }, key =>
        {
            if (MagicWebManager.Instance?.TryFindBySignature(key, out var spell) == true)
                SkillTooltip.Show(Anchor, spell);
        });
        api.Field<MagicWebEntryView>(Spells, "name", Locale + "name", "Text", row => api.Text(SpellName(row)));
        api.Field<MagicWebEntryView>(Spells, "ring", Locale + "ring", "Integer",
            row => api.Number(row.Profile.Ring), icon: SpellIcon, description: Locale + "ring_description");
        api.Field<MagicWebEntryView>(Spells, "element", Locale + "element", "Choice",
            row => row.Profile.PrimaryElement is { } element ? api.Choice(element.id, element.GetName()) : null);
        api.Field<MagicWebEntryView>(Spells, "publisher", Locale + "publisher", "Text", row => api.Text(row.PublisherName));
        api.Field<MagicWebEntryView>(Spells, "semantics", Locale + "semantics", "Tags",
            row => api.Tags(row.Semantics.ToDictionary(semantic => semantic.id, semantic => semantic.GetName())));
        api.Board(Spells, Locale + "spell_board", Group, 200, SpellIcon, "ring", "element", "publisher");
    }

    private static IEnumerable<MagicWebEntryView> ReadSpells()
    {
        var web = MagicWebManager.Instance;
        return web?.Query(new MagicWebQuery { MaxResults = web.Count }) ?? Array.Empty<MagicWebEntryView>();
    }

    private static string SpellName(MagicWebEntryView row) =>
        EntityName(row.Container, row.Container.GetComponent<SkillContainer>().SkillEntityAssetID);

    private static void RegisterRegions(GodToolsRankingApi api)
    {
        api.Source(Regions, ReadRegions, row => Key(row.Region.getID()), row => row.Region.name,
            row => row.Region.GetCategory()?.GetSpriteIcon(), key =>
            {
                if (GetRegion(key) is not { } region) return;
                var asset = WorldboxGame.MetaTypes.GeoRegion;
                asset.set_selected(region);
                ScrollWindow.showWindow(asset.window_name);
            }, key => GetRegion(key) is { } region ? WorldboxGame.I.GeoRegions.EnumerateRegionTiles(region).FirstOrDefault() : null,
            key => GetRegion(key));
        api.Field<RegionRow>(Regions, "name", Locale + "name", "Text", row => api.Text(row.Region.name));
        api.Field<RegionRow>(Regions, "population", Locale + "population", "Number", row => api.Number(row.Population));
        api.Field<RegionRow>(Regions, "tiles", Locale + "tiles", "Number", row => api.Number(row.Tiles));
        api.Field<RegionRow>(Regions, "category", Locale + "region_category", "Choice",
            row => row.Region.GetCategory() is { } category ? api.Choice(category.id, category.GetDisplayName()) : null);
        api.Board(Regions, Locale + "region_board", Group, 300, RegionIcon, "population", "tiles", "category");
    }

    private static IEnumerable<RegionRow> ReadRegions()
    {
        var manager = WorldboxGame.I?.GeoRegions;
        if (manager?.CanResolveRegionTiles() != true) return Array.Empty<RegionRow>();
        var rows = new List<RegionRow>(manager.Count);
        using var membership = manager.AcquireMembershipReadLease();
        foreach (var region in manager)
        {
            if (region?.data == null || !region.isAlive()) continue;
            int tiles = manager.GetTileCount(region);
            if (tiles > 0) rows.Add(new RegionRow(region, tiles, region.countUnits()));
        }
        return rows;
    }

    private static GeoRegion GetRegion(string key) => ParseKey(key, out long id) &&
        WorldboxGame.I?.GeoRegions?.get(id) is { } region && region.isAlive() ? region : null;

    private static void RegisterArtifacts(GodToolsRankingApi api)
    {
        api.Source(Artifacts, ReadArtifacts, row => row.Key, row => row.Name, row => row.Icon, key =>
        {
            if (TryGetArtifact(key, out var artifact))
                Tooltip.show(Anchor, WorldboxGame.Tooltips.SpecialItem.id, new TooltipData { tip_name = Key(artifact.Id) });
        }, key =>
        {
            lock (EntityStoreLock.GlobalLock)
                return TryGetArtifact(key, out var artifact) ? GetHolder(artifact).Tile : null;
        });
        api.Field<ArtifactRow>(Artifacts, "name", Locale + "name", "Text", row => api.Text(row.Name));
        api.Field<ArtifactRow>(Artifacts, "level", Locale + "grade", "Ordinal",
            row => row.Grade is ItemLevel grade ? Grade(api, grade) : null, GradeOptions, ArtifactIcon);
        api.Field<ArtifactRow>(Artifacts, "abilities", Locale + "abilities", "Number", row => api.Number(row.Abilities));
        api.Field<ArtifactRow>(Artifacts, "holder", Locale + "holder", "Text", row => api.Text(row.Holder));
        api.Field<ArtifactRow>(Artifacts, "shape", Locale + "shape", "Choice", row => api.Choice(row.Shape, row.Shape.Localize()));
        api.Board(Artifacts, Locale + "artifact_board", Group, 400, ArtifactIcon, "level", "abilities", "holder");
    }

    private static IEnumerable<ArtifactRow> ReadArtifacts()
    {
        var rows = new List<ArtifactRow>();
        lock (EntityStoreLock.GlobalLock)
        {
            foreach (var artifact in ModClass.I.W.Query<Artifact, SpecialItem>().Entities)
            {
                if (!IsRankableArtifact(artifact) || !artifact.TryGetComponent(out ItemShape shape)) continue;
                rows.Add(new ArtifactRow(Key(artifact.Pid), EntityName(artifact, shape.shape_id),
                    shape.Type == null ? SpriteTextureLoader.getSprite(ArtifactIcon) : artifact.GetComponent<SpecialItem>().GetSprite(),
                    artifact.TryGetComponent(out ItemLevel grade) ? grade : null,
                    artifact.TryGetComponent(out ArtifactAbilitySet abilities) ? abilities.abilities?.Length ?? 0 : 0,
                    shape.shape_id, GetHolder(artifact).Name));
            }
        }
        return rows;
    }

    private static bool TryGetArtifact(string key, out Entity artifact)
    {
        artifact = default;
        return ParseKey(key, out long id) && ModClass.I.W.TryGetEntityByPid(id, out artifact) &&
            IsRankableArtifact(artifact) && artifact.HasComponent<Artifact>() && artifact.HasComponent<SpecialItem>();
    }

    private static bool IsRankableArtifact(Entity item) => !item.IsNull &&
        !item.Tags.HasAny(Tags.Get<TagUncompleted, TagConsumed, TagRecycle>());

    private static (string Name, WorldTile Tile) GetHolder(Entity artifact)
    {
        foreach (var owner in artifact.GetIncomingLinks<InventoryRelation>().Entities)
        {
            if (owner.TryGetComponent(out ActorBinder actor) && actor.Actor is { } unit && unit.isAlive())
                return (unit.getName(), unit.current_tile);
            if (owner.TryGetComponent(out CityBinder city) && city.City is { } settlement && settlement.isAlive())
                return (settlement.name, settlement.getTile());
            if (owner.TryGetComponent(out SectInventoryBinder sect) && sect.Sect is { } group && group.isAlive())
                return (group.name, group.GetResidenceTile());
        }
        return default;
    }

    private static string EntityName(Entity entity, string fallback) =>
        entity.HasName && !string.IsNullOrWhiteSpace(entity.Name.value) ? entity.Name.value : fallback.Localize();

    private static string Key(long id) => id.ToString(CultureInfo.InvariantCulture);
    private static bool ParseKey(string key, out long id) => long.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
}
