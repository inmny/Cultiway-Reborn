using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cultiway.Content.Combat;
using Cultiway.Content.KnightCombat;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Systems.Render;
using Cultiway.Core.Visuals;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>将共用空间快照追加到 Worldfall 0.9.8 的场景，不加载或分发其程序集。</summary>
internal static class WorldfallVisualBridge
{
    /// <summary>仅用于本模组可选显示接入的补丁编号。</summary>
    private const string HarmonyId = "Cultiway.Worldfall.SpatialVisuals";

    /// <summary>程序集加载通知只登记候选，在游戏主线程完成接入。</summary>
    private static Assembly pendingAssembly;

    /// <summary>已经核对过的外部结构。</summary>
    private static Binding binding;

    /// <summary>遇到外部结构不符后停止该次接入，避免逐帧重复报错。</summary>
    private static bool disabled;

    /// <summary>共用模型到外部模型的转换缓存。</summary>
    private static readonly Dictionary<(SpatialVisualMesh, SpatialSurface), object> models = new();

    /// <summary>复用外部实例，避免每帧重新分配模型和上传几何。</summary>
    private static readonly List<object> instances = new();

    /// <summary>同一场景收集内，身体相连的部件复用承托修正。</summary>
    private static readonly Dictionary<Actor, Vector3> bodyShifts = new();

    /// <summary>供游戏验证读取的当前接入状态。</summary>
    internal static string Status { get; private set; } = "未加载 Worldfall";

    /// <summary>当前是否已有可用的三维消费者。</summary>
    internal static bool IsInstalled => binding != null && !disabled;

    /// <summary>最近一次实际提交的模型数量。</summary>
    internal static int LastMeshCount { get; private set; }

    /// <summary>最近一次实际提交的路径数量。</summary>
    internal static int LastLineCount { get; private set; }

    /// <summary>最近一次转换与追加耗时，不含外部模组绘制。</summary>
    internal static double LastSubmitMilliseconds { get; private set; }

    /// <summary>最近实际收集场景的视点，用于共用声音定位。</summary>
    private static Vector3 listenerPosition, listenerRight;

    /// <summary>视点只在对应场景仍在绘制时有效。</summary>
    private static int listenerFrame = -10;

    /// <summary>三维视角启用时提供其真实视点；否则保留调用方的原版镜头。</summary>
    /// <param name="position">视点位置。</param>
    /// <param name="right">视点右侧方向。</param>
    internal static void TryGetListener(ref Vector3 position, ref Vector3 right)
    {
        if (!IsInstalled || Time.frameCount - listenerFrame > 2) return;
        position = listenerPosition;
        right = listenerRight;
    }

    /// <summary>发现已经加载或稍后加载的 Worldfall；不要求玩家安装它。</summary>
    internal static void Initialize()
    {
        SpatialCombatFeedback.AudioPosition = ToAudioPosition;
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) Discover(assembly);
    }

    /// <summary>让游戏原有声音素材相对实际三维视点定位，沿用游戏已有音量与素材管理。</summary>
    /// <param name="position">真实作用的地图坐标。</param>
    private static Vector3 ToAudioPosition(Vector3 position)
    {
        if (!IsInstalled || Time.frameCount - listenerFrame > 2) return position;
        Vector3 delta = position - listenerPosition;
        delta.z = 0f;
        Vector3 camera = World.world.camera.transform.position;
        return new Vector3(camera.x + Vector3.Dot(delta, listenerRight),
            camera.y + Vector3.Dot(delta, Vector3.Cross(Vector3.forward, listenerRight)));
    }

    /// <summary>登记可选依赖，不在程序集加载线程操作 Unity 或安装补丁。</summary>
    /// <param name="sender">程序集加载通知来源。</param>
    /// <param name="args">刚加载的程序集。</param>
    private static void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args) => Discover(args.LoadedAssembly);

    /// <summary>通过实际入口类型识别包，而非依赖机器上的安装路径。</summary>
    /// <param name="assembly">当前已经加载的程序集。</param>
    private static void Discover(Assembly assembly)
    {
        if (assembly.GetType("FirstPerson.WorldBoxMod", false) != null) pendingAssembly = assembly;
    }

    /// <summary>只在主线程首次发现依赖时核对结构并安装场景收集后的追加入口。</summary>
    internal static void InstallIfNeeded()
    {
        if (disabled) return;
        if (binding != null)
        {
            FormWorldfallRenderer.ClearIfNative();
            return;
        }

        if (pendingAssembly == null) return;
        try
        {
            Binding candidate = new(pendingAssembly);
            var terrainSource = new WorldfallTerrainSource(pendingAssembly);
            new Harmony(HarmonyId).Patch(candidate.Collect,
                postfix: new HarmonyMethod(typeof(WorldfallVisualBridge), nameof(AfterCollect)));
            new Harmony(HarmonyId).Patch(candidate.PlaceItem,
                prefix: new HarmonyMethod(typeof(WorldfallVisualBridge), nameof(BeforePlaceItem)));
            new Harmony(HarmonyId).Patch(candidate.PlaceArm,
                prefix: new HarmonyMethod(typeof(WorldfallVisualBridge), nameof(BeforePlaceArm)));
            new Harmony(HarmonyId).Patch(candidate.PossessedStep,
                prefix: new HarmonyMethod(typeof(WorldfallVisualBridge), nameof(BeforePossessedStep)));
            new Harmony(HarmonyId).Patch(candidate.KeepBodyClear,
                prefix: new HarmonyMethod(typeof(WorldfallVisualBridge), nameof(BeforeKeepBodyClear)));
            new Harmony(HarmonyId).Patch(candidate.DrawThrown,
                prefix: new HarmonyMethod(typeof(WorldfallVisualBridge), nameof(BeforeDrawThrown)));
            binding = candidate;
            FormWorldfallRenderer.Install(pendingAssembly);
            BodyWorldfallRenderer.Install(pendingAssembly);
            terrainSource.Install();
            RenderMotionRibbonTrailSystem.SpatialHistoryRequested = true;
            Status = "已接入 Worldfall 0.9.8 共用空间战斗表现";
            ModClass.LogInfo(Status);
        }
        catch (Exception exception)
        {
            // 可选模组的结构和补丁入口是外部边界；接入失败不应中断原版战斗。
            Disable(exception);
        }
    }

    /// <summary>近战武器已交给真实运动体时，只隐藏第一人称重复的静态持物，不改变持有和攻击状态。</summary>
    private static bool BeforePlaceItem()
    {
        if (disabled || binding == null) return true;
        object root = binding.RootInstance.GetValue(null);
        Actor actor = root == null ? null : (Actor)binding.RootHost.GetValue(root);
        return root == null || !KnightTechniqueVisuals.IsWeaponDetached(actor) &&
            !Skills.ConstructedAbilityService.IsWeaponBorrowed(actor);
    }

    /// <summary>按本轮已经发布的左右手分别替换第一人称手臂，预留但未接管的部位保持原显示。</summary>
    /// <param name="__3">是否为左臂。</param>
    private static bool BeforePlaceArm(bool __3)
    {
        if (disabled || binding == null) return true;
        object root = binding.RootInstance.GetValue(null);
        Actor actor = root == null ? null : (Actor)binding.RootHost.GetValue(root);
        return actor == null || !Skills.ConstructedAbilityService.TryGetBodyPose(actor.getID(),
            __3 ? Core.Skills.BodyOccupancy.LeftArm : Core.Skills.BodyOccupancy.RightArm, out _);
    }

    /// <summary>外部模组替换了原版受控移动，需在它实际使用的入口遵守同一动作占用。</summary>
    /// <param name="__0">当前受控人物。</param>
    /// <param name="__result">原地动作期间的主动移动量。</param>
    private static bool BeforePossessedStep(Actor __0, ref float __result)
    {
        if (disabled || !CombatWorldService.ShouldPauseMovement(__0)) return true;
        __result = 0f;
        return false;
    }

    /// <summary>原地动作期间暂缓外部视角的重叠推离；动作结束后继续它原有的碰撞处理。</summary>
    /// <param name="__0">正在完成原地动作的人物。</param>
    private static bool BeforeKeepBodyClear(Actor __0) => disabled || __0 == null ||
                                                          !CombatWorldService.ShouldPauseMovement(__0);

    /// <summary>御器代理弹道已由真实投射物快照显示，避免外部按未知编号再叠一张平面图片。</summary>
    /// <param name="__0">正在绘制的实际原版投射物。</param><param name="__result">是否已经提供绘制。</param>
    private static bool BeforeDrawThrown(Projectile __0, ref bool __result)
    {
        if (disabled || __0.asset == null ||
            !__0.asset.id.StartsWith(WeaponControl.WeaponControlProjectileProxyLibrary.ProxyIdPrefix,
                StringComparison.Ordinal)) return true;
        __result = true;
        return false;
    }

    /// <summary>读取同一份快照；此回调可能为多个视角执行，绝不推进状态或产生玩法作用。</summary>
    /// <param name="__instance">外部场景收集器。</param>
    /// <param name="__0">该次绘制的视点。</param>
    /// <param name="__1">该次可见范围。</param>
    /// <param name="__3">外部地面采样包装。</param>
    /// <param name="__4">本次场景使用的原版人物图片缓存。</param>
    private static void AfterCollect(object __instance, object __0, float __1, object __3, object __4)
    {
        if (disabled || binding == null) return;
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            object terrain = binding.SamplerCache.GetValue(__3);
            Func<float, float, float> ground = (Func<float, float, float>)Delegate.CreateDelegate(
                typeof(Func<float, float, float>), terrain, binding.SampleHeight);
            bodyShifts.Clear();
            float eyeX = (float)binding.ViewX.GetValue(__0);
            float eyeY = (float)binding.ViewY.GetValue(__0);
            float yaw = (float)binding.ViewYaw.GetValue(__0);
            FormWorldfallRenderer.Prepare(ground, id =>
            {
                Actor actor = World.world.units.get(id);
                return actor == null ? Vector3.zero : ResolveBodyShift(actor, terrain, __4, ground);
            });
            IList targetMeshes = (IList)binding.SceneMeshes.GetValue(__instance);
            IList targetLines = (IList)binding.SceneStreaks.GetValue(__instance);
            listenerPosition = new Vector3(eyeX, eyeY);
            listenerRight = new Vector3(Mathf.Sin(yaw), -Mathf.Cos(yaw));
            listenerFrame = Time.frameCount;
            SpatialVisualEffects.SetView(new Vector2(eyeX, eyeY), __1);
            LastMeshCount = 0;
            LastLineCount = 0;
            for (int i = 0; i < CombatSpatialVisualSystem.Meshes.Count; i++)
            {
                SpatialVisualMeshInstance draw = CombatSpatialVisualSystem.Meshes[i];
                if (Vector2.Distance(draw.TransformPoint(draw.Model.Bounds.center), new Vector2(eyeX, eyeY)) >
                    __1 + Vector3.Scale(draw.Model.Bounds.extents, draw.Scale).magnitude) continue;
                if (!models.TryGetValue((draw.Model, draw.Surface), out object model))
                {
                    model = ConvertModel(draw.Model, draw.Surface);
                    models.Add((draw.Model, draw.Surface), model);
                }

                if (LastMeshCount == instances.Count) instances.Add(Activator.CreateInstance(binding.Instance.Type));
                object instance = instances[LastMeshCount++];
                binding.Instance.Set(instance, "Model", model);
                Vector3 shift = draw.GroundActor == null
                    ? Vector3.zero
                    : ResolveBodyShift(draw.GroundActor, terrain, __4, ground);
                float baseHeight = ground(draw.GroundAnchor.x, draw.GroundAnchor.y);
                Vector3 position = draw.Position + shift + Vector3.forward * baseHeight;
                binding.Instance.Set(instance, "Position", binding.Vector(position.x, position.y, position.z));
                Quaternion rotation = draw.Rotation;
                Vector3 scale = draw.Scale;
                if (draw.Model == SpatialVisualGeometry.Rod)
                {
                    Vector3 end = draw.TransformPoint(Vector3.forward) + shift;
                    end.z += draw.GroundActor == null ? ground(end.x, end.y) : baseHeight;
                    Vector3 delta = end - position;
                    rotation = Quaternion.FromToRotation(Vector3.forward, delta);
                    scale.z = delta.magnitude;
                }

                binding.Instance.Set(instance, "Basis", binding.Matrix(rotation, scale));
                binding.Instance.Set(instance, "Multiply", Pack(draw.Tint));
                binding.Instance.Set(instance, "Fade", draw.Tint.a);
                bool lit = draw.Surface is SpatialSurface.Stone or SpatialSurface.Water or SpatialSurface.Ice
                    or SpatialSurface.Foliage or SpatialSurface.Wood or SpatialSurface.Metal;
                binding.Instance.Set(instance, "Emissive", draw.Emissive && !lit);
                binding.Instance.Set(instance, "Tag", -2);
                binding.Instance.Set(instance, "KeepSmall", true);
                targetMeshes.Add(instance);
            }

            for (int i = 0; i < RenderMotionRibbonTrailSystem.SpatialLines.Count; i++)
            {
                SpatialVisualLine line = RenderMotionRibbonTrailSystem.SpatialLines[i];
                if (Vector2.Distance(line.End, new Vector2(eyeX, eyeY)) > __1 + 8f) continue;
                object streak = Activator.CreateInstance(binding.Streak.Type);
                binding.Streak.Set(streak, "A", binding.Vector(line.Start.x, line.Start.y,
                    ground(line.Start.x, line.Start.y) + line.Start.z));
                binding.Streak.Set(streak, "B", binding.Vector(line.End.x, line.End.y,
                    ground(line.End.x, line.End.y) + line.End.z));
                binding.Streak.Set(streak, "Color", Pack(line.Color));
                binding.Streak.Set(streak, "AlphaA", line.Color.a);
                binding.Streak.Set(streak, "AlphaB", line.Color.a);
                binding.Streak.Set(streak, "Width", line.Width);
                binding.Streak.Set(streak, "Glow", 0.15f);
                targetLines.Add(streak);
                LastLineCount++;
            }
        }
        catch (Exception exception)
        {
            // 外部绘制资料可能随版本变化；在唯一显示边界报告并停用接入，不干预玩法实体。
            Disable(exception);
        }

        LastSubmitMilliseconds = watch.Elapsed.TotalMilliseconds;
    }

    /// <summary>与外部模组实际身体使用同一水面、栈桥和跳起规则，不改变二维位置与玩法高度。</summary>
    /// <param name="actor">实际身体。</param><param name="terrain">本帧地形缓存。</param>
    /// <param name="sprites">本帧图片缓存。</param><param name="ground">已绑定的地表采样。</param>
    private static Vector3 ResolveBodyShift(Actor actor, object terrain, object sprites,
        Func<float, float, float> ground)
    {
        if (bodyShifts.TryGetValue(actor, out Vector3 cached)) return cached;
        float x = actor.current_position.x + actor.move_jump_offset.x;
        float y = actor.current_position.y;
        float groundHeight = ground(x, y);
        bool swimming = binding.IsSwimming(actor);
        if (actor.position_height < .3f && !actor.asset.is_boat && binding.DeckHeight(x, y, out float deck) &&
            deck > groundHeight - .05f)
        {
            groundHeight = deck;
            swimming = false;
        }

        float foot = groundHeight + actor.position_height + (binding.Hovers(actor) ? actor.move_jump_offset.y : 0f);
        if (swimming)
        {
            object image = binding.SpriteGet.Invoke(sprites, new object[] { actor.getSpriteToRender() });
            if (image != null)
            {
                float visibleHeight = (float)binding.ImageHeight.GetValue(image) * Mathf.Abs(actor.current_scale.y) *
                                      (float)binding.ImageVisible.GetValue(image);
                foot = (float)binding.LiquidSurface.Invoke(terrain, new object[] { x, y }) - visibleHeight * .4f;
            }
        }

        Vector3 shift = new(actor.move_jump_offset.x, 0f,
            foot - ground(actor.current_position.x, y) - actor.position_height);
        bodyShifts.Add(actor, shift);
        return shift;
    }

    /// <summary>一次转换局部顶点、面色和三角形，后续帧只更新姿态。</summary>
    /// <param name="mesh">不含外部绘制句柄的共用模型。</param>
    /// <param name="surface">本次组合选用的材料。</param>
    private static object ConvertModel(SpatialVisualMesh mesh, SpatialSurface surface)
    {
        Dictionary<uint, byte> palette = new();
        List<uint> orderedColors = new();
        byte[] slots = new byte[mesh.Colors.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            uint color = Pack(mesh.Colors[i]);
            if (!palette.TryGetValue(color, out byte slot))
            {
                if (palette.Count == 256) throw new InvalidOperationException("空间模型的材质色超过 Worldfall 的 256 色限制");
                slot = (byte)palette.Count;
                palette.Add(color, slot);
                orderedColors.Add(color);
            }

            slots[i] = slot;
        }

        object part = Activator.CreateInstance(binding.Part.Type);
        binding.Part.Set(part, "Name", "body");
        binding.Part.Set(part, "Parent", -1);
        binding.Part.Set(part, "Pos", Flatten(mesh.Vertices));
        binding.Part.Set(part, "Nrm", Flatten(mesh.Normals));
        binding.Part.Set(part, "Tri", mesh.Triangles);
        binding.Part.Set(part, "Slot", slots);
        binding.Part.Set(part, "Axis", new byte[slots.Length]);
        Array parts = Array.CreateInstance(binding.Part.Type, 1);
        parts.SetValue(part, 0);
        object model = Activator.CreateInstance(binding.Model.Type);
        binding.Model.Set(model, "Name", "Cultiway/" + mesh.Name);
        binding.Model.Set(model, "Parts", parts);
        binding.Model.Set(model, "SlotNames", orderedColors.Select((_, i) => "material_" + i).ToArray());
        // 已核对 Worldfall 0.9.8 的 MeshKind 与 MeshSlotFlags，使用它自己的火、玻璃、岩石和叶面质感。
        byte kind = surface switch
        {
            SpatialSurface.Flame => 11,
            SpatialSurface.Water or SpatialSurface.Ice or SpatialSurface.Energy => 12,
            SpatialSurface.Stone => 14,
            SpatialSurface.Foliage => 1,
            SpatialSurface.Mist => 7,
            SpatialSurface.Wood => 2,
            SpatialSurface.Metal => 10,
            _ => 0
        };
        byte flags = surface switch
        {
            SpatialSurface.Flame => 1,
            SpatialSurface.Water or SpatialSurface.Ice => 24,
            SpatialSurface.Energy => 40,
            SpatialSurface.Mist => 8,
            SpatialSurface.Metal => 16,
            _ => 0
        };
        binding.Model.Set(model, "SlotKind", Enumerable.Repeat(kind, palette.Count).ToArray());
        binding.Model.Set(model, "SlotFlags", Enumerable.Repeat(flags, palette.Count).ToArray());
        binding.Model.Set(model, "SlotColor", orderedColors.ToArray());
        Vector3 min = mesh.Vertices[0];
        Vector3 max = min;
        foreach (Vector3 vertex in mesh.Vertices)
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        binding.Model.Set(model, "Min", binding.Vector(min.x, min.y, min.z));
        binding.Model.Set(model, "Max", binding.Vector(max.x, max.y, max.z));
        return model;
    }

    /// <summary>将显式顶点数组展开为外部绘制所需的浮点资料。</summary>
    /// <param name="values">需要展开的向量数组。</param>
    private static float[] Flatten(Vector3[] values)
    {
        float[] result = new float[values.Length * 3];
        for (int i = 0; i < values.Length; i++)
        {
            result[i * 3] = values[i].x;
            result[i * 3 + 1] = values[i].y;
            result[i * 3 + 2] = values[i].z;
        }

        return result;
    }

    /// <summary>外部颜色按红、绿、蓝、透明度排列。</summary>
    /// <param name="color">待转换颜色。</param>
    private static uint Pack(Color color)
    {
        Color32 value = color;
        return (uint)(value.r | value.g << 8 | value.b << 16) | 0xFF000000u;
    }

    /// <summary>唯一故障出口，保留具体原因并停止本次可选显示接入。</summary>
    /// <param name="exception">外部显示边界的具体故障。</param>
    private static void Disable(Exception exception)
    {
        disabled = true;
        Skills.ConstructedAbilityService.GroundProvider = null;
        RenderMotionRibbonTrailSystem.SpatialHistoryRequested = false;
        Status = "Worldfall 空间显示接入已停用：" + exception.Message;
        ModClass.LogError(Status + "\n" + exception);
    }

    /// <summary>世界切换时释放外部模型引用；已安装的入口继续等待新世界快照。</summary>
    internal static void ClearWorldState()
    {
        FormWorldfallRenderer.Clear();
        OrganWorldfallRenderer.Clear();
        models.Clear();
        instances.Clear();
        bodyShifts.Clear();
        LastMeshCount = LastLineCount = 0;
        listenerFrame = -10;
    }

    /// <summary>在接入边界一次核对所需外部字段，不把外部类型泄漏给核心框架。</summary>
    private sealed class Shape
    {
        /// <summary>外部资料类型。</summary>
        internal readonly Type Type;

        /// <summary>已经核对的字段。</summary>
        private readonly Dictionary<string, FieldInfo> fields = new();

        /// <summary>显式列出当前版本实际需要的字段，缺失时接入失败。</summary>
        /// <param name="assembly">可选模组的已加载程序集。</param>
        /// <param name="name">外部资料类型的短名称。</param>
        /// <param name="names">当前适配实际使用的字段名。</param>
        internal Shape(Assembly assembly, string name, params string[] names)
        {
            Type = assembly.GetType("FirstPerson.Core." + name, true);
            foreach (string field in names)
                fields.Add(field, Type.GetField(field) ??
                                  throw new MissingFieldException(Type.FullName, field));
        }

        /// <summary>写入已经核对的显示字段。</summary>
        /// <param name="target">外部资料实例。</param>
        /// <param name="field">已经核对的字段名。</param>
        /// <param name="value">待写入的显示资料。</param>
        internal void Set(object target, string field, object value) => fields[field].SetValue(target, value);
    }

    /// <summary>当前参考版本的最小结构；所有反射查找在安装时完成。</summary>
    private sealed class Binding
    {
        /// <summary>外部栈桥给出的承托高度。</summary>
        internal delegate bool DeckHeightResolver(float x, float y, out float height);

        /// <summary>外部实际身体的水面、跳起和栈桥查询。</summary>
        internal readonly Func<Actor, bool> IsSwimming, Hovers;

        /// <summary>栈桥高度查询。</summary>
        internal readonly DeckHeightResolver DeckHeight;

        /// <summary>水面及图片查询，复用外部本帧已经使用的数据。</summary>
        internal readonly MethodInfo LiquidSurface, SpriteGet;

        /// <summary>原版人物图片的真实高度与有效像素比例。</summary>
        internal readonly FieldInfo ImageHeight;

        /// <summary>可见身体的高度比例。</summary>
        internal readonly PropertyInfo ImageVisible;

        /// <summary>场景收集入口。</summary>
        internal readonly MethodInfo Collect;

        /// <summary>未知原版投射物的平面回退入口，仅接管本模组的御器代理。</summary>
        internal readonly MethodInfo DrawThrown;

        /// <summary>第一人称持物绘制入口，仅处理重复的静态武器。</summary>
        internal readonly MethodInfo PlaceItem;

        /// <summary>第一人称手臂入口，掌击时让共用手臂代替右臂。</summary>
        internal readonly MethodInfo PlaceArm;

        /// <summary>外部模组替换后的受控移动及近处重叠修正。</summary>
        internal readonly MethodInfo PossessedStep, KeepBodyClear;

        /// <summary>外部视角的实例与真实宿主。</summary>
        internal readonly PropertyInfo RootInstance, RootHost;

        /// <summary>场景模型列表。</summary>
        internal readonly FieldInfo SceneMeshes;

        /// <summary>场景路径列表。</summary>
        internal readonly FieldInfo SceneStreaks;

        /// <summary>地表采样资料。</summary>
        internal readonly FieldInfo SamplerCache;

        /// <summary>地表高度方法。</summary>
        internal readonly MethodInfo SampleHeight;

        /// <summary>视点三维坐标及平面朝向。</summary>
        internal readonly FieldInfo ViewX, ViewY, ViewZ, ViewYaw;

        /// <summary>局部模型、部件、实例和路径所需字段。</summary>
        internal readonly Shape Model, Part, Instance, Streak;

        /// <summary>外部向量构造方法。</summary>
        private readonly ConstructorInfo vector;

        /// <summary>外部变换基构造方法。</summary>
        private readonly ConstructorInfo matrix;

        /// <summary>只核对确实使用的场景与绘制资料。</summary>
        /// <param name="assembly">可选模组的已加载程序集。</param>
        internal Binding(Assembly assembly)
        {
            Type root = assembly.GetType("FirstPerson.WorldBoxMod", true);
            RootInstance = root.GetProperty("Instance") ?? throw new MissingMemberException(root.FullName, "Instance");
            RootHost = root.GetProperty("Host") ?? throw new MissingMemberException(root.FullName, "Host");
            KeepBodyClear = root.GetMethod("KeepBodyClear", BindingFlags.Instance | BindingFlags.NonPublic) ??
                            throw new MissingMethodException(root.FullName, "KeepBodyClear");
            Type possession = assembly.GetType("FirstPerson.PossessionHooks", true);
            PossessedStep = possession.GetMethod("PossessedStep", BindingFlags.Static | BindingFlags.NonPublic) ??
                            throw new MissingMethodException(possession.FullName, "PossessedStep");
            Type hands = assembly.GetType("FirstPerson.Core.Hands", true);
            PlaceItem = hands.GetMethod("PlaceItem", BindingFlags.Instance | BindingFlags.NonPublic) ??
                        throw new MissingMethodException(hands.FullName, "PlaceItem");
            PlaceArm = hands.GetMethod("PlaceArm", BindingFlags.Instance | BindingFlags.NonPublic) ??
                       throw new MissingMethodException(hands.FullName, "PlaceArm");
            Type scene = assembly.GetType("FirstPerson.SceneCollector", true);
            IsSwimming =
                (Func<Actor, bool>)Delegate.CreateDelegate(typeof(Func<Actor, bool>), scene.GetMethod("IsSwimming"));
            Hovers = (Func<Actor, bool>)Delegate.CreateDelegate(typeof(Func<Actor, bool>),
                scene.GetMethod("Hovers", BindingFlags.Static | BindingFlags.NonPublic));
            DeckHeight = (DeckHeightResolver)Delegate.CreateDelegate(typeof(DeckHeightResolver),
                assembly.GetType("FirstPerson.DockDecks", true).GetMethod("HeightAt"));
            SpriteGet = assembly.GetType("FirstPerson.SpriteCache", true).GetMethod("Get", new[] { typeof(Sprite) });
            Type spriteImage = assembly.GetType("FirstPerson.Core.SpriteImage", true);
            ImageHeight = spriteImage.GetField("UnitHeight");
            ImageVisible = spriteImage.GetProperty("VisibleFraction");
            Collect = scene.GetMethod("Collect") ?? throw new MissingMethodException(scene.FullName, "Collect");
            DrawThrown = scene.GetMethod("DrawThrown", BindingFlags.Instance | BindingFlags.NonPublic) ??
                         throw new MissingMethodException(scene.FullName, "DrawThrown");
            SceneMeshes = scene.GetField("Meshes") ?? throw new MissingFieldException(scene.FullName, "Meshes");
            SceneStreaks = scene.GetField("Streaks") ?? throw new MissingFieldException(scene.FullName, "Streaks");
            SamplerCache = assembly.GetType("FirstPerson.WorldSampler", true).GetField("Cache") ??
                           throw new MissingFieldException("FirstPerson.WorldSampler", "Cache");
            SampleHeight = SamplerCache.FieldType.GetMethod("SampleHeight", new[] { typeof(float), typeof(float) }) ??
                           throw new MissingMethodException(SamplerCache.FieldType.FullName, "SampleHeight");
            LiquidSurface = SamplerCache.FieldType.GetMethod("LiquidSurface", new[] { typeof(float), typeof(float) });
            Type view = assembly.GetType("FirstPerson.Core.ViewState", true);
            ViewX = view.GetField("X") ?? throw new MissingFieldException(view.FullName, "X");
            ViewY = view.GetField("Y") ?? throw new MissingFieldException(view.FullName, "Y");
            ViewZ = view.GetField("Z") ?? throw new MissingFieldException(view.FullName, "Z");
            ViewYaw = view.GetField("Yaw") ?? throw new MissingFieldException(view.FullName, "Yaw");
            Model = new Shape(assembly, "MeshModel", "Name", "Parts", "SlotNames", "SlotKind", "SlotFlags", "SlotColor",
                "Min", "Max");
            Part = new Shape(assembly, "MeshPart", "Name", "Parent", "Pos", "Nrm", "Tri", "Slot", "Axis");
            Instance = new Shape(assembly, "MeshInstance", "Model", "Position", "Basis", "Multiply", "Fade", "Emissive",
                "Tag", "KeepSmall");
            Streak = new Shape(assembly, "Streak", "A", "B", "Color", "AlphaA", "AlphaB", "Width", "Glow");
            Type vec = assembly.GetType("FirstPerson.Core.Vec3", true);
            vector = vec.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) }) ??
                     throw new MissingMethodException(vec.FullName, ".ctor");
            matrix = assembly.GetType("FirstPerson.Core.Mat3", true).GetConstructor(new[] { vec, vec, vec }) ??
                     throw new MissingMethodException("FirstPerson.Core.Mat3", ".ctor");
        }

        /// <summary>构造一个已经拆开平面与高度的外部向量。</summary>
        /// <param name="x">地图横向位置。</param>
        /// <param name="y">地图纵向位置。</param>
        /// <param name="z">独立高度。</param>
        internal object Vector(float x, float y, float z) => vector.Invoke(new object[] { x, y, z });

        /// <summary>完整传递三个局部方向，避免三维模型退化为朝向镜头的图片。</summary>
        /// <param name="rotation">完整空间旋转。</param>
        /// <param name="scale">三个局部方向的显示缩放。</param>
        internal object Matrix(Quaternion rotation, Vector3 scale)
        {
            Vector3 x = rotation * Vector3.right * scale.x;
            Vector3 y = rotation * Vector3.up * scale.y;
            Vector3 z = rotation * Vector3.forward * scale.z;
            return matrix.Invoke(new[] { Vector(x.x, x.y, x.z), Vector(y.x, y.y, y.z), Vector(z.x, z.y, z.z) });
        }
    }
}