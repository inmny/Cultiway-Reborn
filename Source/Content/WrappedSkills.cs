using System.Collections.Generic;
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
    public static WrappedSkillAsset StartOutSurroundFireBlade { get; private set; }
    public static WrappedSkillAsset StartForwardFireBlade { get; private set; }
    public static WrappedSkillAsset StartAllFireBlade { get; private set; }
    protected override void OnInit()
    {
        StartWeaponSkill = CommonWeaponSkills.StartWeaponSkill.SelfWrap(WrappedSkillType.Attack);
        StartWeaponSkill.enhance = (ActorExtend ae, string source) =>
        {
            var modifier_data = ae.GetOrNewSkillActionModifiers(StartWeaponSkill.id).Data;
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
        
        StartOutSurroundFireBlade = CommonBladeSkills.StartOutSurroundFireBlade.SelfWrap(WrappedSkillType.Attack);
        StartOutSurroundFireBlade.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);
        
        StartForwardFireBlade = CommonBladeSkills.StartForwardFireBlade.SelfWrap(WrappedSkillType.Attack);
        StartForwardFireBlade.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);
        
        StartAllFireBlade = CommonBladeSkills.StartAllFireBlade.SelfWrap(WrappedSkillType.Attack);
        StartAllFireBlade.cost_check = WrappedSkillCostChecks.DefaultWakanCost(0.01f);
        StartAllFireBlade.enhance = (ActorExtend ae, string source) =>
        {
            var available_enhancements = new List<int>()
            {
                0, 1, 2, 3
            };
            var caster_modifiers = ae.GetOrNewSkillEntityModifiers(CommonBladeSkills.FireBladeCasterEntity.id).Data;
            if (caster_modifiers.Get<StageModifier>().Value >= 3)
            {
                available_enhancements.RemoveAll(x=> x == 3);
            }
            
            if (available_enhancements.Count == 0) return;
            switch (available_enhancements.GetRandom())
            {
                case 0:
                    // 火斩实体扩大
                    ae.GetOrNewSkillEntityModifiers(CommonBladeSkills.UntrajedFireBladeEntity.id)
                        .GetComponent<ScaleModifier>().Value += 0.1f;
                    break;
                case 1:
                    // 连发数量增加
                    caster_modifiers.Get<CastCountModifier>().Value++;
                    break;
                case 2:
                    // 某一子技能的齐射数量增加
                    switch (Toolbox.randomInt(0,caster_modifiers.Get<StageModifier>().Value))
                    {
                        case 0:
                            ae.GetOrNewSkillActionModifiers(CommonBladeSkills.StartForwardFireBlade.id)
                                .GetComponent<SalvoCountModifier>().Value++;
                            break;
                        case 1:
                            ae.GetOrNewSkillActionModifiers(CommonBladeSkills.StartSelfSurroundFireBlade.id)
                                .GetComponent<SalvoCountModifier>().Value++;
                            break;
                        case 2:
                            ae.GetOrNewSkillActionModifiers(CommonBladeSkills.StartOutSurroundFireBlade.id)
                                .GetComponent<SalvoCountModifier>().Value++;
                            break;
                    }
                    break;
                case 3:
                    // 添加新的子技能
                    caster_modifiers.Get<StageModifier>().Value++;
                    break;
            }
        };
    }
}