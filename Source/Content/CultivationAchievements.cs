using System;
using System.Collections.Generic;
using Cultiway.Abstract;

namespace Cultiway.Content;

/// <summary>注册修仙成就分组、稳定目录与原版成就资产。</summary>
[Dependency(typeof(Cultisyses))]
public sealed class CultivationAchievements : ICanInit
{
    public const string GroupId = "cultiway_cultivation";

    private static readonly Dictionary<string, Achievement> assets = new(StringComparer.Ordinal);
    private static readonly List<Achievement> orderedAssets = new();

    private static readonly Definition[] definitions =
    [
        new(CultivationAchievementIds.RootAwakened, "root_awakened"),
        new(CultivationAchievementIds.FoundationEstablished, "foundation_established"),
        new(CultivationAchievementIds.GoldenCoreFormed, "golden_core_formed"),
        new(CultivationAchievementIds.NinefoldCore, "ninefold_core"),
        new(CultivationAchievementIds.NascentSoulFormed, "nascent_soul_formed"),
        new(CultivationAchievementIds.HeavenGradeCore, "heaven_grade_core"),
        new(CultivationAchievementIds.FlawlessLineage, "flawless_lineage", true),
        new(CultivationAchievementIds.ChaosNascentSoul, "chaos_nascent_soul", true),
        new(CultivationAchievementIds.RealmDefier, "realm_defier", true),
        new(CultivationAchievementIds.FirstElixir, "first_elixir"),
        new(CultivationAchievementIds.EarthGradeElixir, "earth_grade_elixir"),
        new(CultivationAchievementIds.FirstArtifact, "first_artifact"),
        new(CultivationAchievementIds.EarthGradeArtifact, "earth_grade_artifact"),
        new(CultivationAchievementIds.ArtifactSpiritAwakened, "artifact_spirit_awakened", true),
        new(CultivationAchievementIds.SectFounded, "sect_founded"),
        new(CultivationAchievementIds.FiveDisciples, "five_disciples"),
        new(CultivationAchievementIds.ApprenticeGraduated, "apprentice_graduated"),
        new(CultivationAchievementIds.GreatSect, "great_sect")
    ];

    public static AchievementGroupAsset Group { get; private set; }
    public static IReadOnlyList<Achievement> Ordered => orderedAssets;
    public static bool Ready { get; private set; }

    public void Init()
    {
        ValidateCatalog();

        Group = AssetManager.achievement_groups.add(new AchievementGroupAsset
        {
            id = GroupId,
            color = "#78D7B2",
            show_counter = true
        });

        for (var i = 0; i < definitions.Length; i++)
        {
            Definition definition = definitions[i];
            Achievement achievement = AssetManager.achievements.add(new Achievement
            {
                id = definition.Id,
                locale_key = definition.Id,
                icon = $"cultiway/icons/achievements/{definition.IconName}",
                group = GroupId,
                hidden = definition.Hidden,
                unlocks_something = false,
                action = _ => false
            });
            assets.Add(achievement.id, achievement);
            orderedAssets.Add(achievement);
            Group.achievements_list.Add(achievement);
        }

        CultivationAchievementService.Initialize();
        Ready = true;
    }

    public static Achievement Get(string id)
    {
        return id != null && assets.TryGetValue(id, out Achievement achievement) ? achievement : null;
    }

    private static void ValidateCatalog()
    {
        if (Ready || assets.Count != 0 || orderedAssets.Count != 0)
            throw new InvalidOperationException("修仙成就目录已经初始化。");
        if (GameProgress.instance == null)
            throw new InvalidOperationException("GameProgress 尚未初始化，不能注册修仙成就。");
        if (AssetManager.achievements == null || AssetManager.achievement_groups == null)
            throw new InvalidOperationException("原版成就资产库尚未初始化。");
        if (definitions.Length != 18)
            throw new InvalidOperationException($"修仙成就目录应有 18 项，当前为 {definitions.Length} 项。");
        if (AssetManager.achievement_groups.get(GroupId) != null)
            throw new InvalidOperationException($"成就分组 ID 已存在：{GroupId}");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var hiddenCount = 0;
        for (var i = 0; i < definitions.Length; i++)
        {
            Definition definition = definitions[i];
            if (string.IsNullOrEmpty(definition.Id) || !ids.Add(definition.Id))
                throw new InvalidOperationException($"修仙成就 ID 为空或重复：{definition.Id}");
            if (AssetManager.achievements.get(definition.Id) != null)
                throw new InvalidOperationException($"成就 ID 已存在：{definition.Id}");
            if (string.IsNullOrEmpty(definition.IconName))
                throw new InvalidOperationException($"修仙成就缺少图标名：{definition.Id}");
            if (definition.Hidden) hiddenCount++;
        }

        if (hiddenCount != 4)
            throw new InvalidOperationException($"修仙隐藏成就应有 4 项，当前为 {hiddenCount} 项。");
    }

    private readonly struct Definition
    {
        public Definition(string id, string iconName, bool hidden = false)
        {
            Id = id;
            IconName = iconName;
            Hidden = hidden;
        }

        public string Id { get; }
        public string IconName { get; }
        public bool Hidden { get; }
    }
}

/// <summary>修仙成就稳定 ID；已发布后不得改名或复用。</summary>
internal static class CultivationAchievementIds
{
    public const string RootAwakened = "Cultiway.Achievement.RootAwakened";
    public const string FoundationEstablished = "Cultiway.Achievement.FoundationEstablished";
    public const string GoldenCoreFormed = "Cultiway.Achievement.GoldenCoreFormed";
    public const string NinefoldCore = "Cultiway.Achievement.NinefoldCore";
    public const string NascentSoulFormed = "Cultiway.Achievement.NascentSoulFormed";
    public const string HeavenGradeCore = "Cultiway.Achievement.HeavenGradeCore";
    public const string FlawlessLineage = "Cultiway.Achievement.FlawlessLineage";
    public const string ChaosNascentSoul = "Cultiway.Achievement.ChaosNascentSoul";
    public const string RealmDefier = "Cultiway.Achievement.RealmDefier";
    public const string FirstElixir = "Cultiway.Achievement.FirstElixir";
    public const string EarthGradeElixir = "Cultiway.Achievement.EarthGradeElixir";
    public const string FirstArtifact = "Cultiway.Achievement.FirstArtifact";
    public const string EarthGradeArtifact = "Cultiway.Achievement.EarthGradeArtifact";
    public const string ArtifactSpiritAwakened = "Cultiway.Achievement.ArtifactSpiritAwakened";
    public const string SectFounded = "Cultiway.Achievement.SectFounded";
    public const string FiveDisciples = "Cultiway.Achievement.FiveDisciples";
    public const string ApprenticeGraduated = "Cultiway.Achievement.ApprenticeGraduated";
    public const string GreatSect = "Cultiway.Achievement.GreatSect";
}
