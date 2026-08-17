using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>标记由专用主动能力 Provider 执行的已学技能容器。</summary>
public struct SpecializedActiveAbility : IComponent
{
    public string ProviderId;
    public string EntryId;
}
