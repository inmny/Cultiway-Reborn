using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
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
        public static CombatActionAsset CastActiveAbility { get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            CastActiveAbility.rate = 1;
            CastActiveAbility.action = data =>
            {
                var ae = data.initiator.a.GetExtend();
                using var candidates = new ListPool<ActiveAbilityHandle>();
                using var weights = new ListPool<int>();
                if (!ActiveAbilityService.TrySelectForAi(
                        ae,
                        data.target,
                        candidates,
                        weights,
                        out ActiveAbilityHandle selected)) return false;

                var target = new ActiveAbilityTarget(data.target, data.target.GetSimPos());
                return ActiveAbilityService.TryUse(ae, selected, target, ActiveAbilityUseOrigin.Autonomous);
            };
        }
    }
}
