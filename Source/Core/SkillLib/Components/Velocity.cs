using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLib.Components;

public struct Velocity : IComponent
{
    public Vector3 scale = Vector3.one;

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