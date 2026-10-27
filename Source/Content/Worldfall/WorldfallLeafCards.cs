#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using FirstPerson.Core;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Worldfall;

/// <summary>使用原生透明交叉贴片显示群系树叶与花草。</summary>
internal static class WorldfallLeafCards
{
    /// <summary>一组原生交叉叶簇，坐标沿用对应植被模型的编辑尺寸。</summary>
    private sealed class LeafCard
    {
        /// <summary>横向中心。</summary>
        public float X { get; set; }
        /// <summary>前后中心。</summary>
        public float Y { get; set; }
        /// <summary>贴片下边缘高度。</summary>
        public float Z { get; set; }
        /// <summary>贴片宽度。</summary>
        public float Width { get; set; }
        /// <summary>贴片高度。</summary>
        public float Height { get; set; }
        /// <summary>相对树体的绕竖轴角度，使用弧度。</summary>
        public float Angle { get; set; }
        /// <summary>实际加载的模组贴图路径。</summary>
        public string Image { get; set; }
        /// <summary>是否沿用原生贴片阴影。</summary>
        public bool Shadow { get; set; }
        /// <summary>树人叶簇所依附的活动枝干部件；静态植被留空。</summary>
        public string Part { get; set; }
    }

    /// <summary>给已登记的植被模型提供对应距离档的叶簇。</summary>
    private static readonly Dictionary<string, LeafCard[]> LeafCardsByModel = new(StringComparer.Ordinal);
    /// <summary>共用已由 Worldfall 读入的透明图片。</summary>
    private static readonly Dictionary<string, SpriteImage> LeafCardImages = new(StringComparer.Ordinal);
    /// <summary>原生记录里已经完成放置的植被实体。</summary>
    private static FieldInfo LeafCardModelMesh;
    /// <summary>原生渲染器本帧收集的图片贴片。</summary>
    private static FieldInfo LeafCardBillboards;
    /// <summary>由现有 Worldfall 贴图缓存读取图片。</summary>
    private static Func<string, SpriteImage> ReadLeafImage;
    /// <summary>容纳多群系叶片与原生图片的共享图集边长，单图上限仍为512。</summary>
    private const int LeafAtlasSide = 6144;

    /// <summary>读入已经发布的植被叶簇，通过原生贴片入口显示。</summary>
    /// <param name="collector">实际版本的 Worldfall 场景类型。</param>
    /// <param name="harmony">已有的模型接入补丁组。</param>
    /// <param name="readImage">现有原生贴图读取入口。</param>
    internal static void Install(Type collector, Harmony harmony, Func<string, SpriteImage> readImage)
    {
        ReadLeafImage = readImage;
        string directory = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Worldfall", "LeafCards");
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.GetFiles(directory, "*.json"))
        {
            JObject document = JObject.Parse(File.ReadAllText(path));
            string family = (string)document["ModelName"];
            foreach ((string level, string suffix) in new[] { ("Near", ""), ("Middle", "_m"), ("Far", "_f") })
            {
                LeafCard[] cards = document["Levels"][level].ToObject<LeafCard[]>();
                foreach (LeafCard card in cards) card.Image = (string)document["Textures"][card.Image];
                LeafCardsByModel.Add(family + suffix, cards);
            }
        }
        if (LeafCardsByModel.Count == 0) return;
        MethodInfo draw = AccessTools.Method(collector, "DrawAsModel");
        LeafCardModelMesh = AccessTools.Field(draw.GetParameters()[0].ParameterType, "Mesh");
        LeafCardBillboards = AccessTools.Field(collector, "Billboards");
        HarmonyMethod drawLeaves = new(AccessTools.Method(typeof(WorldfallLeafCards), nameof(DrawLeafCards)));
        harmony.Patch(draw, postfix: drawLeaves);
        harmony.Patch(AccessTools.Method(collector, "DrawPlantModel"), postfix: drawLeaves);
        harmony.Patch(AccessTools.Method(collector, "CollectUnits"),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(WorldfallLeafCards), nameof(DrawCreatureLeafCards))));
        Type sprites = collector.Assembly.GetType("FirstPerson.Gpu.GpuSprites", true);
        HarmonyMethod expandAtlas = new(AccessTools.Method(typeof(WorldfallLeafCards), nameof(ExpandLeafAtlas)));
        harmony.Patch(AccessTools.Method(sprites, "Place"), transpiler: expandAtlas);
        harmony.Patch(AccessTools.Method(sprites, "Prepare"),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(WorldfallLeafCards), nameof(ResetSmallLeafAtlas))),
            transpiler: expandAtlas);
    }

    /// <summary>同步扩大原生图集分配、行列边界及着色器尺寸，避免多群系图片覆盖先前内容。</summary>
    /// <param name="instructions">已核对的Worldfall 1.0.0图片放置或上传指令。</param>
    /// <returns>只替换2048图集尺寸及其倒数，保留512单图上限的指令。</returns>
    private static IEnumerable<CodeInstruction> ExpandLeafAtlas(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4 && (int)instruction.operand == 2048)
                instruction.operand = LeafAtlasSide;
            else if (instruction.opcode == OpCodes.Ldc_R4)
            {
                float value = (float)instruction.operand;
                if (value == 2048f) instruction.operand = (float)LeafAtlasSide;
                else if (value == 1f / 2048f) instruction.operand = 1f / LeafAtlasSide;
            }
            yield return instruction;
        }
    }

    /// <summary>接入时若原生小图集已经建立，在本帧收集前重建，避免旧坐标沿用。</summary>
    /// <param name="____atlas">原生共享图集。</param>
    /// <param name="____placed">原生图片位置缓存。</param>
    /// <param name="____shelfX">当前行的横向位置。</param>
    /// <param name="____shelfY">当前行的纵向位置。</param>
    /// <param name="____shelfH">当前行高度。</param>
    private static void ResetSmallLeafAtlas(ref UnityEngine.Texture2D ____atlas,
        Dictionary<SpriteImage, UnityEngine.RectInt> ____placed,
        ref int ____shelfX, ref int ____shelfY, ref int ____shelfH)
    {
        if (____atlas == null || ____atlas.width == LeafAtlasSide) return;
        UnityEngine.Object.Destroy(____atlas);
        ____atlas = null;
        ____placed.Clear();
        ____shelfX = ____shelfY = ____shelfH = 0;
    }

    /// <summary>从现有原生贴图缓存获取图片，保留透明孔洞。</summary>
    /// <param name="path">模组贴图资源路径。</param>
    /// <returns>本次可以显示的原生图片。</returns>
    private static SpriteImage LeafCardImage(string path)
    {
        if (LeafCardImages.TryGetValue(path, out SpriteImage image)) return image;
        image = ReadLeafImage(path);
        // 原生缓存每帧限制读图次数，尚未读到时留待下一帧。
        if (image != null)
        {
            image.PivotX = 0.5f;
            image.PivotY = 0f;
            LeafCardImages.Add(path, image);
        }
        return image;
    }

    /// <summary>把本地叶簇转换到已放置植被的位置和尺寸。</summary>
    /// <param name="card">该距离档的本地叶簇。</param>
    /// <param name="plant">已经经过原生随机缩放、朝向与落地的植被实体。</param>
    /// <param name="image">带透明孔洞的叶簇图。</param>
    /// <returns>正常使用不透明裁切，靠近树冠时与树体一起淡出的双面交叉贴片。</returns>
    private static Billboard MakeLeafCard(LeafCard card, MeshInstance plant, SpriteImage image)
    {
        Vec3 position = plant.Position + plant.Basis.Mul(new Vec3(card.X, card.Y, card.Z));
        float across = (float)Math.Sqrt(Vec3.Dot(plant.Basis.C0, plant.Basis.C0));
        return new Billboard
        {
            X = position.X, Y = position.Y, Z = position.Z, GroundZ = plant.Position.Z,
            Width = card.Width * across, Height = card.Height * plant.Basis.C2.Z,
            Angle = card.Angle + (float)Math.Atan2(plant.Basis.C0.Y, plant.Basis.C0.X),
            Image = image, Kind = BillboardKind.Cross,
            Alpha = plant.Fade >= 1f ? (byte)0 : (byte)Math.Max(1, (int)(plant.Fade * 255f)), Shadow = card.Shadow,
            Tag = plant.Tag, Flash = plant.Flash, Multiply = plant.Multiply
        };
    }

    /// <summary>树木或花草完成原生放置后，附上同一距离档的叶簇。</summary>
    /// <param name="__instance">本帧的原生场景。</param>
    /// <param name="nb">当前植被记录。</param>
    /// <param name="__result">原生入口是否成功处理这株植被。</param>
    private static void DrawLeafCards(object __instance, object nb, bool __result)
    {
        if (!__result) return;
        MeshInstance plant = (MeshInstance)LeafCardModelMesh.GetValue(nb);
        if (plant?.Model == null || plant.ShadowOnly ||
            !LeafCardsByModel.TryGetValue(plant.Model.Name, out LeafCard[] cards)) return;
        List<Billboard> billboards = (List<Billboard>)LeafCardBillboards.GetValue(__instance);
        foreach (LeafCard card in cards)
        {
            SpriteImage image = LeafCardImage(card.Image);
            if (image != null) billboards.Add(MakeLeafCard(card, plant, image));
        }
    }

    /// <summary>人物收集完成后，把树人的叶簇附到实际活动枝干上。</summary>
    /// <param name="___Meshes">原生场景已经放置和摆好姿态的网格。</param>
    /// <param name="___Billboards">本帧沿用的原生图片贴片。</param>
    private static void DrawCreatureLeafCards(List<MeshInstance> ___Meshes, List<Billboard> ___Billboards)
    {
        foreach (MeshInstance mesh in ___Meshes)
        {
            if (mesh.ShadowOnly || !mesh.Model.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal) ||
                !LeafCardsByModel.TryGetValue(mesh.Model.Name, out LeafCard[] cards)) continue;
            foreach (LeafCard card in cards)
            {
                int part = mesh.Model.PartIndex(card.Part);
                bool hidden = false;
                for (int index = part; index >= 0; index = mesh.Model.Parts[index].Parent)
                    if ((mesh.Hidden & (1UL << index)) != 0) { hidden = true; break; }
                if (hidden) continue;
                SpriteImage image = LeafCardImage(card.Image);
                if (image != null) ___Billboards.Add(MakeCreatureLeafCard(card, mesh, image, part));
            }
        }
    }

    /// <summary>叶簇底边跟随部件位置，方向沿用枝干转向，交叉片仍保持原生竖直显示。</summary>
    /// <param name="card">该距离档的叶簇。</param>
    /// <param name="mesh">已经放置并完成姿态的树人。</param>
    /// <param name="image">与对应普通树共用的透明图片。</param>
    /// <param name="part">承托叶簇的实际部件下标。</param>
    /// <returns>带原生选择、闪烁和浸水阴影状态的叶簇。</returns>
    private static Billboard MakeCreatureLeafCard(LeafCard card, MeshInstance mesh, SpriteImage image, int part)
    {
        Vec3 position = PersonRig.Transform(mesh, part, new Vec3(card.X, card.Y, card.Z));
        Vec3 across = PersonRig.TransformDir(mesh, part,
            new Vec3((float)Math.Cos(card.Angle), (float)Math.Sin(card.Angle), 0));
        Vec3 up = PersonRig.TransformDir(mesh, part, new Vec3(0, 0, 1));
        return new Billboard
        {
            X = position.X, Y = position.Y, Z = position.Z, GroundZ = mesh.Position.Z,
            Width = card.Width * (float)Math.Sqrt(Vec3.Dot(across, across)),
            Height = card.Height * (float)Math.Sqrt(Vec3.Dot(up, up)),
            Angle = (float)Math.Atan2(across.Y, across.X), Image = image, Kind = BillboardKind.Cross,
            Alpha = mesh.Fade >= 1f ? (byte)0 : (byte)Math.Max(1, (int)(mesh.Fade * 255f)),
            Shadow = card.Shadow && !mesh.NoShadow,
            Tag = mesh.Tag, Flash = mesh.Flash, Multiply = mesh.Multiply
        };
    }
}
#endif
