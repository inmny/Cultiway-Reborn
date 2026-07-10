using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class WorldPage : ModInfoPage
{
    public override string Id => "World";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.World";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.World Description";
    public override string IconPath => "cultiway/icons/iconWakan";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "World Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "大千世界", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "这不是一张静止的地图。山会被风化成海，海湾会淤积成陆；灵气在地下流淌、扩散、被抽取；传送阵与列车连接起远方的城邦。而在这片活着的土地上，上百种种族共生共斗。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform races = CreateCard(root, "Races", width, 6, 6, 5, 5, 4f);
        AddText(races, "Title", "百族争锋", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform r1 = AddTwoColumnRow(races, "Race Row 1", width - 12f);
        AddMiniCard(r1, "Eastern", "cultiway/icons/iconTab", "东方神话", "青龙白虎朱雀玄武·麒麟·九尾仙狐·齐天大圣", 99f);
        AddMiniCard(r1, "Dino", "cultiway/icons/iconTab", "远古巨兽", "暴龙·三角龙·翼龙·无畏巨龙", 99f);
        Transform r2 = AddTwoColumnRow(races, "Race Row 2", width - 12f);
        AddMiniCard(r2, "Chaos", "cultiway/icons/iconTab", "混沌恶魔", "恐虐·色孽·奸奇·纳垢四神眷族", 99f);
        AddMiniCard(r2, "Empire", "cultiway/icons/iconTab", "帝国秩序", "泰拉帝皇·阿斯塔特·帝皇禁卫", 99f);

        Transform geo = CreateCard(root, "Geo Evolve", width, 6, 6, 5, 5, 2f);
        AddText(geo, "Title", "地理演化", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddBullet(geo, "世界自动划分为地区、地貌、陆块、半岛、海峡、群岛六层。");
        AddBullet(geo, "地形随时间侵蚀与反侵蚀，唯有城墙岿然不动。");

        Transform wakan = CreateCard(root, "Wakan Map", width, 6, 6, 5, 5, 3f);
        AddText(wakan, "Title", "灵气地图", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(wakan, "Body",
            "开启灵气地图模式，可见天地灵气的浓淡分布。烟尘浊气之地亦有修行之法——以九转之功，化浊为清。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform traffic = CreateCard(root, "Traffic", width, 6, 6, 5, 5, 4f);
        AddText(traffic, "Title", "交通", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform t1 = AddTwoColumnRow(traffic, "Traffic Row", width - 12f);
        AddMiniCard(t1, "Teleport", "cultiway/icons/iconTab", "传送阵", "阵阵相连，瞬息即至", 99f);
        AddMiniCard(t1, "Train", "cultiway/icons/iconTab", "列车", "城主联外交、修铁路、定时发车", 99f);

        Transform hint = CreateCard(root, "Chaos Hint", width, 6, 6, 5, 5, 3f);
        AddText(hint, "Title", "天外魔音", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hint, "Body",
            "当一位国王杀戮盈野、或魅惑众生、或智冠天下、或身染瘟疫……远方或有古老存在应召而来。详见「混沌降临」分页。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform badge = CreatePlainGroup(hint, "Badge", width - 12f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(badge, "详见混沌降临", 70f, WarnColor);
    }
}
