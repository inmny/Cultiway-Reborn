using System;
using System.Reflection;
using System.Text;
using Cultiway.Abstract;

namespace Cultiway.Content;

/// <summary>注册修仙成就分组与原版成就资产。</summary>
[Dependency(typeof(Cultisyses))]
public sealed class CultivationAchievements : ExtendLibrary<Achievement, CultivationAchievements>
{
    public const string GroupId = "cultiway_cultivation";

    public static Achievement RootAwakened { get; private set; }
    public static Achievement FoundationEstablished { get; private set; }
    public static Achievement GoldenCoreFormed { get; private set; }
    public static Achievement NinefoldCore { get; private set; }
    public static Achievement NascentSoulFormed { get; private set; }
    public static Achievement HeavenGradeCore { get; private set; }

    [HiddenAchievement]
    public static Achievement FlawlessLineage { get; private set; }

    [HiddenAchievement]
    public static Achievement ChaosNascentSoul { get; private set; }

    [HiddenAchievement]
    public static Achievement RealmDefier { get; private set; }

    public static Achievement FirstElixir { get; private set; }
    public static Achievement EarthGradeElixir { get; private set; }
    public static Achievement FirstArtifact { get; private set; }
    public static Achievement EarthGradeArtifact { get; private set; }

    [HiddenAchievement]
    public static Achievement ArtifactSpiritAwakened { get; private set; }

    public static Achievement SectFounded { get; private set; }
    public static Achievement FiveDisciples { get; private set; }
    public static Achievement ApprenticeGraduated { get; private set; }
    public static Achievement GreatSect { get; private set; }

    public static AchievementGroupAsset Group { get; private set; }
    public static bool Ready { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Achievement";

    protected override void ActionAfterCreation(PropertyInfo property, Achievement achievement)
    {
        achievement.locale_key = achievement.id;
        achievement.icon = $"cultiway/icons/achievements/{ToSnakeCase(property.Name)}";
        achievement.group = GroupId;
        achievement.hidden = property.GetCustomAttribute<HiddenAchievementAttribute>() != null;
        achievement.unlocks_something = false;
        achievement.action = _ => false;
    }

    protected override Achievement Add(Achievement achievement)
    {
        if (cached_library.has(achievement.id))
            throw new InvalidOperationException($"成就 ID 已存在：{achievement.id}");
        return base.Add(achievement);
    }

    protected override void OnInit()
    {
        Group = AssetManager.achievement_groups.add(new AchievementGroupAsset
        {
            id = GroupId,
            color = "#78D7B2",
            show_counter = true
        });
    }

    protected override void PostInit(Achievement achievement)
    {
        Group.achievements_list.Add(achievement);
    }

    protected override void GlobalPostInit()
    {
        CultivationAchievementService.Initialize();
        Ready = true;
    }

    private static string ToSnakeCase(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsUpper(character) && i > 0) result.Append('_');
            result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class HiddenAchievementAttribute : Attribute
    {
    }
}
