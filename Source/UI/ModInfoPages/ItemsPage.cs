using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class ItemsPage : ModInfoPage
{
    public override string Id => "Items";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.Items";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.Items Description";
    public override string IconPath => "cultiway/icons/iconWriting";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "Items Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "丹器符药", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "修真之士随身之物，分材、丹、符、器、阵、傀六大类。从龙鳞晶石到九转金丹，皆是修行路上的资粮与杀器。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform books = CreateCard(root, "Books", width, 6, 6, 5, 5, 4f);
        AddText(books, "Title", "三类典籍", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform b1 = AddTwoColumnRow(books, "Book Row 1", width - 12f);
        AddMiniCard(b1, "Cultibook", "cultiway/icons/iconWriting", "功法", "修行根本，定修炼与法术", 99f);
        AddMiniCard(b1, "Elixirbook", "cultiway/icons/iconWriting", "丹方", "炼丹配方，按材料产出丹药", 99f);
        AddMiniCard(books, "Skillbook", "cultiway/icons/iconWriting", "术法",
            "独立成书的法术，可单修。", width - 12f);

        Transform elixir = CreateCard(root, "Elixir", width, 6, 6, 5, 5, 2f);
        AddText(elixir, "Title", "丹药", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddBullet(elixir, "开灵丹：诱导灵根诞生");
        AddBullet(elixir, "补气丹：恢复灵力");
        AddBullet(elixir, "悟道丹：临时大幅提升悟性");
        AddBullet(elixir, "开启 AI 后，同样材料能炼出药效各异、由 AI 命名的无数种丹药。");

        Transform ingredient = CreateCard(root, "Ingredients", width, 6, 6, 5, 5, 3f);
        AddText(ingredient, "Title", "材料器形", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(ingredient, "Body",
            "材料按形状分为二十七种——血材、骨材、晶石、羽翎、灵液、莲、菌……杀什么生灵、得什么材料，皆由其所是而定。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);

        Transform talisman = CreateCard(root, "Talisman", width, 6, 6, 5, 5, 3f);
        AddText(talisman, "Title", "符箓", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(talisman, "Body",
            "符箓是一次性法术的载体，战斗中自动取用。它不耗修士自身灵力，是关键时刻的保命底牌。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
    }
}
