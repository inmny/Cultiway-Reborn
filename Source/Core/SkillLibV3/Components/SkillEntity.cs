using Friflo.Engine.ECS;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Core.SkillLibV3.Components;
/// <summary>
/// 技能实体，在地图上出现的技能单体
/// </summary>
public struct SkillEntity : IComponent
{
    public SkillEntityAsset Asset;
    public Entity SkillContainer;
    public SkillVfxElementAsset VfxElement;
}
