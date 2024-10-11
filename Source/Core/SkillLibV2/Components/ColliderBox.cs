using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Components;

[StructLayout(LayoutKind.Explicit)]
public struct ColliderBox : IComponent
{
    [FieldOffset(0)] public float   x_half;
    [FieldOffset(4)] public float   y_half;
    [FieldOffset(8)] public float   z_half;
    [FieldOffset(0)] public Vector3 v3;
    [FieldOffset(0)] public Vector2 v2;
}