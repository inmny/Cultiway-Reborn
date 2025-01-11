using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Components;

[StructLayout(LayoutKind.Explicit)]
public struct ColliderBox : IComponent
{
    [FieldOffset(0)] public float   x_half;
    [FieldOffset(4)] public float   y_half;
    [FieldOffset(8)] public float   z_half;
    [Ignore]
    [FieldOffset(0)] public Vector3 v3;
    [Ignore]
    [FieldOffset(0)] public Vector2 v2;
}