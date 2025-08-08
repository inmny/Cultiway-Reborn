using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class StatusEffects : ExtendLibrary<StatusAsset, StatusEffects>
    {
        [GetOnly(S_Status.burning)] public static StatusAsset Burning { get; private set; }
        [GetOnly(S_Status.caffeinated)]public static StatusAsset Caffeinated { get; private set; }
        [GetOnly(S_Status.spell_silence)] public static StatusAsset SpellSilence { get; private set; }
        [GetOnly(S_Status.frozen)] public static StatusAsset Frozen { get; private set; }
        [GetOnly(S_Status.stunned)] public static StatusAsset Stunned { get; private set; }
        protected override void OnInit()
        {
            RegisterAssets();

            Burning.GetExtend<StatusAssetExtend>().negative = true;
            SpellSilence.GetExtend<StatusAssetExtend>().negative = true;
            Frozen.GetExtend<StatusAssetExtend>().negative = true;
            Stunned.GetExtend<StatusAssetExtend>().negative = true;
        }
    }
}