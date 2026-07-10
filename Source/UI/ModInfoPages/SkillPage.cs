using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class SkillPage : ModInfoPage
{
    public override string Id => "Skill";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Skill";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Skill Description";
    public override string IconPath => "cultiway/icons/iconWriting";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Skill Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "法术御剑", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "修士将所悟功法化为法术。八种元素、十余种弹道、二十余种附魔词条相乘，世上没有两道完全相同的法术。金丹期可一气连发数十道，元婴以上则是铺天盖地的弹幕。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform compose = CreateCard(root, "Skill Compose", width, 6, 6, 5, 5, 4f);
        AddText(compose, "Title", "法术构成", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddMiniCard(compose, "Elements", "cultiway/icons/iconTab", "八种元素",
            "金·木·水·火·土·阴·阳·混沌", width - 12f);
        AddBullet(compose, "弹道有直线、正弦波、螺旋追踪、回旋镖、抛物线、天降打击等十余种。");
        AddBullet(compose, "附魔词条分四阶稀有度，从灼烧、冰冻到终焉死亡、永世诅咒。");

        Transform dual = CreateCard(root, "Element Dual", width, 6, 6, 5, 5, 3f);
        AddText(dual, "Title", "元素双轨", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(dual, "Body",
            "每种元素既决定你打出的伤害（精通），也减免你受到的同类伤害（抗性）。火系修士既擅火咒，也不畏火攻。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform poss = CreateCard(root, "Possession", width, 6, 6, 5, 5, 3f);
        AddText(poss, "Title", "附身御剑", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(poss, "Body",
            "附身一名修士，游戏瞬间变成 ARPG。长按施法键圈定目标、滚轮缩放范围、一键升空飞行——亲手把元婴期的千发弹幕砸向魔族大军。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform badge = CreatePlainGroup(poss, "Badge", width - 12f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(badge, "玩家操控", 50f, AccentTextColor);
    }
}
