using System.Collections.Generic;

namespace Cultiway.Core.Performance;

internal static class PresentationVisibility
{
    internal static ulong GetSignature(bool renderGameplay)
    {
        unchecked
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            ulong hash = (offset ^ (renderGameplay ? 1UL : 0UL)) * prime;
            List<TileZone> zones =
                World.world?.zone_camera?.getVisibleZones();
            if (zones == null)
            {
                return hash;
            }

            hash = (hash ^ (ulong)zones.Count) * prime;
            for (int i = 0; i < zones.Count; i++)
            {
                hash = (hash ^ (uint)(zones[i]?.id ?? -1)) * prime;
            }

            return hash;
        }
    }
}
