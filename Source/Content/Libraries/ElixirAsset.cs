namespace Cultiway.Content.Libraries;

public struct ElixirIngrediantCheck
{
    public bool   need_element_root;
    public string element_root_id;
    public bool   need_jindan;
    public string jindan_id;
    public int    count;
}

public class ElixirAsset : Asset
{
    public ElixirIngrediantCheck[] ingrediants;
    public string                  name_key;
}