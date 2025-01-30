using System;
using Friflo.Engine.ECS;
using NeoModLoader.General;

namespace Cultiway.Core.Components;

public struct ItemLevel : IComponent
{
    public int Stage;
    public int Level;

    public string GetName()
    {
        var stage = Math.Max(0, Math.Min(Stage, 3));
        var level = Math.Max(0, Math.Min(Level, 8));
        return LM.Get($"Cultiway.Stage.{stage}") + "阶" + LM.Get($"Cultiway.Level.{level}");
    }
}