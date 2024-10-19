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

    public void Setup(BaseSimObject target)
    {
        v2 = target.currentPosition;
        z = target.getZ();
    }
}