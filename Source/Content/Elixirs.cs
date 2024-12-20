using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content;

[Dependency(typeof(Jindans))]
public class Elixirs : ExtendLibrary<ElixirAsset, Elixirs>
{
    private const string      prefix = "Cultiway.Elixir";
    public static ElixirAsset OpenElementRootElixir { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets(prefix);
        OpenElementRootElixir.name_key = $"{prefix}.OpenElementRootElixir";
        OpenElementRootElixir.ingrediants = new ElixirIngrediantCheck[]
        {
            new()
            {
                count = 1,
                element_root_id = ElementRoots.Earth.id,
                jindan_id = Jindans.Bentonite.id
            },
            new()
            {
                count = 1,
                element_root_id = ElementRoots.Wood.id,
                jindan_id = Jindans.Condensed.id
            },
            new()
            {
                count = 1,
                need_element_root = true
            }
        };
    }
}