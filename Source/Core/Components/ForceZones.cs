using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct ForceZones : IComponent
{
    public List<TileZone> Zones;
}