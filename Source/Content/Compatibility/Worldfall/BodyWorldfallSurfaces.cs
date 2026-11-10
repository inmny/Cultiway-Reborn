using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>目标身体表面与三维显示共用按游戏时间生成的原骨架姿态。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>原骨架自己的姿态、摆放及走路入口。</summary>
    private static MethodInfo surfacePose, surfacePlace, surfaceStep;

    /// <summary>实际模型三角面及原步态参数。</summary>
    private static FieldInfo partTriangles, robe, walkerStride, walkerPhase, walkerRun, figurePhase;

    /// <summary>原骨架根据实际模型计算腿部比例。</summary>
    private static PropertyInfo legShare;

    /// <summary>只在外部模型安装时取得固定入口。</summary>
    /// <param name="assembly">外部模型程序集。</param><param name="rig">实际人物骨架类型。</param>
    private static void InstallSurfaces(Assembly assembly, Type rig)
    {
        surfacePose = AccessTools.Method(rig, "Pose");
        surfacePlace = AccessTools.Method(rig, "Place");
        surfaceStep = AccessTools.Method(walk.FieldType, "Step");
        partTriangles = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Tri");
        legShare = AccessTools.Property(rig, "LegShare");
        robe = AccessTools.Field(rig, "Robe");
        walkerStride = AccessTools.Field(walk.FieldType, "Stride");
        walkerPhase = AccessTools.Field(walk.FieldType, "Phase");
        walkerRun = AccessTools.Field(walk.FieldType, "Run");
        figurePhase = AccessTools.Field(bodyMesh.DeclaringType, "Phase");
        InstallAttachments(assembly, rig);
        BodySurfaceBinding.Provider = CreateSurface;
    }

    /// <summary>不依赖人物曾被镜头采集，直接取得其正式外观。</summary>
    /// <param name="actor">实际身体。</param>
    private static BodySurfaceBinding CreateSurface(Actor actor)
    {
        // 当前同步的是People人物显示；其他身体须接入其实际骨架和绘制，不能借用人形姿态冒充。
        if (!actor.asset.is_humanoid) return BodyOrganWorldfall.CreateSurface(actor);
        object external = externalInstance.GetValue(null), sprites = externalSprites.GetValue(external);
        WorldfallVisualBridge.PrepareSprites(sprites);
        object image = spriteImage.Invoke(sprites, new object[] { actor.getSpriteToRender() });
        if (image == null || !WorldfallVisualBridge.TryBodyHeight(actor, 1f, sprites, out float height)) return null;
        object figure = figureOf.Invoke(null, new[] { actor, image });
        object family = lookFamily.Invoke(null, new[] { figure, familyOf.Invoke(null, new[] { actor, figure }) });
        object model = detailedBody.Invoke(null, new[] { family, 0f, null });
        if (model == null) return null;
        object fitModel = model;
        object mesh = Activator.CreateInstance(meshType), rig = rigOf.Invoke(null, new[] { model });
        meshModel.SetValue(mesh, model);
        dressBody.Invoke(rig, new[] { mesh, figurePalette.GetValue(figure), true, female.GetValue(figure) });
        dressLook.Invoke(null, new[] { actor, figure, family, rig, mesh });
        model = lookBody.Invoke(null, new[] { figure, model });
        meshModel.SetValue(mesh, model);
        rig = rigOf.Invoke(null, new[] { model });
        dressBody.Invoke(rig, new[] { mesh, figurePalette.GetValue(figure), true, female.GetValue(figure) });
        dressLook.Invoke(null, new[] { actor, figure, family, rig, mesh });
        return new SurfaceBinding(actor, mesh, rig, figure, height, fitModel);
    }

    /// <summary>一个真实目标的模型与独立步态；镜头中的额外采集不能重复推进它。</summary>
    private sealed partial class SurfaceBinding : BodySurfaceBinding
    {
        /// <summary>原模型实例、骨架、人物外观及独立走路状态，仅在主线程使用。</summary>
        private readonly object mesh, rig, figure, walker;

        /// <summary>模型部件及其不会随动作改变的真实几何。</summary>
        private readonly Array nativeParts;

        /// <summary>实际资源几何，不复制为人物包络。</summary>
        private readonly BodySurfacePart[] parts;

        /// <summary>实际模型体高与共同体高的比例。</summary>
        internal readonly float HeightScale;

        /// <summary>两次实际采集之间保留的朝向，不从镜头读上一帧显示方向。</summary>
        private float heading;

        /// <summary>进入或离开游泳、飞行及悬浮状态时，不沿用旧接触固定点。</summary>
        private readonly bool swimming, flying, hovering;

        /// <summary>游泳沿原模组的独立划水相位推进，不使用落脚求解。</summary>
        private float swimPhase;

        /// <summary>身体状态变化由实际关系维护路径解除。</summary>
        internal override string ChangedReason => base.ChangedReason ?? HeldChangedReason ??
            (WorldfallVisualBridge.IsSwimming(Actor) != swimming || Actor.isFlying() != flying ||
             Actor.isHovering() != hovering ? "BodyChanged" : null);

        /// <summary>从人物正式外观准备一次骨架。</summary>
        /// <param name="actor">实际人物。</param><param name="mesh">独立模型实例。</param><param name="rig">真实骨架。</param>
        /// <param name="figure">正式外观。</param><param name="heightScale">实际高度比例。</param>
        /// <param name="fitModel">原模组用于适配甲片的基础身体。</param>
        internal SurfaceBinding(Actor actor, object mesh, object rig, object figure, float heightScale,
            object fitModel) : base(actor)
        {
            this.mesh = mesh;
            this.rig = rig;
            this.figure = figure;
            swimming = WorldfallVisualBridge.IsSwimming(actor);
            flying = actor.isFlying();
            hovering = actor.isHovering();
            swimPhase = (float)figurePhase.GetValue(figure);
            HeightScale = heightScale;
            walker = Activator.CreateInstance(walk.FieldType, true);
            heading = actor.flip ? Mathf.PI : 0f;
            resetWalk.Invoke(walker, new object[] { heading });
            nativeParts = (Array)modelParts.GetValue(meshModel.GetValue(mesh));
            PrepareArmour(fitModel);
            var surfaces = new List<BodySurfacePart>();
            ulong mask = (ulong)hidden.GetValue(mesh);
            for (int i = 0; i < nativeParts.Length; i++)
            {
                object part = nativeParts.GetValue(i);
                var positions = (float[])partPositions.GetValue(part);
                var points = new Vector3[positions.Length / 3];
                for (int vertex = 0; vertex < points.Length; vertex++)
                    points[vertex] = new Vector3(positions[vertex * 3], positions[vertex * 3 + 1],
                        positions[vertex * 3 + 2]);
                int[] triangles = i < 64 && (mask & (1UL << i)) != 0
                    ? Array.Empty<int>()
                    : (int[])partTriangles.GetValue(part);
                AppendSurface(surfaces, i, points, triangles);
            }

            AppendArmour(surfaces);
            PrepareHeld(surfaces);
            parts = surfaces.ToArray();
        }

        /// <summary>掌按原握持挂点、足按原足底与腿长划分；衣甲和身体使用同一边界。</summary>
        /// <param name="surfaces">当前实际表面。</param><param name="index">原骨架部件。</param>
        /// <param name="points">模型顶点。</param><param name="triangles">原三角面。</param>
        /// <param name="attachment">实际外层装备。</param>
        private void AppendSurface(List<BodySurfacePart> surfaces, int index, Vector3[] points, int[] triangles,
            string attachment = null)
        {
            string role = Part(index), distal = null;
            Vector3 point = Vector3.zero, normal = Vector3.down;
            if (role is "arm.right.lower" or "arm.left.lower")
            {
                bool left = role == "arm.left.lower";
                int lower = (int)(left ? lowerLeft : lowerRight).GetValue(rig);
                // 与原PersonRig.HandWorld/LeftHandWorld的真实安装点一致。
                Vector3 pivot = ReadVector(partPivot.GetValue(nativeParts.GetValue(lower)));
                normal = new Vector3(left ? -.004f : .004f, -.012f, -.3f);
                point = pivot + normal;
                distal = left ? "hand.left" : "hand.right";
            }
            else if (role is "leg.right.lower" or "leg.left.lower")
            {
                bool left = role == "leg.left.lower";
                int lower = (int)(left ? lowerLegLeft : lowerLegRight).GetValue(rig);
                int upper = (int)(left ? upperLegLeft : upperLegRight).GetValue(rig);
                Vector3 sole = ReadVector((left ? soleLeft : soleRight).GetValue(rig));
                Vector3 knee = ReadVector(partPivot.GetValue(nativeParts.GetValue(lower))),
                    hip = ReadVector(partPivot.GetValue(nativeParts.GetValue(upper)));
                float length = Vector3.Distance(hip, knee) + Vector3.Distance(knee, sole);
                point = sole + Vector3.forward * (length * .12f);
                normal = Vector3.back;
                distal = left ? "foot.left" : "foot.right";
            }

            var source = new BodySurfacePart(role, points, triangles, attachment, index);
            if (distal == null) surfaces.Add(source);
            else
            {
                BodySurfacePartition.Split(source, distal, point, normal, out var limb, out var end);
                surfaces.Add(limb);
                surfaces.Add(end);
            }
        }

        /// <summary>将外部骨架明确声明的部位映射到共同名称，附属头发沿父级归属。</summary>
        /// <param name="index">真实模型部件。</param>
        private string Part(int index)
        {
            for (int part = index; part >= 0; part = (int)parentPart.GetValue(nativeParts.GetValue(part)))
            {
                if (part == (int)headPart.GetValue(rig)) return "head";
                if (part == (int)lowerRight.GetValue(rig)) return "arm.right.lower";
                if (part == (int)upperRight.GetValue(rig)) return "arm.right.upper";
                if (part == (int)lowerLeft.GetValue(rig)) return "arm.left.lower";
                if (part == (int)upperLeft.GetValue(rig)) return "arm.left.upper";
                if (part == (int)lowerLegRight.GetValue(rig)) return "leg.right.lower";
                if (part == (int)upperLegRight.GetValue(rig)) return "leg.right.upper";
                if (part == (int)lowerLegLeft.GetValue(rig)) return "leg.left.lower";
                if (part == (int)upperLegLeft.GetValue(rig)) return "leg.left.upper";
                if (part == (int)torsoPart.GetValue(rig) || part == (int)hips.GetValue(rig)) return "torso";
            }

            return "surface";
        }

        /// <summary>实际位移驱动原走路求解，呼吸使用游戏时间；不调用场景收集或推进游戏人物。</summary>
        /// <param name="foot">共同脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        protected override BodySurfacePose Sample(Vector3 foot, float height, double time)
        {
            float dt = To == null ? 0f : (float)(time - ToTime);
            Vector3 delta = To == null ? Vector3.zero : foot - To.Foot;
            float speed = dt > 0f ? new Vector2(delta.x, delta.y).magnitude / dt : 0f;
            float oldHeading = heading;
            if (speed > .00001f) heading = Mathf.Atan2(delta.y, delta.x);
            float turn = dt > 0f
                ? Mathf.DeltaAngle(oldHeading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt
                : 0f;
            float stride, phase, run;
            if (swimming)
            {
                // 原场景给游泳人物的步速为0.5；原骨架不植足，保持真实划水相位。
                swimPhase += dt * (.5f / Mathf.Max(.1f, height * HeightScale * .55f) + 3f);
                stride = .35f;
                phase = swimPhase;
                run = 0f;
            }
            else
            {
                surfaceStep.Invoke(walker, new[]
                {
                    (object)speed, height * HeightScale, turn, heading, dt, legShare.GetValue(rig), robe.GetValue(rig)
                });
                stride = (float)walkerStride.GetValue(walker);
                phase = (float)walkerPhase.GetValue(walker);
                run = (float)walkerRun.GetValue(walker);
            }

            surfacePose.Invoke(rig, new object[]
            {
                mesh, stride, phase, 0f, (float)time + (swimming ? Actor.GetHashCode() & 63 : Id & 63), 0f, 0f,
                run, swimming ? null : walker
            });
            PlaceSurface(swimming ? WorldfallVisualBridge.SwimmingRoot(Actor, foot,
                externalSprites.GetValue(externalInstance.GetValue(null))) : foot, height);
            UpdateHeld(height);
            int count = held.Count == 0
                ? nativeParts.Length
                : held[held.Count - 1].Start + held[held.Count - 1].Parts.Length;
            var matrices = new Matrix4x4[count];
            for (int part = 0; part < nativeParts.Length; part++) matrices[part] = WorldMatrix(mesh, part);
            CaptureHeld(matrices);
            return new BodySurfacePose(parts, matrices, foot, height, heading);
        }

        /// <summary>动作表面沿用真实骨架，将整身支撑、头部及衣甲套用同一共同姿态。</summary>
        /// <param name="basis">原身体表面。</param><param name="stance">共同支撑。</param>
        /// <param name="head">头部动作。</param><param name="right">右臂。</param><param name="left">左臂。</param>
        /// <param name="organ">实际独立器官。</param>
        internal override BodySurfacePose Acting(BodySurfacePose basis, BodyPose? stance, BodyPose? head,
            BodyPose? right, BodyPose? left, BodyOrganBinding organ)
        {
            // 先恢复该时点的原骨架，再应用本次动作，不能叠加上一次查询的偏移。
            surfacePlace.Invoke(null, new object[]
            {
                mesh, basis.Foot.x, basis.Foot.y, basis.Foot.z, basis.Heading,
                basis.Height * HeightScale * (float)bodyTall.Invoke(null, new[] { figure }) /
                (float)rigHeight.GetValue(rig)
            });
            ApplySurfaceMatrices(mesh, basis, Vector3.zero);
            Vector3 foot = basis.Foot;
            float facing = basis.Heading;
            if (stance.HasValue)
            {
                BodyPose pose = stance.Value;
                foot = pose.Foot;
                facing = Mathf.Atan2(pose.Forward.y, pose.Forward.x) + pose.Turn * Mathf.Deg2Rad;
                surfacePlace.Invoke(null, new object[]
                {
                    mesh, foot.x, foot.y, foot.z, facing,
                    pose.BodyHeight * (float)bodyTall.Invoke(null, new[] { figure }) / (float)rigHeight.GetValue(rig)
                });
            }

            ResetTrunk(mesh, rig, head.HasValue);
            if (stance.HasValue) RetargetStance(mesh, rig, stance.Value);
            if (head.HasValue) RetargetHead(mesh, rig, head.Value);
            if (right.HasValue) RetargetArmature(mesh, rig, false, right.Value, Vector3.zero);
            if (left.HasValue) RetargetArmature(mesh, rig, true, left.Value, Vector3.zero);
            UpdateHeld(basis.Height);
            var matrices = new Matrix4x4[basis.Matrices.Length];
            for (int part = 0; part < nativeParts.Length; part++) matrices[part] = WorldMatrix(mesh, part);
            CaptureHeld(matrices);
            return new BodySurfacePose(parts, matrices, foot, basis.Height, facing);
        }

        /// <summary>把完整世界变换还原到原骨架；显示与采集都不依赖父级写入顺序。</summary>
        /// <param name="target">独立采集或实际显示骨架。</param><param name="pose">同一身体姿态。</param>
        /// <param name="shift">本次整体显示平移。</param>
        private void ApplySurfaceMatrices(object target, BodySurfacePose pose, Vector3 shift)
        {
            Matrix4x4 shiftMatrix = Matrix4x4.Translate(shift), rootInverse = WorldMatrix(target, -1).inverse;
            var targetRotations = (Array)rotations.GetValue(target);
            var targetOffsets = (Array)offsets.GetValue(target);
            var targetScales = (float[])scales.GetValue(target);
            for (int part = 0; part < nativeParts.Length; part++)
            {
                object resource = nativeParts.GetValue(part);
                int parent = (int)parentPart.GetValue(resource);
                Matrix4x4 local = (parent < 0 ? rootInverse : (shiftMatrix * pose.Matrices[parent]).inverse) *
                                  shiftMatrix * pose.Matrices[part];
                Vector3 pivot = ReadVector(partPivot.GetValue(resource));
                targetRotations.SetValue(matrixValue(local), part);
                targetOffsets.SetValue(vectorValue(local.MultiplyPoint3x4(pivot) - pivot), part);
                targetScales[part] = 1f;
            }
        }

        /// <summary>将同一共同姿态写入实际身体；父级先后顺序不影响结果。</summary>
        /// <param name="pose">已经确认的表面姿态。</param>
        internal void Display(BodySurfacePose pose, IList draws)
        {
            object actual = ((IDictionary)figures.GetValue(null))[Actor];
            if (actual == null) return;
            object target = bodyMesh.GetValue(actual);
            if (target == null || meshModel.GetValue(target) != meshModel.GetValue(mesh)) return;
            ApplySurfaceMatrices(target, pose, swimming ? Vector3.zero : WorldfallVisualBridge.BodyShift(Actor));
            if (!ConstructedAbilityService.IsWeaponBorrowed(Actor)) DisplayHeld(draws, actual, pose, target);
        }

        /// <summary>身体和持物准备阶段复用相同的原生根摆放。</summary>
        /// <param name="foot">共同根点。</param><param name="height">共同体高。</param>
        private void PlaceSurface(Vector3 foot, float height) => surfacePlace.Invoke(null, new object[]
        {
            mesh, foot.x, foot.y, foot.z, heading, height * HeightScale *
            (float)bodyTall.Invoke(null, new[] { figure }) / (float)rigHeight.GetValue(rig)
        });
    }
}
