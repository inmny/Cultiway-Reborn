using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 解析光束、连锁和墙体等线性法术的视觉缩放，避免作用范围倍率在长度轴上重复生效。
/// </summary>
public static class SkillLinearVisual
{
    public static Vector3 ResolveScale(Entity skillEntity, float length, Vector3 scaledBase)
    {
        float frameWidth = skillEntity.GetComponent<AnimData>().CurrentFrame.bounds.size.x;
        return new Vector3(
            Mathf.Max(0.1f, length) / frameWidth,
            scaledBase.y,
            scaledBase.z);
    }

    public static void Apply(Entity skillEntity, float length, Vector3 scaledBase)
    {
        ref AnimLinearLayout layout = ref skillEntity.GetComponent<AnimLinearLayout>();
        layout.WorldLength = Mathf.Max(0.1f, length);

        ref Scale scale = ref skillEntity.GetComponent<Scale>();
        scale.value = layout.Mode == AnimLinearLayoutMode.Tile
            ? scaledBase
            : ResolveScale(skillEntity, layout.WorldLength, scaledBase);
    }
}
