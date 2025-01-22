using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct TileCollisionContext : IEventContext
{
    public WorldTile Tile;
    public bool JustTriggered { get; set; }
}