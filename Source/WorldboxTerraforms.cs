using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Terraforms : ExtendLibrary<TerraformOptions, Terraforms>
    {
        [GetOnly("earthquake")]     public static TerraformOptions Earthquake     { get; }
        [CloneSource("earthquake")] public static TerraformOptions EarthquakeBurn { get; }

        protected override void OnInit()
        {
            RegisterAssets();

            EarthquakeBurn.addBurned = true;
        }
    }
}