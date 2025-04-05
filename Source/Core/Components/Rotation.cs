using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

[StructLayout(LayoutKind.Explicit)]
public struct Rotation : IComponent
{
    [Ignore]
    [FieldOffset(0)] public Vector3 value;
    [Ignore]
    [FieldOffset(0)] public Vector2 in_plane;
    [FieldOffset(0)] public float   x;
    [FieldOffset(4)] public float   y;
    [FieldOffset(8)] public float   z;

    public Rotation()
    {
    }

    public void Setup(BaseSimObject start, BaseSimObject end, Vector3 offset = default)
    {
        in_plane = end.current_position - start.current_position;
        z = end.getHeight()                 - start.getHeight();
        value += offset;
    }

    public Rotation(Vector2 plane_value, float z)
    {
        in_plane = plane_value;
        this.z = z;
    }

    public Rotation(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Rotation(Vector3 value)
    {
        this.value = value;
    }
}