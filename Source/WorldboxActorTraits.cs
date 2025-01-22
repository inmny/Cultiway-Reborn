using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class ActorTraits : ExtendLibrary<ActorTrait, ActorTraits>
    {
        /// <summary>
        /// 野心勃勃
        /// </summary>
        [GetOnly("ambitious")] public static ActorTrait Ambitious { get; private set; }
        /// <summary>
        /// 上帝之痕
        /// </summary>
        [GetOnly("scar_of_divinity")]public static ActorTrait ScarOfDivinity { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets("Cultiway.ActorTraits");
        }
    }
}