using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public enum BalefulWindTribulationSkillKind : byte
{
    Center,
    Wave
}

/// <summary>把无施法者龙卷风技能关联到对应的渡劫者和劫数。</summary>
public struct BalefulWindTribulationSkill : IComponent
{
    public long target_actor_id;
    public byte wave;
    public BalefulWindTribulationSkillKind kind;
}
