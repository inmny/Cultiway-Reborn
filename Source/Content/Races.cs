using Cultiway.Abstract;

namespace Cultiway.Content;
[Dependency(typeof(KingdomAssets))]
public class Races : ExtendLibrary<Race, Races>
{
    [GetOnly("human")]
    public static Race Human { get; private set; }
    [CloneSource("human"), AssetId($"Cultiway.{nameof(Ming)}")]
    public static Race Ming { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        SetupMing();
    }

    private void SetupMing()
    {
        Ming.nameLocale = "Cultiway.Actor.Ming";
        Ming.path_icon = "cultiway/icons/races/iconMings";
        Ming.nomad_kingdom_id = KingdomAssets.NoMadsMing.id;
        Ming.skin_citizen_male = ["unit_male_1"];
        Ming.skin_citizen_female = ["unit_female_1"];
        Ming.skin_warrior = ["unit_warrior_1"];
        Ming.build_order_id = Ming.id;
        AssetManager.raceLibrary.t = Ming;
        AssetManager.raceLibrary.setPreferredStatPool("diplomacy#5,warfare#5,stewardship#5,intelligence#5");
        AssetManager.raceLibrary.setPreferredFoodPool(
            "berries#5,bread#5,fish#5,meat#2,sushi#2,jam#1,cider#1,ale#2,burger#1,pie#1,tea#2");
        AssetManager.raceLibrary.addPreferredWeapon("stick", 5);
        AssetManager.raceLibrary.addPreferredWeapon("sword", 5);
        AssetManager.raceLibrary.addPreferredWeapon("axe", 2);
        AssetManager.raceLibrary.addPreferredWeapon("spear", 2);
        AssetManager.raceLibrary.addPreferredWeapon("bow", 5);
        AssetManager.raceLibrary.cloneBuildingKeys(SK.human, Ming.id);
        AssetManager.race_build_orders.clone(Ming.build_order_id, "kingdom_base");
        Ming.building_order_keys[SB.order_bonfire] = "bonfire_ming";
    }
}