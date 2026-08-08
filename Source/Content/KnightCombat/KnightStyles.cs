using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content.KnightCombat;

/// <summary>注册骑士首批流派资产。</summary>
[Dependency(typeof(Cultisyses))]
public sealed class KnightStyles : ExtendLibrary<KnightStyleAsset, KnightStyles>
{
    public static KnightStyleAsset Guardian { get; private set; }

    public static KnightStyleAsset Lancer { get; private set; }

    public static KnightStyleAsset Duelist { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.KnightStyle";

    protected override void OnInit()
    {
        Configure(
            Guardian,
            "Cultiway.KnightStyle.Guardian",
            "Cultiway.KnightStyle.Guardian.Description",
            0,
            new[] { "axe", "hammer" });
        Configure(
            Lancer,
            "Cultiway.KnightStyle.Lancer",
            "Cultiway.KnightStyle.Lancer.Description",
            1,
            new[] { "spear" });
        Configure(
            Duelist,
            "Cultiway.KnightStyle.Duelist",
            "Cultiway.KnightStyle.Duelist.Description",
            2,
            new[] { "sword" });
    }

    private static void Configure(
        KnightStyleAsset style,
        string nameKey,
        string descriptionKey,
        int sortOrder,
        string[] weaponGroups)
    {
        style.NameKey = nameKey;
        style.DescriptionKey = descriptionKey;
        style.IconPath = "cultiway/icons/iconKnight";
        style.SortOrder = sortOrder;
        style.MinimumKnightLevel = 0;
        style.WeaponGroups = weaponGroups;
    }
}
