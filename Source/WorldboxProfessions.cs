using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Professions : ExtendLibrary<ProfessionAsset, Professions>
    {
        [GetOnly(S_Profession.warrior)] public static ProfessionAsset Warrior { get; private set; }

        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
        }
    }
}