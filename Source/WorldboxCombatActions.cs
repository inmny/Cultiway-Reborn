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
        public static CombatActionAsset CastSkill { get; private set; }
        protected override void OnInit()
        {
            RegisterAssets("Cultiway.CombatActions");
            CastSkill.rate = 10;
            CastSkill.action = data =>
            {
                var ae = data.initiator.a.GetExtend();

                var skill = ae.tmp_all_attack_skills.GetRandom();
                return data.initiator.a.GetExtend().CastSkillV2(skill, data.target);
            };
        }
    }
}