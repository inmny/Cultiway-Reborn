using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class ArtifactPage : ModInfoPage
{
    public override string Id => "Artifact";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Artifact";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Artifact Description";
    public override string IconPath => "cultiway/icons/artifact_atoms/sword_edge";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Artifact Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "法宝百珍", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "法宝是修士的随身道器。剑、印、袍、镜、鼎、幡、钟、葫、扇、塔、珠十一器形，与材质、饰纹相合，炼出形制各异的本命法宝——或显化于世、随主杀敌，或静守丹田、护主佑身。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform forge = CreateCard(root, "Artifact Forge", width, 6, 6, 5, 5, 4f);
        AddText(forge, "Title", "器形·材质·饰纹", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform f1 = AddTwoColumnRow(forge, "Forge Row 1", width - 12f);
        AddMiniCard(f1, "Shapes", "cultiway/icons/artifact_atoms/sword_edge", "器形",
            "飞剑·法印·法袍·宝镜·法鼎·旗幡·钟·葫芦·扇·塔·珠", 99f);
        AddMiniCard(f1, "Materials", "cultiway/icons/artifact_atoms/jade", "材质",
            "青玉·灵晶·玄铁·天蚕丝·虚空石……", 99f);
        AddBullet(forge, "饰纹再添一分神异：赤火、玄金、流云、雷霆、山岳、水月诸纹各有所长。");
        AddBullet(forge, "形、材、纹相乘，法宝的形制与神通便千变万化，世上无两件完全相同的法宝。");

        Transform powers = CreateCard(root, "Artifact Powers", width, 6, 6, 5, 5, 3f);
        AddText(powers, "Title", "法宝神通", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(powers, "Body",
            "法宝自带神通：主动者可亲自施展，被动者护主佑身，还能显化布阵、落下领域镇压一方。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        AddBullet(powers, "剑阵追击、镇狱封禁、金钟护体、吞天摄灵、明镜破妄……");
        AddBullet(powers, "附身修士时，法宝主动技就列于指边，随取随用。");

        Transform spirit = CreateCard(root, "Artifact Spirit", width, 6, 6, 5, 5, 4f);
        AddText(spirit, "Title", "器灵与御器", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform s1 = AddTwoColumnRow(spirit, "Spirit Row", width - 12f);
        AddMiniCard(s1, "Spirit", "cultiway/icons/artifact_atoms/heavy_seal", "器灵",
            "久用生灵，随杀敌施法成长", 99f);
        AddMiniCard(s1, "Vehicle", "cultiway/icons/artifact_atoms/cloud_robe", "御器",
            "高阶法宝可载人飞天", 99f);
        AddBullet(spirit, "器灵与主人结下羁绊，一朝唤醒，化作化身护法。");
        AddBullet(spirit, "御器飞行，载人数随品阶而增，巡游千里不过转瞬。");

        Transform baibao = CreateCard(root, "Baibao", width, 6, 6, 5, 5, 3f);
        AddText(baibao, "Title", "百宝阁", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(baibao, "Body",
            "见过的好法宝可在百宝阁留底为蓝图，随时重炼再赠。宗门还可供奉法宝于阁中，泽被全门弟子。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform badge = CreatePlainGroup(baibao, "Badge", width - 12f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(badge, "新增玩法", 58f, GoodColor);
    }
}
