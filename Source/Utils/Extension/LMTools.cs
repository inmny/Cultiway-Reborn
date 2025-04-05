namespace Cultiway.Utils.Extension;

public static class LMTools
{
    public static bool Has(string key)
    {
        return LocalizedTextManager.instance._localized_text.ContainsKey(key);
    }
}