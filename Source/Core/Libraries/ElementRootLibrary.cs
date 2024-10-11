using Cultiway.Utils;

namespace Cultiway.Core.Libraries;

public class ElementRootLibrary : AssetLibrary<ElementRootAsset>
{
    public float            base_prob = 0.1f;
    public ElementRootAsset Common { get; private set; }

    public override void init()
    {
        Common = add(new ElementRootAsset(
            id: nameof(Common),
            composition: new ElementComposition([0.2f, 0.2f, 0.2f, 0.2f, 0.2f])
        ));
    }

    public ElementRootAsset GetRootType(float[] composition, out float final_sim)
    {
        ElementRootAsset asset = Common;
        float best_sim = 0;
        foreach (var type in list)
        {
            var sim = MathUtils.CosineSimilarity(composition, type.composition.AsArray());
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