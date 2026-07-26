using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>在原版角色显示坐标与 Skill 动画 ECS 坐标之间转换。</summary>
public static class SkillVisualCoordinates
{
    /// <summary>
    /// 把 y 中已经包含飞行高度的角色显示坐标拆成地面 y 与独立 z，
    /// 避免通用动画渲染器再次叠加高度。
    /// </summary>
    public static Vector3 FromActor(Actor actor)
    {
        Vector3 position = actor.cur_transform_position;
        position.y -= position.z;
        return position;
    }
}
