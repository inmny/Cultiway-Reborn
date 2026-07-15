using System.Collections.Generic;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 法器能力注册表。新增能力只需注册新的 <see cref="ArtifactAbilityAsset"/>。
/// </summary>
public class ArtifactAbilityLibrary : AssetLibrary<ArtifactAbilityAsset>
{
    internal IEnumerable<ArtifactAbilityAsset> All => list;
}
