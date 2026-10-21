using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>消费真实结算事件，生成可在原版和空间视角中共用的反馈。</summary>
internal static class SkillWorldVisualRuntime
{
    /// <summary>净化传播等短延迟事件。</summary>
    private static readonly List<(SkillWorldVisualEvent value, double at)> delayed = new();
    /// <summary>累计的世界时间只来自游戏，显示帧和真实时间不参与计时。</summary>
    private static double Now => World.world.getCurWorldTime();

    /// <summary>消费已有结算队列，不创建第二份场域生命周期。</summary>
    /// <param name="deltaTime">保留现有系统调用约定，计时使用世界时钟。</param>
    public static void Update(float deltaTime)
    {
        while (SkillWorldVisualService.TryDequeue(out SkillWorldVisualEvent item))
        {
            if (item.Delay > 0f) { if (delayed.Count < 512) delayed.Add((item, Now + item.Delay)); }
            else Show(item);
        }
        for (int i = delayed.Count - 1; i >= 0; i--)
            if (delayed[i].at <= Now) { Show(delayed[i].value); delayed.RemoveAt(i); }
    }

    /// <summary>将一次真实结果组合成局部叶片、碎屑或范围波纹。</summary>
    /// <param name="item">已确认发生的作用。</param>
    private static void Show(SkillWorldVisualEvent item)
    {
        SkillWorldVisualProfile profile = item.Profile;
        // 场域及范围命中已有随真实执行体绘制的主体，不再叠加一圈通用描线。
        if (item.Kind is SkillWorldVisualEventKind.BeginField or SkillWorldVisualEventKind.AreaResolved) return;
        bool leaf = profile.LocalEffect is SkillLocalEffectVisualKind.Healing or SkillLocalEffectVisualKind.Rejuvenation or
            SkillLocalEffectVisualKind.NatureGrowth or SkillLocalEffectVisualKind.Fertilize;
        bool inward = profile.LocalEffect is SkillLocalEffectVisualKind.Healing or SkillLocalEffectVisualKind.Purification or
            SkillLocalEffectVisualKind.LowerTerrain or SkillLocalEffectVisualKind.DrainWater;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * .5f;
            Vector3 side = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 start = item.Position + side * (inward ? .24f : .04f);
            Vector3 velocity = side * (inward ? -.45f : .45f) + Vector3.forward * (leaf ? .7f : .4f);
            SpatialVisualEffects.Particle(start, velocity, i == 0 ? profile.GlowColor : profile.PrimaryColor,
                leaf ? .1f : .06f, .45f, leaf, leaf ? 0f : -1f);
        }
    }

    /// <summary>按实际显示视点省略装饰。</summary>
    /// <param name="position">世界位置。</param><param name="radius">外缘。</param>
    public static bool IsVisible(Vector3 position, float radius = 0f) => SpatialVisualEffects.IsRelevant(position, radius);

    /// <summary>世界切换不重放旧结果，也不遗留旧粒子。</summary>
    public static void ClearWorldState() { delayed.Clear(); SpatialVisualEffects.ClearWorldState(); }
}
