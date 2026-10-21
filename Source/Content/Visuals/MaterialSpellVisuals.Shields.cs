using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Visuals;

internal static partial class MaterialSpellVisuals
{
    /// <summary>毒种在真正的受术者身上开绽，不画成一条未曾移动的藤蔓。</summary>
    /// <param name="output">共同模型。</param><param name="context">实际目标。</param>
    /// <param name="age">施放年龄。</param><param name="fade">生命周期透明度。</param>
    internal static void PoisonBloom(List<SpatialVisualMeshInstance> output, SkillContext context, float age, float fade)
    {
        Vector3 position = context.TargetObj != null && !context.TargetObj.isRekt()
            ? context.TargetObj.GetSimPos() : context.TargetPos;
        float size = context.TargetObj?.isActor() == true ? Mathf.Max(.25f, context.TargetObj.a.actor_scale * 1.5f) : .4f;
        float opening = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .2f));
        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.PI * 2 / 7;
            Vector3 offset = new(Mathf.Cos(angle), Mathf.Sin(angle), .35f);
            SpatialVisualDraw.Mesh(output, SpatialVisualGeometry.Leaf, position + offset * size * opening * .35f,
                Quaternion.Euler(35 + opening * 45, 0, angle * Mathf.Rad2Deg),
                new Vector3(.65f, .3f, .9f) * size * opening,
                Color.Lerp(new Color(.3f, .65f, .13f, fade), new Color(.55f, .12f, .5f, fade), i % 3 / 2f), false);
        }
        SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Orb, position + Vector3.forward * size * .25f,
            Quaternion.identity, Vector3.one * size * .22f * opening, new Color(.5f, .18f, .48f, fade), false);
    }

    /// <summary>护盾按材质形成不同结构，共用真实护持半径与身体位置。</summary>
    /// <param name="output">共同模型。</param><param name="skill">真实护盾。</param>
    /// <param name="position">身体位置。</param><param name="radius">护持半径。</param>
    /// <param name="age">施放年龄。</param><param name="fade">生命周期透明度。</param>
    internal static void Shield(List<SpatialVisualMeshInstance> output, SkillEntity skill, Vector3 position,
        float radius, float age, float fade)
    {
        Vector3 center = position + Vector3.forward * radius * .13f;
        if (skill.VfxElement == SkillVfxElements.Fire)
        {
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9 + age * .18f;
                Vector3 foot = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius * .78f;
                float flicker = 1 + Mathf.Sin(age * 11 + i * 2.4f) * .16f;
                SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Flame, foot, Quaternion.Euler(0, -14, angle * Mathf.Rad2Deg),
                    new Vector3(.4f, .33f, 1.15f * flicker) * radius, new Color(1, .42f, .08f, fade * .72f), true,
                    additive: false, surface: SpatialSurface.Flame);
            }
        }
        else if (skill.VfxElement == SkillVfxElements.Lightning)
        {
            for (int i = 0; i < 6; i++)
            {
                float turn = i * Mathf.PI / 3;
                Vector3 start = center + new Vector3(Mathf.Cos(turn), Mathf.Sin(turn), .08f) * radius * .9f;
                Vector3 end = center + new Vector3(Mathf.Cos(turn + 1), Mathf.Sin(turn + 1), .65f) * radius * .66f;
                int frame = (i + Mathf.FloorToInt(age * 8)) % 8;
                SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Bolts[frame], start,
                    Quaternion.FromToRotation(Vector3.forward, end - start), new Vector3(radius * .09f, radius * .09f, (end - start).magnitude),
                    new Color(.55f, .77f, 1, fade * .85f), true);
            }
            Plates(new Color(.3f, .48f, 1, fade * .07f), radius * .93f, age * 8);
        }
        else if (skill.VfxElement == SkillVfxElements.Wind)
        {
            for (int i = 0; i < 4; i++)
                SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Stream, center,
                    Quaternion.Euler(50, 0, -age * 85 + i * 90), new Vector3(.8f, .8f, 1.3f) * radius,
                    new Color(.66f, .9f, .78f, fade * .25f), true, additive: false, surface: SpatialSurface.Energy);
        }
        else if (skill.VfxElement == SkillVfxElements.Earth)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Rocks[i % 4],
                    position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .15f) * radius * .7f,
                    Quaternion.Euler(0, -25, angle * Mathf.Rad2Deg), new Vector3(.25f, .5f, .65f) * radius,
                    new Color(1, 1, 1, fade), false);
            }
        }
        else Plates(new Color(.5f, .75f, 1, fade * .2f), radius, age * 4);

        void Plates(Color color, float size, float turn)
        {
            for (int i = 0; i < 6; i++)
                SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.ShieldPetal, center,
                    Quaternion.Euler(0, 0, i * 60 + turn), Vector3.one * size, color, true,
                    additive: false, surface: SpatialSurface.Energy);
        }
    }
}
