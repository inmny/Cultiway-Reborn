using System.Text;

namespace Cultiway.Core.Logging;

public static class CultiLogFormatter
{
    public static string Format(string template, CultiLogArg[] args)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (args == null || args.Length == 0) return template;

        StringBuilder sb = new(template.Length + args.Length * 8);
        for (int i = 0; i < template.Length; i++)
        {
            char ch = template[i];
            if (ch != '{')
            {
                sb.Append(ch);
                continue;
            }

            int end = template.IndexOf('}', i + 1);
            if (end < 0)
            {
                sb.Append(ch);
                continue;
            }

            string key = template.Substring(i + 1, end - i - 1);
            if (TryGetArg(args, key, out var value))
            {
                sb.Append(value);
            }
            else
            {
                sb.Append('{').Append(key).Append('}');
            }

            i = end;
        }

        return sb.ToString();
    }

    private static bool TryGetArg(CultiLogArg[] args, string key, out string value)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Key != key) continue;
            value = args[i].FormatValue();
            return true;
        }

        value = null;
        return false;
    }
}
