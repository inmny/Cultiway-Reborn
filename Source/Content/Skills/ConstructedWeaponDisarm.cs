using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>技能只请求真实脱手，原物品的地面状态和后续拾取由器物玩法维护。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>先核对真实持有和对抗，再提交器物状态，最后中止旧借用及发布本次结果。</summary>
    /// <param name="hit">短段复核后的真实器物接触。</param><param name="rule">显式脱手关系。</param>
    private static void ResolveWeaponDisarm(in ContactChoice hit, in CompiledBodyRelation rule)
    {
        RunningCast source = hit.Left.Running;
        Actor target = hit.Actor.Actor;
        RunningCast holder = null;
        foreach (RunningCast candidate in casts)
            if (candidate.SourceId == hit.Actor.Id && candidate.BodyControlled && candidate.Body.Weapon != null)
            { holder = candidate; break; }
        string reason = !source.BodyControlled || source.Cast.Finished || target.data?.id != hit.Actor.Id ||
            !target.isAlive() || !target.hasHealth() ? "InvalidBody" :
            holder == null && BodyControlState.IsBusy(target.id, BodyOccupancy.RightArm) ? "Occupied" :
            rule.ControlStrength * target.GetExtend().ForceResponseScale(source.Source,
                source.InitialOwner.Damage.AttackerPowerLevel) < 1f ? "Resistance" : null;
        if (reason == null)
        {
            Vector3 velocity = hit.Left.SurfaceVelocity(hit.Point, hit.Normal, hit.Fraction, hit.Seconds);
            if (GroundWeaponService.TryDrop(target, hit.BodySurface, velocity, LogicTime, holder?.Cast.Plan.Body.WeaponForm, out reason))
            {
                // 先真实解除装备，再撤销原施放的身体借用；独立法术仍按自己的过程运行。
                foreach (RunningCast candidate in casts)
                    if (candidate.SourceId == target.id && candidate.BodyControlled && candidate.Body.Weapon != null)
                    { candidate.Cast.InterruptBody(); ReleaseBody(candidate); }
                NotifyActorChanged(target);
                source.Cast.ConfirmContact(-1, 1f, hit.Point, target.id);
                ConfirmBodyFeedback(source, hit.Point);
                reason = "Disarmed";
            }
        }
        BodyConnectionChanged?.Invoke(new ConstructedBodyConnection(source.Id, source.SourceId, hit.Actor.Id,
            rule, reason == "Disarmed" ? ConstructedBodyConnectionState.Released : ConstructedBodyConnectionState.Rejected,
            hit.Point, hit.Point, LogicTime, LogicTime, 0f, reason));
    }
}
