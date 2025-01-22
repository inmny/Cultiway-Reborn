using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Terraforms : ExtendLibrary<TerraformOptions, Terraforms>
    {
        [GetOnly("earthquake")]     public static TerraformOptions Earthquake     { get; private set; }
        [CloneSource("earthquake")] public static TerraformOptions EarthquakeBurn { get; private set; }
        public static TerraformOptions HitGround { get; private set; }
        public static TerraformOptions RemoveAll { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets("Cultiway.Terraforms");

            EarthquakeBurn.addBurned = true;

            HitGround.addBurned = true;
            HitGround.applyForce = true;
            HitGround.removeFrozen = true;
            HitGround.damage = 20;
            HitGround.lightningEffect = true;


            RemoveAll.removeFrozen = true;
            RemoveAll.removeBorders = true;
            RemoveAll.removeBurned = true;
            RemoveAll.removeFire = true;
            RemoveAll.removeTopTile = true;
            RemoveAll.removeRoads = true;
            RemoveAll.removeTornado = true;
            RemoveAll.removeLava = true;
            RemoveAll.removeWater = true;
            RemoveAll.removeRuins = true;
            RemoveAll.removeTreesFully = true;
            RemoveAll.destroyBuildings = true;

        }
    }
}