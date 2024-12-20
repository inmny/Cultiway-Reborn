using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class BaseStats : ExtendLibrary<BaseStatAsset, BaseStats>
    {
        [GetOnly("armor")] public static BaseStatAsset Armor { get; private set; }

        [AssetId(nameof(IronArmor))] public static BaseStatAsset IronArmor { get; private set; }

        [AssetId(nameof(WoodArmor))] public static BaseStatAsset WoodArmor { get; private set; }

        [AssetId(nameof(WaterArmor))] public static BaseStatAsset WaterArmor { get; private set; }

        [AssetId(nameof(FireArmor))] public static BaseStatAsset FireArmor { get; private set; }

        [AssetId(nameof(EarthArmor))] public static BaseStatAsset EarthArmor { get; private set; }

        [AssetId(nameof(NegArmor))] public static BaseStatAsset NegArmor { get; private set; }

        [AssetId(nameof(PosArmor))] public static BaseStatAsset PosArmor { get; private set; }

        [AssetId(nameof(EntropyArmor))] public static BaseStatAsset EntropyArmor { get; private set; }

        [AssetId(nameof(IronMaster))] public static BaseStatAsset IronMaster { get; private set; }

        [AssetId(nameof(WoodMaster))] public static BaseStatAsset WoodMaster { get; private set; }

        [AssetId(nameof(WaterMaster))] public static BaseStatAsset WaterMaster { get; private set; }

        [AssetId(nameof(FireMaster))] public static BaseStatAsset FireMaster { get; private set; }

        [AssetId(nameof(EarthMaster))] public static BaseStatAsset EarthMaster { get; private set; }

        [AssetId(nameof(NegMaster))] public static BaseStatAsset NegMaster { get; private set; }

        [AssetId(nameof(PosMaster))] public static BaseStatAsset PosMaster { get; private set; }

        [AssetId(nameof(EntropyMaster))] public static BaseStatAsset EntropyMaster { get; private set; }

        [AssetId(nameof(HealthRegen))] public static BaseStatAsset HealthRegen { get; private set; }

        [AssetId(nameof(WakanRegen))] public static BaseStatAsset WakanRegen { get; private set; }

        [AssetId(nameof(MaxSoul))] public static BaseStatAsset MaxSoul { get; private set; }

        public static ReadOnlyCollection<string> ArmorStats  { get; private set; }
        public static ReadOnlyCollection<string> MasterStats { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets("Cultiway.BaseStats");
            Armor.normalize = false;

            ArmorStats = new ReadOnlyCollection<string>(new List<string>
            {
                IronArmor.id,
                WoodArmor.id,
                WaterArmor.id,
                FireArmor.id,
                EarthArmor.id,
                NegArmor.id,
                PosArmor.id,
                EntropyArmor.id
            });
            MasterStats = new ReadOnlyCollection<string>(new List<string>
            {
                IronMaster.id,
                WoodMaster.id,
                WaterMaster.id,
                FireMaster.id,
                EarthMaster.id,
                NegMaster.id,
                PosMaster.id,
                EntropyMaster.id
            });
        }

        protected override BaseStatAsset Add(BaseStatAsset asset)
        {
            if (asset.mod) return base.Add(asset);
            Add(new BaseStatAsset
            {
                id = $"Mod{asset.id}",
                main_stat_to_mod = asset.id,
                mod = true
            });
            return base.Add(asset);
        }
    }
}