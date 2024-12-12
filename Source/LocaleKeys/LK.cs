using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cultiway.LocaleKeys.UI;

namespace Cultiway.LocaleKeys;

public class LK(string component) : LocalComponent(component)
{
    public static readonly LK Root = new("cultiway");

    private       Locale ui;
    public static Locale UI => Root.ui;

    internal void ExportTo(string path, string locale_id = "cz")
    {
        var old_locale_id = LocalizedTextManager.instance.language;
        if (old_locale_id != locale_id)
            LocalizedTextManager.instance.setLanguage(locale_id);

        Stack<LocalComponent> stack = new();
        stack.Push(this);
        StringBuilder sb = new();
        sb.AppendLine($"key,{locale_id}");
        while (stack.Count > 0)
        {
            LocalComponent component = stack.Pop();

            var fields = component.GetType().GetFields();
            foreach (FieldInfo field in fields.Where(x => x.FieldType == typeof(LocalComponent)))
            {
                var key_component = (LocalComponent)field.GetValue(component);
                sb.AppendLine($"{key_component.Key},{key_component.Value}");
            }

            foreach (FieldInfo field in fields.Where(x => x.FieldType != typeof(LocalComponent)))
                stack.Push((LocalComponent)field.GetValue(component));
        }

        File.WriteAllText(path, sb.ToString());


        if (old_locale_id != locale_id)
            LocalizedTextManager.instance.setLanguage(old_locale_id);
    }
}