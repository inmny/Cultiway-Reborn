using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>让旧技能和人物运动查询当前已经确认的真实屏障。</summary>
internal interface ISkillBarrierBridge
{
    /// <summary>是否有需要阻挡地面人物的实际屏障。</summary>
    bool HasGroundBarriers { get; }

    /// <summary>在落点、行进附加作用及人物伤害之前拦截本段路径。</summary>
    /// <param name="entity">已经完成轨迹更新的旧执行体。</param>
    void Intercept(Entity entity);

    /// <summary>人物正常移动在本轮真实屏障前停下。</summary>
    /// <param name="actor">当前人物。</param><param name="from">前次位置。</param><param name="to">本次位置。</param>
    Vector2 RestrictGroundActor(Actor actor, Vector2 from, Vector2 to);
}
