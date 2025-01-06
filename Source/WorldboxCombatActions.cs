using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class CombatActions : ExtendLibrary<BaseStatAsset, CombatActions>
    {
        [GetOnly(nameof(CombatActionLibrary.combat_attack_melee))]
        public static CombatActionAsset AttackMelee { get; private set; }
        [GetOnly(nameof(CombatActionLibrary.combat_attack_range))]
        public static CombatActionAsset AttackRange { get; private set; }
        [GetOnly(nameof(CombatActionLibrary.combat_cast_spell))]
        public static CombatActionAsset CastVanillaSpell { get; private set; }
        public static CombatActionAsset CastSkill { get; private set; }
        protected override void OnInit()
        {
            RegisterAssets("Cultiway.CombatActions");
        }
    }
}