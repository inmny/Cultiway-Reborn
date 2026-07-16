using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>法器上持久保存的器灵成长状态；显化生物死亡或回收不会丢失。</summary>
public struct ArtifactSpiritState : IComponent
{
    public bool awakened;
    public float experience;
    public int level;
    public float bond;
    public double recovery_until;
}

/// <summary>法器指向当前器灵显化生物的运行期关系。</summary>
public struct ArtifactSpiritAvatarRelation : ILinkRelation
{
    public Entity avatar;

    public Entity GetRelationKey()
    {
        return avatar;
    }
}

/// <summary>器灵显化生物上的法器来源和已解析战斗参数。</summary>
public struct ArtifactSpiritAvatar : IComponent
{
    public Entity artifact;
    public Entity controller;
    public string ability_instance_id;
    public float damage_ratio;
    public float health_ratio;
    public float armor_bonus;
    public float recovery_duration;
    public bool recover_on_death;
}
