using System;
using System.Collections;
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
    private static FieldInfo partTriangles, robe, walkerStride, walkerPhase, walkerRun;

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
        BodySurfaceBinding.Provider = CreateSurface;
    }

    /// <summary>不依赖人物曾被镜头采集，直接取得其正式外观。</summary>
    /// <param name="actor">实际身体。</param>
    private static BodySurfaceBinding CreateSurface(Actor actor)
    {
        // 当前同步的是People人物显示；其他身体须接入其实际骨架和绘制，不能借用人形姿态冒充。
        if (!actor.asset.is_humanoid) return null;
        object external = externalInstance.GetValue(null), sprites = externalSprites.GetValue(external);
        WorldfallVisualBridge.PrepareSprites(sprites);
        object image = spriteImage.Invoke(sprites, new object[] { actor.getSpriteToRender() });
        if (image == null || !WorldfallVisualBridge.TryBodyHeight(actor, 1f, sprites, out float height)) return null;
        object figure = figureOf.Invoke(null, new[] { actor, image });
        object family = lookFamily.Invoke(null, new[] { figure, familyOf.Invoke(null, new[] { actor, figure }) });
        object model = detailedBody.Invoke(null, new[] { family, 0f, null });
        if (model == null) return null;
        object mesh = Activator.CreateInstance(meshType), rig = rigOf.Invoke(null, new[] { model });
        meshModel.SetValue(mesh, model);
        dressBody.Invoke(rig, new[] { mesh, figurePalette.GetValue(figure), true, female.GetValue(figure) });
        dressLook.Invoke(null, new[] { actor, figure, family, rig, mesh });
        model = lookBody.Invoke(null, new[] { figure, model });
        meshModel.SetValue(mesh, model);
        rig = rigOf.Invoke(null, new[] { model });
        dressBody.Invoke(rig, new[] { mesh, figurePalette.GetValue(figure), true, female.GetValue(figure) });
        dressLook.Invoke(null, new[] { actor, figure, family, rig, mesh });
        return new SurfaceBinding(actor, mesh, rig, figure, height);
    }

    /// <summary>一个真实目标的模型与独立步态；镜头中的额外采集不能重复推进它。</summary>
    private sealed class SurfaceBinding : BodySurfaceBinding
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

        /// <summary>从人物正式外观准备一次骨架。</summary>
        /// <param name="actor">实际人物。</param><param name="mesh">独立模型实例。</param><param name="rig">真实骨架。</param>
        /// <param name="figure">正式外观。</param><param name="heightScale">实际高度比例。</param>
        internal SurfaceBinding(Actor actor, object mesh, object rig, object figure, float heightScale) : base(actor)
        {
            this.mesh = mesh;
            this.rig = rig;
            this.figure = figure;
            HeightScale = heightScale;
            walker = Activator.CreateInstance(walk.FieldType, true);
            heading = actor.flip ? Mathf.PI : 0f;
            resetWalk.Invoke(walker, new object[] { heading });
            nativeParts = (Array)modelParts.GetValue(meshModel.GetValue(mesh));
            parts = new BodySurfacePart[nativeParts.Length];
            ulong mask = (ulong)hidden.GetValue(mesh);
            for (int i = 0; i < parts.Length; i++)
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
                parts[i] = new BodySurfacePart(Part(i), points, triangles);
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
            surfaceStep.Invoke(walker,
                new[]
                {
                    (object)speed, height * HeightScale, turn, heading, dt, legShare.GetValue(rig), robe.GetValue(rig)
                });
            surfacePose.Invoke(rig, new[]
            {
                mesh, walkerStride.GetValue(walker),
                walkerPhase.GetValue(walker), 0f, (float)time + (Id & 63), 0f, 0f,
                walkerRun.GetValue(walker), walker
            });
            surfacePlace.Invoke(null, new object[]
            {
                mesh, foot.x, foot.y, foot.z, heading,
                height * HeightScale * (float)bodyTall.Invoke(null, new[] { figure }) / (float)rigHeight.GetValue(rig)
            });
            var matrices = new Matrix4x4[parts.Length];
            for (int part = 0; part < parts.Length; part++) matrices[part] = WorldMatrix(mesh, part);
            return new BodySurfacePose(parts, matrices, foot, height, heading);
        }

        /// <summary>将同一共同姿态写入实际身体；父级先后顺序不影响结果。</summary>
        /// <param name="pose">已经确认的表面姿态。</param>
        internal void Display(BodySurfacePose pose)
        {
            object actual = ((IDictionary)figures.GetValue(null))[Actor];
            if (actual == null) return;
            object target = bodyMesh.GetValue(actual);
            if (target == null || meshModel.GetValue(target) != meshModel.GetValue(mesh)) return;
            Vector3 shift = WorldfallVisualBridge.BodyShift(Actor);
            Matrix4x4 shiftMatrix = Matrix4x4.Translate(shift), rootInverse = WorldMatrix(target, -1).inverse;
            var targetRotations = (Array)rotations.GetValue(target);
            var targetOffsets = (Array)offsets.GetValue(target);
            var targetScales = (float[])scales.GetValue(target);
            for (int part = 0; part < parts.Length; part++)
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
    }
}