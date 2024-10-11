using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLib.Components;

[StructLayout(LayoutKind.Explicit)]
public struct Rotation : IComponent
{
    [FieldOffset(0)]  public Quaternion value;
    [FieldOffset(0)]  public float      x;
    [FieldOffset(4)]  public float      y;
    [FieldOffset(8)]  public float      z;
    [FieldOffset(12)] public float      w;

    public Rotation()
    {
    }

    public Rotation(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public Rotation(Quaternion value)
    {
        this.value = value;
    }
}