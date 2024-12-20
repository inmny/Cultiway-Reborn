using Cultiway.Content.CultisysComponents;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

public struct ElixirIngrediantCheck
{
    public bool   need_element_root;
    public string element_root_id;
    public bool   need_jindan;
    public string jindan_id;
    public int    count;
}

public delegate void ElixirConsumedDelegate(ActorExtend ae, Entity elixir_entity, ref Elixir elixir);

public class ElixirAsset : Asset
{
    public ElixirConsumedDelegate consumed_action;
    public ElixirIngrediantCheck[] ingrediants;
    public string                  name_key;
}