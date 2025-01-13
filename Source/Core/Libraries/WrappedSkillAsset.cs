using Cultiway.Content.Const;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

public delegate bool CostCheck(ActorExtend ae, out float strength);
public delegate void EnhanceSkill(ActorExtend ae, string source);
public class WrappedSkillAsset : Asset
{
    public bool[] skill_type = [];
    public CostCheck cost_check;
    public EnhanceSkill enhance;
    public float default_strength = 100;
    public string LocaleID;
    public void Enhance(ActorExtend ae, string source)
    {
        enhance?.Invoke(ae, source);
    }

    public string GetName()
    {
        return LM.Get(LocaleID);
    }

    public string GetDescription()
    {
        return LM.Get($"{LocaleID}.Info");
    }

    public static Wrapper StartWrap(TriggerActionMeta<StartSkillTrigger, StartSkillContext> meta)
    {
        return new Wrapper(meta);
    }
    public class Wrapper
    {
        private readonly WrappedSkillAsset _asset;
        internal Wrapper(TriggerActionMeta<StartSkillTrigger, StartSkillContext> meta)
        {
            _asset = new WrappedSkillAsset();
            _asset.id = meta.id;
        }
        public Wrapper SetCostCheck(CostCheck check)
        {
            _asset.cost_check = check;
            return this;
        }
        public Wrapper SetEnhance(EnhanceSkill enhance)
        {
            _asset.enhance = enhance;
            return this;
        }
        public Wrapper SetDefaultStrength(float strength)
        {
            _asset.default_strength = strength;
            return this;
        }
        public Wrapper SetLocaleID(string id)
        {
            _asset.LocaleID = id;
            return this;
        }
        public Wrapper WithSkillType(params WrappedSkillType[] types)
        {
            foreach (var type in types)
            {
                _asset.SetSkillType(type);
            }
            return this;
        }
        public WrappedSkillAsset Build()
        {
            ModClass.L.WrappedSkillLibrary.add(_asset);
            return _asset;
        }
    }
    public bool HasSkillType(WrappedSkillType type)
    {
        if ((int) type >= skill_type.Length)
        {
            var new_skill_type = new bool[(int) type + 1];
            for (var i = 0; i < skill_type.Length; i++)
            {
                new_skill_type[i] = skill_type[i];
            }
            skill_type = new_skill_type;
        }
        return skill_type[(int) type];
    } 
    public void SetSkillType(WrappedSkillType type)
    {
        if ((int) type >= skill_type.Length)
        {
            var new_skill_type = new bool[(int) type + 1];
            for (var i = 0; i < skill_type.Length; i++)
            {
                new_skill_type[i] = skill_type[i];
            }
            skill_type = new_skill_type;
        }
        skill_type[(int) type] = true;
    }
}