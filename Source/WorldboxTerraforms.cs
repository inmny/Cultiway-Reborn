using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Terraforms : ExtendLibrary<TerraformOptions, Terraforms>
    {
        [GetOnly(S_Terraform.earthquake)]     public static TerraformOptions Earthquake     { get; private set; }
        [CloneSource(S_Terraform.earthquake)] public static TerraformOptions EarthquakeBurn { get; private set; }
        public static TerraformOptions HitGround { get; private set; }
        public static TerraformOptions RemoveAll { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets();

            EarthquakeBurn.add_burned = true;

            HitGround.add_burned = true;
            HitGround.apply_force = true;
            HitGround.remove_frozen = true;
            HitGround.damage = 20;
            HitGround.lightning_effect = true;


            RemoveAll.remove_frozen = true;
            RemoveAll.remove_borders = true;
            RemoveAll.remove_burned = true;
            RemoveAll.remove_fire = true;
            RemoveAll.remove_top_tile = true;
            RemoveAll.remove_roads = true;
            RemoveAll.remove_tornado = true;
            RemoveAll.remove_lava = true;
            RemoveAll.remove_water = true;
            RemoveAll.remove_ruins = true;
            RemoveAll.remove_trees_fully = true;
            RemoveAll.destroy_buildings = true;

        }
    }
}