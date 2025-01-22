using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct TileCollisionTrigger() : IEventTrigger<TileCollisionTrigger, TileCollisionContext>
{
    public bool Enabled { get; set; } = true;
    public TriggerActionMeta<TileCollisionTrigger, TileCollisionContext> TriggerActionMeta { get; set; } = null;
}