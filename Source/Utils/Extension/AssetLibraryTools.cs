namespace Cultiway.Utils.Extension;

public static class AssetLibraryTools
{
    public static bool Contains<T>(this AssetLibrary<T> library, string id) where T : Asset
    {
        return library?.dict.ContainsKey(id) ?? false;
    }

    public static T GetRandom<T>(this AssetLibrary<T> library) where T : Asset
    {
        if (library.list.Count == 0)
        {
            return null;
        }
        return library.list.GetRandom();
    }
}