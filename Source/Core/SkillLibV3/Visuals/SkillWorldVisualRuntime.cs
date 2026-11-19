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
        // 场域及范围命中已有随真实执行体绘制的主体，不再叠加一圈通用描线。
        if (item.Kind != SkillWorldVisualEventKind.EffectResolved) return;
        ShowLocal(item.Profile, item.Position);
    }

    /// <summary>从已经确认发生变化的位置发射，不补画整个作用范围。</summary>
    /// <param name="profile">实际结果对应的材质表现。</param><param name="position">发生变化的位置。</param>
    internal static void ShowLocal(SkillWorldVisualProfile profile, Vector3 position)
    {
        SkillLocalEffectVisualKind kind = profile.LocalEffect;
        SpatialParticleMaterial material = kind switch
        {
            SkillLocalEffectVisualKind.Healing or SkillLocalEffectVisualKind.Rejuvenation or
                SkillLocalEffectVisualKind.NatureGrowth or SkillLocalEffectVisualKind.Fertilize => SpatialParticleMaterial.Leaf,
            SkillLocalEffectVisualKind.RaiseTerrain or SkillLocalEffectVisualKind.LowerTerrain or
                SkillLocalEffectVisualKind.GuardBlessing or SkillLocalEffectVisualKind.CleanLand => SpatialParticleMaterial.Fragment,
            SkillLocalEffectVisualKind.FillWater or SkillLocalEffectVisualKind.DrainWater => SpatialParticleMaterial.Water,
            _ => SpatialParticleMaterial.Energy,
        };
        bool down = kind is SkillLocalEffectVisualKind.LowerTerrain or SkillLocalEffectVisualKind.DrainWater;
        bool inward = down || kind is SkillLocalEffectVisualKind.Healing or SkillLocalEffectVisualKind.Purification;
        float lift = down ? -.35f : material == SpatialParticleMaterial.Leaf ? .7f : .5f;
        float gravity = material is SpatialParticleMaterial.Fragment or SpatialParticleMaterial.Water ? -1.4f : 0f;
        int count = material is SpatialParticleMaterial.Fragment or SpatialParticleMaterial.Water ? 2 : 4;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.3999632f + position.x * .8f + position.y * 1.3f;
            Vector3 side = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 start = position + side * (inward ? .21f : .04f) + Vector3.forward * (down ? .22f : .04f);
            Vector3 velocity = side * (inward ? -.45f : .45f) + Vector3.forward * lift;
            SpatialVisualEffects.Particle(start, velocity, i == 0 && material == SpatialParticleMaterial.Energy
                    ? profile.GlowColor : profile.PrimaryColor,
                material == SpatialParticleMaterial.Energy ? .06f : .14f, down ? .29f : .45f, material, gravity);
        }
    }

    /// <summary>按实际显示视点省略装饰。</summary>
    /// <param name="position">世界位置。</param><param name="radius">外缘。</param>
    public static bool IsVisible(Vector3 position, float radius = 0f) => SpatialVisualEffects.IsRelevant(position, radius);

    /// <summary>世界切换不重放旧结果，也不遗留旧粒子。</summary>
    public static void ClearWorldState()
    {
        delayed.Clear();
        SkillParticleEmitter.ClearWorldState();
        SpatialVisualEffects.ClearWorldState();
    }
}
