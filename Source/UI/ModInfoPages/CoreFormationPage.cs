using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class CoreFormationPage : ModInfoPage
{
    public override string Id => "CoreFormation";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.CoreFormation";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.CoreFormation Description";
    public override string IconPath => "cultiway/icons/iconMagic";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Daoguo Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "本命道果", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "每一次大境界突破，修士都会将毕生所修凝成一件本命道果：炼气凝真气、筑基结仙基、金丹成丹、元婴孕婴、化神养神。灵根、功法、际遇不同，道果便各不相同——它是修士真正的根本。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform phase = CreateCard(root, "Daoguo Phase", width, 6, 6, 5, 5, 4f);
        AddText(phase, "Title", "五果叠印", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddMiniCard(phase, "PhaseLine", "cultiway/icons/iconTab", "五境五果",
            "真气 → 仙基 → 金丹 → 元婴 → 元神", width - 12f);
        AddBullet(phase, "前境道果是后境的根基——金丹承仙基之谱，元婴续金丹之系，层层叠印。");
        AddBullet(phase, "道果分阶段显化：境界越高，越多神通苏醒。");

        Transform aspect = CreateCard(root, "Daoguo Aspect", width, 6, 6, 5, 5, 4f);
        AddText(aspect, "Title", "诸气赋形", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform a1 = AddTwoColumnRow(aspect, "Aspect Row", width - 12f);
        AddMiniCard(a1, "Elements", "cultiway/icons/iconTab", "主相", "金·木·水·火·土·阴·阳·混沌", 99f);
        AddMiniCard(a1, "Structures", "cultiway/icons/iconTab", "结构", "混元·凝元·精元·灵台", 99f);
        AddBullet(aspect, "诸气居于主位者为主相：金主锋锐、火主炽烈、混沌主异变归一……主相决定道果的品性。");
        AddBullet(aspect, "结构定其形：混元制衡、凝元蓄势、精元炼体、灵台养神。");
        AddBullet(aspect, "剑道、炼体、幻道、龙脉诸般烙印，则来自修士的所学与所历。");

        Transform lineage = CreateCard(root, "Daoguo Lineage", width, 6, 6, 5, 5, 3f);
        AddText(lineage, "Title", "谱系传承", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(lineage, "Body",
            "道果有谱系，可传后人。继承者未必照单全收——青出于蓝还是敝帚自珍，看的是各自的造化。一门道果绵延数代，便是一部修行世家的族谱。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform powers = CreateCard(root, "Daoguo Powers", width, 6, 6, 5, 5, 3f);
        AddText(powers, "Title", "神通苏醒", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(powers, "Body",
            "每件道果都孕育独有神通：金斩裂空、水寒缚敌、火烙灼魂、混沌回响连绵不绝……附着于修士的角色信息中，随时可查。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform badge = CreatePlainGroup(powers, "Badge", width - 12f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(badge, "养成核心", 58f, GoodColor);
    }
}
