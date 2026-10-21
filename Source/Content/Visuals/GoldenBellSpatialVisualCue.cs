using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>金钟的空间表现：真实钟体、受击和收散共用现有法宝视觉生命周期。</summary>
internal sealed class GoldenBellSpatialVisualCue : IArtifactVisualCue
{
    /// <summary>持续护罩与短时受击分别由既有循环和信号创建。</summary>
    private readonly bool impact;
    /// <summary>仍在显示的持续效果和短暂收尾。</summary>
    private static readonly List<Entry> entries = new();
    /// <summary>选择持续护罩或一次真实吸收的局部反馈。</summary>
    /// <param name="impact">是否为受击信号。</param>
    internal GoldenBellSpatialVisualCue(bool impact) => this.impact = impact;

    /// <summary>受理现有视觉声明；不开辟施法、支付或伤害入口。</summary>
    /// <param name="context">能力的实际运行快照。</param>
    /// <param name="now">世界时间。</param>
    /// <param name="duration">短时信号的显示时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        if (!ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact, out SpatialVisualMeshInstance pose))
            return ArtifactEmptyVisualLease.Instance;
        Entry entry = new(context, pose, impact, now, duration);
        if (entry.Broken) RemoveQuietEnding(context);
        entries.Add(entry);
        if (impact || now - context.runtime.activity_started_at < 0.25d)
            CombatVisualAudio.PlayBell(entry.Center, entry.Broken ? 0.9f : impact ? 0.45f : 0.65f);
        return entry;
    }

    /// <summary>最后一次吸收已耗尽护盾，取消同一能力的安静收尾。</summary>
    /// <param name="context">发生耗尽的实际作用。</param>
    private static void RemoveQuietEnding(ArtifactAbilityVisualContext context)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
            if (!entries[i].Impact && entries[i].Context.artifact == context.artifact &&
                entries[i].Context.ability.instance_id == context.ability.instance_id) entries.RemoveAt(i);
    }

    /// <summary>在原版和三维收集前读取同一份效果进度。</summary>
    /// <param name="meshes">本帧共用模型快照。</param>
    /// <param name="now">世界时间。</param>
    /// <param name="detailBudget">全帧剩余装饰数量，主体不占用此预算。</param>
    internal static void AppendMeshes(List<SpatialVisualMeshInstance> meshes, double now, ref int detailBudget)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            float age = (float)(now - entry.Started);
            float closing = entry.ClosingAt >= 0d ? (float)(now - entry.ClosingAt) : -1f;
            if (closing >= 0.32f || entry.Impact && age >= entry.Duration)
            {
                entries.RemoveAt(i);
                continue;
            }
            float fade = closing >= 0f ? 1f - closing / 0.32f : 1f;
            int first = meshes.Count;
            if (entry.Impact)
                DrawImpact(entry, age, meshes, ref detailBudget);
            else
            {
                float opening = Mathf.Clamp01(age / 0.16f);
                float scale = 0.86f + 0.14f * (1f - Mathf.Pow(1f - opening, 3f));
                if (closing >= 0f) scale *= 1f - 0.13f * closing / 0.32f;
                Color color = entry.Tint;
                color.a = (0.08f + 0.10f * entry.Context.intensity) * fade * Mathf.Max(0.3f, opening);
                AddBody(entry, meshes, scale, color);
                if (age < 0.28f)
                {
                    color.a = (1f - age / 0.28f) * 0.7f;
                    if (ArtifactAppearanceRenderer.TryResolveSpatialAnchor(entry.Context.artifact, "mouth", "center", out Vector3 mouth))
                        meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Ring, mouth,
                            Quaternion.identity, Vector3.one * entry.Radius * (0.3f + age * 2.5f), color,
                            true, true, entry.Context.artifact.GetComponent<Position>().value));
                }
                Actor actor = entry.Context.controller.GetComponent<ActorBinder>().Actor;
                for (int n = first; n < meshes.Count; n++)
                    if (meshes[n].GroundAnchor == (Vector2)entry.Center) meshes[n] = meshes[n].WithBodyAnchor(actor);
            }
        }
    }

    /// <summary>绘制方向明确的一次接触；破裂时另加少量有界碎屑。</summary>
    /// <param name="entry">已确认发生吸收的信号。</param>
    /// <param name="age">此次反馈经过的世界时间。</param>
    /// <param name="meshes">当前模型快照。</param>
    /// <param name="detailBudget">剩余装饰数量。</param>
    private static void DrawImpact(Entry entry, float age, List<SpatialVisualMeshInstance> meshes, ref int detailBudget)
    {
        float progress = Mathf.Clamp01(age / entry.Duration);
        Color color = Color.Lerp(entry.Tint, Color.white, 0.2f);
        color.a = (1f - progress) * Mathf.Clamp(0.4f + entry.Context.intensity, 0.4f, 1f);
        Vector3 direction = entry.Context.direction;
        bool directed = direction.sqrMagnitude >= 0.001f;
        direction = directed ? direction.normalized : Vector3.zero;
        Vector3 contact = entry.Center + Vector3.forward * entry.Height * 0.6f + direction * entry.Radius;
        Quaternion rotation = directed ? Quaternion.FromToRotation(Vector3.forward, direction) : Quaternion.identity;
        if (directed)
            meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Ring, contact, rotation,
                Vector3.one * entry.Height * (0.09f + progress * 0.35f), color, true, true, entry.Center));
        else
        {
            Color quiet = color;
            quiet.a *= 0.3f;
            AddBody(entry, meshes, 1f + progress * 0.025f, quiet);
        }
        if (directed && age < 0.12f)
            meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Spark, contact, rotation,
                Vector3.one * entry.Height * (0.16f + entry.Context.intensity * 0.1f), color, true, true, entry.Center));
        if (!entry.Broken) return;
        color.a *= 0.65f;
        AddBody(entry, meshes, 1f + progress * 0.2f, color);
        for (int i = 0; i < 8 && detailBudget > 0; i++, detailBudget--)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 offset = new(Mathf.Cos(angle) * entry.Radius * (1f + progress),
                Mathf.Sin(angle) * entry.Radius * (1f + progress),
                entry.Height * (0.4f + 0.35f * Mathf.Sin(i * 2f) + progress * 0.4f - progress * progress));
            meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Spark, entry.Center + offset,
                Quaternion.Euler(i * 37f, progress * 180f, i * 59f),
                Vector3.one * entry.Height * 0.07f, color, true, true, entry.Center));
        }
    }

    /// <summary>保留实际钟形和朝向，把其底部对齐护持者脚下。</summary>
    /// <param name="entry">护罩显示资料。</param>
    /// <param name="meshes">当前模型快照。</param>
    /// <param name="scale">出现或收尾的缩放。</param>
    /// <param name="color">此次显示的透明度。</param>
    private static void AddBody(Entry entry, List<SpatialVisualMeshInstance> meshes, float scale, Color color)
    {
        float size = entry.ModelScale * scale;
        Vector3 origin = entry.Center + Vector3.forward * entry.Height * 0.5f * scale -
                         entry.Orientation * entry.Model.Bounds.center * size;
        meshes.Add(new SpatialVisualMeshInstance(entry.Model, origin, entry.Orientation,
            Vector3.one * size, color, true, false, entry.Center, additive:false, surface:SpatialSurface.Metal));
    }

    /// <summary>为实体钟提供短暂微振，显示变化不回写法宝位置。</summary>
    /// <param name="artifact">实际法宝。</param>
    /// <param name="now">世界时间。</param>
    /// <param name="position">当前显示位置。</param>
    /// <param name="yaw">当前显示角度。</param>
    internal static void ApplyBodyMotion(Entity artifact, double now, ref Vector3 position, ref float yaw)
    {
        float amplitude = 0f;
        foreach (Entry entry in entries)
        {
            if (entry.Context.artifact != artifact) continue;
            float age = (float)(now - entry.Started);
            if (age < 0f || age > 0.32f) continue;
            amplitude += Mathf.Sin(age * 65f) * Mathf.Exp(-age * 13f) * (entry.Impact ? 1f : 0.6f);
        }
        position.x += amplitude * 0.025f;
        yaw += amplitude * 2f;
    }

    /// <summary>切换世界时一并释放循环、收尾与组合模型轮廓。</summary>
    internal static void ClearWorldState()
    {
        entries.Clear();
    }

    /// <summary>既有视觉系统拥有的显示资料；结束后仅保留一次短暂收散。</summary>
    private sealed class Entry : IArtifactVisualLease
    {
        /// <summary>当前能力与配色。</summary>
        internal ArtifactAbilityVisualContext Context;
        /// <summary>实际组合钟体，直接保留封闭表面。</summary>
        internal SpatialVisualMesh Model;
        /// <summary>保持钟形竖直的原始姿态。</summary>
        internal Quaternion Orientation;
        /// <summary>原模型缩放到护持者体型的比例。</summary>
        internal float ModelScale;
        /// <summary>护罩的世界高度和近似半径。</summary>
        internal float Height, Radius;
        /// <summary>护持者脚下的空间位置。</summary>
        internal Vector3 Center;
        /// <summary>从实际法宝材质得到的暖色调。</summary>
        internal Color Tint;
        /// <summary>短时受击与护盾耗尽标记。</summary>
        internal readonly bool Impact;
        /// <summary>来自作用时刻的不可变结果，不从之后被清空的容量推断。</summary>
        internal bool Broken;
        /// <summary>实际开始时间和反馈时长。</summary>
        internal double Started;
        /// <summary>短时反馈显示时长。</summary>
        internal float Duration;
        /// <summary>小于零表示尚未开始安静收尾。</summary>
        internal double ClosingAt = -1d;
        /// <summary>既有管理器是否仍拥有此租约。</summary>
        private bool alive = true;
        /// <summary>向既有管理器报告租约状态。</summary>
        public bool IsAlive => alive;

        /// <summary>冻结几何比例并以真实活动开始时间建立显示。</summary>
        /// <param name="context">能力当前状态。</param>
        /// <param name="pose">实际法宝的空间姿态。</param>
        /// <param name="impact">是否为一次受击反馈。</param>
        /// <param name="now">世界时间。</param>
        /// <param name="duration">短时反馈时长。</param>
        internal Entry(ArtifactAbilityVisualContext context, SpatialVisualMeshInstance pose,
            bool impact, double now, float duration)
        {
            Context = context;
            Impact = impact;
            Broken = impact && context.resource_depleted;
            Started = impact ? now : context.runtime.activity_started_at;
            Duration = Mathf.Max(0.08f, duration);
            ConfigureShape(context, pose);
        }

        /// <summary>代表法宝改变时同步模型、体型和材质，避免沿用上一件法宝的外观。</summary>
        /// <param name="context">当前实际法宝。</param>
        /// <param name="pose">其显示姿态。</param>
        private void ConfigureShape(ArtifactAbilityVisualContext context, SpatialVisualMeshInstance pose)
        {
            Model = pose.Model;
            Orientation = pose.Rotation;
            Actor actor = context.controller.GetComponent<ActorBinder>().Actor;
            Height = SpatialActorPose.Height(actor) * 1.6f;
            Bounds rotated = new(Orientation * Model.Vertices[0], Vector3.zero);
            foreach (Vector3 vertex in Model.Vertices) rotated.Encapsulate(Orientation * vertex);
            ModelScale = Height / rotated.size.z;
            Radius = Mathf.Max(rotated.size.x, rotated.size.y) * ModelScale * 0.45f;
            Tint = Color.Lerp(new Color(1f, 0.78f, 0.27f), context.theme.primary, 0.3f);
            Center = SpatialActorPose.Foot(actor);
        }

        /// <summary>刷新同一效果的上下文，不重播开启声。</summary>
        /// <param name="context">新的实际状态。</param>
        /// <param name="now">世界时间。</param>
        /// <param name="duration">信号时长。</param>
        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            Update(context, now);
            if (!Impact) return;
            Started = now;
            Duration = duration;
            Broken = context.resource_depleted;
            Center = SpatialActorPose.Foot(context.controller.GetComponent<ActorBinder>().Actor);
            if (Broken) RemoveQuietEnding(context);
            CombatVisualAudio.PlayBell(Center, Broken ? 0.9f : 0.45f);
        }

        /// <summary>持续护罩跟随实际护持者；受击保留发生时的位置。</summary>
        /// <param name="context">新的实际状态。</param>
        /// <param name="now">世界时间。</param>
        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (context.artifact != Context.artifact)
            {
                if (!ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact, out SpatialVisualMeshInstance pose))
                {
                    End();
                    return;
                }
                ConfigureShape(context, pose);
                Started = Impact ? now : context.runtime.activity_started_at;
            }
            Context = context;
            if (!Impact) Center = SpatialActorPose.Foot(context.controller.GetComponent<ActorBinder>().Actor);
        }

        /// <summary>管理器结束租约时停止跟随，短暂收尾仍按世界时间消散。</summary>
        public void End()
        {
            if (!alive) return;
            alive = false;
            ClosingAt = World.world.getCurWorldTime();
            if (Impact) entries.Remove(this);
        }
    }
}
