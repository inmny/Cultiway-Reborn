using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class StatusEffects : ExtendLibrary<StatusEffect, StatusEffects>
    {
        [GetOnly("burning")] public static StatusEffect Burning { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets("Cultiway.StatusEffects");
        }
    }
}