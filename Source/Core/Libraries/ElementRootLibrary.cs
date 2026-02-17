using Cultiway.Const;
using Cultiway.Utils;

namespace Cultiway.Core.Libraries;

public class ElementRootLibrary : AssetLibrary<ElementRootAsset>
{
    public ElementRootAsset Common { get; private set; }
    public ElementRootAsset Entropy { get; private set; }

    public override void init()
    {
        Common = add(new ElementRootAsset(
            id: nameof(Common),
            new ElementComposition([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.5f, 0.5f, 1f])
        ));
        t.icon_path = "common";
        Entropy = add(new ElementRootAsset(
            id: nameof(Entropy),
            new ElementComposition([0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f])
        ));
        t.icon_path = "entropy";
    }

    public ElementRootAsset GetRootType(float[] composition, out float final_sim)
    {
        ElementRootAsset asset = Common;
        float best_sim = 0;
        foreach (var type in list)
        {
            var sim = MathUtils.CosineSimilarity(composition, type.composition.AsArray(), ElementIndex.Entropy);
            if (sim >= best_sim)
            {
                best_sim = sim;
                asset = type;
            }
        }

        final_sim = best_sim;

        return asset;
    }
}