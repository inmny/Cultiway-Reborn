using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cultiway.Utils.Extension
{
    public static class ProfessionAssetTools
    {
        public static void RemoveDecision(this ProfessionAsset asset, string id)
        {
            asset.decision_ids?.Remove(id);
            asset.decisions_assets = asset.decision_ids?.Select(AssetManager.decisions_library.get).ToArray() ?? Array.Empty<DecisionAsset>();
        }
    }
}