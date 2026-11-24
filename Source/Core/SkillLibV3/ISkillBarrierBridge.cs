using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>让旧技能和人物运动查询当前已经确认的真实屏障。</summary>
internal interface ISkillBarrierBridge
{
    /// <summary>是否有需要阻挡地面人物的实际屏障。</summary>
    bool HasGroundBarriers { get; }

    /// <summary>人物正常移动在本轮真实屏障前停下。</summary>
    /// <param name="actor">当前人物。</param><param name="from">前次位置。</param><param name="to">本次位置。</param>
    Vector2 RestrictGroundActor(Actor actor, Vector2 from, Vector2 to);
}
