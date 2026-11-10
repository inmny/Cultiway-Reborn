using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Skills;
using Cultiway.Utils.Extension;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>贴身纹样沿实际身体三角面着色，显示与接触共用同一派生模型。</summary>
internal static partial class BodyOrganWorldfall
{
    /// <summary>当前动物绘制期间的实际人物；场景之外不改变模型。</summary>
    private static Actor patternActor;
    /// <summary>本版外部模型的派生、材质、法线及调色板字段。</summary>
    private static MethodInfo derivePatternModel, clonePatternPart, patternModelInfo;
    /// <summary>保留原几何资料，仅替换细分后的数组与三角面颜色。</summary>
    private static FieldInfo patternNormals, patternSlots, patternAxes, patternVersion,
        patternSlotNames, patternSlotKinds, patternSlotFlags, patternSlotColors, patternStatePalette, patternMeshPalette,
        patternModelMin, patternModelMax;
    /// <summary>同一身体和相同纹样共享制作结果，不逐帧细分。</summary>
    private static readonly Dictionary<(object model, PatternPlan plan), PatternBody> patternBodies = new();
    /// <summary>识别外部返回的已有派生模型，避免继续在派生模型上叠加细分。</summary>
    private static readonly Dictionary<object, PatternBody> patternOrigins = new();
    /// <summary>相同原图与身体方案共用像素制作资料。</summary>
    private static readonly Dictionary<(string signature, int sprite), PatternPlan> patternPlans = new();
    /// <summary>外部人物状态消失后自动释放其个体配色。</summary>
    private static ConditionalWeakTable<object, PatternPalette> patternPalettes = new();
    /// <summary>图片重载使旧制作缓存失效。</summary>
    private static int patternResourceVersion = -1;

    /// <summary>在现有接入边界一次绑定外部字段。</summary>
    /// <param name="assembly">已核对的Worldfall程序集。</param>
    private static void InstallPatterns(Assembly assembly)
    {
        Type model = assembly.GetType("FirstPerson.Core.MeshModel", true), part = assembly.GetType("FirstPerson.Core.MeshPart", true);
        derivePatternModel = AccessTools.Method(model, "Derive");
        patternModelInfo = AccessTools.Method(model, "Info");
        clonePatternPart = AccessTools.Method(typeof(object), "MemberwiseClone");
        patternNormals = AccessTools.Field(part, "Nrm"); patternSlots = AccessTools.Field(part, "Slot");
        patternAxes = AccessTools.Field(part, "Axis"); patternVersion = AccessTools.Field(part, "Version");
        patternSlotNames = AccessTools.Field(model, "SlotNames"); patternSlotKinds = AccessTools.Field(model, "SlotKind");
        patternSlotFlags = AccessTools.Field(model, "SlotFlags"); patternSlotColors = AccessTools.Field(model, "SlotColor");
        patternModelMin = AccessTools.Field(model, "Min"); patternModelMax = AccessTools.Field(model, "Max");
        patternStatePalette = AccessTools.Field(assembly.GetType("FirstPerson.Creatures", true).GetNestedType("State", BindingFlags.NonPublic), "Palette");
        patternMeshPalette = AccessTools.Field(meshType, "Palette");
        var harmony = new Harmony("cultiway.body.pattern.worldfall");
        harmony.Patch(detailed,
            postfix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(AfterPatternModel)));
        harmony.Patch(rigOf, prefix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(BeforePatternRig)));
    }

    /// <summary>派生纹样不改变骨架，各纹样共用原骨架及其缓存。</summary>
    /// <param name="__0">外部本轮取得的实际模型。</param>
    private static void BeforePatternRig(ref object __0)
    {
        if (__0 != null && patternOrigins.TryGetValue(__0, out PatternBody painted)) __0 = painted.Base;
    }

    /// <summary>模型只在真正动物绘制的当前人物范围内替换。</summary>
    /// <param name="__result">外部已经选定精度的原模型。</param>
    private static void AfterPatternModel(ref object __result)
    {
        if (patternActor == null || __result == null) return;
        if (patternOrigins.TryGetValue(__result, out PatternBody previous)) __result = previous.Base;
        __result = PatternFor(patternActor, __result)?.Model ?? __result;
    }

    /// <summary>绘制与目标绑定取得同一模型，纹样被移除时归还原模型。</summary>
    /// <param name="actor">实际人物。</param><param name="model">当前精度的外部模型。</param>
    private static PatternBody PatternFor(Actor actor, object model)
    {
        if (patternResourceVersion != CreatureAppearanceService.ResourceVersion)
        {
            // 已在绘制中的旧派生模型仍需还原其原模型和个体底色；世界清理时一并释放。
            patternBodies.Clear();
            patternPlans.Clear();
            patternResourceVersion = CreatureAppearanceService.ResourceVersion;
        }
        if (patternOrigins.TryGetValue(model, out PatternBody existing)) model = existing.Base;
        PatternPlan plan = PatternPlanFor(actor);
        if (plan == null) return null;
        var key = (model, plan);
        if (patternBodies.TryGetValue(key, out PatternBody cached)) return cached;
        PatternBody result = BuildPatternBody(model, plan);
        patternBodies.Add(key, result); patternOrigins.Add(result.Model, result);
        return result;
    }

    /// <summary>固定使用原身体制作帧，纹样随骨架而动，不随行走切图在表面跳动。</summary>
    /// <param name="actor">真实身体。</param>
    private static PatternPlan PatternPlanFor(Actor actor)
    {
        if (!actor.TryGetExtend(out var owner) || !owner.E.TryGetComponent(out CreaturePhenotype phenotype) ||
            !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out var compiled)) return null;
        var layers = BodyOrganBinding.VisibleLayers(actor).Where(layer => layer.ClipToBody)
            .OrderBy(layer => layer.Depth).ThenBy(layer => layer.id, StringComparer.Ordinal).ToArray();
        if (layers.Length == 0) return null;
        string rig = compiled.Morph.VisualRigId;
        string frameId = layers[0].FramesByRig[rig].Keys.OrderBy(id => id == "walk_0" ? 0 : 1)
            .ThenBy(id => id, StringComparer.Ordinal).First(id => layers.All(layer => layer.FramesByRig[rig].ContainsKey(id)));
        actor.checkAnimationContainer();
        Sprite body = actor.animation_container.sprites[frameId];
        var key = (compiled.Signature, body.GetInstanceID());
        if (patternPlans.TryGetValue(key, out PatternPlan cached)) return cached;
        var plan = new PatternPlan(body, layers, rig, frameId);
        patternPlans.Add(key, plan);
        return plan;
    }

    /// <summary>外部完成配色后保留个体底色，再混入实际三角面的纹样颜色。</summary>
    /// <param name="actor">本次已完成绘制准备的人物。</param>
    private static void FinishPatterns(Actor actor)
    {
        patternActor = null;
        object state = stateOf.Invoke(null, new object[] { actor, familyOf.Invoke(null, new object[] { actor }) });
        object mesh = stateMesh.GetValue(state), model = meshModel.GetValue(mesh);
        var palette = patternPalettes.GetValue(state, _ => new PatternPalette());
        uint[] current = (uint[])patternStatePalette.GetValue(state);
        if (!ReferenceEquals(current, palette.Applied)) palette.Base = current;
        if (!patternOrigins.TryGetValue(model, out PatternBody painted))
        {
            if (palette.Applied != null)
            {
                patternStatePalette.SetValue(state, palette.Base); patternMeshPalette.SetValue(mesh, palette.Base);
                palette.Applied = null; palette.Model = null;
            }
            return;
        }
        if (palette.Model != painted || !ReferenceEquals(current, palette.Applied))
        {
            uint[] values = (uint[])painted.Colors.Clone();
            for (int i = 0; i < painted.BaseSlots; i++)
                if (palette.Base != null && i < palette.Base.Length && palette.Base[i] != 0) values[i] = palette.Base[i];
            for (int i = painted.BaseSlots; i < values.Length; i++)
                values[i] = painted.Paints[i - painted.BaseSlots].Apply(values[painted.Sources[i - painted.BaseSlots]]);
            palette.Model = painted; palette.Applied = values;
        }
        patternStatePalette.SetValue(state, palette.Applied); patternMeshPalette.SetValue(mesh, palette.Applied);
    }

    /// <summary>资源或世界清理时释放已派生的托管数组，外部原模型不归此处销毁。</summary>
    private static void ClearPatterns()
    {
        patternBodies.Clear(); patternOrigins.Clear(); patternPlans.Clear();
        patternPalettes = new ConditionalWeakTable<object, PatternPalette>();
    }

    /// <summary>一个个体保留原版配色，避免换纹样时累积混色。</summary>
    private sealed class PatternPalette
    {
        /// <summary>外部原配色与当前纹样配色。</summary>
        internal uint[] Base, Applied;
        /// <summary>当前实际使用的派生身体。</summary>
        internal PatternBody Model;
    }

    /// <summary>派生模型以及同一批三角面的纹样归属。</summary>
    private sealed class PatternBody
    {
        /// <summary>外部原模型和用于实际绘制的派生模型。</summary>
        internal object Base, Model;
        /// <summary>原材质数量及派生默认配色。</summary>
        internal int BaseSlots;
        /// <summary>所有材质的实际默认颜色。</summary>
        internal uint[] Colors;
        /// <summary>每个附加颜色对应的原材质和纹样。</summary>
        internal byte[] Sources;
        /// <summary>与附加颜色一一对应的混色资料。</summary>
        internal PatternSample[] Paints;
        /// <summary>每个原部件按实际纹样分组的三角面。</summary>
        internal PatternGroup[][] Groups;
    }

    /// <summary>同一部件的真实面只归一组，选择名称可以复用这些面。</summary>
    private sealed class PatternGroup
    {
        /// <summary>实际三角面索引与可见图层。</summary>
        internal int[] Triangles;
        /// <summary>仍然可见的所有纹样来源。</summary>
        internal string[] Layers;
    }

    /// <summary>某种像素混色及其仍可见的来源。</summary>
    private sealed class PatternSample
    {
        /// <summary>共享样本编号，零号为未涂色。</summary>
        internal int Id;
        /// <summary>按绘制顺序混入的原制作颜色。</summary>
        internal Color32[] Colors;
        /// <summary>未被完全盖住的图层。</summary>
        internal string[] Layers;

        /// <summary>沿用Worldfall低位红、高位蓝的颜色格式，纹样混入当前实际底色。</summary>
        /// <param name="color">人物自己的底色。</param>
        internal uint Apply(uint color)
        {
            int r = (byte)color, g = (byte)(color >> 8), b = (byte)(color >> 16);
            foreach (Color32 paint in Colors)
            {
                r = (r * (255 - paint.a) + paint.r * paint.a) / 255;
                g = (g * (255 - paint.a) + paint.g * paint.a) / 255;
                b = (b * (255 - paint.a) + paint.b * paint.a) / 255;
            }
            return 0xff000000u | (uint)r | (uint)g << 8 | (uint)b << 16;
        }
    }

    /// <summary>原版制作像素与三维身体共用的纹样资料。</summary>
    private sealed class PatternPlan
    {
        /// <summary>完整原身体画布尺寸。</summary>
        internal readonly int Width, Height;
        /// <summary>原身体真正可见的包围矩形。</summary>
        internal readonly Rect Bounds;
        /// <summary>逐像素共享颜色及图层；透明处为零号样本。</summary>
        internal readonly PatternSample[] Pixels;

        /// <summary>眼部通道沿外部真实eyes材质着色，不把二维低位眼点投到三维颈部或空处。</summary>
        private readonly Color32[] eyeColors;

        /// <summary>与眼部颜色对应的原图层。</summary>
        private readonly string[] eyeLayers;

        /// <summary>相同表面与眼色组合只创建一次。</summary>
        private readonly Dictionary<int, PatternSample> materialSamples = new();

        /// <summary>新组合沿本制作方案统一编号。</summary>
        private int nextSampleId;

        /// <summary>原模型已经明确标记的双眼共用实际材质与接触面。</summary>
        /// <param name="material">原模型材质名。</param><param name="basis">该面已有的身体纹样。</param>
        internal PatternSample ForMaterial(string material, PatternSample basis)
        {
            if (material != "eyes" || eyeLayers.Length == 0) return basis;
            if (materialSamples.TryGetValue(basis.Id, out PatternSample cached)) return cached;
            var colors = new List<Color32>(basis.Colors);
            var layers = new List<string>(basis.Layers);
            for (int i = 0; i < eyeColors.Length; i++)
            {
                if (eyeColors[i].a == 255) { colors.Clear(); layers.Clear(); }
                colors.Add(eyeColors[i]); layers.Add(eyeLayers[i]);
            }
            var sample = new PatternSample { Id = nextSampleId++, Colors = colors.ToArray(), Layers = layers.ToArray() };
            materialSamples.Add(basis.Id, sample);
            return sample;
        }

        /// <summary>按相同覆盖规则读取制作图，完全不透明的前层清除后台归属。</summary>
        /// <param name="body">原身体制作帧。</param><param name="layers">实际采用的贴身图层。</param>
        /// <param name="rig">当前物种。</param><param name="frameId">固定制作帧。</param>
        internal PatternPlan(Sprite body, CreatureVisualLayerAsset[] layers, string rig, string frameId)
        {
            Width = (int)body.rect.width; Height = (int)body.rect.height;
            Color32[] original = CreatureAppearanceService.ReadPixels(body);
            var images = layers.Select(layer =>
            {
                CreatureLayerFrame frame = layer.FramesByRig[rig][frameId];
                Sprite image = SpriteTextureLoader.getSprite(frame.SpritePath);
                return (layer.id, channel: layer.Channel, frame, width: (int)image.rect.width, height: (int)image.rect.height,
                    pixels: CreatureAppearanceService.ReadPixels(image));
            }).ToArray();
            // 当前眼部图层是单像素着色；映射到原模型真实眼面，身体图案仍按原图在原面内制作。
            var eyes = images.Where(image => image.channel == "eye").ToArray();
            eyeLayers = eyes.Select(image => image.id).ToArray();
            eyeColors = eyes.Select(image => image.pixels.Where(color => color.a != 0)
                .GroupBy(color => color).OrderByDescending(group => group.Count()).First().Key).ToArray();
            var empty = new PatternSample { Id = 0, Colors = Array.Empty<Color32>(), Layers = Array.Empty<string>() };
            Pixels = Enumerable.Repeat(empty, original.Length).ToArray();
            var shared = new Dictionary<string, PatternSample>();
            int minX = Width, minY = Height, maxX = 0, maxY = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (original[y * Width + x].a == 0) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x + 1); maxY = Math.Max(maxY, y + 1);
                var colors = new List<Color32>(); var names = new List<string>();
                foreach (var image in images)
                {
                    if (image.channel == "eye") continue;
                    int px = x - image.frame.X, py = y - image.frame.Y;
                    if (px < 0 || px >= image.width || py < 0 || py >= image.height) continue;
                    Color32 color = image.pixels[py * image.width + px];
                    if (color.a == 0) continue;
                    if (color.a == 255) { colors.Clear(); names.Clear(); }
                    colors.Add(color); names.Add(image.id);
                }
                if (names.Count == 0) continue;
                string key = string.Join("|", names.Zip(colors, (name, color) => name + ":" + color.r + "," + color.g + "," + color.b + "," + color.a));
                if (!shared.TryGetValue(key, out PatternSample sample))
                {
                    sample = new PatternSample { Id = shared.Count + 1, Colors = colors.ToArray(), Layers = names.ToArray() };
                    shared.Add(key, sample);
                }
                Pixels[y * Width + x] = sample;
            }
            Bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            nextSampleId = shared.Count + 1;
        }
    }
}
