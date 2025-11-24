using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class TileTypes : ExtendLibrary<TileType, TileTypes>
    {
        [GetOnly("pit_deep_ocean")]
        public static TileType PitDeepOcean { get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
        }
    }
}