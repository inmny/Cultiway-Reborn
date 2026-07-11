using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Cultiway.Core.SkillLibV3.Editor;

public enum SkillEditorFieldKind
{
    Float,
    Integer,
    Toggle,
    Text,
    StringSet
}

public class SkillEditorFieldAsset : Asset
{
    private const double NumericTolerance = 0.000001d;

    public string ParameterKey;
    public string DisplayNameKey;
    public SkillEditorFieldKind Kind;
    public string DefaultValue;
    public double MinValue;
    public double MaxValue;
    public double Step;
    public double DisplayScale = 1d;
    public string UnitKey;
    public string DisplayFormat;
    public string DisplayName => DisplayNameKey.Localize();
    public string Unit => string.IsNullOrEmpty(UnitKey) ? string.Empty : UnitKey.Localize();

    public bool TryValidate(string value, out string error)
    {
        error = null;
        switch (Kind)
        {
            case SkillEditorFieldKind.Float:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                    double.IsNaN(number) || double.IsInfinity(number))
                {
                    error = string.Format("Cultiway.SkillEditor.FieldError.FiniteNumber".Localize(), DisplayName);
                    return false;
                }
                if (number < MinValue - NumericTolerance || number > MaxValue + NumericTolerance)
                {
                    error = string.Format("Cultiway.SkillEditor.FieldError.Range".Localize(), DisplayName, MinValue,
                        MaxValue);
                    return false;
                }
                return true;
            case SkillEditorFieldKind.Integer:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    error = string.Format("Cultiway.SkillEditor.FieldError.Integer".Localize(), DisplayName);
                    return false;
                }
                if (integer < MinValue || integer > MaxValue)
                {
                    error = string.Format("Cultiway.SkillEditor.FieldError.Range".Localize(), DisplayName, MinValue,
                        MaxValue);
                    return false;
                }
                return true;
            case SkillEditorFieldKind.Toggle:
                if (!bool.TryParse(value, out _))
                {
                    error = string.Format("Cultiway.SkillEditor.FieldError.Toggle".Localize(), DisplayName);
                    return false;
                }
                return true;
            case SkillEditorFieldKind.Text:
                if (value == null)
                {
                    error = string.Format("Cultiway.SkillEditor.FieldError.Null".Localize(), DisplayName);
                    return false;
                }
                return true;
            case SkillEditorFieldKind.StringSet:
                return value != null;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public string ToDisplayValue(string storedValue)
    {
        if (Kind != SkillEditorFieldKind.Float || DisplayScale == 1d) return storedValue;
        if (!double.TryParse(storedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return storedValue;
        }
        return (number * DisplayScale).ToString(DisplayFormat, CultureInfo.InvariantCulture);
    }

    public bool TryConvertDisplayValue(string displayValue, out string storedValue, out string error)
    {
        if (Kind != SkillEditorFieldKind.Float)
        {
            return TryNormalizeStoredValue(displayValue, out storedValue, out error);
        }

        if (!double.TryParse(displayValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            double.IsNaN(number) || double.IsInfinity(number))
        {
            storedValue = null;
            error = string.Format("Cultiway.SkillEditor.FieldError.FiniteNumber".Localize(), DisplayName);
            return false;
        }

        var internalValue = number / DisplayScale;
        if (internalValue < MinValue - NumericTolerance || internalValue > MaxValue + NumericTolerance)
        {
            storedValue = null;
            error = string.Format("Cultiway.SkillEditor.FieldError.Range".Localize(), DisplayName,
                MinValue * DisplayScale, MaxValue * DisplayScale);
            return false;
        }

        storedValue = ((float)internalValue).ToString("R", CultureInfo.InvariantCulture);
        error = null;
        return true;
    }

    public bool TryNormalizeStoredValue(string value, out string normalizedValue, out string error)
    {
        normalizedValue = null;
        if (!TryValidate(value, out error)) return false;

        switch (Kind)
        {
            case SkillEditorFieldKind.Float:
                normalizedValue = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
                    .ToString("R", CultureInfo.InvariantCulture);
                break;
            case SkillEditorFieldKind.Integer:
                normalizedValue = long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case SkillEditorFieldKind.Toggle:
                normalizedValue = bool.Parse(value).ToString();
                break;
            case SkillEditorFieldKind.Text:
                normalizedValue = value;
                break;
            case SkillEditorFieldKind.StringSet:
                normalizedValue = string.Join(",", value
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        error = null;
        return true;
    }

    internal object Deserialize(Type type, string value)
    {
        if (type == typeof(float)) return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (type == typeof(double)) return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (type == typeof(int)) return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (type == typeof(long)) return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return bool.Parse(value);
        if (type == typeof(string)) return value;
        if (type == typeof(HashSet<string>))
        {
            return new HashSet<string>(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
        }
        throw new NotSupportedException(string.Format(
            "Cultiway.SkillEditor.UnsupportedFieldType".Localize(), type.FullName));
    }

    internal string Serialize(object value)
    {
        return value switch
        {
            null => string.Empty,
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            bool flag => flag.ToString(),
            string text => text,
            HashSet<string> values => string.Join(",", values.OrderBy(item => item, StringComparer.Ordinal)),
            _ => throw new NotSupportedException(string.Format(
                "Cultiway.SkillEditor.UnsupportedFieldType".Localize(), value.GetType().FullName))
        };
    }
}
