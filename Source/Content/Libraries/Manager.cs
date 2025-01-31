namespace Cultiway.Content.Libraries;

public class Manager
{
    public static JindanLibrary      JindanLibrary      { get; } = new();
    public static JindanGroupLibrary JindanGroupLibrary { get; } = new();
    public static ElixirLibrary ElixirLibrary { get; } = new();
    public static YuanyingLibrary YuanyingLibrary { get; } = new();
    public static CultibookLibrary CultibookLibrary { get; } = new();
    public static CultibookBaseLibrary CultibookBaseLibrary { get; } = new();

    internal static void Init()
    {
        AssetManager.instance.add(JindanGroupLibrary, "jindan_groups");
        AssetManager.instance.add(JindanLibrary,      "jindan_types");
        AssetManager.instance.add(ElixirLibrary, "elixirs");
        AssetManager.instance.add(YuanyingLibrary, "yuanying_types");
        AssetManager.instance.add(CultibookLibrary, "cultibooks");
        AssetManager.instance.add(CultibookBaseLibrary, "cultibook_bases");
        
        PostInit();
    }

    private static void PostInit()
    {
        JindanLibrary.post_init();
        JindanGroupLibrary.post_init();
        ElixirLibrary.post_init();
        YuanyingLibrary.post_init();
        CultibookLibrary.post_init();
        CultibookBaseLibrary.post_init();
    }
}