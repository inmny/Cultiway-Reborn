using System;
using Friflo.Engine.ECS;
using NeoModLoader.General;

namespace Cultiway.Core.Components;

public struct ItemLevel : IComponent
{
    public int Stage;
    public int Level;

    /// <summary>
    /// 从连续等级值创建品级，值域限制为 ItemLevel 当前支持的 0-35。
    /// </summary>
    public static ItemLevel FromValue(int value)
    {
        value = Math.Max(0, Math.Min(value, 35));
        return new ItemLevel
        {
            Stage = value / 9,
            Level = value % 9
        };
    }

    public string GetName()
    {
        var stage = Math.Max(0, Math.Min(Stage, 3));
        var level = Math.Max(0, Math.Min(Level, 8));
        return LM.Get($"Cultiway.Stage.{stage}") + "阶" + LM.Get($"Cultiway.Level.{level}");
    }

    public static implicit operator int(ItemLevel level)
    {
        return level.Stage * 9 + level.Level;
    }
}
