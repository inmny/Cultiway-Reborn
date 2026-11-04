using System;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>动物目标采用自己的原骨架和网格，抓持不套用人形比例。</summary>
internal static partial class BodyOrganWorldfall
{
    /// <summary>原动物骨架的尺寸和步态参数。</summary>
    private static PropertyInfo creatureHeight, creatureLegLength, creatureSwing;

    /// <summary>原有悬浮偏移与姿态数组准备入口。</summary>
    private static MethodInfo creatureHover, creatureEnsurePose;

    /// <summary>只在安装时绑定原模组的固定入口。</summary>
    /// <param name="assembly">已加载的三维模组。</param>
    private static void InstallSurfaces(Assembly assembly)
    {
        Type rig = assembly.GetType("FirstPerson.Core.CreatureRig", true);
        creatureHeight = AccessTools.Property(rig, "Height");
        creatureLegLength = AccessTools.Property(rig, "LegLength");
        creatureSwing = AccessTools.Property(rig, "SwingPerStride");
        creatureHover = AccessTools.Method(assembly.GetType("FirstPerson.Creatures", true), "Hover");
        creatureEnsurePose = AccessTools.Method(meshType, "EnsurePose");
    }

    /// <summary>先接入使用地面步态的实际动物，缺少模型及独立飞行或游泳规则时不借用人形。</summary>
    /// <param name="actor">真实目标。</param>
    internal static BodySurfaceBinding CreateSurface(Actor actor)
    {
        if (actor.isFlying() || actor.isAffectedByLiquid()) return null;
        string family = (string)familyOf.Invoke(null, new object[] { actor });
        if (family == null || !family.StartsWith("animal_", StringComparison.Ordinal) ||
            (float)creatureHover.Invoke(null, new object[] { family }) != 0f) return null;
        object model = detailed.Invoke(null, new object[] { family, 0f, null });
        if (model == null) return null;
        object state = stateOf.Invoke(null, new object[] { actor, family });
        return new CreatureSurface(actor, family, model, state);
    }

    /// <summary>绘制使用已经确认的目标表面，暂停时不再推进其骨架。</summary>
    /// <param name="actor">当前显示的真实动物。</param><param name="x">横向位置。</param>
    /// <param name="y">纵向位置。</param><param name="z">脚下高度。</param>
    /// <param name="heading">实际朝向。</param><param name="speed">原绘制步速。</param>
    /// <param name="distance">本次模型精度距离。</param><param name="dt">原绘制步长。</param>
    /// <param name="swimming">原游泳显示。</param><param name="walkDt">原步态步长。</param>
    private static bool PrepareSurfaceDisplay(Actor actor, ref float x, ref float y, ref float z,
        ref float heading, ref float speed, ref float distance, ref float dt, ref bool swimming, ref float walkDt)
    {
        if (!ConstructedAbilityService.TryGetBodySurface(actor.id, out BodySurfaceBinding binding,
                out BodySurfacePose pose) || binding is not CreatureSurface surface ||
            ConstructedAbilityService.TryGetBodyPose(actor.id, out _) ||
            ReferenceEquals(surface.State, figure.GetValue(null))) return false;
        Vector3 foot = pose.Foot + WorldfallVisualBridge.BodyShift(actor);
        x = foot.x;
        y = foot.y;
        z = foot.z;
        heading = pose.Heading;
        speed = dt = walkDt = distance = 0f;
        swimming = false;
        stateScale.SetValue(surface.State, surface.Scale);
        return true;
    }

    /// <summary>一个动物目标的独立数值采集，不要求镜头先看到它。</summary>
    private sealed class CreatureSurface : BodySurfaceBinding
    {
        /// <summary>原模组持有的实际外观状态，仅主线程使用。</summary>
        internal readonly object State;

        /// <summary>与原动物显示相同的个体比例。</summary>
        internal readonly float Scale;

        /// <summary>实际动物模型、原骨架和独立采集实例。</summary>
        private readonly object model, rig, mesh;

        /// <summary>原模型固定部件及其共享数值表面。</summary>
        private readonly Array native;

        /// <summary>不随每轮姿态重建的真实三角面。</summary>
        private readonly BodySurfacePart[] surfaces;

        /// <summary>真实物种模型及成年状态，改变后不继续沿旧骨架控制。</summary>
        private readonly string family;

        /// <summary>出生阶段参与原模型比例。</summary>
        private readonly bool adult;

        /// <summary>原个体的固定步态种子。</summary>
        private readonly float seed;

        /// <summary>游戏时间推进的步态和实际朝向。</summary>
        private float phase, heading;

        /// <summary>从原资源采集模型，普通身体和头尾等原部件保留可选名称。</summary>
        /// <param name="actor">实际动物。</param><param name="family">原模型家族。</param>
        /// <param name="model">实际详细模型。</param><param name="state">原个体外观。</param>
        internal CreatureSurface(Actor actor, string family, object model, object state) : base(actor)
        {
            this.family = family;
            this.model = model;
            State = state;
            adult = actor.isAdult();
            seed = (float)stateSeed.GetValue(state);
            // 与原Creatures.Add的动物比例一致，查询不能取决于是否已被镜头绘制。
            Scale = .5714286f * (adult ? 1f : .55f) * (.94f + .12f * (seed % 1f));
            rig = rigOf.Invoke(null, new[] { model });
            mesh = Activator.CreateInstance(meshType);
            meshModel.SetValue(mesh, model);
            creatureEnsurePose.Invoke(mesh, null);
            native = (Array)modelParts.GetValue(model);
            surfaces = new BodySurfacePart[native.Length];
            for (int i = 0; i < native.Length; i++)
            {
                object part = native.GetValue(i);
                var raw = (float[])partPositions.GetValue(part);
                var vertices = new Vector3[raw.Length / 3];
                for (int p = 0; p < vertices.Length; p++)
                    vertices[p] = new Vector3(raw[p * 3], raw[p * 3 + 1], raw[p * 3 + 2]);
                string name = (string)partName.GetValue(part), group = null;
                for (int parent = i; parent >= 0; parent = (int)partParent.GetValue(native.GetValue(parent)))
                {
                    string ancestor = (string)partName.GetValue(native.GetValue(parent));
                    group = ancestor switch
                    {
                        "head" => "head", "body" => "torso", "tail" => "tail",
                        "wing_l" => "wing.left", "wing_r" => "wing.right", _ => null
                    };
                    if (group != null) break;
                }

                surfaces[i] = new BodySurfacePart("part:" + name, vertices,
                    (int[])partTriangles.GetValue(part), transformIndex: i, group: group);
            }

            heading = actor.flip ? Mathf.PI : 0f;
        }

        /// <summary>成年、飞行或液体状态改变后，旧地面骨架关系失效。</summary>
        internal override string ChangedReason => base.ChangedReason ??
                                                  (Actor.isAdult() != adult || Actor.isFlying() ||
                                                   Actor.isAffectedByLiquid() ||
                                                   (string)familyOf.Invoke(null, new object[] { Actor }) != family
                                                      ? "BodyChanged"
                                                      : null);

        /// <summary>按原步态公式推进同一世界时钟，捕获后的矩阵交给接触与显示共用。</summary>
        /// <param name="foot">实际脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        protected override BodySurfacePose Sample(Vector3 foot, float height, double time)
        {
            float dt = To == null ? 0f : (float)(time - ToTime);
            Vector3 delta = To == null ? Vector3.zero : foot - To.Foot;
            float speed = dt > 0f ? new Vector2(delta.x, delta.y).magnitude / dt : 0f;
            if (speed > .00001f) heading = Mathf.Atan2(delta.y, delta.x);
            float bodyHeight = (float)creatureHeight.GetValue(rig) * Scale,
                leg = (float)creatureLegLength.GetValue(rig) * Scale;
            float stride = Mathf.Clamp01(speed / Mathf.Max(.05f, bodyHeight * 1.5f));
            if (leg > .02f)
            {
                float angle = Mathf.Clamp(.22f + .1f * speed / leg, .22f, .6f);
                stride = Mathf.Clamp01(speed / Mathf.Max(.05f, leg * .6f)) * angle /
                         (float)creatureSwing.GetValue(rig);
                phase += dt * Mathf.PI * speed / (2f * leg * Mathf.Sin(angle));
            }
            else phase += dt * speed / Mathf.Max(.04f, bodyHeight * .5f);

            float attack = (float)(time - Actor.last_attack_timestamp);
            attack = attack >= 0f && attack < .45f ? attack / .45f : 0f;
            poseRig.Invoke(rig, new[] { mesh, (object)stride, phase, (float)time, false, attack, seed });
            place.Invoke(mesh, new object[] { foot.x, foot.y, foot.z, heading, Scale, Scale, Scale });
            var matrices = new Matrix4x4[native.Length];
            for (int i = 0; i < matrices.Length; i++) matrices[i] = BodyWorldfallRenderer.WorldMatrix(mesh, i);
            return new BodySurfacePose(surfaces, matrices, foot, height, heading);
        }

        /// <summary>实际动物显示读取已经确认的矩阵，不另求一轮抓持位置。</summary>
        /// <param name="pose">本轮共同姿态。</param>
        internal void Display(BodySurfacePose pose)
        {
            if (ConstructedAbilityService.TryGetBodyPose(Actor.id, out _)) return;
            object actual = stateMesh.GetValue(State);
            if (ReferenceEquals(State, figure.GetValue(null)) || meshModel.GetValue(actual) != model) return;
            var turns = (Array)rotations.GetValue(actual);
            var shifts = (Array)offsets.GetValue(actual);
            var sizes = (float[])scales.GetValue(actual);
            Matrix4x4 shift = Matrix4x4.Translate(WorldfallVisualBridge.BodyShift(Actor)),
                root = BodyWorldfallRenderer.WorldMatrix(actual, -1).inverse;
            for (int i = 0; i < native.Length; i++)
            {
                int parent = (int)partParent.GetValue(native.GetValue(i));
                Matrix4x4 local = (parent < 0 ? root : (shift * pose.Matrices[parent]).inverse) *
                                  shift * pose.Matrices[i];
                Vector3 pivot = Read(partPivot.GetValue(native.GetValue(i)));
                turns.SetValue(Matrix(local), i);
                shifts.SetValue(Vector(local.MultiplyPoint3x4(pivot) - pivot), i);
                sizes[i] = 1f;
            }

            cacheModel.SetValue(actual, null);
        }
    }
}