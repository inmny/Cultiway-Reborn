using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

[StructLayout(LayoutKind.Explicit)]
public struct Position : IComponent
{
    [Ignore]
    [FieldOffset(0)] public Vector3 value;
    [Ignore]
    [FieldOffset(0)] public Vector2 v2;
    [FieldOffset(0)] public float   x;
    [FieldOffset(4)] public float   y;
    [FieldOffset(8)] public float   z;

    public Position()
    {
    }

    public Position(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Position(Vector3 value)
    {
        this.value = value;
    }

    public override string ToString()
    {
        return value.ToString();
    }
}