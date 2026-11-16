using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>把人物与环境地点转换成同一份实际施放输入，不制造代替地点的人物。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>在正式使用边界解析选择，人物采用共同时间姿态，地点保持受理时的位置。</summary>
    /// <param name="caster">实际施法者。</param>
    /// <param name="plan">已经编译的选择与作用规则。</param>
    /// <param name="target">统一主动入口传入的选择。</param>
    /// <param name="actor">需要跟随的真实人物，地点为空。</param>
    /// <param name="point">共同地图坐标及离地高度。</param>
    /// <param name="height">人物实际体高，地点为零。</param>
    private static bool TryResolveUseTarget(ActorExtend caster, CompiledAbility plan,
        in ActiveAbilityTarget target, out Actor actor, out Vector3 point, out float height)
    {
        actor = null;
        point = target.Position;
        height = 0f;
        ActiveAbilityTargetMode mode = plan.Use.Target;
        if (mode == ActiveAbilityTargetMode.Self)
        {
            if (caster == null) return false;
            actor = caster.Base;
        }
        else if (mode == ActiveAbilityTargetMode.Object ||
                 mode == ActiveAbilityTargetMode.ObjectOrPoint && target.Object != null)
        {
            if (target.Object == null || target.Object.isRekt() || !target.Object.isActor()) return false;
            actor = target.Object.a;
            if (!actor.isAlive() || !actor.hasHealth() ||
                (caster != null && !SkillTargetRelationResolver.Matches(plan.Impact.Targets, caster.Base, actor, target.AttackKingdom) ||
                 caster == null && !plan.Impact.SelectedTargetOnly)) return false;
        }

        if (actor != null)
        {
            StartingPose(actor, out point, out height);
            return true;
        }

        // 地点可没有活体；地图边界和非数值只在接受外部选择时检查。
        return !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
               !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z) &&
               World.world.GetTile(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y)) != null;
    }
}
