namespace Cultiway.Utils.Extension;

public static class AssetLibraryTools
{
    public static bool Contains<T>(this AssetLibrary<T> library, string id) where T : Asset
    {
        return library?.dict.ContainsKey(id) ?? false;
    }
}