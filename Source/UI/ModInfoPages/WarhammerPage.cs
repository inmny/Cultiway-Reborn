using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class WarhammerPage : ModInfoPage
{
    private static readonly Color ChaosRed = new(0.90f, 0.25f, 0.25f, 1f);

    public override string Id => "Warhammer";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Warhammer";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Warhammer Description";
    public override string IconPath => "cultiway/icons/iconTab";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Warhammer Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "混沌降临", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "当世间的杀戮、欲望、诡智与瘟疫累积到极点，混沌领域的四位邪神便会循味而来。这是来自异世界的终极灾厄——发起召唤的领袖将献祭己身、化身大魔，其下凡人尽数沦为恶魔。唯有修真之士，凭一身灵根，方能在这场浩劫之中保全本心。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform heroBadge = CreatePlainGroup(hero, "Badges", width - 14f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(heroBadge, "跨界灾厄", 50f, ChaosRed);
        AddBadge(heroBadge, "隐藏内容", 50f, AccentTextColor);

        Transform gods = CreateCard(root, "Four Gods", width, 6, 6, 5, 5, 4f);
        AddText(gods, "Title", "四邪神", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform g1 = AddTwoColumnRow(gods, "God Row 1", width - 12f);
        AddMiniCard(g1, "Khorne", "cultiway/icons/iconTab", "恐虐 · 血神",
            "杀戮满二十，血神应召。嗜血狂魔从血泊中升起，凡人化为放血鬼与血肉猎犬。", 99f);
        AddMiniCard(g1, "Slaanesh", "cultiway/icons/iconTab", "色孽 · 极乐之主",
            "领袖外交登峰，守密者降临，凡人沉沦为欲魔与寻觅者。", 99f);
        Transform g2 = AddTwoColumnRow(gods, "God Row 2", width - 12f);
        AddMiniCard(g2, "Tzeentch", "cultiway/icons/iconTab", "奸奇 · 万变之主",
            "领袖智谋绝伦，万变魔君垂注，凡人异化为粉蓝惧妖。", 99f);
        AddMiniCard(g2, "Nurgle", "cultiway/icons/iconTab", "纳垢 · 慈父",
            "瘟疫使者叩门，大不净者降世，凡人腐朽为纳垢灵与携疫者。", 99f);

        Transform ascend = CreateCard(root, "Demon Ascension", width, 6, 6, 5, 5, 2f);
        AddText(ascend, "Title", "魔神化", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(ascend, "Body",
            "召唤仪式一旦完成，发起者本人将蜕变——这就是魔神化。从此他不再是凡人，而是混沌在人间的化身。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        AddBullet(ascend, "凡人转化波及发起者统治的所有城市；若发起者是国王，则举国沦丧。");
        AddBullet(ascend, "有灵根的修士对恶魔转化免疫——修真文明，是凡俗世界最后的防线。");

        Transform tower = CreateCard(root, "Tower", width, 6, 6, 5, 5, 3f);
        AddText(tower, "Title", "魔塔", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(tower, "Body",
            "少数情况下，召唤会在城中树起一座邪神魔塔：颅献黄铜王座、极乐尖啸御座、万变星璇祭坛、七重溃烂熔炉。魔塔一旦建成，便会源源不断地产出恶魔，直至被彻底摧毁。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform era = CreateCard(root, "Era", width, 6, 6, 5, 5, 3f);
        AddText(era, "Title", "纪元改写", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(era, "Body",
            "四神降临会强制改写世界纪元——混沌纪、月之纪、奇观纪、灰烬纪。这是一个被混沌烙印的时代，天地规则都随之扭曲。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform order = CreateCard(root, "Order", width, 6, 6, 5, 5, 3f);
        AddText(order, "Title", "秩序阵营", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(order, "Body",
            "与混沌相对的，是来自遥远星海的秩序之力：泰拉帝皇的化身、阿斯塔特战团、帝皇禁卫、静默修女、机械教徒……他们同样会现身这场跨界的混战。混沌与秩序的战火，烧到了修真世界。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform orderBadge = CreatePlainGroup(order, "Badge", width - 12f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(orderBadge, "帝国", 38f, CoolColor);
    }
}
