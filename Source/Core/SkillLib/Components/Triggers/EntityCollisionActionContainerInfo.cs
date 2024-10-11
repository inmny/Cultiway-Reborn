using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct EntityCollisionActionContainerInfo(ActionMeta<EntityCollisionTrigger, Entity> meta)
    : IActionContainerInfo<EntityCollisionTrigger, Entity>
{
    public ActionMeta<EntityCollisionTrigger, Entity> Meta { get; set; } = meta;
}