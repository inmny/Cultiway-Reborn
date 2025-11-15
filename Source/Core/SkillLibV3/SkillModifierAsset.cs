using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;


public delegate void SetupAction(Entity skill_entity);
public delegate void EffectObjAction(Entity skill_entity, BaseSimObject obj);
public delegate bool AddOrUpgradeAction(SkillContainerBuilder builder);
public delegate string GetDescription(Entity skill_entity);
public class SkillModifierAsset : Asset
{
    public SetupAction OnSetup;
    public EffectObjAction OnEffectObj;
    public AddOrUpgradeAction OnAddOrUpgrade;
    public GetDescription GetDescription;
}