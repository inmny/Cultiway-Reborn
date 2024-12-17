namespace Cultiway.Content.Libraries;

public class Manager
{
    public static JindanLibrary      JindanLibrary      { get; } = new();
    public static JindanGroupLibrary JindanGroupLibrary { get; } = new();

    internal static void Init()
    {
        AssetManager.instance.add(JindanGroupLibrary, "jindan_groups");
        AssetManager.instance.add(JindanLibrary,      "jindan_types");
    }
}