using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>同一角色以元婴形态寻找宿主时的最小短期状态。</summary>
public struct YuanyingSoulState : IComponent
{
    public double expires_at;
    public double channel_started_at;
    public long target_actor_id;
}

/// <summary>夺舍后休眠、但仍保有再次出逃资格的原生元婴。</summary>
public struct YuanyingSeed : IComponent
{
    public CoreFormationSnapshot formation;
    public float source_power_level;

    public readonly bool IsValid => formation.IsFinalized
                                    && formation.realm == CoreFormationRealm.Yuanying
                                    && formation.strength > 0f;

    public float strength
    {
        readonly get => formation.strength;
        set => formation.strength = value;
    }

    public static YuanyingSeed FromYuanying(in Yuanying source, float powerLevel)
    {
        return new YuanyingSeed
        {
            formation = source.formation.DeepClone(),
            source_power_level = powerLevel
        };
    }

    public readonly YuanyingSeed DeepClone()
    {
        var clone = this;
        clone.formation = formation.DeepClone();
        return clone;
    }

    public readonly Yuanying Restore()
    {
        return new Yuanying
        {
            formation = formation.DeepClone()
        };
    }
}
