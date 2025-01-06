using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content;
[Dependency(typeof(CommonWeaponSkills))]
public class WrappedSkills : ExtendLibrary<WrappedSkillAsset, WrappedSkills>
{
    public static WrappedSkillAsset StartWeaponSkill { get; private set; }
    protected override void OnInit()
    {
        StartWeaponSkill = CommonWeaponSkills.StartWeaponSkill.SelfWrap(WrappedSkillType.Attack);
        StartWeaponSkill.enhance = (ActorExtend ae, Entity modifier_container, string source) =>
        {
            var modifier_data = modifier_container.Data;
            switch (Toolbox.randomInt(0, 1))
            {
                case 0:
                    modifier_data.Get<ScaleModifier>().Value += 0.1f;
                    break;
            }
        };
        StartWeaponSkill.cost_check = (ActorExtend ae, out float strength) =>
        {
            strength = 0;
            if (!ae.TryGetComponent(out Xian xian))
            {
                return false;
            }

            strength = ae.Base.stats[BaseStatses.MaxWakan.id] * 0.01f;
            if (xian.wakan < strength)
            {
                return false;
            }

            strength *= 10;
            
            return true;
        };
    }
}