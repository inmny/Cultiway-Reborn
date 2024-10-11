using Cultiway.Abstract;

namespace Cultiway.Core.Libraries;

public class CoreBaseStats : ExtendLibrary<BaseStatAsset, CoreBaseStats>
{
    public static BaseStatAsset IronArmor   { get; private set; }
    public static BaseStatAsset WoodArmor   { get; private set; }
    public static BaseStatAsset WaterArmor  { get; private set; }
    public static BaseStatAsset FireArmor   { get; private set; }
    public static BaseStatAsset EarthArmor  { get; private set; }
    public static BaseStatAsset SoulArmor   { get; private set; }
    public static BaseStatAsset IronMaster  { get; private set; }
    public static BaseStatAsset WoodMaster  { get; private set; }
    public static BaseStatAsset WaterMaster { get; private set; }
    public static BaseStatAsset FireMaster  { get; private set; }
    public static BaseStatAsset EarthMaster { get; private set; }
    public static BaseStatAsset SoulMaster  { get; private set; }
    public static BaseStatAsset HealthRegen { get; private set; }
    public static BaseStatAsset WakanRegen  { get; private set; }
    public static BaseStatAsset MaxSoul     { get; private set; }

    protected override void OnInit()
    {
        IronArmor = AddWithMod(new BaseStatAsset()
        {
            id = nameof(IronArmor)
        });
        WoodArmor = AddWithMod(new BaseStatAsset()
        {
            id = nameof(WoodArmor)
        });
        WaterArmor = AddWithMod(new BaseStatAsset()
        {
            id = nameof(WaterArmor)
        });
        FireArmor = AddWithMod(new BaseStatAsset()
        {
            id = nameof(FireArmor)
        });
        EarthArmor = AddWithMod(new BaseStatAsset()
        {
            id = nameof(EarthArmor)
        });
        SoulArmor = AddWithMod(new BaseStatAsset()
        {
            id = nameof(SoulArmor)
        });
        IronMaster = AddWithMod(new BaseStatAsset()
        {
            id = nameof(IronMaster)
        });
        WoodMaster = AddWithMod(new BaseStatAsset()
        {
            id = nameof(WoodMaster)
        });
        WaterMaster = AddWithMod(new BaseStatAsset()
        {
            id = nameof(WaterMaster)
        });
        FireMaster = AddWithMod(new BaseStatAsset()
        {
            id = nameof(FireMaster)
        });
        EarthMaster = AddWithMod(new BaseStatAsset()
        {
            id = nameof(EarthMaster)
        });
        SoulMaster = AddWithMod(new BaseStatAsset()
        {
            id = nameof(SoulMaster)
        });

        HealthRegen = AddWithMod(new BaseStatAsset()
        {
            id = nameof(HealthRegen)
        });
        WakanRegen = AddWithMod(new BaseStatAsset()
        {
            id = nameof(WakanRegen)
        });
        MaxSoul = AddWithMod(new BaseStatAsset()
        {
            id = nameof(MaxSoul)
        });
    }

    private BaseStatAsset AddWithMod(BaseStatAsset asset)
    {
        var mod_asset = new BaseStatAsset()
        {
            id = $"Mod{asset.id}",
            main_stat_to_mod = asset.id,
            mod = true
        };
        Add(mod_asset);
        Add(asset);
        return asset;
    }
}