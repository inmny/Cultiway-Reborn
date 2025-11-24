using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using Cultiway.Utils.Extension;

namespace Cultiway;

public partial class WorldboxGame
{
    public class CombatActions : ExtendLibrary<CombatActionAsset, CombatActions>
    {
        [GetOnly(nameof(CombatActionLibrary.combat_attack_melee))]
        public static CombatActionAsset AttackMelee { get; private set; }
        [GetOnly(nameof(CombatActionLibrary.combat_attack_range))]
        public static CombatActionAsset AttackRange { get; private set; }
        [GetOnly(nameof(CombatActionLibrary.combat_cast_spell))]
        public static CombatActionAsset CastVanillaSpell { get; private set; }
        public static CombatActionAsset CastSkillV3 {get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            CastSkillV3.rate = 10;
            CastSkillV3.action = data =>
            {
                var ae = data.initiator.a.GetExtend();
                var skill = ae.all_attack_skills.GetRandom();
                return ae.CastSkillV3(skill, data.target);
            };
        }
    }
}