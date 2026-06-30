using System.Globalization;
using System.Text;

namespace Cultiway.Core.Logging;

public enum CultiLogArgType
{
    Null,
    String,
    Integer,
    Float,
    Boolean
}

public readonly struct CultiLogArg
{
    public readonly string Key;
    public readonly CultiLogArgType Type;
    public readonly string StringValue;
    public readonly long IntegerValue;
    public readonly double FloatValue;
    public readonly bool BooleanValue;

    private CultiLogArg(string key, CultiLogArgType type, string stringValue, long integerValue, double floatValue,
        bool booleanValue)
    {
        Key = key;
        Type = type;
        StringValue = stringValue;
        IntegerValue = integerValue;
        FloatValue = floatValue;
        BooleanValue = booleanValue;
    }

    public static CultiLogArg Null(string key)
    {
        return new CultiLogArg(key, CultiLogArgType.Null, null, 0, 0, false);
    }

    public static CultiLogArg Str(string key, string value)
    {
        return value == null
            ? Null(key)
            : new CultiLogArg(key, CultiLogArgType.String, value, 0, 0, false);
    }

    public static CultiLogArg Int(string key, long value)
    {
        return new CultiLogArg(key, CultiLogArgType.Integer, null, value, 0, false);
    }

    public static CultiLogArg Float(string key, double value)
    {
        return new CultiLogArg(key, CultiLogArgType.Float, null, 0, value, false);
    }

    public static CultiLogArg Bool(string key, bool value)
    {
        return new CultiLogArg(key, CultiLogArgType.Boolean, null, 0, 0, value);
    }

    public string FormatValue()
    {
        return Type switch
        {
            CultiLogArgType.String => StringValue,
            CultiLogArgType.Integer => IntegerValue.ToString(CultureInfo.InvariantCulture),
            CultiLogArgType.Float => FloatValue.ToString("0.###", CultureInfo.InvariantCulture),
            CultiLogArgType.Boolean => BooleanValue ? "true" : "false",
            _ => "null"
        };
    }

    internal void AppendJsonValue(StringBuilder sb)
    {
        switch (Type)
        {
            case CultiLogArgType.String:
                CultiLogJson.AppendQuoted(sb, StringValue);
                break;
            case CultiLogArgType.Integer:
                sb.Append(IntegerValue.ToString(CultureInfo.InvariantCulture));
                break;
            case CultiLogArgType.Float:
                sb.Append(FloatValue.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case CultiLogArgType.Boolean:
                sb.Append(BooleanValue ? "true" : "false");
                break;
            default:
                sb.Append("null");
                break;
        }
    }
}
