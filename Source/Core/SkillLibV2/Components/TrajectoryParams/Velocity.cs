using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
[StructLayout(LayoutKind.Explicit)]
public struct Velocity : IComponent
{
    [FieldOffset(0)]
    public Vector3 scale = Vector3.one;
    [FieldOffset(0)]
    public Vector2 scale2 = Vector2.one;
    public Velocity()
    {
    }

    public Velocity(float value)
    {
        scale = Vector3.one * value;
    }

    public Velocity(float x, float y, float z)
    {
        scale = new(x, y, z);
    }

    public Velocity(Vector3 scale)
    {
        this.scale = scale;
    }
}