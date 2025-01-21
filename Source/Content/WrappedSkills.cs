using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;
using Cultiway.Utils.Predefined;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;
[Dependency(
    typeof(CommonWeaponSkills), 
    typeof(CommonBladeSkills), 
    typeof(SwordSkills), 
    typeof(GroundThornSkills)
)]
public class WrappedSkills : ExtendLibrary<WrappedSkillAsset, WrappedSkills>
{
    public static WrappedSkillAsset StartWeaponSkill { get; private set; }
    public static WrappedSkillAsset StartSelfSurroundFireBlade { get; private set; }
    public static WrappedSkillAsset StartOutSurroundFireBlade  { get; private set; }
    public static WrappedSkillAsset StartForwardFireBlade      { get; private set; }
    public static WrappedSkillAsset StartAllFireBlade          { get; private set; }
    public static WrappedSkillAsset StartSelfSurroundGoldBlade { get; private set; }
    public static WrappedSkillAsset StartOutSurroundGoldBlade  { get; private set; }
    public static WrappedSkillAsset StartForwardGoldBlade      { get; private set; }
    public static WrappedSkillAsset StartAllGoldBlade          { get; private set; }
    public static WrappedSkillAsset StartSelfSurroundWaterBlade { get; private set; }
    public static WrappedSkillAsset StartOutSurroundWaterBlade  { get; private set; }
    public static WrappedSkillAsset StartForwardWaterBlade      { get; private set; }
    public static WrappedSkillAsset StartAllWaterBlade          { get; private set; }
    public static WrappedSkillAsset StartSelfSurroundWindBlade { get; private set; }
    public static WrappedSkillAsset StartOutSurroundWindBlade  { get; private set; }
    public static WrappedSkillAsset StartForwardWindBlade      { get; private set; }
    public static WrappedSkillAsset StartAllWindBlade          { get; private set; }
    public static WrappedSkillAsset StartSpecialGoldSword { get; private set; }
    public static WrappedSkillAsset StartSelfSurroundGoldSword { get; private set; }
    public static WrappedSkillAsset StartForwardGoldSword      { get; private set; }
    public static WrappedSkillAsset StartAllGoldSword          { get; private set; }
    public static WrappedSkillAsset StartSingleGroundThorn { get; private set; }
    public static WrappedSkillAsset StartLineGroundThorn { get; private set; }
    public static WrappedSkillAsset StartCircleGroundThorn { get; private set; }
    public static WrappedSkillAsset StartAllGroundThorn { get; private set; }
    
    
    public static WrappedSkillAsset StartFireball { get; private set; }
    public static WrappedSkillAsset StartFireballCaster { get; private set; }
    protected override void OnInit()
    {
        StartWeaponSkill = WrapAttackSkill(CommonWeaponSkills.StartWeaponSkill);
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

        StartSelfSurroundFireBlade = WrapAttackSkill(CommonBladeSkills.StartSelfSurroundFireBlade);
        StartOutSurroundFireBlade = WrapAttackSkill(CommonBladeSkills.StartOutSurroundFireBlade);
        StartForwardFireBlade = WrapAttackSkill(CommonBladeSkills.StartForwardFireBlade);
        StartAllFireBlade = WrapAttackSkill(CommonBladeSkills.StartAllFireBlade);
        StartAllFireBlade.enhance = GetMultiStageProjectionEnhanceAction(
            CommonBladeSkills.UntrajedFireBladeEntity.id,
            CommonBladeSkills.FireBladeCasterEntity.id,
            StartForwardFireBlade.id,
            StartSelfSurroundFireBlade.id,
            StartOutSurroundFireBlade.id
        );
        StartSelfSurroundWindBlade = WrapAttackSkill(CommonBladeSkills.StartSelfSurroundWindBlade);
        StartOutSurroundWindBlade = WrapAttackSkill(CommonBladeSkills.StartOutSurroundWindBlade);
        StartForwardWindBlade = WrapAttackSkill(CommonBladeSkills.StartForwardWindBlade);
        StartAllWindBlade = WrapAttackSkill(CommonBladeSkills.StartAllWindBlade);
        StartAllWindBlade.enhance = GetMultiStageProjectionEnhanceAction(
            CommonBladeSkills.UntrajedWindBladeEntity.id,
            CommonBladeSkills.WindBladeCasterEntity.id,
            StartForwardWindBlade.id,
            StartSelfSurroundWindBlade.id,
            StartOutSurroundWindBlade.id
        );
        StartSelfSurroundWaterBlade = WrapAttackSkill(CommonBladeSkills.StartSelfSurroundWaterBlade);
        StartOutSurroundWaterBlade = WrapAttackSkill(CommonBladeSkills.StartOutSurroundWaterBlade);
        StartForwardWaterBlade = WrapAttackSkill(CommonBladeSkills.StartForwardWaterBlade);
        StartAllWaterBlade = WrapAttackSkill(CommonBladeSkills.StartAllWaterBlade);
        StartAllWaterBlade.enhance = GetMultiStageProjectionEnhanceAction(
            CommonBladeSkills.UntrajedWaterBladeEntity.id,
            CommonBladeSkills.WaterBladeCasterEntity.id,
            StartForwardWaterBlade.id,
            StartSelfSurroundWaterBlade.id,
            StartOutSurroundWaterBlade.id
        );
        StartSelfSurroundGoldBlade = WrapAttackSkill(CommonBladeSkills.StartSelfSurroundGoldBlade);
        StartOutSurroundGoldBlade = WrapAttackSkill(CommonBladeSkills.StartOutSurroundGoldBlade);
        StartForwardGoldBlade = WrapAttackSkill(CommonBladeSkills.StartForwardGoldBlade);
        StartAllGoldBlade = WrapAttackSkill(CommonBladeSkills.StartAllGoldBlade);
        StartAllGoldBlade.enhance = GetMultiStageProjectionEnhanceAction(
            CommonBladeSkills.UntrajedGoldBladeEntity.id,
            CommonBladeSkills.GoldBladeCasterEntity.id,
            StartForwardGoldBlade.id,
            StartSelfSurroundGoldBlade.id,
            StartOutSurroundGoldBlade.id
        );
        StartSpecialGoldSword = WrapAttackSkill(SwordSkills.StartSpecialGoldSword);
        StartSelfSurroundGoldSword = WrapAttackSkill(SwordSkills.StartSelfSurroundGoldSword);
        StartForwardGoldSword = WrapAttackSkill(SwordSkills.StartForwardGoldSword);
        StartAllGoldSword = WrapAttackSkill(SwordSkills.StartAllGoldSword);
        StartAllGoldSword.enhance = (ActorExtend ae, string source) =>
        {
            var available_enhancements = new List<int>()
            {
                0, 1, 1, 1, 2, 2, 3, 3, 3
            };
            var caster_modifiers = ae.GetOrNewSkillEntityModifiers(SwordSkills.GoldSwordCasterEntity.id).Data;
            if (caster_modifiers.Get<StageModifier>().Value >= SwordSkills.starters.Length)
            {
                available_enhancements.RemoveAll(x => x == 3);
            }

            if (available_enhancements.Count == 0) return;
            switch (available_enhancements.GetRandom())
            {
                case 0:
                    // 实体扩大
                    ae.GetOrNewSkillEntityModifiers(SwordSkills.UntrajedGoldSwordEntity.id)
                        .GetComponent<ScaleModifier>().Value += 0.1f;
                    break;
                case 1:
                    // 连发数量增加
                    caster_modifiers.Get<CastCountModifier>().Value++;
                    break;
                case 2:
                    // 某一子技能的齐射数量增加
                    ae.GetOrNewSkillActionModifiers(
                            SwordSkills.starters[Toolbox.randomInt(0, caster_modifiers.Get<StageModifier>().Value)])
                        .GetComponent<SalvoCountModifier>().Value+=8;
                    break;
                case 3:
                    // 添加新的子技能
                    caster_modifiers.Get<StageModifier>().Value++;
                    break;
            }
        };
        StartSingleGroundThorn = WrapAttackSkill(GroundThornSkills.StartSingleGroundThorn);
        StartLineGroundThorn = WrapAttackSkill(GroundThornSkills.StartLineGroundThorn);
        StartCircleGroundThorn = WrapAttackSkill(GroundThornSkills.StartCircleGroundThorn);
        StartAllGroundThorn = WrapAttackSkill(GroundThornSkills.StartAllGroundThorn);
        StartAllGroundThorn.enhance = [Hotfixable](ae, source) =>
        {
            var available_enhancements = new List<int>()
            {
                0, 1, 1, 2, 2, 3, 3, 3
            };
            var caster_modifiers = ae.GetOrNewSkillEntityModifiers(GroundThornSkills.GroundThornCasterEntity.id).Data;
            if (caster_modifiers.Get<StageModifier>().Value >= GroundThornSkills.starters.Length)
            {
                available_enhancements.RemoveAll(x => x == 3);
            }
            
            if (available_enhancements.Count == 0) return;
            switch (available_enhancements.GetRandom())
            {
                case 0:
                    // 实体扩大
                    ae.GetOrNewSkillEntityModifiers(GroundThornSkills.SingleGroundThornEntity.id)
                        .GetComponent<ScaleModifier>().Value += 0.1f;
                    break;
                case 1:
                    // 连发数量增加
                    caster_modifiers.Get<CastCountModifier>().Value++;
                    break;
                case 2:
                    // 某一子技能的齐射数量增加
                    var stage = Toolbox.randomInt(0, caster_modifiers.Get<StageModifier>().Value);
                    if (stage != 2)
                    {
                        ae.GetOrNewSkillActionModifiers(
                                GroundThornSkills.starters[stage])
                            .GetComponent<SalvoCountModifier>().Value+=1;
                    }
                    else
                    {
                        ae.GetOrNewSkillEntityModifiers(GroundThornSkills.CircleGroundThornCasterEntity.id)
                            .GetComponent<SalvoCountModifier>().Value += 1;
                    }
                    break;
                case 3:
                    // 添加新的子技能
                    caster_modifiers.Get<StageModifier>().Value++;
                    break;
            }
        };

        StartFireball = WrapAttackSkill(FireballSkills.StartFireball);
        StartFireballCaster = WrapAttackSkill(FireballSkills.StartFireballCaster);
    }

    private EnhanceSkill GetMultiStageProjectionEnhanceAction(string proj_entity_id, string all_caster_id, params string[] blade_skill_ids)
    {
        return (ActorExtend ae, string source) =>
        {
            var available_enhancements = new List<int>()
            {
                0, 1, 2, 3
            };
            var caster_modifiers = ae.GetOrNewSkillEntityModifiers(all_caster_id).Data;
            if (caster_modifiers.Get<StageModifier>().Value >= blade_skill_ids.Length)
            {
                available_enhancements.RemoveAll(x => x == 3);
            }

            if (available_enhancements.Count == 0) return;
            switch (available_enhancements.GetRandom())
            {
                case 0:
                    // 火斩实体扩大
                    ae.GetOrNewSkillEntityModifiers(proj_entity_id)
                        .GetComponent<ScaleModifier>().Value += 0.1f;
                    break;
                case 1:
                    // 连发数量增加
                    caster_modifiers.Get<CastCountModifier>().Value++;
                    break;
                case 2:
                    // 某一子技能的齐射数量增加
                    ae.GetOrNewSkillActionModifiers(
                            blade_skill_ids[Toolbox.randomInt(0, caster_modifiers.Get<StageModifier>().Value)])
                        .GetComponent<SalvoCountModifier>().Value++;
                    break;
                case 3:
                    // 添加新的子技能
                    caster_modifiers.Get<StageModifier>().Value++;
                    break;
            }
        };
    }

    private WrappedSkillAsset WrapAttackSkill(TriggerActionMeta<StartSkillTrigger, StartSkillContext> skill_starter, float cost=0.01f)
    {
        return WrappedSkillAsset.StartWrap(skill_starter)
            .WithSkillType(WrappedSkillType.Attack)
            .SetDefaultWakanCost(cost)
            .SetLocaleID(skill_starter.id.Split('.').Last())
            .Build();
    }
}