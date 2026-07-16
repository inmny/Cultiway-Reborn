using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>由法器能力维持的真实召唤生物。</summary>
public struct ArtifactSummon : IComponent
{
    public Entity artifact;
    public Entity controller;
    public string ability_instance_id;
    public double expires_at;
    public float damage_ratio;
    public float health_ratio;
    public float armor_bonus;
}
