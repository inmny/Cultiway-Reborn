using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class SectPage : ModInfoPage
{
    public override string Id => "Sect";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Sect";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Sect Description";
    public override string IconPath => "cultiway/icons/iconSect";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Sect Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "宗门传承", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "元婴期修士若已有传人、已悟功法，便可开宗立派。宗门会自行选址、招募、讲法、传承衣钵——一个能延续百年的修真势力，就此诞生。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform strategy = CreateCard(root, "Residence Strategy", width, 6, 6, 5, 5, 4f);
        AddText(strategy, "Title", "四种驻地策略", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform s1 = AddTwoColumnRow(strategy, "Strategy Row 1", width - 12f);
        AddMiniCard(s1, "Secluded", "cultiway/icons/iconSect", "隐世山门", "择灵秀山地而居，远离凡尘", 99f);
        AddMiniCard(s1, "Seeking", "cultiway/icons/iconWakan", "逐灵择地", "唯灵气是图，不问远近", 99f);
        Transform s2 = AddTwoColumnRow(strategy, "Strategy Row 2", width - 12f);
        AddMiniCard(s2, "CityBranch", "cultiway/icons/iconSect", "城坊别院", "立于城郭之侧，广收俗世门人", 99f);
        AddMiniCard(s2, "Territorial", "cultiway/icons/iconSect", "开疆立派", "据广阔之地，远避他宗", 99f);

        Transform roles = CreateCard(root, "Sect Roles", width, 6, 6, 5, 5, 2f);
        AddText(roles, "Title", "宗门运作", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddBullet(roles, "门阶：外门弟子 → 内门弟子 → 亲传弟子");
        AddBullet(roles, "职司：执事 → 长老 → 掌门");
        AddBullet(roles, "头衔：衣钵传人（掌门继任首选）");
        AddBullet(roles, "门阶、职司、头衔三槽独立，可自由组合——你可以是亲传弟子兼执事。");

        Transform scripture = CreateCard(root, "Scripture Pavilion", width, 6, 6, 5, 5, 3f);
        AddText(scripture, "Title", "藏经阁", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(scripture, "Body",
            "宗门典籍分功法、术法、丹方三类，按基础、核心、高阶三层存放。门人凭贡献研读，讲法开坛可让同门顿悟。写书入库，是赚取贡献最快的途径。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform master = CreateCard(root, "Master Apprentice", width, 6, 6, 5, 5, 3f);
        AddText(master, "Title", "师徒", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddMiniCard(master, "MA Types", "cultiway/icons/iconTab", "师徒四阶",
            "记名 → 入室 → 亲传 → 衣钵，亲密度随传功而升。", width - 12f);
    }
}
