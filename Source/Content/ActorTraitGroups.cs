using Cultiway.Abstract;
using UnityEngine;

namespace Cultiway.Content;

public class ActorTraitGroups : ExtendLibrary<ActorTraitGroupAsset, ActorTraitGroups>
{
    public static ActorTraitGroupAsset System { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        System.name = System.id;
        System.color = Color.white;
    }
}