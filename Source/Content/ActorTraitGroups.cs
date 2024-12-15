using Cultiway.Abstract;
using UnityEngine;

namespace Cultiway.Content;

public class ActorTraitGroups : ExtendLibrary<ActorTraitGroupAsset, ActorTraitGroups>
{
    [GetOnly("mind")] public static ActorTraitGroupAsset Mind { get; private set; }

    public static ActorTraitGroupAsset System { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        System.name = System.id;
        System.color = Color.white;
    }
}