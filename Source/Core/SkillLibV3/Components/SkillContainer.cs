using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Motions;
using Cultiway.Core.SkillLibV3.Visuals;

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

    /// <summary>
    /// 在所属法术实体内部绑定的动画索引。
    /// </summary>
    public int AnimationIndex;

    /// <summary>
    /// 该法术实际要求的资源通道；从实体默认值复制后可由具体容器独立改写。
    /// </summary>
    public SkillCastResourceRequirement CastResourceRequirement;

    public SetupAction OnSetup;
    public TravelAction OnTravel;
    public EffectObjAction OnEffectObj;

    /// <summary>
    /// 构建完成时解析出的视觉元素，供生成的技能实体直接继承。
    /// </summary>
    public SkillVfxElementAsset VfxElement;

    /// <summary>由技能完整语义解析出的运行时调色板，不参与技能容器存档。</summary>
    [Ignore]
    public SemanticColorPalette ColorPalette;

    /// <summary>
    /// 构建完成时解析出的运动配置，供生成的法术实体直接继承。
    /// </summary>
    public SkillMotionProfileAsset MotionProfile;

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
