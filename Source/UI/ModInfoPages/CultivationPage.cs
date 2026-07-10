using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class CultivationPage : ModInfoPage
{
    public override string Id => "Cultivation";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Cultivation";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Cultivation Description";
    public override string IconPath => "cultiway/icons/iconCultivation";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Cultivation Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "修真炼气", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "修士在天地灵气中积淀修为，依次走过炼气、筑基、金丹、元婴四大关隘。每一次突破都是一场渡劫——成了得道，败了也能歪打正着，强化一门法术。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform er = CreateCard(root, "Element Root", width, 6, 6, 5, 5, 4f);
        AddText(er, "Title", "灵根", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform erRow = AddTwoColumnRow(er, "ER Row", width - 12f);
        AddMiniCard(erRow, "Five", "cultiway/icons/iconTab", "五行灵根", "金·木·水·火·土", 99f);
        AddMiniCard(erRow, "YinYang", "cultiway/icons/iconTab", "阴阳灵根", "阴·阳", 99f);
        AddBullet(er, "灵根决定元素亲和与修炼效率；多数人五行杂糅、灵根平庸，极少数天才是单一强灵根——由天生决定，强求不来。");
        AddBullet(er, "灵根并非人族专属，草木禽兽亦能修仙。");

        Transform methods = CreateCard(root, "Cultivate Methods", width, 6, 6, 5, 5, 4f);
        AddText(methods, "Title", "四种修炼功法", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform m1 = AddTwoColumnRow(methods, "Method Row 1", width - 12f);
        AddMiniCard(m1, "Standard", "cultiway/icons/iconCultivation", "标准闭关", "静坐吸收天地灵气", 99f);
        AddMiniCard(m1, "Water", "cultiway/icons/iconWakan", "水中修炼", "水灵根者事半功倍", 99f);
        Transform m2 = AddTwoColumnRow(methods, "Method Row 2", width - 12f);
        AddMiniCard(m2, "Battle", "cultiway/icons/iconTab", "战斗修炼", "攻伐与挨打皆长修为", 99f);
        AddMiniCard(m2, "Kingdom", "cultiway/icons/iconTab", "国运修炼", "国王城主以国运养身", 99f);

        Transform jindan = CreateCard(root, "Jindan", width, 6, 6, 5, 5, 4f);
        AddText(jindan, "Title", "金丹九转", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(jindan, "Body",
            "筑基三花聚顶、五气朝元之后，方能凝结金丹。金丹分九转，转数越高越是精纯，却也越难突破。寿元将尽之时，修士会不顾一切强行冲关——成则元婴，败则身亡。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        AddMiniCard(jindan, "JindanTypes", "cultiway/icons/iconTab", "金丹品相",
            "普通·金煌·剑煌·青木·寒霜·烈火·润土·凝元·幻影·恶龙", width - 12f);
    }
}
