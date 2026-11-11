using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>真正的独立龙骨架、飞行和翼膜变形接入，不使用通用动物龙姿态。</summary>
internal static partial class BodyOrganWorldfall
{
    /// <summary>独立龙的实际状态、输入和飞行类型。</summary>
    private static Type dragonStateType, dragonInputType, dragonFlightType;

    /// <summary>本版独立龙的实际入口。</summary>
    private static MethodInfo dragonIs, dragonBase, dragonRigFor, dragonNewModel, dragonApply, dragonPlace,
        dragonStep, dragonBegin, dragonBody, dragonZombiePalette;

    /// <summary>该资源包确实包含独立龙时才接入。</summary>
    private static PropertyInfo dragonReady;

    /// <summary>原模型中仅供发光、不作为实体身体的部件位。</summary>
    private static FieldInfo dragonGlowMask;

    /// <summary>外部按真实人物保存的龙状态。</summary>
    private static FieldInfo dragonStates, dragonStateFlight, dragonStateBase, dragonStateModel, dragonStateRig,
        dragonStateMesh, dragonStatePalette, dragonStateClock, dragonStateZombie, dragonStateGame, dragonStateHeld,
        dragonStateSeen, dragonFrame, dragonStateLast;

    /// <summary>真实飞行结果，姿态与显示根点均来自同一次外部求解。</summary>
    private static FieldInfo dragonFlightPose, dragonFlightPosition, dragonFlightHeading, dragonFlightPitch,
        dragonFlightRoll, dragonFlightStarted, dragonFlightHold;

    /// <summary>原生龙动作输入只在主线程边界读取。</summary>
    private static FieldInfo dragonInputX, dragonInputY, dragonInputState, dragonInputFrame, dragonInputFlip,
        dragonInputFlying, dragonInputAlive, dragonInputHostile;

    /// <summary>实际绘制期间的共同龙身体；防止同一帧再次推进飞行。</summary>
    private static DragonSurface drawingDragon;

    /// <summary>一次绑定已核对的真实龙入口。</summary>
    /// <param name="assembly">实际加载的Worldfall程序集。</param>
    private static void InstallDragons(Assembly assembly)
    {
        Type dragons = assembly.GetType("FirstPerson.Dragons", true), rig = assembly.GetType("FirstPerson.Core.DragonRig", true);
        dragonStateType = dragons.GetNestedType("State", BindingFlags.NonPublic);
        dragonInputType = assembly.GetType("FirstPerson.Core.DragonInput", true);
        dragonFlightType = assembly.GetType("FirstPerson.Core.DragonFlight", true);
        dragonIs = AccessTools.Method(dragons, "Is"); dragonReady = AccessTools.Property(dragons, "Ready");
        dragonBase = AccessTools.Method(dragons, "BaseFor"); dragonRigFor = AccessTools.Method(dragons, "RigFor");
        dragonBody = AccessTools.Method(dragons, "Body");
        dragonZombiePalette = AccessTools.Method(dragons, "ZombiePalette");
        dragonNewModel = AccessTools.Method(rig, "NewModel"); dragonApply = AccessTools.Method(rig, "Apply");
        dragonPlace = AccessTools.Method(rig, "Place"); dragonGlowMask = AccessTools.Field(rig, "GlowMask");
        dragonStep = AccessTools.Method(dragonFlightType, "Step"); dragonBegin = AccessTools.Method(dragonFlightType, "Begin");
        dragonStates = AccessTools.Field(dragons, "States");
        dragonStateFlight = AccessTools.Field(dragonStateType, "Flight"); dragonStateBase = AccessTools.Field(dragonStateType, "Base");
        dragonStateModel = AccessTools.Field(dragonStateType, "Model"); dragonStateRig = AccessTools.Field(dragonStateType, "Rig");
        dragonStateMesh = AccessTools.Field(dragonStateType, "Mesh"); dragonStatePalette = AccessTools.Field(dragonStateType, "Palette");
        dragonStateClock = AccessTools.Field(dragonStateType, "Clock"); dragonStateZombie = AccessTools.Field(dragonStateType, "Zombie");
        dragonStateGame = AccessTools.Field(dragonStateType, "GameState"); dragonStateHeld = AccessTools.Field(dragonStateType, "Held");
        dragonStateSeen = AccessTools.Field(dragonStateType, "Seen"); dragonFrame = AccessTools.Field(dragons, "_frame");
        dragonStateLast = AccessTools.Field(dragonStateType, "Last");
        dragonFlightPose = AccessTools.Field(dragonFlightType, "Pose"); dragonFlightPosition = AccessTools.Field(dragonFlightType, "Pos");
        dragonFlightHeading = AccessTools.Field(dragonFlightType, "Heading"); dragonFlightPitch = AccessTools.Field(dragonFlightType, "Pitch");
        dragonFlightRoll = AccessTools.Field(dragonFlightType, "Roll"); dragonFlightStarted = AccessTools.Field(dragonFlightType, "_started");
        dragonFlightHold = AccessTools.Field(dragonFlightType, "HoldFrame");
        dragonInputX = AccessTools.Field(dragonInputType, "X"); dragonInputY = AccessTools.Field(dragonInputType, "Y");
        dragonInputState = AccessTools.Field(dragonInputType, "State"); dragonInputFrame = AccessTools.Field(dragonInputType, "Frame");
        dragonInputFlip = AccessTools.Field(dragonInputType, "Flip"); dragonInputFlying = AccessTools.Field(dragonInputType, "Flying");
        dragonInputAlive = AccessTools.Field(dragonInputType, "Alive"); dragonInputHostile = AccessTools.Field(dragonInputType, "Hostile");
        var harmony = new Harmony("cultiway.body.dragon.worldfall");
        harmony.Patch(AccessTools.Method(dragons, "Add"), prefix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(BeforeDragonAdd)));
        harmony.Patch(dragonBody, prefix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(BeforeDragonBody)),
            postfix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(AfterDragonBody)));
        harmony.Patch(dragonBase, postfix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(AfterDragonBase)));
        harmony.Patch(dragonStep, prefix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(BeforeDragonStep)));
    }

    /// <summary>共同采集已经推进原生帧和飞行，绘制只提交同一结果。</summary>
    /// <param name="__args">原绘制参数，保持实际灯光、特效、地表与视点。</param><param name="__result">原显示结果。</param>
    private static bool BeforeDragonAdd(object[] __args, ref bool __result)
    {
        Actor actor = (Actor)__args[3];
        if (!collecting || !ConstructedAbilityService.TryGetBodySurface(actor.id, out BodySurfaceBinding binding, out _) ||
            binding is not DragonSurface surface) return true;
        object input = surface.Input(0f);
        ((IDictionary)dragonStates.GetValue(null))[actor] = surface.State;
        dragonStateSeen.SetValue(surface.State, (int)dragonFrame.GetValue(null));
        dragonStateLast.SetValue(surface.State, input);
        __result = (bool)dragonBody.Invoke(null, new[] { surface.State, actor, input, __args[0], __args[1], __args[2],
            __args[4], __args[5], 0f, __args[7], __args[8], __args[9], __args[10], __args[11], 1f });
        return false;
    }

    /// <summary>原龙绘制继续使用自己的身体和特效，接管时沿共同状态与最高精度资源。</summary>
    /// <param name="__0">原状态。</param><param name="__1">真实龙。</param><param name="__8">原推进步长。</param>
    private static void BeforeDragonBody(object __0, Actor __1, ref float __8)
    {
        patternActor = collecting ? __1 : null;
        drawingDragon = null;
        if (!collecting || !ConstructedAbilityService.TryGetBodySurface(__1.id, out BodySurfaceBinding binding, out _) ||
            binding is not DragonSurface surface) return;
        drawingDragon = surface;
        if (!ReferenceEquals(dragonStateBase.GetValue(__0), surface.Basis))
            dragonStatePalette.SetValue(__0, (bool)dragonStateZombie.GetValue(__0)
                ? dragonZombiePalette.Invoke(null, new[] { surface.Basis }) : null);
        dragonStateBase.SetValue(__0, surface.Basis); dragonStateRig.SetValue(__0, surface.Rig);
        dragonStateModel.SetValue(__0, surface.model); dragonStateFlight.SetValue(__0, surface.flight);
        dragonStateClock.SetValue(__0, surface.Clock);
        __8 = 0f;
    }

    /// <summary>按当前人物对实际龙模型派生纹样；共同接管不因镜头距离换骨架。</summary>
    /// <param name="__result">实际选定的原模型。</param>
    private static void AfterDragonBase(ref object __result)
    {
        if (drawingDragon != null) { __result = drawingDragon.Basis; return; }
        if (patternActor == null || __result == null) return;
        if (patternOrigins.TryGetValue(__result, out PatternBody old)) __result = old.Base;
        __result = PatternFor(patternActor, __result)?.Model ?? __result;
    }

    /// <summary>飞行已经沿世界时间采集，绘制不得再改变姿态或路线。</summary>
    /// <param name="__instance">外部实际飞行状态。</param>
    private static bool BeforeDragonStep(object __instance) => drawingDragon == null || !ReferenceEquals(__instance, drawingDragon.flight);

    /// <summary>实际龙的新模型保留原图层归属，接触与显示也共用翼膜变形。</summary>
    /// <param name="__0">原状态。</param><param name="__1">本次真实龙。</param>
    private static void AfterDragonBody(object __0, Actor __1)
    {
        drawingDragon = null; patternActor = null;
        if (!collecting) return;
        object basis = dragonStateBase.GetValue(__0), mesh = dragonStateMesh.GetValue(__0), model = meshModel.GetValue(mesh);
        if (model == null) return;
        if (basis != null && model != null && patternOrigins.TryGetValue(basis, out PatternBody painted))
            RegisterPatternInstance(model, painted);
        FinishPatternPalette(__0, mesh, dragonStatePalette);
        if (ConstructedAbilityService.TryGetBodySurface(__1.id, out BodySurfaceBinding binding, out BodySurfacePose pose) &&
            binding is DragonSurface surface) surface.Display(mesh, pose);
    }

    /// <summary>龙的外部状态按人物对象缓存，没有普通动物的身份检查，换世界时清除旧人物状态。</summary>
    private static void ClearDragons()
    {
        if (dragonStates != null) ((IDictionary)dragonStates.GetValue(null)).Clear();
        drawingDragon = null;
    }

    /// <summary>独立龙采用原飞行、关节和可变翼膜，共同快照不持有游戏线程之外的外部对象。</summary>
    private sealed class DragonSurface : BodySurfaceBinding
    {
        /// <summary>实际外部状态及相同的原飞行实例。</summary>
        internal readonly object State, flight;
        /// <summary>原骨架使用的静态纹样模型和骨架。</summary>
        internal readonly object Basis, Rig;
        /// <summary>本次独立数值采集实例与原翼膜模型。</summary>
        private readonly object mesh;
        /// <summary>同一部件数组和本次真实绘制模型。</summary>
        internal readonly object model;
        /// <summary>真实龙的原部件，包括两侧可变翼膜。</summary>
        private readonly Array native;
        /// <summary>静态原面分组与不复制的接触原型。</summary>
        private readonly BodySurfacePart[] surfaces;
        /// <summary>创建时的实际飞行及原生龙状态。</summary>
        private readonly bool flying;
        /// <summary>进入另一种原生状态后解除旧固定点。</summary>
        private readonly DragonState gameState;
        /// <summary>制作图重载后不继续使用旧纹样面。</summary>
        private readonly int resourceVersion;
        /// <summary>原龙的动作时钟，之后只沿实际世界时间推进。</summary>
        internal float Clock;

        /// <summary>绑定实际独立龙状态；未被镜头看见时也从同一原生输入初始化。</summary>
        /// <param name="actor">真实龙。</param>
        internal DragonSurface(Actor actor) : base(actor)
        {
            var states = (IDictionary)dragonStates.GetValue(null);
            State = states[actor];
            if (State == null)
            {
                State = Activator.CreateInstance(dragonStateType, true);
                dragonStateZombie.SetValue(State, actor.asset.id == "zombie_dragon");
                dragonStateFlight.SetValue(State, Activator.CreateInstance(dragonFlightType, (uint)actor.GetHashCode()));
                states[actor] = State;
            }
            flight = dragonStateFlight.GetValue(State);
            object original = dragonBase.Invoke(null, new object[] { 0f, null });
            Basis = PatternFor(actor, original)?.Model ?? original;
            Rig = dragonRigFor.Invoke(null, new[] { Basis });
            model = dragonNewModel.Invoke(Rig, null);
            if (patternOrigins.TryGetValue(Basis, out PatternBody painted)) RegisterPatternInstance(model, painted);
            mesh = Activator.CreateInstance(meshType); meshModel.SetValue(mesh, model); creatureEnsurePose.Invoke(mesh, null);
            dragonStatePalette.SetValue(State, (bool)dragonStateZombie.GetValue(State)
                ? dragonZombiePalette.Invoke(null, new[] { Basis }) : null);
            FinishPatternPalette(State, mesh, dragonStatePalette);
            patternMeshPalette.SetValue(mesh, dragonStatePalette.GetValue(State));
            native = (Array)modelParts.GetValue(model);
            flying = actor.isFlying(); gameState = actor.getActorComponent<Dragon>().state;
            Clock = (float)dragonStateClock.GetValue(State);
            resourceVersion = CreatureAppearanceService.ResourceVersion;
            ulong glow = (ulong)dragonGlowMask.GetValue(Rig);
            hidden.SetValue(mesh, glow);
            var parts = new List<BodySurfacePart>();
            for (int i = 0; i < native.Length; i++)
            {
                object part = native.GetValue(i);
                string name = (string)partName.GetValue(part);
                Vector3[] vertices = Points(part);
                if ((glow & 1UL << i) != 0) continue;
                PatternGroup[] regions = painted != null && i < painted.Groups.Length ? painted.Groups[i] :
                    new[] { new PatternGroup { Triangles = (int[])partTriangles.GetValue(part), Layers = Array.Empty<string>() } };
                foreach (PatternGroup region in regions)
                {
                    string[] selections = BodyOrganBinding.VisibleOrgans(actor).Where(organ => region.Layers.Contains(organ.Layer.id))
                        .Select(organ => "organ:" + organ.Id).Distinct().ToArray();
                    parts.Add(new BodySurfacePart("part:" + name, vertices, region.Triangles, transformIndex: i,
                        group: name == "head" || name == "jaw" ? "head" : name.StartsWith("tail_") ? "tail" :
                        name == "skin_l" ? "wing.left" : name == "skin_r" ? "wing.right" : name == "body" ? "torso" : null,
                        faceColors: WorldfallSurfaceColors.Read(mesh, part, region.Triangles))
                        .WithSelections(selections));
                }
            }
            surfaces = parts.ToArray();
        }

        /// <summary>实际状态、飞行或制作图变化时旧关系失效。</summary>
        internal override string ChangedReason => base.ChangedReason ??
            (resourceVersion != CreatureAppearanceService.ResourceVersion ? "OrganResourceChanged" :
             Actor.isFlying() != flying || Actor.getActorComponent<Dragon>().state != gameState ? "BodyChanged" : null);

        /// <summary>沿原龙规则采集动作帧与输入，绘制时以零步长读取，不重复推进原生动画。</summary>
        /// <param name="dt">本次世界步长。</param>
        internal object Input(float dt)
        {
            Dragon dragon = Actor.getActorComponent<Dragon>();
            SpriteAnimation animation = dragon.spriteAnimation != null ? dragon.spriteAnimation : Actor.sprite_animation;
            if ((int)dragonStateGame.GetValue(State) != (int)dragon.state)
            {
                dragonStateGame.SetValue(State, (int)dragon.state); dragonStateHeld.SetValue(State, 0f);
            }
            if (dt > 0f && animation != null)
            {
                int hold = (int)dragonFlightHold.GetValue(flight);
                float held = (float)dragonStateHeld.GetValue(State);
                if (hold > 0 && animation.currentFrameIndex == hold - 1 && held < 8f)
                {
                    animation.nextFrameTime = Mathf.Max(animation.nextFrameTime, .12f);
                    dragonStateHeld.SetValue(State, held + dt);
                }
                else if (!Actor.is_visible) animation.update(dt);
            }
            object input = Activator.CreateInstance(dragonInputType);
            dragonInputX.SetValue(input, Actor.current_position.x); dragonInputY.SetValue(input, Actor.current_position.y);
            dragonInputState.SetValue(input, (int)dragon.state); dragonInputFrame.SetValue(input, animation?.currentFrameIndex ?? 0);
            dragonInputFlip.SetValue(input, Actor.flip); dragonInputFlying.SetValue(input, Actor.isFlying());
            dragonInputAlive.SetValue(input, Actor.isAlive());
            Actor.data.get("cityToAttack", out long city, -1L);
            dragonInputHostile.SetValue(input, dragon.aggroTargets?.Count > 0 || city >= 0);
            return input;
        }

        /// <summary>按原飞行及原翼膜求解采集，数值快照复制变形顶点，旧帧不随外部数组改变。</summary>
        /// <param name="foot">共同身体根点。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        protected override BodySurfacePose Sample(Vector3 foot, float height, double time)
        {
            float dt = To == null ? 0f : (float)(time - ToTime);
            object input = Input(dt);
            AbilityHeightField terrain = ConstructedAbilityService.GroundProvider(new Rect(foot.x - 40f, foot.y - 40f, 80f, 80f));
            Func<float, float, float> ground = terrain.Sample;
            object viewer = Vector(new Vector3(foot.x, foot.y, foot.z + height));
            if (!(bool)dragonFlightStarted.GetValue(flight)) dragonBegin.Invoke(flight, new[] { input, ground });
            float remaining = dt;
            do
            {
                float step = Mathf.Min(.1f, remaining);
                dragonStep.Invoke(flight, new[] { input, step, ground, viewer });
                remaining -= step;
            } while (remaining > 0f);
            Clock += dt;
            dragonApply.Invoke(Rig, new[] { mesh, dragonFlightPose.GetValue(flight), Clock });
            dragonPlace.Invoke(null, new[] { mesh, dragonFlightPosition.GetValue(flight), dragonFlightHeading.GetValue(flight),
                dragonFlightPitch.GetValue(flight), dragonFlightRoll.GetValue(flight), .8f });
            var matrices = new Matrix4x4[native.Length];
            for (int i = 0; i < matrices.Length; i++) matrices[i] = BodyWorldfallRenderer.WorldMatrix(mesh, i);
            var deformed = new Vector3[surfaces.Length][];
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i].Id is "part:skin_l" or "part:skin_r") deformed[i] = Points(native.GetValue(surfaces[i].TransformIndex));
            return new BodySurfacePose(surfaces, matrices, foot, height, (float)dragonFlightHeading.GetValue(flight)).Deformed(deformed);
        }

        /// <summary>显示沿已确认的关节和翼膜，接触被冻结时翼膜也停在同一真实形状。</summary>
        /// <param name="actual">原模组实际提交的龙。</param><param name="pose">已确认或维持中的真实表面。</param>
        internal void Display(object actual, BodySurfacePose pose)
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i].Id is not ("part:skin_l" or "part:skin_r")) continue;
                object part = native.GetValue(surfaces[i].TransformIndex);
                var positions = (float[])partPositions.GetValue(part);
                Vector3[] vertices = pose.Vertices(i);
                for (int v = 0; v < vertices.Length; v++)
                { positions[v * 3] = vertices[v].x; positions[v * 3 + 1] = vertices[v].y; positions[v * 3 + 2] = vertices[v].z; }
                RebuildSkinNormals(part);
            }
            DisplaySurface(actual, native, pose, Vector3.zero);
        }

        /// <summary>复制本轮原模型的真实顶点。</summary>
        /// <param name="part">实际部件。</param>
        private static Vector3[] Points(object part)
        {
            var raw = (float[])partPositions.GetValue(part);
            var result = new Vector3[raw.Length / 3];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(raw[i * 3], raw[i * 3 + 1], raw[i * 3 + 2]);
            return result;
        }

        /// <summary>冻结后的真实翼膜按同一三角面恢复法线，避免沿用了后续帧的光照。</summary>
        /// <param name="part">本轮实际翼膜。</param>
        private static void RebuildSkinNormals(object part)
        {
            Vector3[] points = Points(part), normals = new Vector3[points.Length];
            var triangles = (int[])partTriangles.GetValue(part);
            var axes = (byte[])patternAxes.GetValue(part);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                normals[a] += normal; normals[b] += normal; normals[c] += normal;
                Vector3 absolute = new(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                axes[i / 3] = (byte)(absolute.x >= absolute.y && absolute.x >= absolute.z ? 0 : absolute.y >= absolute.z ? 1 : 2);
            }
            var raw = (float[])patternNormals.GetValue(part);
            for (int i = 0; i < normals.Length; i++)
            { Vector3 normal = normals[i].normalized; raw[i * 3] = normal.x; raw[i * 3 + 1] = normal.y; raw[i * 3 + 2] = normal.z; }
            patternVersion.SetValue(part, (int)patternVersion.GetValue(part) + 1);
        }
    }
}
