using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>从实际人物帧与体型取得身体、手部的显示尺度，不改变角色位置和碰撞。</summary>
internal static class SpatialActorPose
{
    /// <summary>取得实际身体脚下位置，排除只用于命中计算的额外高度。</summary>
    /// <param name="actor">当前角色。</param>
    internal static Vector3 Foot(Actor actor) => new(actor.current_position.x, actor.current_position.y, actor.position_height);

    /// <summary>以站立帧的头部位置估计身体尺度，避免走动或切帧使接触距离来回改变。</summary>
    /// <param name="actor">当前有效角色。</param>
    internal static float Height(Actor actor)
    {
        AnimationFrameData frame = actor.getAnimationFrameData();
        AnimationContainerUnit container = actor.animation_container;
        if (container?.has_idle == true && container.idle.frames.Length > 0 &&
            container.dict_frame_data.TryGetValue(container.idle.frames[0].name, out AnimationFrameData standing))
            frame = standing;
        float scale = Mathf.Max(0.03f, actor.actor_scale);
        return Mathf.Max(scale * 5f, (frame == null ? 6f : frame.pos_head.y) * scale + scale * 2f);
    }

    /// <summary>取得不包含二维纵向投影的手部近似点。</summary>
    /// <param name="actor">当前有效角色。</param>
    /// <param name="direction">出手方向。</param>
    /// <param name="extension">由身体向前伸出的距离。</param>
    internal static Vector3 Hand(Actor actor, Vector3 direction, float extension)
    {
        float height = Height(actor);
        Vector3 side = Vector3.Cross(direction, Vector3.forward);
        return Foot(actor) + direction * extension + side * height * 0.12f +
               Vector3.forward * height * 0.6f;
    }
}
