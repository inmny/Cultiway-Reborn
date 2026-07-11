using System.Collections.Generic;
using Cultiway.Abstract;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Drops : ExtendLibrary<DropAsset, Drops>
    {
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaMetal { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaWood { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaWater { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaIce { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaFire { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaEarth { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaNeg { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaPos { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaEntropy { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaWind { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaLightning { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaPoison { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaExplosion { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaBurnout { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaGravity { get; private set; }
        [CloneSource(S_Drop.dust_white)] public static DropAsset WanfaCurse { get; private set; }

        public static IEnumerable<DropAsset> WanfaDrops
        {
            get
            {
                yield return WanfaMetal;
                yield return WanfaWood;
                yield return WanfaWater;
                yield return WanfaIce;
                yield return WanfaFire;
                yield return WanfaEarth;
                yield return WanfaNeg;
                yield return WanfaPos;
                yield return WanfaEntropy;
                yield return WanfaWind;
                yield return WanfaLightning;
                yield return WanfaPoison;
                yield return WanfaExplosion;
                yield return WanfaBurnout;
                yield return WanfaGravity;
                yield return WanfaCurse;
            }
        }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            SetupWanfaDrop(WanfaMetal, "drops/drop_metal");
            SetupWanfaDrop(WanfaWood, "drops/drop_life_seed", randomFrame: true);
            SetupWanfaDrop(WanfaWater, "drops/drop_rain", randomFrame: true);
            SetupWanfaDrop(WanfaIce, "drops/drop_snow", randomFrame: true);
            SetupWanfaDrop(WanfaFire, "drops/drop_fire", animated: true, randomFrame: true);
            SetupWanfaDrop(WanfaEarth, "drops/drop_stone");
            SetupWanfaDrop(WanfaNeg, "drops/drop_curse", randomFrame: true);
            SetupWanfaDrop(WanfaPos, "drops/drop_blessing", animated: true);
            SetupWanfaDrop(WanfaEntropy, "drops/drop_madness", randomFrame: true);
            SetupWanfaDrop(WanfaWind, "drops/drop_magic_rain", randomFrame: true);
            SetupWanfaDrop(WanfaLightning, "drops/drop_gamma_rain", randomFrame: true);
            SetupWanfaDrop(WanfaPoison, "drops/drop_acid", randomFrame: true);
            SetupWanfaDrop(WanfaExplosion, "drops/drop_fireworks");
            SetupWanfaDrop(WanfaBurnout, "drops/drop_lava", animated: true);
            SetupWanfaDrop(WanfaGravity, "drops/drop_antimatterbomb");
            SetupWanfaDrop(WanfaCurse, "drops/drop_curse", randomFrame: true);
        }

        private static void SetupWanfaDrop(DropAsset drop, string texturePath, bool animated = false,
            bool randomFrame = false)
        {
            drop.path_texture = texturePath;
            drop.cached_sprites = null;
            drop.animated = animated;
            drop.random_frame = randomFrame;
            drop.falling_speed = 5f;
            drop.falling_speed_random = 0f;
            drop.default_scale = 0.12f;
            drop.sound_drop = string.Empty;
            drop.action_landed = null;
            drop.action_landed_drop = null;
        }
    }
}
