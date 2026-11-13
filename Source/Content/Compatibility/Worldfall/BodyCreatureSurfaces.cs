using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Cultiway.Content.Skills;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
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

    /// <summary>普通显示已有步态和实际动画时间，用于接管时接续当前帧。</summary>
    private static FieldInfo creaturePhase;

    /// <summary>外部显式冻结制作姿态时保留其冻结规则。</summary>
    private static Func<bool> creatureClockFrozen;

    /// <summary>从原模型实际足垫上沿制作的切面，不套用人类足长。</summary>
    private static Dictionary<string, float> creatureFootRegions;

    /// <summary>只在安装时绑定原模组的固定入口。</summary>
    /// <param name="assembly">已加载的三维模组。</param>
    private static void InstallSurfaces(Assembly assembly)
    {
        creatureFootRegions = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float>>(File.ReadAllText(
            Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content/BodyVisuals/worldfall-foot-regions.json")));
        if (creatureFootRegions.Any(entry => float.IsNaN(entry.Value) || float.IsInfinity(entry.Value) || entry.Value <= 0f))
            throw new InvalidDataException("三维实际足部区域需要正有限高度");
        Type rig = assembly.GetType("FirstPerson.Core.CreatureRig", true);
        creatureHeight = AccessTools.Property(rig, "Height");
        creatureLegLength = AccessTools.Property(rig, "LegLength");
        creatureSwing = AccessTools.Property(rig, "SwingPerStride");
        creatureHover = AccessTools.Method(assembly.GetType("FirstPerson.Creatures", true), "Hover");
        creatureEnsurePose = AccessTools.Method(meshType, "EnsurePose");
        creaturePhase = AccessTools.Field(stateMesh.DeclaringType, "Phase");
        Type clock = assembly.GetType("FirstPerson.PoseClock", true);
        creatureClockFrozen = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), AccessTools.PropertyGetter(clock, "Frozen"));
        new Harmony("cultiway.body.creature.clock.worldfall").Patch(AccessTools.PropertyGetter(clock, "Now"),
            postfix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(AfterCreatureClock)));
        InstallDragons(assembly);
    }

    /// <summary>世界动物的悬浮和振翼沿共同世界时钟；人物卡片及显式制作冻结保持原规则。</summary>
    /// <param name="__result">本次原模组实际使用的动画时点。</param>
    private static void AfterCreatureClock(ref float __result)
    {
        if (collecting && patternActor != null && !creatureClockFrozen()) __result = (float)World.world.getCurWorldTime();
    }

    /// <summary>动物沿真实骨架采集，龙使用实际独立龙骨架。</summary>
    /// <param name="actor">真实目标。</param>
    internal static BodySurfaceBinding CreateSurface(Actor actor)
    {
        if ((bool)dragonIs.Invoke(null, new object[] { actor }) && (bool)dragonReady.GetValue(null))
            return new DragonSurface(actor);
        string family = (string)familyOf.Invoke(null, new object[] { actor });
        if (family == null || !family.StartsWith("animal_", StringComparison.Ordinal)) return null;
        object image = BodyWorldfallRenderer.SurfaceImage(actor);
        if (image == null) return null;
        object model = detailed.Invoke(null, new object[] { family, 0f, null });
        if (model == null) return null;
        model = PatternFor(actor, model)?.Model ?? model;
        object state = stateOf.Invoke(null, new object[] { actor, family });
        return new CreatureSurface(actor, family, model, state, image);
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
            ConstructedAbilityService.TryGetBodyPose(actor.id, out var active) && active.Joint == null ||
            ReferenceEquals(surface.State, figure.GetValue(null))) return false;
        Vector3 foot = pose.Foot + WorldfallVisualBridge.BodyShift(actor);
        x = foot.x;
        y = foot.y;
        z = foot.z;
        heading = pose.Heading;
        speed = dt = walkDt = distance = 0f;
        swimming = surface.Swimming;
        stateScale.SetValue(surface.State, surface.Scale);
        creaturePhase.SetValue(surface.State, surface.Phase);
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

        /// <summary>沿原外观缓存建立借出器官后的独立采集。</summary>
        private readonly object image;

        /// <summary>原模型固定部件及其共享数值表面。</summary>
        private readonly Array native;

        /// <summary>不随每轮姿态重建的真实三角面。</summary>
        private readonly BodySurfacePart[] surfaces;

        /// <summary>贴身制作图改变后不再沿用旧面分组。</summary>
        private readonly int resourceVersion;

        /// <summary>正常显示的附加器官，沿真实承接部件采集，不扩大原动物包络。</summary>
        private readonly List<OrganSurface> organs = new();

        /// <summary>一个真实器官与实际身体父部件的固定对应。</summary>
        private sealed class OrganSurface
        {
            /// <summary>稳定器官、外观层及原模型。</summary>
            internal string Id, Layer;

            /// <summary>实际共享模型及其版本。</summary>
            internal BodyOrganModel Model;

            /// <summary>承接部件及本表面的变换编号。</summary>
            internal int Parent, Transform;

            /// <summary>身体父部件内的真实安装变换。</summary>
            internal Matrix4x4 Local;

            /// <summary>明确被升级器官替换的原部件。</summary>
            internal string[] Replaces;

            /// <summary>本次已借出，仅保留原部件替换规则，不重复生成静态器官。</summary>
            internal bool Borrowed;
        }

        /// <summary>真实物种模型及成年状态，改变后不继续沿旧骨架控制。</summary>
        private readonly string family;

        /// <summary>出生阶段参与原模型比例。</summary>
        private readonly bool adult;

        /// <summary>进入或离开飞行、液体状态时旧关系必须解除。</summary>
        private readonly bool flying, swimming, hovering;

        /// <summary>该物种真实悬浮偏移。</summary>
        private readonly float hover;

        /// <summary>原个体的固定步态种子。</summary>
        private readonly float seed;

        /// <summary>游戏时间推进的步态和实际朝向。</summary>
        private float phase, heading;

        /// <summary>绘制接续共同步态，释放后不从旧相位重新开始。</summary>
        internal float Phase => phase;

        /// <summary>绘制保留原本游泳的水中显示标记。</summary>
        internal bool Swimming => swimming;

        /// <summary>从原资源采集模型，普通身体和头尾等原部件保留可选名称。</summary>
        /// <param name="actor">实际动物。</param><param name="family">原模型家族。</param>
        /// <param name="model">实际详细模型。</param><param name="state">原个体外观。</param>
        /// <param name="image">原图片缓存已经完成的真实帧。</param><param name="excludedOrganLayer">本次借出的外观层。</param>
        internal CreatureSurface(Actor actor, string family, object model, object state, object image, string excludedOrganLayer = null) : base(actor)
        {
            this.image = image;
            this.family = family;
            this.model = model;
            resourceVersion = CreatureAppearanceService.ResourceVersion;
            State = state;
            adult = actor.isAdult();
            flying = actor.isFlying();
            hovering = actor.isHovering();
            swimming = WorldfallVisualBridge.IsSwimming(actor);
            hover = (float)creatureHover.Invoke(null, new object[] { family });
            phase = (float)creaturePhase.GetValue(state);
            seed = (float)stateSeed.GetValue(state);
            // 与原Creatures.Add的动物比例一致，查询不能取决于是否已被镜头绘制。
            Scale = .5714286f * (adult ? 1f : .55f) * (.94f + .12f * (seed % 1f));
            rig = rigOf.Invoke(null, new[] { model });
            mesh = Activator.CreateInstance(meshType);
            meshModel.SetValue(mesh, model);
            creatureEnsurePose.Invoke(mesh, null);
            if (patternStatePalette.GetValue(state) == null)
            {
                object basis = patternOrigins.TryGetValue(model, out PatternBody patterned) ? patterned.Base : model;
                patternStatePalette.SetValue(state, WorldfallSurfaceColors.CreaturePalette(basis, image));
            }
            FinishPatternPalette(state, mesh, patternStatePalette);
            patternMeshPalette.SetValue(mesh, patternStatePalette.GetValue(state));
            native = (Array)modelParts.GetValue(model);
            var parts = new List<BodySurfacePart>();
            foreach (var visible in BodyOrganBinding.VisibleOrgans(actor))
            {
                BodyOrganModel organ = BodyOrganWorldfall.Capture(actor, visible.Layer);
                if (organ == null) continue;
                int parent = Enumerable.Range(0, native.Length).First(i =>
                    (string)partName.GetValue(native.GetValue(i)) == organ.Parent);
                organs.Add(new OrganSurface
                {
                    Id = "organ:" + visible.Id, Layer = visible.Layer.id, Model = organ, Parent = parent,
                    Transform = native.Length + organs.Count, Replaces = models[visible.Layer.id].Replaces,
                    Borrowed = visible.Layer.id == excludedOrganLayer,
                    Local = organ.Parts[organ.Parent].inverse * Matrix4x4.TRS(organ.Mount, organ.Rotation,
                        Vector3.one * (organ.Scale / organ.BodyScale))
                });
            }

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

                if (patternOrigins.TryGetValue(model, out PatternBody painted) && !organs.Any(o => o.Replaces.Contains(name)))
                {
                    foreach (PatternGroup region in painted.Groups[i])
                    {
                        string[] selections = BodyOrganBinding.VisibleOrgans(actor)
                            .Where(visible => region.Layers.Contains(visible.Layer.id))
                            .Select(visible => "organ:" + visible.Id).Distinct().ToArray();
                        parts.Add(new BodySurfacePart("part:" + name, vertices, region.Triangles,
                            transformIndex: i, group: group, faceColors: WorldfallSurfaceColors.Read(mesh, part, region.Triangles)).WithSelections(selections));
                    }
                }
                else parts.Add(new BodySurfacePart("part:" + name, vertices,
                        organs.Any(o => o.Replaces.Contains(name)) ? Array.Empty<int>() : (int[])partTriangles.GetValue(part),
                        transformIndex: i, group: group, faceColors: WorldfallSurfaceColors.Read(mesh, part)));
            }

            foreach (OrganSurface organ in organs.Where(organ => !organ.Borrowed))
                for (int part = 0; part < organ.Model.Resource.Meshes.Length; part++)
                {
                    Mesh resource = organ.Model.Resource.Meshes[part];
                    string control = organ.Model.Resource.Description.Parts[part].Control;
                    parts.Add(new BodySurfacePart(string.IsNullOrEmpty(control) ? organ.Id : organ.Id + "/" + control, resource.vertices, resource.triangles,
                        transformIndex: organ.Transform, group: models[organ.Layer].Mount == "tail" ? "tail" : null,
                        vertexColors: WorldfallSurfaceColors.Read(organ.Model.Resource, part)).WithSelections(
                        organ.Model.Parent == "head" ? new[] { organ.Id, "head" } : new[] { organ.Id }));
                }

            surfaces = parts.ToArray();

            heading = actor.flip ? Mathf.PI : 0f;
        }

        /// <summary>共用原详细模型，为本次借出建立独立身体，原替换部件仍保持隐藏。</summary>
        /// <param name="organ">实际借出的器官。</param>
        internal override BodySurfaceBinding ExcludingOrgan(BodyOrganBinding organ) => new CreatureSurface(Actor, family, model, State, image, organ.LayerKey);

        /// <summary>成年、飞行或液体状态改变后，旧状态的身体关系失效。</summary>
        internal override string ChangedReason => base.ChangedReason ??
                                                  (resourceVersion != CreatureAppearanceService.ResourceVersion
                                                      ? "OrganResourceChanged" : null) ??
                                                  (organs.Any(o =>
                                                      !ReferenceEquals(
                                                          FormResources.Load(o.Model.Resource.Description.Id),
                                                          o.Model.Resource))
                                                      ? "OrganResourceChanged"
                                                      : null) ??
                                                  (Actor.isAdult() != adult || Actor.isFlying() != flying || Actor.isHovering() != hovering ||
                                                   WorldfallVisualBridge.IsSwimming(Actor) != swimming ||
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
            if (leg > .02f && !swimming)
            {
                float angle = Mathf.Clamp(.22f + .1f * speed / leg, .22f, .6f);
                stride = Mathf.Clamp01(speed / Mathf.Max(.05f, leg * .6f)) * angle /
                         (float)creatureSwing.GetValue(rig);
                phase += dt * Mathf.PI * speed / (2f * leg * Mathf.Sin(angle));
            }
            else phase += dt * (speed / Mathf.Max(.04f, bodyHeight * .5f) + (swimming ? 2f : 0f));

            float attack = (float)(time - Actor.last_attack_timestamp);
            attack = attack >= 0f && attack < .45f ? attack / .45f : 0f;
            float poseTime = (float)time;
            poseRig.Invoke(rig, new[] { mesh, (object)(swimming ? Mathf.Max(.4f, stride) : stride), phase,
                poseTime, hover > 0f, attack, seed });
            Vector3 root = swimming ? foot + NativeOffset(Actor) : WorldfallVisualBridge.SurfaceRoot(Actor, foot);
            float z = root.z;
            if (hover > 0f) z += hover + .08f * Mathf.Sin(poseTime * 2.3f + seed * 5f);
            if (swimming) z = -bodyHeight * (family == "animal_fish" ? .75f : .5f) + .02f * Mathf.Sin(poseTime * 2f + seed);
            place.Invoke(mesh, new object[] { root.x, foot.y, z, heading, Scale, Scale, Scale });
            var matrices = new Matrix4x4[native.Length + organs.Count];
            for (int i = 0; i < native.Length; i++) matrices[i] = BodyWorldfallRenderer.WorldMatrix(mesh, i);
            foreach (OrganSurface organ in organs) matrices[organ.Transform] = matrices[organ.Parent] * organ.Local;
            return new BodySurfacePose(surfaces, matrices, foot, height, heading);
        }

        /// <summary>独立器官出招时，身体沿已绑定的实际站立骨架；其余器官仍随对应父部件。</summary>
        /// <param name="basis">普通步态表面。</param><param name="stance">器官动作的整身姿态。</param>
        /// <param name="head">头部动作。</param><param name="right">右臂。</param><param name="left">左臂。</param>
        /// <param name="organ">本次真正受控的器官。</param>
        internal override BodySurfacePose Acting(BodySurfacePose basis, BodyPose? stance, BodyPose? head,
            BodyPose? right, BodyPose? left, BodyOrganBinding organ)
        {
            if (organ?.Model == null || !stance.HasValue) return basis;
            BodyPose pose = stance.Value;
            float angle = Mathf.Atan2(pose.Forward.y, pose.Forward.x);
            float scale = organ.Model.BodyScale;
            place.Invoke(mesh, new object[] { pose.Foot.x, pose.Foot.y, pose.Foot.z, angle, scale, scale, scale });
            Matrix4x4 root = BodyWorldfallRenderer.WorldMatrix(mesh, -1);
            var matrices = new Matrix4x4[basis.Matrices.Length];
            for (int i = 0; i < native.Length; i++)
                matrices[i] = root * organ.Model.Parts[(string)partName.GetValue(native.GetValue(i))];
            foreach (OrganSurface extra in organs) matrices[extra.Transform] = matrices[extra.Parent] * extra.Local;
            return new BodySurfacePose(surfaces, matrices, pose.Foot, basis.Height, angle);
        }

        /// <summary>实际动物显示读取已经确认的矩阵，不另求一轮抓持位置。</summary>
        /// <param name="pose">本轮共同姿态。</param>
        internal void Display(BodySurfacePose pose)
        {
            object actual = stateMesh.GetValue(State);
            if (ReferenceEquals(State, figure.GetValue(null)) || meshModel.GetValue(actual) != model) return;
            DisplaySurface(actual, native, pose, Vector3.zero);
        }

        /// <summary>保留原动物真实骨段、父子和关节；足底从原腿部最低面取得。</summary>
        /// <param name="surface">同轮详细身体表面。</param>
        internal override BodyStructure Structure(BodySurfacePose surface)
        {
            var bones = new List<BodyStructureBone>();
            var supports = new List<string>();
            for (int i = 0; i < native.Length; i++)
            {
                object part = native.GetValue(i);
                int parent = (int)partParent.GetValue(part);
                string name = (string)partName.GetValue(part), selection = "part:" + name;
                Matrix4x4 matrix = surface.Matrices[i];
                bones.Add(new BodyStructureBone(selection,
                    parent < 0 ? null : "part:" + (string)partName.GetValue(native.GetValue(parent)),
                    matrix.MultiplyPoint3x4(Read(partPivot.GetValue(part))),
                    Quaternion.LookRotation(-matrix.MultiplyVector(Vector3.up), matrix.MultiplyVector(Vector3.forward))));
                // 这些名称是CreatureRig明确驱动的原兽足骨段；不将尾、翅或任意低处网格当足。
                if (name is "leg_fl" or "leg_fr" or "leg_bl" or "leg_br" or "leg_l" or "leg_r") supports.Add(selection);
            }
            return new BodyStructure(surface, bones, supports);
        }

        /// <summary>只裁出原模型已经制作的足垫区域，原完整模型仍由同一姿态绘制。</summary>
        /// <param name="structure">同轮真实身体。</param><param name="part">原骨段。</param>
        internal override BodySurfacePose JointContact(BodyStructure structure, string part)
        {
            if (!creatureFootRegions.TryGetValue(family, out float top) || !structure.Supports.Any(support => support.Part == part)) return null;
            var parts = new List<BodySurfacePart>();
            foreach (BodySurfacePart source in structure.Surface.Parts.Where(piece => piece.Matches(part)))
            {
                BodySurfacePartition.Split(source, part + "/paw", new Vector3(0f, 0f, top), Vector3.back, out _, out BodySurfacePart paw);
                if (paw.Triangles.Length > 0) parts.Add(paw);
            }
            return parts.Count == 0 ? null : new BodySurfacePose(parts.ToArray(), structure.Surface.Matrices,
                structure.Surface.Foot, structure.Surface.Height, structure.Surface.Heading);
        }

        /// <summary>原版悬浮是身体真实显示位置的一部分，先进入共同几何，显示时不重复加高。</summary>
        /// <param name="actor">实际动物。</param>
        private static Vector3 NativeOffset(Actor actor) => new(actor.move_jump_offset.x, 0f,
            actor.asset.hovering || actor.isHovering() ? actor.move_jump_offset.y : 0f);
    }

    /// <summary>实际动物和龙的绘制沿相同矩阵还原关节，不再次求接触或动作。</summary>
    /// <param name="actual">实际绘制实例。</param><param name="parts">该模型原部件。</param>
    /// <param name="pose">已经确认的共同表面。</param><param name="offset">原版地表到显示场景的偏移。</param>
    private static void DisplaySurface(object actual, Array parts, BodySurfacePose pose, Vector3 offset)
    {
        var turns = (Array)rotations.GetValue(actual);
        var shifts = (Array)offsets.GetValue(actual);
        var sizes = (float[])scales.GetValue(actual);
        Matrix4x4 shift = Matrix4x4.Translate(offset), root = BodyWorldfallRenderer.WorldMatrix(actual, -1).inverse;
        for (int i = 0; i < parts.Length; i++)
        {
            int parent = (int)partParent.GetValue(parts.GetValue(i));
            Matrix4x4 local = (parent < 0 ? root : (shift * pose.Matrices[parent]).inverse) * shift * pose.Matrices[i];
            Vector3 pivot = Read(partPivot.GetValue(parts.GetValue(i)));
            turns.SetValue(Matrix(local), i);
            shifts.SetValue(Vector(local.MultiplyPoint3x4(pivot) - pivot), i);
            sizes[i] = 1f;
        }
        cacheModel.SetValue(actual, null);
    }
}
