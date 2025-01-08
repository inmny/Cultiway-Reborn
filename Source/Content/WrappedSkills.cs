using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Utils.Extension;
using Cultiway.Utils.Predefined;
using Friflo.Engine.ECS;

namespace Cultiway.Content;
[Dependency(typeof(CommonWeaponSkills), typeof(CommonBladeSkills))]
public class WrappedSkills : ExtendLibrary<WrappedSkillAsset, WrappedSkills>
{
    public static WrappedSkillAsset StartWeaponSkill { get; private set; }
    public static WrappedSkillAsset StartSelfSurroundFireBlade { get; private set; }
    public static WrappedSkillAsset StartForwardFireBlade { get; private set; }
    public static WrappedSkillAsset StartAllFireBlade { get; private set; }
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
        StartWeaponSkill.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);

        StartSelfSurroundFireBlade = CommonBladeSkills.StartSelfSurroundFireBlade.SelfWrap(WrappedSkillType.Attack);
        StartSelfSurroundFireBlade.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);
        
        StartForwardFireBlade = CommonBladeSkills.StartForwardFireBlade.SelfWrap(WrappedSkillType.Attack);
        StartForwardFireBlade.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);
        
        StartAllFireBlade = CommonBladeSkills.StartAllFireBlade.SelfWrap(WrappedSkillType.Attack);
        StartAllFireBlade.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);
    }
}