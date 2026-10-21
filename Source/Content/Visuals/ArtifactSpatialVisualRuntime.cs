using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>法宝各类显示部件共用的空间采样，不持有支付、攻击或持续状态。</summary>
internal static class ArtifactSpatialVisualRuntime
{
    /// <summary>由原有能力显示管理器拥有的租约及其短暂收尾。</summary>
    private static readonly List<Lease> entries = new();
    /// <summary>供实际覆盖检查使用的活动数量。</summary>
    internal static int Count => entries.Count;

    /// <summary>在已有管理器中建立一个可供两个视角读取的部件。</summary>
    /// <param name="cue">明确的已有部件类型。</param><param name="context">实际快照。</param>
    /// <param name="now">世界时间。</param><param name="duration">原有持续时间。</param>
    internal static IArtifactVisualLease Begin(IArtifactVisualCue cue, ArtifactAbilityVisualContext context, double now, float duration)
    {
        Lease lease = new(cue); lease.Refresh(context, now, duration); entries.Add(lease); return lease;
    }

    /// <summary>按原有部件类型采样；过期收尾仅在世界时间推进时消退。</summary>
    /// <param name="meshes">两个显示方式的共同快照。</param><param name="details">装饰预算。</param>
    internal static void Append(List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        double now = World.world.getCurWorldTime();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Lease entry = entries[i];
            float closing = entry.Closing < 0d ? 0f : (float)(now - entry.Closing) / .14f;
            if (closing >= 1f) { entries.RemoveAt(i); continue; }
            if (!entry.Context.artifact.IsAvailable()) { entries.RemoveAt(i); continue; }
            float age = (float)(now - entry.Started);
            float progress = entry.Duration > 0f ? Mathf.Clamp01(age / entry.Duration) : 0f;
            float fade = 1f - closing;
            switch (entry.Cue)
            {
                case ArtifactBeamVisualCue cue: Beam(entry.Context, cue, age, fade, meshes, ref details); break;
                case ArtifactTrailVisualCue cue: Trail(entry, cue, now, fade, meshes); break;
                case ArtifactAreaVisualCue cue: Area(entry.Context, cue, age, progress, fade, meshes, ref details); break;
                case ArtifactGlyphVisualCue cue: Glyph(entry.Context, cue, age, progress, fade, meshes, ref details); break;
                case ArtifactSectorVisualCue cue: Sector(entry.Context, cue, age, progress, fade, meshes, ref details); break;
                case ArtifactProjectionVisualCue cue: Projection(entry.Context, cue, age, progress, fade, meshes); break;
                case ArtifactOrbitProjectionVisualCue cue: Orbit(entry.Context, cue, age, fade, meshes, ref details); break;
                case ArtifactLocalVisualCue cue: Local(entry.Context, cue, age, progress, fade, meshes); break;
                default: throw new InvalidOperationException("尚未接入空间显示的法宝部件：" + entry.Cue.GetType().Name);
            }
        }
    }

    /// <summary>取得真实部位的空间位置；器身不再经过图片投影。</summary>
    /// <param name="context">实际快照。</param><param name="anchor">实际作用部位。</param><param name="offset">局部偏移。</param>
    /// <param name="matchScale">是否匹配人物尺度。</param><param name="position">空间位置。</param>
    internal static bool Resolve(ArtifactAbilityVisualContext context, ArtifactVisualAnchorRef anchor, Vector3 offset,
        bool matchScale, out Vector3 position)
    {
        position = context.position;
        if (anchor.use_body_anchor)
        {
            ArtifactBodyAnchorRef body = anchor.body_anchor;
            if (body.UsesAppearanceAnchor && ArtifactAppearanceRenderer.TryResolveSpatialAnchor(context.artifact, body.slot, body.anchor, out position))
            { position += offset * Scale(context, matchScale); return true; }
            if (!ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact, out SpatialVisualMeshInstance pose)) return false;
            position = body.fallback == ArtifactBodyAnchorKind.ForwardTip ? pose.TransformPoint(new Vector3(0f, pose.Model.Bounds.max.y, 0f)) : pose.Position;
        }
        else switch (anchor.kind)
        {
            case ArtifactVisualAnchorKind.Controller:
                if (context.controller.IsNull || !context.controller.HasComponent<ActorBinder>()) return false;
                Actor actor = context.controller.GetComponent<ActorBinder>().Actor;
                if (actor == null || !actor.isAlive()) return false;
                position = SpatialActorPose.Foot(actor) + Vector3.forward * SpatialActorPose.Height(actor) * .5f;
                break;
            case ArtifactVisualAnchorKind.Target:
                if (context.target == null || context.target.isRekt()) return false;
                position = context.target.GetSimPos(); break;
            case ArtifactVisualAnchorKind.ActiveExecution:
                if (context.runtime.active_execution.IsAvailable() && context.runtime.active_execution.HasComponent<Position>())
                { position = context.runtime.active_execution.GetComponent<Position>().value; break; }
                goto case ArtifactVisualAnchorKind.Artifact;
            case ArtifactVisualAnchorKind.DeploymentOrigin:
                if (context.artifact.TryGetComponent(out ArtifactDeployment deployment))
                    return Resolve(context, deployment.ResolveBodyAnchor(), offset, matchScale, out position);
                goto case ArtifactVisualAnchorKind.Artifact;
            case ArtifactVisualAnchorKind.Artifact:
                if (!ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact, out SpatialVisualMeshInstance artifact)) return false;
                position = artifact.Position; break;
            case ArtifactVisualAnchorKind.Point: break;
            default: throw new ArgumentOutOfRangeException(nameof(anchor));
        }
        position += offset * Scale(context, matchScale);
        return true;
    }

    /// <summary>沿用原有部件的体型约定。</summary>
    /// <param name="context">快照。</param><param name="enabled">是否匹配。</param>
    private static float Scale(ArtifactAbilityVisualContext context, bool enabled) => enabled ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;

    /// <summary>沿用原有颜色与强度，不通过换色掩盖形式差别。</summary>
    /// <param name="context">快照。</param><param name="role">颜色角色。</param><param name="alpha">当前不透明度。</param>
    private static Color ColorOf(ArtifactAbilityVisualContext context, ArtifactVisualColorRole role, float alpha)
    { Color color = ArtifactVisualCueTools.ResolveColor(context.theme, role); color.a *= alpha; return color; }

    /// <summary>用实际两端和现有曲率表现光束、锁链和牵引。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    /// <param name="details">剩余装饰预算。</param>
    private static void Beam(ArtifactAbilityVisualContext context, ArtifactBeamVisualCue cue, float age, float fade,
        List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (!Resolve(context, cue.from, cue.from_offset, cue.match_actor_scale, out Vector3 start) ||
            !Resolve(context, cue.to, cue.to_offset, cue.match_actor_scale, out Vector3 end)) return;
        float scale = Scale(context, cue.match_actor_scale);
        ArtifactVfxPathStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Path;
        if ((end - start).sqrMagnitude < .0001f) end = start + ArtifactVisualCueTools.ResolveDirection(context) * cue.fallback_length * scale;
        Color color = ColorOf(context, cue.color_role, cue.alpha * fade);
        Color glow = color; glow.a = cue.glow_alpha * fade;
        Vector3 side = Vector3.Cross((end - start).normalized, Vector3.forward);
        Vector3 previous = start;
        int segments = cue.curvature != 0f || cue.wave_amplitude != 0f ? 8 : 1;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = Vector3.Lerp(start, end, t) + Vector3.forward * Mathf.Sin(t * Mathf.PI) * cue.curvature * scale +
                side * Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * cue.wave_frequency * Mathf.PI * 2f - age * cue.wave_speed) * cue.wave_amplitude * scale;
            SpatialVisualDraw.Rod(meshes, previous, point, cue.width * cue.glow_width_multiplier * scale, glow, true);
            float widthProfile = t < .5f ? Mathf.Lerp(style.StartWidth, style.MiddleWidth, t * 2f) : Mathf.Lerp(style.MiddleWidth, style.EndWidth, (t - .5f) * 2f);
            SpatialVisualDraw.Rod(meshes, previous, point, cue.width * scale * widthProfile * (1f + Mathf.Sin(age * cue.width_pulse_speed) * cue.width_pulse_amplitude), color, true);
            previous = point;
        }
        SpatialVisualDraw.Ring(meshes, start, cue.width * scale * 2f, color, Quaternion.FromToRotation(Vector3.forward, end - start));
        for (int i = 0; i < Mathf.Clamp(style.Strands * 2, 1, 6) && details > 0; i++, details--)
        {
            float t = Mathf.Repeat(age * style.FlowSpeed + i * .37f, 1f);
            SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Spark, Vector3.Lerp(start, end, t), Quaternion.identity,
                Vector3.one * cue.width * scale * 1.5f, color, true);
        }
    }

    /// <summary>只绘制采样到的真实运动历史。</summary>
    /// <param name="entry">实际显示租约。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="now">当前世界时间。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    private static void Trail(Lease entry, ArtifactTrailVisualCue cue, double now, float fade, List<SpatialVisualMeshInstance> meshes)
    {
        Color color = ColorOf(entry.Context, cue.color_role, cue.alpha * fade);
        for (int i = 1; i < entry.Points.Count; i++)
        {
            Color current = color; current.a *= Mathf.Clamp01(1f - (float)(now - entry.Points[i].time) / cue.history);
            SpatialVisualDraw.Rod(meshes, entry.Points[i - 1].position, entry.Points[i].position,
                cue.width * Scale(entry.Context, cue.match_actor_scale), current, true);
        }
    }

    /// <summary>保留真实边界，避免大范围显示被画成实心光盘。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="progress">原管理器持续阶段的进度。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    private static void Area(ArtifactAbilityVisualContext context, ArtifactAreaVisualCue cue, float age, float progress,
        float fade, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (!Resolve(context, cue.anchor, cue.offset, false, out Vector3 point)) return;
        float radius = Mathf.Max(.05f, cue.ResolveRadius?.Invoke(context) ?? 1f) * Mathf.Lerp(cue.start_scale, cue.end_scale, progress);
        radius *= 1f + Mathf.Sin(age * cue.pulse_speed) * cue.pulse_amplitude;
        Color color = ColorOf(context, cue.color_role, cue.line_alpha * fade * (cue.fade_out ? 1f - progress : 1f));
        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Surface;
        if(style.Pattern==ArtifactVfxStyles.Devouring)
        {
            Color vortex=Color.Lerp(ColorOf(context,cue.color_role,.22f*fade),new Color(.35f,.12f,.72f,.22f*fade),.7f);
            for(int i=0;i<3;i++)
                SpatialVisualDraw.Mesh(meshes,SpatialEnergyGeometry.Vortex,point,Quaternion.Euler(0,0,-age*35f+i*120f),
                    new Vector3(radius*.85f,radius*.85f,radius*.75f),vortex,true);
            SpatialVisualDraw.Mesh(meshes,SpatialEnergyGeometry.Orb,point+Vector3.forward*radius*.12f,Quaternion.identity,
                new Vector3(radius*.2f,radius*.2f,radius*.1f),new Color(.08f,.025f,.16f,.35f*fade),true,additive:false);
        }
        if(style.Pattern==ArtifactVfxStyles.Soul)
        {
            for(int i=0;i<3;i++)
                SpatialVisualDraw.Mesh(meshes,SpatialVisualGeometry.Ring,point+Vector3.forward*radius*.12f,
                    Quaternion.Euler(30f+i*22f,0,age*40f+i*120f),Vector3.one*radius*(.65f+i*.12f),color,true);
        }
        if(radius>5f && style.Pattern is ArtifactVfxStyles.Soul or ArtifactVfxStyles.Devouring &&
            ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact,out SpatialVisualMeshInstance apparition))
        {
            Vector3 span=Vector3.Scale(apparition.Model.Bounds.size,apparition.Scale);
            float growth=radius*(style.Pattern==ArtifactVfxStyles.Soul?.75f:.38f)/Mathf.Max(span.x,span.y,span.z);
            Vector3 size=apparition.Scale*growth;
            Vector3 bottom=apparition.Rotation*Vector3.Scale(new Vector3(0,0,apparition.Model.Bounds.min.z),size);
            Vector3 origin=point-bottom+Vector3.forward*(style.Pattern==ArtifactVfxStyles.Devouring?radius*.12f:0f);
            SpatialVisualDraw.Mesh(meshes,apparition.Model,origin,apparition.Rotation,size,
                ColorOf(context,cue.color_role,.14f*fade),true);
        }
        for (int i = 0; i < style.NodeCount && details > 0; i++, details--)
        {
            float angle = i * Mathf.PI * 2f / style.NodeCount + age * style.OuterRotation * Mathf.Deg2Rad;
            SpatialVisualDraw.Mesh(meshes, style.Pattern == ArtifactVfxStyles.Healing ? SpatialVisualGeometry.Leaf : SpatialVisualGeometry.Spark,
                point + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .9f, Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg),
                Vector3.one * Mathf.Min(.12f, radius * .08f), color, true);
        }
    }

    /// <summary>少量符纹与节点沿范围布置，旋转不会改变作用范围。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="progress">原管理器持续阶段的进度。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    /// <param name="details">剩余装饰预算。</param>
    private static void Glyph(ArtifactAbilityVisualContext context, ArtifactGlyphVisualCue cue, float age, float progress,
        float fade, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (!Resolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 point)) return;
        float radius = (cue.ResolveRadius?.Invoke(context) ?? cue.radius) * Scale(context, cue.match_actor_scale) *
            Mathf.Lerp(cue.start_scale, cue.end_scale, progress) * (1f + Mathf.Sin(age * cue.pulse_speed) * cue.pulse_amplitude);
        Color color = ColorOf(context, cue.color_role, cue.alpha * fade * (cue.fade_out ? 1f - progress : 1f));
        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Surface;
        int nodes = Mathf.Min(cue.sides, style.NodeCount);
        for (int i = 0; i < nodes && details > 0; i++, details--)
        {
            float angle = i * Mathf.PI * 2f / nodes + age * cue.rotation_speed * Mathf.Deg2Rad;
            SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Spark, point + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg), Vector3.one * Mathf.Min(.1f, radius * .08f), color, true);
        }
    }

    /// <summary>按真实锥角布置扇面与喷流前缘，绝不发射替代玩法的火球。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="progress">原管理器持续阶段的进度。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    /// <param name="details">剩余装饰预算。</param>
    private static void Sector(ArtifactAbilityVisualContext context, ArtifactSectorVisualCue cue, float age, float progress,
        float fade, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (!Resolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 origin)) return;
        if (!cue.anchor.use_body_anchor && cue.anchor.kind == ArtifactVisualAnchorKind.Point && context.runtime.has_activity_position)
            origin = context.runtime.activity_position + cue.offset * Scale(context, cue.match_actor_scale);
        float scale = Scale(context, cue.match_actor_scale);
        float radius = (cue.ResolveRadius?.Invoke(context) ?? cue.radius) * scale * Mathf.Lerp(cue.start_scale, cue.end_scale, progress);
        float angle = cue.ResolveAngle?.Invoke(context) ?? cue.angle;
        Vector3 forward = ArtifactVisualCueTools.ResolveDirection(context); forward.z = 0f; forward.Normalize();
        Color color = ColorOf(context, cue.color_role, cue.line_alpha * fade * (cue.fade_out ? 1f - progress : 1f));
        for (int i = 0; i <= 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, 0f, Mathf.Lerp(-angle * .5f, angle * .5f, i / 8f)) * forward;
            Vector3 end = origin + dir * radius;
            if (details <= 0) continue;
            details--;
            float flow = Mathf.Repeat(age * 2f + i * .113f, 1f);
            Vector3 point = origin + dir * radius * flow;
            Color stream = color; stream.a = cue.fill_alpha * fade;
            bool wind = ArtifactVfxStyleCatalog.Get(cue.style_key).Surface.Pattern == ArtifactVfxStyles.Wind;
            SpatialVisualDraw.Mesh(meshes, wind ? SpatialEnergyGeometry.Cloud : SpatialEnergyGeometry.Flame, point,
                Quaternion.FromToRotation(Vector3.forward, dir) * Quaternion.Euler(0f, 0f, wind ? age * 160f : 0f),
                new Vector3(.3f + flow * .6f, .3f + flow * .6f, .5f + flow) * scale, stream, true);
        }
    }

    /// <summary>投影直接使用真实组合模型。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="progress">原管理器持续阶段的进度。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    private static void Projection(ArtifactAbilityVisualContext context, ArtifactProjectionVisualCue cue, float age, float progress,
        float fade, List<SpatialVisualMeshInstance> meshes)
    {
        if (!Resolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 point) ||
            !ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact, out SpatialVisualMeshInstance pose)) return;
        float worldSize = cue.ResolveWorldSize?.Invoke(context) ?? context.artifact.GetComponent<ArtifactManifestation>().world_size;
        float ratio = worldSize / Mathf.Max(.01f, context.artifact.GetComponent<ArtifactManifestation>().world_size) * cue.scale *
            Mathf.Lerp(cue.start_scale, cue.end_scale, progress) * (1f + Mathf.Sin(age * cue.pulse_speed) * cue.pulse_amplitude);
        Color color = ColorOf(context, cue.color_role, cue.alpha * fade * (cue.fade_out ? 1f - progress : 1f));
        Quaternion rotation = pose.Rotation;
        if (!cue.follow_artifact_rotation)
            rotation = Quaternion.Inverse(Quaternion.Euler(0f, 0f, context.artifact.GetComponent<Rotation>().z)) * rotation;
        SpatialVisualDraw.Mesh(meshes, pose.Model, point, Quaternion.Euler(0f, 0f, age * cue.rotation_speed) * rotation,
            pose.Scale * ratio, color, true, point);
    }

    /// <summary>环绕的透明器影与实际攻击实体分开显示。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    /// <param name="details">剩余装饰预算。</param>
    private static void Orbit(ArtifactAbilityVisualContext context, ArtifactOrbitProjectionVisualCue cue, float age, float fade,
        List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (!Resolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 center) ||
            !ArtifactAppearanceRenderer.TryGetSpatialPose(context.artifact, out SpatialVisualMeshInstance pose)) return;
        int count = cue.ResolveCount?.Invoke(context) ?? cue.count;
        float scale = Scale(context, cue.match_actor_scale);
        float radius = (cue.ResolveRadius?.Invoke(context) ?? cue.radius) * scale;
        float worldSize = cue.ResolveWorldSize?.Invoke(context) ?? context.artifact.GetComponent<ArtifactManifestation>().world_size;
        float ratio = worldSize / Mathf.Max(.01f, context.artifact.GetComponent<ArtifactManifestation>().world_size) * (1f + Mathf.Sin(age * cue.pulse_speed) * cue.pulse_amplitude);
        Color color = ColorOf(context, cue.color_role, cue.alpha * fade);
        for (int i = 0; i < count && details > 0; i++, details--)
        {
            float angle = (age * cue.angular_speed + i * 360f / count) * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, Mathf.Sin(angle * 2f) * radius * cue.vertical_ratio + Mathf.Sin(age * cue.bob_speed + i) * cue.bob_amplitude * scale);
            SpatialVisualDraw.Mesh(meshes, pose.Model, point, Quaternion.Euler(0f, 0f, cue.radial_facing ? angle * Mathf.Rad2Deg + cue.rotation_offset : 0f) * pose.Rotation,
                pose.Scale * ratio, color, true, center);
        }
    }

    /// <summary>在真实部位绘制火舌、根蔓、护持弧和强化纹，局部形式各不相同。</summary>
    /// <param name="context">实际能力快照。</param>
    /// <param name="cue">已配置的显示部件。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="progress">原管理器持续阶段的进度。</param>
    /// <param name="fade">短暂收尾的剩余比例。</param>
    /// <param name="meshes">本帧共用快照。</param>
    private static void Local(ArtifactAbilityVisualContext context, ArtifactLocalVisualCue cue, float age, float progress,
        float fade, List<SpatialVisualMeshInstance> meshes)
    {
        if (!Resolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 point)) return;
        float size = cue.scale * 8f * Scale(context, cue.match_actor_scale) * (1f + Mathf.Sin(age * cue.scale_pulse_speed) * cue.scale_pulse_amplitude);
        Color color = ColorOf(context, cue.color_role, cue.alpha * fade * (cue.loop ? .65f : 1f - progress));
        if (cue.alpha_pulse_period > 0f)
            color.a *= Mathf.Lerp(cue.alpha_pulse_floor, 1f, .5f + .5f * Mathf.Sin(age * Mathf.PI * 2f / cue.alpha_pulse_period));
        switch (cue.kind)
        {
            case ArtifactLocalVisualKind.Flame:
                for (int i = 0; i < 3; i++)
                    SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Flame, point + new Vector3(Mathf.Sin(age * 8f + i) * size * .2f, 0f, 0f),
                        Quaternion.Euler(0f, Mathf.Sin(age * 11f + i) * 15f, i * 120f), new Vector3(size * .5f, size * .5f, size), color, true);
                break;
            case ArtifactLocalVisualKind.Shield:
                SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Arc, point, Quaternion.Euler(70f, age * 50f, age * 30f), Vector3.one * size, color, true); break;
            case ArtifactLocalVisualKind.Root:
                for (int i = 0; i < 3; i++)
                    SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Leaf, point, Quaternion.Euler(i * 25f, 0f, i * 120f), Vector3.one * size, color, false);
                break;
            case ArtifactLocalVisualKind.Blessing:
                SpatialVisualDraw.Ring(meshes, point, size, color, Quaternion.identity); break;
            default:
                SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Spark, point, Quaternion.Euler(age * 90f, age * 45f, 0f), Vector3.one * size, color, true); break;
        }
    }

    /// <summary>切换世界不保留法宝、运动历史或显示收尾。</summary>
    internal static void ClearWorldState() => entries.Clear();

    /// <summary>原有显示管理器拥有的生命周期，空间运行时仅读取它。</summary>
    private sealed class Lease : IArtifactVisualLease
    {
        /// <summary>明确的部件。</summary>
        internal readonly IArtifactVisualCue Cue;
        /// <summary>最近的真实快照。</summary>
        internal ArtifactAbilityVisualContext Context;
        /// <summary>建立、持续和收尾的世界时间。</summary>
        internal double Started, Closing = -1d;
        /// <summary>原管理器给出的持续时长。</summary>
        internal float Duration;
        /// <summary>真实运动采样。</summary>
        internal readonly List<(Vector3 position, double time)> Points = new();
        /// <summary>租约是否仍由已有管理器持有。</summary>
        public bool IsAlive => Closing < 0d;
        /// <summary>选择实际部件。</summary><param name="cue">部件。</param>
        internal Lease(IArtifactVisualCue cue) => Cue = cue;
        /// <summary>同一信号刷新不另造施法过程。</summary>
        /// <param name="context">快照。</param><param name="now">世界时间。</param><param name="duration">时长。</param>
        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        { Started = now; Duration = duration; Closing = -1d; Points.Clear(); Update(context, now); }
        /// <summary>同步实际载体，拖尾只记录真实位置。</summary>
        /// <param name="context">快照。</param><param name="now">世界时间。</param>
        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            Context = context;
            if (Cue is not ArtifactTrailVisualCue trail || !Resolve(context, trail.anchor, trail.offset, trail.match_actor_scale, out Vector3 point)) return;
            while (Points.Count > 0 && now - Points[0].time >= trail.history) Points.RemoveAt(0);
            if (Points.Count == 0 || Vector3.Distance(Points[^1].position, point) >= trail.min_distance * Scale(context, trail.match_actor_scale))
                Points.Add((point, now));
            else if (Points[^1].time != now) Points[^1] = (point, now);
            while (Points.Count > Mathf.Max(2, trail.max_points)) Points.RemoveAt(0);
        }
        /// <summary>停止跟随，仅保留很短的世界时间收尾。</summary>
        public void End() { if (Closing < 0d) Closing = World.world.getCurWorldTime(); }
    }
}
