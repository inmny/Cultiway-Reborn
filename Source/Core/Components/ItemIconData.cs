using System;
using System.Collections.Generic;
using System.Text;
using Friflo.Engine.ECS;
using Newtonsoft.Json;

namespace Cultiway.Core.Components;

public struct ItemIconData : IComponent, IEquatable<ItemIconData>
{
    [JsonProperty("color_hex_1")] public string ColorHex1;
    [JsonProperty("color_hex_2")] public string ColorHex2;
    [JsonProperty("color_hex_3")] public string ColorHex3;
    [JsonProperty("decorations")] public List<string> Decorations;
    
    public override int GetHashCode()
    {
        StringBuilder sb = new StringBuilder();
        if (ColorHex1 != null) sb.Append(ColorHex1);
        if (ColorHex2 != null) sb.Append(ColorHex2);
        if (ColorHex3 != null) sb.Append(ColorHex3);
        if (Decorations != null)
            foreach (var decoration in Decorations)
            {
                sb.Append(decoration);
            }

        return sb.ToString().GetHashCode();
    }

    public bool Equals(ItemIconData other)
    {
        return ColorHex1 == other.ColorHex1 && ColorHex2 == other.ColorHex2 && ColorHex3 == other.ColorHex3 && Equals(Decorations, other.Decorations);
    }

    public override bool Equals(object obj)
    {
        return obj is ItemIconData other && Equals(other);
    }
}