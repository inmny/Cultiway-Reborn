using System;
using System.Text;

namespace Cultiway.Core.Logging;

internal static class CultiLogJson
{
    public static void AppendRecord(StringBuilder sb, CultiLogRecord record, bool renderMessage)
    {
        sb.Append('{');
        AppendProperty(sb, "seq", record.Sequence).Append(',');
        AppendProperty(sb, "ticks", record.RealTicks).Append(',');
        AppendProperty(sb, "worldTime", record.WorldTime).Append(',');
        AppendProperty(sb, "level", record.Level.ToString()).Append(',');
        AppendProperty(sb, "category", record.Category.ToString()).Append(',');
        AppendProperty(sb, "eventId", record.EventId).Append(',');
        AppendProperty(sb, "event", record.EventName).Append(',');
        AppendProperty(sb, "actorId", record.ActorId).Append(',');
        AppendProperty(sb, "targetId", record.TargetId).Append(',');
        AppendProperty(sb, "entityId", record.EntityId).Append(',');
        AppendProperty(sb, "x", record.X).Append(',');
        AppendProperty(sb, "y", record.Y).Append(',');
        AppendProperty(sb, "template", record.Template).Append(',');
        sb.Append("\"args\":{");

        CultiLogArg[] args = record.Args ?? Array.Empty<CultiLogArg>();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(',');
            AppendQuoted(sb, args[i].Key);
            sb.Append(':');
            args[i].AppendJsonValue(sb);
        }

        sb.Append('}');
        if (renderMessage)
        {
            sb.Append(',');
            AppendProperty(sb, "message", record.RenderMessage());
        }

        sb.Append('}');
    }

    public static StringBuilder AppendProperty(StringBuilder sb, string key, string value)
    {
        AppendQuoted(sb, key);
        sb.Append(':');
        AppendQuoted(sb, value);
        return sb;
    }

    public static StringBuilder AppendProperty(StringBuilder sb, string key, long value)
    {
        AppendQuoted(sb, key);
        sb.Append(':').Append(value);
        return sb;
    }

    public static StringBuilder AppendProperty(StringBuilder sb, string key, float value)
    {
        AppendQuoted(sb, key);
        sb.Append(':').Append(value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        return sb;
    }

    public static void AppendQuoted(StringBuilder sb, string value)
    {
        if (value == null)
        {
            sb.Append("null");
            return;
        }

        sb.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (ch < ' ')
                    {
                        sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
