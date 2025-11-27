using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;
/// <summary>
/// 小人/符箓/法宝等每有一个技能就会有一个SkillContainer来存储词条和释放的技能实体
/// </summary>
public struct SkillContainer : IComponent
{
    /// <summary>
    /// 会召唤出一个什么样的技能实体（大部分就是一个投掷物，比如说剑,火球等）
    /// </summary>
    public string SkillEntityAssetID;

    public SetupAction OnSetup;
    public TravelAction OnTravel;
    public EffectObjAction OnEffectObj;
    public SkillEntityAsset Asset
    {
        get
        {
            _asset ??= ModClass.I.SkillV3.SkillLib.get(SkillEntityAssetID);
            return _asset;
        }
    }
    private SkillEntityAsset _asset;
}