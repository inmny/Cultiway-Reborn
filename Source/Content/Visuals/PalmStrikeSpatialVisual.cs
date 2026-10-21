using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>与身体相连的两段手臂、立掌和短气浪，不生成脱离人物飞行的拳影。</summary>
internal static class PalmStrikeSpatialVisual
{
    /// <summary>固定手臂段，局部高度为一。</summary>
    private static readonly SpatialVisualMesh arm = BuildArm();
    /// <summary>能从侧面看出厚度、拇指和四指的立掌。</summary>
    private static readonly SpatialVisualMesh palm = BuildPalm();

    /// <summary>从唯一动作曲线生成身体到掌部的连接，结果反馈只读取实际结算。</summary>
    /// <param name="meshes">两个视角共用的模型快照。</param>
    /// <param name="now">世界时间。</param>
    internal static void AppendMeshes(List<SpatialVisualMeshInstance> meshes, double now)
    {
        foreach (PalmStrikeCombat.Action action in PalmStrikeCombat.Actions.Values)
        {
            int first = meshes.Count;
            AppendAction(action, meshes, now);
            for (int i = first; i < meshes.Count; i++)
                meshes[i] = meshes[i].WithBodyAnchor(action.Caster.Base);
        }
    }

    /// <summary>按一个真实动作采样手臂、掌缘与实际接触反馈。</summary>
    /// <param name="action">唯一的玩法动作。</param>
    /// <param name="meshes">本帧共用快照。</param>
    /// <param name="now">世界时间。</param>
    private static void AppendAction(PalmStrikeCombat.Action action, List<SpatialVisualMeshInstance> meshes, double now)
    {
        Actor actor = action.Caster.Base;
        if (actor.isRekt() || actor.hasWeapon()) return;
        float height = SpatialActorPose.Height(actor);
        float age = (float)(now - action.Started);
        float extension = PalmStrikeCombat.Extension(age);
        Vector3 direction = action.Direction;
        Vector3 side = Vector3.Cross(direction, Vector3.forward);
        Vector3 shoulder = SpatialActorPose.Foot(actor) + side * height * 0.16f + Vector3.forward * height * 0.62f;
        Vector3 hand = SpatialActorPose.Hand(actor, direction, PalmStrikeCombat.Reach(actor) * extension);
        Vector3 elbow = Vector3.Lerp(shoulder, hand, 0.5f) + side * height * (1f - extension) * 0.22f -
                        Vector3.forward * height * 0.05f;
        Color sleeve = Color.Lerp(actor.kingdom.getColor().getColorMain(), Color.white, 0.35f);
        AddArm(meshes, shoulder, elbow, height * 0.085f, sleeve);
        AddArm(meshes, elbow, hand, height * 0.07f, new Color(0.89f, 0.86f, 0.78f));
        Quaternion facing = Quaternion.LookRotation(direction, Vector3.forward);
        meshes.Add(new SpatialVisualMeshInstance(palm, hand, facing, Vector3.one * height,
            new Color(0.96f, 0.92f, 0.82f), true));
        float air = age < 0.12f ? 0.15f : age < 0.22f ? 0.55f : (1f - (age - 0.22f) / 0.18f) * 0.25f;
        meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Ring, hand + direction * height * 0.03f,
            facing, Vector3.one * height * (0.07f + extension * 0.08f), new Color(0.9f, 0.93f, 1f, air), true, true));
        if (action.FeedbackAt < 0d) return;
        float feedbackAge = (float)(now - action.FeedbackAt);
        if (feedbackAge > 0.16f) return;
        float fade = 1f - feedbackAge / 0.16f;
        Color color = action.Blocked ? new Color(0.55f, 0.73f, 1f, fade) : new Color(1f, 0.94f, 0.72f, fade);
        meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Ring, action.Contact, facing,
            Vector3.one * height * (0.1f + feedbackAge), color, true, true));
        if (action.Damage > 0f)
            meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Spark, action.Contact, facing,
                Vector3.one * height * 0.15f * fade, color, true, true));
    }

    /// <summary>以现成手臂段连接两个身体挂点，几何不随帧重建。</summary>
    /// <param name="meshes">当前快照。</param>
    /// <param name="start">关节起点。</param>
    /// <param name="end">关节终点。</param>
    /// <param name="width">当前体型下的粗细。</param>
    /// <param name="color">衣袖或缠手颜色。</param>
    private static void AddArm(List<SpatialVisualMeshInstance> meshes, Vector3 start, Vector3 end, float width, Color color)
    {
        Vector3 delta = end - start;
        meshes.Add(new SpatialVisualMeshInstance(arm, start, Quaternion.FromToRotation(Vector3.forward, delta),
            new Vector3(width, width, delta.magnitude), color, true));
    }

    /// <summary>共用的短四面手臂。</summary>
    private static SpatialVisualMesh BuildArm()
    {
        SpatialVisualMeshBuilder builder = new();
        SpatialVisualGeometry.AddRod(builder, Vector3.zero, Vector3.forward, 1f, Color.white);
        return builder.Build("palm_strike_arm");
    }

    /// <summary>用少量硬面建立缠手立掌，保留指缝与拇指，避免一团发光球。</summary>
    private static SpatialVisualMesh BuildPalm()
    {
        SpatialVisualMeshBuilder builder = new();
        AddBox(builder, new Vector3(0f, 0.015f, 0f), new Vector3(0.095f, 0.11f, 0.045f));
        for (int i = 0; i < 4; i++)
            AddBox(builder, new Vector3((i - 1.5f) * 0.025f, 0.105f - Mathf.Abs(i - 1f) * 0.006f, 0f),
                new Vector3(0.02f, 0.085f - Mathf.Abs(i - 1f) * 0.012f, 0.035f));
        AddBox(builder, new Vector3(-0.064f, 0.015f, 0.005f), new Vector3(0.035f, 0.055f, 0.04f));
        return builder.Build("palm_strike_hand");
    }

    /// <summary>加入一个有厚度的掌部块面。</summary>
    /// <param name="builder">掌部模型。</param>
    /// <param name="center">局部中心。</param>
    /// <param name="size">三个方向的尺寸。</param>
    private static void AddBox(SpatialVisualMeshBuilder builder, Vector3 center, Vector3 size)
    {
        Vector3[] v = new Vector3[8];
        for (int i = 0; i < 8; i++) v[i] = center + Vector3.Scale(size * 0.5f,
            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
        int[,] faces = { {0, 4, 6, 2}, {1, 3, 7, 5}, {0, 1, 5, 4}, {2, 6, 7, 3}, {0, 2, 3, 1}, {4, 5, 7, 6} };
        for (int i = 0; i < 6; i++) builder.AddFace(new[] { v[faces[i, 0]], v[faces[i, 1]], v[faces[i, 2]], v[faces[i, 3]] },
            Color.Lerp(new Color(0.75f, 0.75f, 0.75f), Color.white, i / 5f));
    }
}
