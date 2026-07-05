using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class OverviewPage : ModInfoPage
{
    public override string Id => "Overview";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Overview";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Overview Description";
    public override string IconPath => "cultiway/icons/iconTab";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Overview Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "修真之路 / Cultiway", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "以下完全由AI生成的介绍，后续再人工撰写。把 WorldBox 的文明演化扩展成修真生态：修士成长、宗门传承、世界灵气和特殊资源共同改变长期世界。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        AddProgress(hero, "进度条", 0.5f, width - 12f, WarnColor);

        Transform badges = CreatePlainGroup(hero, "Badges", width - 14f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(badges, "预览版", 38f, WarnColor);
        AddBadge(badges, "长期世界", 48f, GoodColor);
        AddBadge(badges, "设定驱动", 48f, CoolColor);

        Transform focus = CreateCard(root, "Core Loop", width, 6, 6, 5, 5, 4f);
        AddText(focus, "Title", "核心循环", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform row1 = AddTwoColumnRow(focus, "Loop Row 1", width - 12f);
        AddMiniCard(row1, "Cultivation", "cultiway/icons/iconCultivation", "修士成长", "灵根、境界、功法、突破", 99f);
        AddMiniCard(row1, "World", "cultiway/icons/iconWakan", "世界灵气", "扩散、吸收、区域差异", 99f);

        Transform row2 = AddTwoColumnRow(focus, "Loop Row 2", width - 12f);
        AddMiniCard(row2, "Sect", "cultiway/icons/iconSect", "宗门组织", "职位、藏经阁、门派任务", 99f);
        AddMiniCard(row2, "Items", "cultiway/icons/iconWriting", "特殊资源", "书籍、丹方、法术、器物", 99f);

        Transform current = CreateCard(root, "Current Goals", width, 7, 7, 5, 5, 2f);
        AddText(current, "Title", "当前定位", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddBullet(current, "优先保证核心玩法链路可跑通，而不是堆一次性内容。");
        AddBullet(current, "世界侧系统会尽量做成可开关，方便排查和平衡。");
        AddBullet(current, "高境界单位应承担修真事务，不只是更强战斗单位。");
        AddWideImage(current, "Wide Image", "cultiway/icons/iconTab", width);
    }
}
