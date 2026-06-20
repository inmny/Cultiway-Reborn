namespace Cultiway.Utils.Extension;

public static class LMTools
{
    public static bool Has(string key)
    {
        return LocalizedTextManager.instance._localized_text.ContainsKey(key);
    }

    public static string GetOrKey(string key)
    {
        return Has(key) ? LocalizedTextManager.getText(key, null, false) : key;
    }

    public static string GetOrFallback(string key, string fallback)
    {
        return Has(key) ? LocalizedTextManager.getText(key, null, false) : fallback;
    }

    public static string Format(string key, params (string key, object value)[] replacements)
    {
        string text = GetOrKey(key).Replace("\\n", "\n");
        for (int i = 0; i < replacements.Length; i++)
        {
            (string replacementKey, object value) = replacements[i];
            text = text.Replace($"${replacementKey}$", value?.ToString() ?? "");
        }

        return text;
    }
}
