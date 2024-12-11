namespace Cultiway.Content.Libraries;

public class Manager
{
    public static JindanLibrary JindanLibrary { get; } = new();

    internal static void Init()
    {
        AssetManager.instance.add(JindanLibrary, "jindan_types");
    }
}