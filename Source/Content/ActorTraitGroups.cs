using Cultiway.Abstract;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public class ActorTraitGroups : ExtendLibrary<ActorTraitGroupAsset, ActorTraitGroups>
{
    [GetOnly("mind")] public static ActorTraitGroupAsset Mind { get; private set; }
    [GetOnly(S_TraitGroup.miscellaneous)]public static ActorTraitGroupAsset Miscellaneous { get; private set; }
    public static ActorTraitGroupAsset System { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        System.name = System.id;
        System.color = Toolbox.colorToHex(Color.white);
        AssetList.MoveTo(System, Miscellaneous);
    }
}