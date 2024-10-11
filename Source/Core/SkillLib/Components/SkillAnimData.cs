using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLib.Components;

public struct SkillAnimData : IComponent
{
    public float          timer;
    public int            idx;
    public SpriteRenderer bind_renderer;
}