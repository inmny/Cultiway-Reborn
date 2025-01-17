using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.General.Game.extensions;

namespace Cultiway.Content;

[Dependency(typeof(ActorJobs))]
public partial class Actors : ExtendLibrary<ActorAsset, Actors>
{
    [CloneSource("_mob")] public static ActorAsset Plant { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Actor");
        SetupPlant();
        SetupConstraintSpirit();
    }

    private void SetupPlant()
    {
        Plant.canTurnIntoMush = false;
        Plant.canTurnIntoZombie = false;
        Plant.canTurnIntoIceOne = false;
        Plant.canTurnIntoTumorMonster = false;
        Plant.can_turn_into_demon_in_age_of_chaos = false;
        Plant.run_to_water_when_on_fire = false;
        Plant.inspect_children = false;
        Plant.needFood = false;
        Plant.animal = false;
        Plant.procreate = false;
        Plant.source_meat = false;
        Plant.base_stats[S.max_age] = 1000;
        Plant.base_stats[S.speed] = -999999;
        Plant.job = ActorJobs.PlantXianCultivator.id;
        Plant.actorSize = ActorSize.S0_Bug;
        Plant.shadowTexture = "unitShadow_2";
        Plant.maxRandomAmount = 1000;
        Plant.prefab = "p_unit";
        Plant.nameLocale = "Cultiway.Actor.Plant";
        Plant.texture_path = "t_grasshopper";
        Plant.animation_idle = "walk_0,walk_1,walk_2";
        Plant.animation_walk = "walk_0,walk_1,walk_2";
        Plant.animation_swim = "walk_0,walk_1,walk_2";

        Plant.race = SK.nature;
        Plant.kingdom = SK.nature;

        Plant.GetExtend<ActorAssetExtend>().must_have_element_root = true;

        AssetManager.biome_library.ForEach<BiomeAsset, BiomeLibrary>(biome => biome.addUnit(Plant.id));

    }

    protected override void PostInit(ActorAsset asset)
    {
        if (asset.shadow) AssetManager.actor_library.loadShadow(asset);
    }
}