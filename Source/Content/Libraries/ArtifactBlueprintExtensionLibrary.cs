using System.Collections.Generic;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 百宝阁法宝蓝图扩展注册表。
/// </summary>
public sealed class ArtifactBlueprintExtensionLibrary : AssetLibrary<ArtifactBlueprintExtensionAsset>
{
    internal IEnumerable<ArtifactBlueprintExtensionAsset> All => list;
}
