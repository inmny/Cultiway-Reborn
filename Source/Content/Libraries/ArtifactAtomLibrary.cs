using System.Collections.Generic;

namespace Cultiway.Content.Libraries;

public class ArtifactAtomLibrary : AssetLibrary<ArtifactAtomAsset>
{
    internal IEnumerable<ArtifactAtomAsset> All => list;
}
