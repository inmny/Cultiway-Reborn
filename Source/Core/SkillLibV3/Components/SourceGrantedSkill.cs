using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 标记一个由外部来源直接授予的只读技能容器。
/// 该技能不能学习、改进或上传，但仍使用标准 SkillContainer 身份参与释放和展示。
/// </summary>
public struct SourceGrantedSkill : IComponent
{
}

/// <summary>标记负责持有全部来源授予技能容器的世界级根实体。</summary>
internal struct SourceGrantedSkillRoot : IComponent
{
}
