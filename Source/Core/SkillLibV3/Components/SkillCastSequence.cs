using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

public struct SkillCastSequence : IComponent
{
    public ActorExtend Caster;
    public Entity SkillContainer;
    public SkillCastStep[] Steps;
    public int NextIndex;
    public float Elapsed;
    public float Strength;
    public float PowerLevel;
    public int MaxEmitPerTick;
}
