using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Components;

[StructLayout(LayoutKind.Explicit)]
public struct SkillTargetPos : IComponent
{
    [FieldOffset(0)] public Vector3 v3;
    [FieldOffset(0)] public Vector2 v2;
    [FieldOffset(8)] public float   z;

    public void Setup(BaseSimObject target, Vector3 offset = default)
    {
        v2 = target.currentPosition;
        z = target.getZ();
        v3 += offset;
    }

    public override string ToString()
    {
        return v3.ToString();
    }
}