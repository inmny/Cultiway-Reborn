using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 为法术容器生成稳定结构签名，用于命名缓存、异步回包保护和同构判定。
/// </summary>
public static class SkillContainerSignature
{
    public static string Build(Entity skill)
    {
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return string.Empty;

        var container = skill.GetComponent<SkillContainer>();
        var sb = new StringBuilder();
        sb.Append(container.SkillEntityAssetID ?? string.Empty);

        var modifierTypes = skill.GetComponentTypes()
            .Where(type => typeof(IModifier).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var modifierType in modifierTypes)
        {
            var modifier = (IModifier)skill.GetComponent(modifierType);
            sb.Append('|');
            sb.Append(modifierType.FullName);
            sb.Append(':');
            sb.Append(modifier.ModifierAsset?.id ?? string.Empty);
            sb.Append('=');
            sb.Append(GetPublicFieldSignature(modifier));
        }

        return sb.ToString();
    }

    private static string GetPublicFieldSignature(IModifier modifier)
    {
        var fields = modifier.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.Name, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (var field in fields)
        {
            sb.Append(field.Name);
            sb.Append(':');
            sb.Append(FormatValue(field.GetValue(modifier)));
            sb.Append(';');
        }

        return sb.ToString();
    }

    private static string FormatValue(object value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case float f:
                return f.ToString("0.###", CultureInfo.InvariantCulture);
            case double d:
                return d.ToString("0.###", CultureInfo.InvariantCulture);
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            case string text:
                return text;
            case IEnumerable enumerable:
                return string.Join(",", enumerable.Cast<object>()
                    .Select(FormatValue)
                    .OrderBy(text => text, StringComparer.Ordinal));
            default:
                return value.ToString();
        }
    }
}
