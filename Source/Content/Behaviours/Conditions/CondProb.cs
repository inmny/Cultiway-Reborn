namespace Cultiway.Content.Behaviours.Conditions;

public class CondProb : BehaviourActorCondition
{
    public CondProb(float chance)
    {
        this.chance = chance;
    }

    private float chance;
    public override bool check(Actor pActor)
    {
        return Randy.randomChance(chance);
    }
}