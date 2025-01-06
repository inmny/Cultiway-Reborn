using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;
[Dependency(typeof(CommonWeaponSkills))]
public class WrappedSkills : ExtendLibrary<WrappedSkillAsset, WrappedSkills>
{
    protected override void OnInit()
    {
        CommonWeaponSkills.StartWeaponSkill.SelfWrap(WrappedSkillType.Attack).cost_check = (ActorExtend actor, out float strength) =>
        {
            strength = 0;
            if (!actor.TryGetComponent(out Xian xian))
            {
                return false;
            }

            strength = actor.Base.stats[BaseStatses.MaxWakan.id] * 0.01f;
            if (xian.wakan < strength)
            {
                return false;
            }

            strength *= 10;
            
            return true;
        };
    }
}