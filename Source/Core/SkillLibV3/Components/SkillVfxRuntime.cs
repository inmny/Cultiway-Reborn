using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components;

public struct SkillVfxRuntime : IComponent
{
    public SkillVfxElementStyle Style;
    public Color Color;
    public Color AccentColor;
    public float Intensity;
    public float NextTrailTime;
    public float TrailWidth;
}
