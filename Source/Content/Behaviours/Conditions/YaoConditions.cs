using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Content.Const;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>角色拥有妖修体系。</summary>
public class CondHasYao : BehaviourActorCondition
{
    /// <summary>检查角色是否已经启灵。</summary>
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().HasCultisys<Yao>();
    }
}

/// <summary>妖修存在可调度的进阶过渡。</summary>
public class CondYaoCanProgress : BehaviourActorCondition
{
    /// <summary>直接查询妖修体系；不使用按注册顺序重选体系的通用行为。</summary>
    public override bool check(Actor pActor)
    {
        ActorExtend extend = pActor.GetExtend();
        if (!extend.HasCultisys<Yao>()) return false;
        ref Yao yao = ref extend.GetCultisys<Yao>();
        return Cultisyses.Yao.CanScheduleProgression(extend);
    }
}

/// <summary>妖力偏低，需要灵气地点休整。</summary>
public class CondYaoPowerLow : BehaviourActorCondition
{
    /// <summary>妖力低于上限三成时考虑休整。</summary>
    public override bool check(Actor pActor)
    {
        ActorExtend extend = pActor.GetExtend();
        if (!extend.HasCultisys<Yao>()) return false;
        float maximum = pActor.stats[BaseStatses.MaxYaoPower.id];
        return maximum > 0f && extend.GetCultisys<Yao>().yao_power < maximum * 0.3f;
    }
}
