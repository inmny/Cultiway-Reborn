using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>角色当前用于载行的法器；同一角色只应保留一条关系。</summary>
public struct ArtifactVehicleRelation : ILinkRelation
{
    public Entity artifact;
    public string ability_instance_id;

    public Entity GetRelationKey()
    {
        return artifact;
    }
}

/// <summary>载具驾驭者指向当前搭载的同行者。</summary>
public struct ArtifactVehiclePassengerRelation : ILinkRelation
{
    public Entity passenger;

    public Entity GetRelationKey()
    {
        return passenger;
    }
}

/// <summary>同行者保存其驾驭者、座位与登乘前飞行状态，供运行系统同步和恢复。</summary>
public struct ArtifactVehiclePassenger : IComponent
{
    public Entity driver;
    public int seat_index;
    public bool was_flying;
}
