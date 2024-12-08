using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Terraforms : ExtendLibrary<TerraformOptions, Terraforms>
    {
        [GetOnly("earthquake")]     public static TerraformOptions Earthquake     { get; private set; }
        [CloneSource("earthquake")] public static TerraformOptions EarthquakeBurn { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets();

            EarthquakeBurn.addBurned = true;
        }
    }
}