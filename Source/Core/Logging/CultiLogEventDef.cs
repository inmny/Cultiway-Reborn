using System;

namespace Cultiway.Core.Logging;

public sealed class CultiLogEventDef
{
    public readonly int Id;
    public readonly string Name;
    public readonly CultiLogCategory Category;
    public readonly CultiLogLevel Level;
    public readonly string Template;

    public CultiLogEventDef(int id, string name, CultiLogCategory category, CultiLogLevel level, string template)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("日志事件名不能为空", nameof(name));

        Id = id;
        Name = name;
        Category = category;
        Level = level;
        Template = string.IsNullOrEmpty(template) ? "{message}" : template;
        CultiLogEventRegistry.Register(this);
    }
}
