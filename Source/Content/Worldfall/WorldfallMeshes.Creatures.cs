#if S3COND2_WORLDFALL
using System;
using System.Globalization;
using System.Linq;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

internal static partial class WorldfallMeshes
{
    /// <summary>生成各身体类别的完整网格，材质槽由原单位贴图提供配色。</summary>
    /// <param name="name">模型名称。</param>
    /// <param name="id">单位资产名称，决定角、耳、花冠和装备等特征。</param>
    /// <param name="shape">身体结构。</param>
    /// <param name="detail">零为近景，一为中景，二为远景。</param>
    /// <returns>保留动作部件和地面锚点的网格。</returns>
    internal static MeshModel BuildCreature(string name, string id, WorldfallBody shape, int detail)
    {
        MeshBuilder mesh = new(name);
        int coat = mesh.Slot("fur", 7, TerrainCache.Rgb(122, 152, 96));
        int light = mesh.Slot("fur_light", 7, TerrainCache.Rgb(211, 214, 174));
        int dark = mesh.Slot("dark", 3, TerrainCache.Rgb(51, 48, 42));
        int accent = mesh.Slot("accent", 10, TerrainCache.Rgb(198, 146, 58));
        int horn = mesh.Slot("horn", 8, TerrainCache.Rgb(228, 220, 191));
        int eyes = mesh.Slot("eyes_fixed", 10, TerrainCache.Rgb(24, 22, 25));
        int wing = mesh.Slot("wing", 7, TerrainCache.Rgb(228, 216, 175));
        int spots = mesh.Slot("spots", 7, TerrainCache.Rgb(238, 232, 206));
        int sides = detail == 0 ? 8 : detail == 1 ? 6 : 4;
        string kind = "biped";
        bool flap = false;
        int body = mesh.Part("body", -1, new Vec3(0f, 0f, 0.42f));
        // 主体和四肢先确定，随后补齐能识别该单位的头、角、尾和装备。
        switch (shape)
        {
            case WorldfallBody.Tree:
                kind = "biped";
                BuildTreeBody(mesh, id, body, coat, light, dark, accent, sides, detail);
                break;
            case WorldfallBody.Quadruped:
            case WorldfallBody.Sauropod:
            case WorldfallBody.Triceratops:
            case WorldfallBody.Turtle:
            case WorldfallBody.Frog:
            case WorldfallBody.Rabbit:
            case WorldfallBody.Centaur:
                kind = "quadruped";
                BuildQuadruped(mesh, id, shape, body, coat, light, dark, horn, eyes, wing, spots, sides, detail);
                flap = id.EndsWith("GriffinKnight", StringComparison.Ordinal);
                break;
            case WorldfallBody.Bird:
            case WorldfallBody.WingedDragon:
            case WorldfallBody.WingedBiped:
            case WorldfallBody.Theropod:
                kind = shape == WorldfallBody.Bird ? "bird" : shape == WorldfallBody.WingedDragon ? "dragon" : "biped";
                BuildWingedBody(mesh, id, shape, body, coat, light, dark, horn, eyes, wing, sides, detail);
                flap = shape != WorldfallBody.Theropod;
                break;
            case WorldfallBody.LongDragon:
                kind = "snake";
                BuildLongDragon(mesh, body, coat, light, horn, eyes, sides, detail);
                break;
            case WorldfallBody.Blob:
                kind = "blob";
                mesh.Blob(coat, new Vec3(0f, 0f, 0.28f), 0.36f, 0.29f, 0.28f, sides);
                AddEyes(mesh, body, eyes, new Vec3(0f, -0.245f, 0.34f), 0.11f, detail);
                mesh.Part("crown", body, new Vec3(0f, 0f, 0.56f));
                mesh.Box(accent, new Vec3(0f, 0f, 0.56f), 0.11f, 0.09f, 0.024f);
                if (detail == 0) for (int i = -1; i <= 1; i++) mesh.Box(accent, new Vec3(i * 0.08f, 0f, 0.62f), 0.018f, 0.045f, 0.06f);
                break;
            case WorldfallBody.Octopus:
                kind = "blob";
                mesh.Blob(coat, new Vec3(0f, 0f, 0.51f), 0.24f, 0.24f, 0.35f, sides);
                AddEyes(mesh, body, eyes, new Vec3(0f, -0.235f, 0.57f), 0.095f, detail);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * (float)Math.PI / 4f;
                    Vec3 at = new((float)Math.Cos(a) * 0.15f, (float)Math.Sin(a) * 0.15f, 0.20f);
                    mesh.Part("leg_" + (i + 1), body, at);
                    mesh.Capsule(coat, at, new Vec3(at.X * 3.7f, at.Y * 3.7f, 0.055f), 0.07f, 0.045f, sides);
                    if (detail == 0) mesh.Blob(light, new Vec3(at.X * 3.25f, at.Y * 3.25f, 0.06f), 0.06f, 0.06f, 0.03f, 4);
                }
                break;
            case WorldfallBody.Ghost:
            case WorldfallBody.Skull:
                kind = "ghost";
                mesh.Loft(coat, [new Vec3(0f, 0f, 0.03f), new Vec3(0f, 0f, 0.39f), new Vec3(0f, 0f, 0.66f)],
                    [0.03f, 0.19f, 0.12f], [0.03f, 0.11f, 0.10f], new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), sides, false);
                int ghostHead = mesh.Part("head", body, new Vec3(0f, 0f, 0.78f));
                mesh.Blob(light, new Vec3(0f, 0f, 0.78f), 0.135f, 0.11f, 0.15f, sides);
                AddEyes(mesh, ghostHead, eyes, new Vec3(0f, -0.105f, 0.79f), 0.055f, detail);
                if (id.Contains("Fire") || id.Contains("Candle") || id.Contains("Flamer"))
                {
                    mesh.Part("flames", body, new Vec3(0f, 0f, 0.5f));
                    for (int i = -1; i <= 1; i++) mesh.Tube(accent, new Vec3(i * 0.1f, 0f, 0.3f), new Vec3(i * 0.06f, 0.02f, 1f - Math.Abs(i) * 0.13f), 0.1f, 0.006f, sides, false);
                }
                else if (shape == WorldfallBody.Skull)
                {
                    mesh.Part("wing_l", body, new Vec3(-0.12f, 0f, 0.6f));
                    mesh.Box(dark, new Vec3(-0.24f, 0f, 0.57f), 0.12f, 0.018f, 0.03f);
                    mesh.Part("wing_r", body, new Vec3(0.12f, 0f, 0.6f));
                    mesh.Box(dark, new Vec3(0.24f, 0f, 0.57f), 0.12f, 0.018f, 0.03f);
                }
                break;
            case WorldfallBody.Mermaid:
                kind = "fish";
                mesh.Blob(light, new Vec3(0f, 0f, 0.72f), 0.13f, 0.09f, 0.22f, sides);
                mesh.Part("seg_1", body, new Vec3(0f, 0f, 0.45f));
                mesh.Capsule(coat, new Vec3(0f, 0f, 0.5f), new Vec3(0f, 0.35f, 0.12f), 0.14f, 0.035f, sides);
                mesh.Part("tail", body, new Vec3(0f, 0.33f, 0.12f));
                mesh.Face(coat, new Vec3(0f, 0.33f, 0.12f), new Vec3(-0.23f, 0.48f, 0.04f), new Vec3(0.23f, 0.48f, 0.04f));
                mesh.Part("head", body, new Vec3(0f, 0f, 0.96f));
                mesh.Blob(light, new Vec3(0f, 0f, 0.96f), 0.105f, 0.095f, 0.12f, sides);
                AddEyes(mesh, body, eyes, new Vec3(0f, -0.092f, 0.97f), 0.043f, detail);
                AddArms(mesh, body, light, sides);
                break;
            case WorldfallBody.Machine:
            case WorldfallBody.Train:
                kind = "biped";
                float length = shape == WorldfallBody.Train ? 0.95f : 0.36f;
                mesh.Box(dark, new Vec3(0f, 0f, 0.2f), 0.23f, length, 0.16f);
                mesh.Part("head", body, new Vec3(0f, -0.12f, 0.44f));
                mesh.Box(coat, new Vec3(0f, -0.08f, 0.48f), 0.17f, length * 0.56f, 0.16f);
                mesh.Capsule(light, new Vec3(0f, -0.12f, 0.5f), new Vec3(0f, -length - 0.18f, 0.5f), 0.055f, 0.045f, sides);
                if (shape == WorldfallBody.Train) mesh.Tube(dark, new Vec3(0f, -0.5f, 0.56f), new Vec3(0f, -0.5f, 0.85f), 0.055f, 0.075f, sides, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    mesh.Part(side < 0 ? "wheel_l" : "wheel_r", body, new Vec3(side * 0.23f, 0f, 0.16f));
                    for (int axle = -1; axle <= 1; axle++) mesh.Tube(dark, new Vec3(side * 0.18f, axle * length * 0.7f, 0.15f), new Vec3(side * 0.29f, axle * length * 0.7f, 0.15f), 0.145f, 0.145f, sides, false);
                }
                break;
            case WorldfallBody.Ray:
                kind = "fish";
                mesh.Blob(coat, new Vec3(0f, 0f, 0.19f), 0.36f, 0.39f, 0.16f, sides);
                mesh.Part("wing_l", body, new Vec3(-0.17f, 0f, 0.2f));
                mesh.Face(wing, new Vec3(-0.17f, -0.3f, 0.2f), new Vec3(-0.65f, 0.1f, 0.14f), new Vec3(-0.17f, 0.35f, 0.2f));
                mesh.Part("wing_r", body, new Vec3(0.17f, 0f, 0.2f));
                mesh.Face(wing, new Vec3(0.17f, -0.3f, 0.2f), new Vec3(0.17f, 0.35f, 0.2f), new Vec3(0.65f, 0.1f, 0.14f));
                mesh.Part("tail", body, new Vec3(0f, 0.29f, 0.17f));
                mesh.Capsule(coat, new Vec3(0f, 0.29f, 0.17f), new Vec3(0f, 0.81f, 0.1f), 0.07f, 0.01f, sides);
                AddEyes(mesh, body, eyes, new Vec3(0f, -0.34f, 0.21f), 0.12f, detail);
                flap = true;
                break;
            default:
                BuildBiped(mesh, id, body, coat, light, dark, horn, eyes, accent, sides, detail);
                break;
        }
        MeshModel model = mesh.Build();
        string height = (model.Max.Z - Math.Max(0f, model.Min.Z)).ToString(CultureInfo.InvariantCulture);
        string span = (model.Max.Y - model.Min.Y).ToString(CultureInfo.InvariantCulture);
        return SetInfo(model, $"kind={kind};height={height};length={span};cultiway_body={shape};flap={(flap ? 1 : 0)};tentacles={(shape == WorldfallBody.Octopus ? 1 : 0)}");
    }

    /// <summary>构建双足单位的身体、五官及识别装饰。</summary>
    /// <param name="mesh">网格生成器。</param><param name="id">资产名称。</param><param name="body">身体部件。</param>
    /// <param name="coat">主色。</param><param name="light">浅色。</param><param name="dark">暗色。</param><param name="horn">角材质。</param>
    /// <param name="eyes">眼睛。</param><param name="accent">装饰色。</param><param name="sides">圆周面数。</param><param name="detail">细节等级。</param>
    private static void BuildBiped(MeshBuilder mesh, string id, int body, int coat, int light, int dark, int horn, int eyes, int accent, int sides, int detail)
    {
        bool giant = id.Contains("Giant") || id.Contains("UncleanOne") || id.Contains("KeeperSecrets");
        bool armoured = id.Contains("Knight") || id.Contains("Astartes") || id.Contains("Custodes") || id.Contains("Emperor") || id.Contains("Robot");
        bool rat = id.Contains("Skaven_");
        float width = giant ? 0.24f : armoured ? 0.18f : 0.14f;
        mesh.Blob(coat, new Vec3(0f, 0f, 0.6f), width, giant ? 0.16f : 0.105f, 0.27f, sides);
        AddLeg(mesh, body, "leg_l", coat, dark, new Vec3(-width * 0.55f, 0f, 0.36f), 0.055f);
        AddLeg(mesh, body, "leg_r", coat, dark, new Vec3(width * 0.55f, 0f, 0.36f), 0.055f);
        AddArms(mesh, body, coat, sides, width);
        int head = mesh.Part("head", body, new Vec3(0f, 0f, 0.92f));
        mesh.Blob(light, new Vec3(0f, 0f, 0.94f), 0.11f, 0.105f, 0.13f, sides);
        AddEyes(mesh, head, eyes, new Vec3(0f, -0.103f, 0.97f), 0.05f, detail);
        if (id.Contains("Anubis") || rat || id.Contains("Werewolf") || id.Contains("FishPeople"))
        {
            mesh.Blob(coat, new Vec3(0f, -0.145f, 0.91f), 0.07f, 0.10f, 0.055f, sides);
            for (int side = -1; side <= 1; side += 2)
                if (id.Contains("FishPeople"))
                    mesh.Extrude(light, new Vec3(side * 0.12f, 0f, 0.98f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f), 0.018f, -0.04f, -0.08f, 0.13f, -0.04f, 0.07f, 0.16f);
                else mesh.Blob(coat, new Vec3(side * 0.095f, 0f, 1.05f), 0.055f, 0.035f, rat ? 0.065f : 0.10f, sides);
        }
        mesh.Part("chest_details", body, new Vec3(0f, 0f, 0.6f));
        if (id.Contains("Mummy") || id.Contains("Skeleton"))
        {
            if (detail == 0) for (int i = 0; i < 5; i++) mesh.Box(light, new Vec3(0f, -0.10f, 0.43f + i * 0.09f), width * 0.86f, 0.012f, 0.022f);
        }
        else if (armoured)
        {
            mesh.Box(accent, new Vec3(0f, -0.11f, 0.67f), width * 0.8f, 0.024f, 0.13f);
            mesh.Part("helmet", head, new Vec3(0f, 0f, 1.02f));
            mesh.Box(coat, new Vec3(0f, 0f, 1.02f), 0.12f, 0.11f, 0.07f);
            mesh.Box(dark, new Vec3(0f, -0.117f, 1f), 0.10f, 0.012f, 0.013f);
        }
        else if (id.Contains("Shaman") || id.Contains("Sorcerer") || id.Contains("Priest") || id.Contains("Pharaoh") || id.Contains("QingDi"))
        {
            mesh.Part("robe", body, new Vec3(0f, 0f, 0.37f));
            mesh.Loft(coat, [new Vec3(0f, 0f, 0.08f), new Vec3(0f, 0f, 0.5f)], [width * 1.4f, width], [0.15f, 0.1f], new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), sides, false);
            mesh.Part("hat", head, new Vec3(0f, 0f, 1.04f));
            mesh.Tube(accent, new Vec3(0f, 0f, 1.01f), new Vec3(0f, 0f, 1.28f), 0.13f, 0.025f, sides, false);
        }
        if (detail < 2 && (giant || id.Contains("Khorne") || id.Contains("Daemon") || id.Contains("Minotaur")))
            AddHorns(mesh, head, horn, new Vec3(0f, 0f, 1.02f), 0.16f, 0.22f, sides);
        if (rat)
        {
            mesh.Part("tail", body, new Vec3(0f, 0.085f, 0.35f));
            mesh.Capsule(light, new Vec3(0f, 0.085f, 0.35f), new Vec3(0f, 0.44f, 0.07f), 0.045f, 0.012f, sides);
        }
        if (detail == 0 && (id.Contains("Soldier") || armoured || id.Contains("Warrior")))
        {
            mesh.Part("weapon", body, new Vec3(0.24f, 0f, 0.5f));
            mesh.Box(horn, new Vec3(0.24f, -0.08f, 0.71f), 0.02f, 0.03f, 0.28f);
            mesh.Box(dark, new Vec3(0.24f, -0.08f, 0.47f), 0.06f, 0.04f, 0.015f);
        }
    }

    /// <summary>生成四足、龟、蛙、兔和半人形混合身体。</summary>
    /// <param name="mesh">网格生成器。</param><param name="id">资产名称。</param><param name="shape">身体结构。</param><param name="body">主体。</param>
    /// <param name="coat">主色。</param><param name="light">浅色。</param><param name="dark">暗色。</param><param name="horn">角材质。</param><param name="eyes">眼睛。</param>
    /// <param name="wing">翼材质。</param><param name="spots">斑纹。</param><param name="sides">圆周面数。</param><param name="detail">细节等级。</param>
    private static void BuildQuadruped(MeshBuilder mesh, string id, WorldfallBody shape, int body, int coat, int light, int dark, int horn, int eyes, int wing, int spots, int sides, int detail)
    {
        bool low = shape == WorldfallBody.Turtle || shape == WorldfallBody.Frog || shape == WorldfallBody.Rabbit || id.Contains("Pig") || id.Contains("Boar");
        float legHeight = low ? 0.19f : 0.37f;
        float depth = shape == WorldfallBody.Sauropod ? 0.48f : 0.31f;
        mesh.Blob(coat, new Vec3(0f, 0f, legHeight + 0.14f), 0.23f, depth, 0.19f, sides);
        string[] legs = ["leg_fl", "leg_fr", "leg_bl", "leg_br"];
        for (int i = 0; i < legs.Length; i++) AddLeg(mesh, body, legs[i], coat, dark, new Vec3(i % 2 == 0 ? -0.16f : 0.16f, i < 2 ? -depth * 0.65f : depth * 0.65f, legHeight), low ? 0.075f : 0.055f);
        Vec3 headAt = new(0f, -depth - 0.08f, legHeight + 0.24f);
        int head = mesh.Part("head", body, headAt);
        if (shape == WorldfallBody.Sauropod)
        {
            headAt = new Vec3(0f, -depth - 0.24f, 1.3f);
            mesh.Capsule(coat, new Vec3(0f, -depth * 0.8f, 0.53f), headAt, 0.13f, 0.07f, sides);
        }
        if (shape == WorldfallBody.Centaur)
        {
            headAt = new Vec3(0f, -0.2f, 1.13f);
            mesh.Blob(light, new Vec3(0f, -0.20f, 0.87f), 0.14f, 0.10f, 0.23f, sides);
            AddArms(mesh, body, light, sides, 0.15f, -0.20f, 0.91f);
        }
        mesh.Blob(shape == WorldfallBody.Centaur || id.Contains("Sphinx") ? light : coat, headAt, low ? 0.19f : 0.12f, low ? 0.17f : 0.15f, low ? 0.12f : 0.13f, sides);
        AddEyes(mesh, head, eyes, new Vec3(0f, headAt.Y - (low ? 0.15f : 0.14f), headAt.Z + 0.045f), low ? 0.10f : 0.065f, detail);
        if (shape == WorldfallBody.Turtle)
        {
            mesh.Part("shell", body, new Vec3(0f, 0f, 0.34f));
            mesh.Blob(dark, new Vec3(0f, 0.03f, 0.4f), 0.29f, 0.34f, 0.23f, sides);
            if (detail == 0) for (int i = -1; i <= 1; i++) mesh.Box(horn, new Vec3(i * 0.14f, 0.02f, 0.58f), 0.055f, 0.12f, 0.025f);
        }
        else if (shape == WorldfallBody.Triceratops)
        {
            mesh.Blob(dark, new Vec3(0f, -0.31f, 0.62f), 0.27f, 0.07f, 0.27f, sides);
            AddHorns(mesh, head, horn, headAt, 0.15f, 0.27f, sides);
            mesh.Tube(horn, new Vec3(0f, headAt.Y - 0.12f, headAt.Z), new Vec3(0f, headAt.Y - 0.28f, headAt.Z + 0.1f), 0.06f, 0.005f, sides, false);
        }
        else if (shape == WorldfallBody.Rabbit)
        {
            for (int side = -1; side <= 1; side += 2) mesh.Capsule(light, new Vec3(side * 0.065f, headAt.Y, headAt.Z + 0.07f), new Vec3(side * 0.1f, headAt.Y + 0.02f, headAt.Z + 0.44f), 0.055f, 0.035f, sides);
        }
        else if (!low && detail < 2)
        {
            for (int side = -1; side <= 1; side += 2) mesh.Blob(coat, new Vec3(side * 0.13f, headAt.Y, headAt.Z + 0.13f), 0.055f, 0.07f, 0.07f, sides);
            if (id.Contains("Deer") || id.Contains("QiLin")) AddHorns(mesh, head, horn, headAt, 0.15f, 0.32f, sides, true);
        }
        if (detail == 0 && (id.Contains("Tiger") || id.Contains("Panda")))
        {
            for (int i = -2; i <= 2; i++) mesh.Box(id.Contains("Panda") ? dark : spots, new Vec3(0f, i * 0.09f, legHeight + 0.30f), 0.19f, 0.025f, 0.025f);
        }
        if (!low || shape == WorldfallBody.Turtle)
        {
            mesh.Part("tail", body, new Vec3(0f, depth * 0.8f, legHeight + 0.12f));
            int tails = id.Contains("FairyFox") && detail == 0 ? 9 : 1;
            for (int i = 0; i < tails; i++) mesh.Capsule(coat, new Vec3(0f, depth * 0.8f, legHeight + 0.12f), new Vec3((i - (tails - 1) / 2f) * 0.085f, depth + (shape == WorldfallBody.Sauropod ? 0.78f : 0.35f), legHeight + (tails > 1 ? 0.27f : 0f)), tails > 1 ? 0.09f : 0.06f, 0.02f, sides);
        }
        if (id.Contains("GriffinKnight")) AddWings(mesh, body, wing, 0.8f, 0.62f);
        if (id.Contains("Bloodcrusher") || id.Contains("GriffinKnight"))
        {
            mesh.Part("rider", body, new Vec3(0f, 0f, 0.64f));
            mesh.Blob(light, new Vec3(0f, 0f, 0.85f), 0.11f, 0.08f, 0.18f, sides);
            mesh.Blob(horn, new Vec3(0f, 0f, 1.04f), 0.08f, 0.08f, 0.10f, sides);
        }
    }

    /// <summary>生成鸟、带翼生物和双足恐龙。</summary>
    /// <param name="mesh">网格生成器。</param><param name="id">资产名称。</param><param name="shape">身体结构。</param><param name="body">主体。</param>
    /// <param name="coat">主色。</param><param name="light">浅色。</param><param name="dark">暗色。</param><param name="horn">角材质。</param><param name="eyes">眼睛。</param>
    /// <param name="wing">翼材质。</param><param name="sides">圆周面数。</param><param name="detail">细节等级。</param>
    private static void BuildWingedBody(MeshBuilder mesh, string id, WorldfallBody shape, int body, int coat, int light, int dark, int horn, int eyes, int wing, int sides, int detail)
    {
        bool upright = shape == WorldfallBody.WingedBiped;
        float z = upright ? 0.60f : 0.44f;
        mesh.Blob(coat, new Vec3(0f, 0f, z), upright ? 0.17f : 0.20f, upright ? 0.11f : 0.31f, upright ? 0.25f : 0.19f, sides);
        AddLeg(mesh, body, "leg_l", coat, dark, new Vec3(-0.12f, 0.13f, 0.31f), 0.055f);
        AddLeg(mesh, body, "leg_r", coat, dark, new Vec3(0.12f, 0.13f, 0.31f), 0.055f);
        Vec3 headAt = new(0f, upright ? 0f : -0.30f, upright ? 0.96f : 0.72f);
        int head = mesh.Part("head", body, headAt);
        mesh.Blob(coat, headAt, 0.11f, 0.14f, 0.14f, sides);
        AddEyes(mesh, head, eyes, new Vec3(0f, headAt.Y - 0.125f, headAt.Z + 0.015f), 0.055f, detail);
        if (shape == WorldfallBody.Bird)
            mesh.Tube(horn, new Vec3(0f, headAt.Y - 0.08f, headAt.Z), new Vec3(0f, headAt.Y - 0.28f, headAt.Z - 0.04f), 0.06f, 0.002f, sides, false);
        else
        {
            mesh.Blob(light, new Vec3(0f, headAt.Y - 0.11f, headAt.Z - 0.065f), 0.095f, 0.16f, 0.055f, sides);
            if (detail < 2) AddHorns(mesh, head, horn, headAt, 0.12f, 0.18f, sides);
        }
        if (shape != WorldfallBody.Theropod) AddWings(mesh, body, wing, shape == WorldfallBody.WingedDragon ? 1.05f : 0.78f, z + 0.10f);
        if (upright || shape == WorldfallBody.Theropod) AddArms(mesh, body, coat, sides, 0.20f, upright ? 0f : -0.17f, upright ? 0.74f : 0.48f, upright);
        mesh.Part("tail", body, new Vec3(0f, 0.23f, z));
        mesh.Capsule(coat, new Vec3(0f, 0.23f, z), new Vec3(0f, 0.86f, 0.12f), 0.14f, 0.015f, sides);
        if (detail == 0 && (id.Contains("Huang") || id.Contains("ZhuQue") || id.Contains("JinWu")))
            for (int i = -2; i <= 2; i++) mesh.Capsule(i % 2 == 0 ? wing : light, new Vec3(0f, 0.26f, z), new Vec3(i * 0.11f, 1.12f - Math.Abs(i) * 0.08f, 0.22f + Math.Abs(i) * 0.08f), 0.055f, 0.015f, sides);
    }

    /// <summary>构建有连续躯干、鬃毛和角的东方龙。</summary>
    /// <param name="mesh">网格生成器。</param><param name="body">主体。</param><param name="coat">鳞片。</param><param name="light">腹部。</param>
    /// <param name="horn">角和爪。</param><param name="eyes">眼睛。</param><param name="sides">圆周面数。</param><param name="detail">细节等级。</param>
    private static void BuildLongDragon(MeshBuilder mesh, int body, int coat, int light, int horn, int eyes, int sides, int detail)
    {
        int parent = body;
        for (int i = 0; i < 8; i++)
        {
            float y = -0.72f + i * 0.22f;
            float x = 0.12f * (float)Math.Sin(i * 0.65f);
            int part = mesh.Part("seg_" + (i + 1), parent, new Vec3(x, y - 0.10f, 0.35f));
            mesh.Capsule(coat, new Vec3(x, y - 0.12f, 0.35f), new Vec3(x, y + 0.12f, 0.35f), 0.115f - i * 0.008f, 0.11f - i * 0.008f, sides);
            if (detail == 0) mesh.Tube(horn, new Vec3(x, y, 0.43f), new Vec3(x, y + 0.04f, 0.62f), 0.04f, 0.002f, sides, false);
            parent = part;
        }
        int head = mesh.Part("head", body, new Vec3(0f, -0.94f, 0.42f));
        mesh.Blob(coat, new Vec3(0f, -0.94f, 0.42f), 0.16f, 0.20f, 0.15f, sides);
        mesh.Blob(light, new Vec3(0f, -1.10f, 0.37f), 0.12f, 0.13f, 0.07f, sides);
        AddEyes(mesh, head, eyes, new Vec3(0f, -1.105f, 0.49f), 0.095f, detail);
        AddHorns(mesh, head, horn, new Vec3(0f, -0.89f, 0.50f), 0.16f, 0.32f, sides, true);
        string[] names = ["leg_fl", "leg_fr", "leg_bl", "leg_br"];
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -0.19f : 0.19f;
            float y = i < 2 ? -0.5f : 0.5f;
            mesh.Part(names[i], body, new Vec3(x, y, 0.35f));
            mesh.Capsule(coat, new Vec3(x, y, 0.35f), new Vec3(x * 1.6f, y - 0.08f, 0.06f), 0.045f, 0.025f, sides);
            mesh.Box(horn, new Vec3(x * 1.6f, y - 0.10f, 0.03f), 0.045f, 0.07f, 0.03f);
        }
    }

    /// <summary>生成有根脚、树冠或枯枝的植物身体。</summary>
    /// <param name="mesh">网格生成器。</param><param name="id">资产名称。</param><param name="body">主体。</param><param name="wood">枝干。</param>
    /// <param name="leaf">树叶。</param><param name="dark">暗色。</param><param name="accent">花或火。</param><param name="sides">圆周面数。</param><param name="detail">细节等级。</param>
    private static void BuildTreeBody(MeshBuilder mesh, string id, int body, int wood, int leaf, int dark, int accent, int sides, int detail)
    {
        bool sprout = id == Actors.Plant.id;
        mesh.Capsule(wood, new Vec3(0f, 0f, 0.2f), new Vec3(0f, 0f, 0.9f), 0.12f, 0.055f, sides);
        AddLeg(mesh, body, "leg_l", wood, dark, new Vec3(-0.06f, 0f, 0.22f), 0.07f);
        AddLeg(mesh, body, "leg_r", wood, dark, new Vec3(0.06f, 0f, 0.22f), 0.07f);
        AddArms(mesh, body, wood, sides, 0.13f, 0f, 0.72f);
        int head = mesh.Part("head", body, new Vec3(0f, 0f, 0.86f));
        bool bare = id.Contains("Death") || id.Contains("Fire") || id.Contains("Acacia");
        if (bare)
        {
            for (int side = -1; side <= 1; side += 2) for (int level = 0; level < (detail < 2 ? 3 : 2); level++)
                mesh.Capsule(wood, new Vec3(0f, 0f, 0.72f + level * 0.13f), new Vec3(side * (0.22f - level * 0.035f), 0.03f, 0.98f + level * 0.11f), 0.04f, 0.01f, sides);
        }
        else if (id.Contains("Coconut"))
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * (float)Math.PI * 0.4f;
                mesh.Capsule(leaf, new Vec3(0f, 0f, 1.05f), new Vec3((float)Math.Cos(angle) * 0.36f, (float)Math.Sin(angle) * 0.36f, 0.87f), 0.075f, 0.025f, sides);
            }
        }
        else
        {
            mesh.Blob(leaf, new Vec3(0f, 0f, 0.98f), sprout ? 0.28f : 0.36f, 0.27f, 0.25f, sides);
            if (detail == 0) for (int i = -1; i <= 1; i++) mesh.Blob(wood, new Vec3(i * 0.17f, 0.03f, 0.89f), 0.18f, 0.18f, 0.17f, sides);
        }
        if (id.Contains("Fire"))
        {
            mesh.Part("flames", head, new Vec3(0f, 0f, 0.96f));
            mesh.Tube(accent, new Vec3(0f, 0f, 0.9f), new Vec3(0f, 0f, 1.24f), 0.12f, 0.003f, sides, false);
        }
    }

    /// <summary>增加绕腿根摆动的腿和保持零高度的脚。</summary>
    /// <param name="mesh">网格生成器。</param><param name="parent">父部件。</param><param name="name">腿部动作名称。</param>
    /// <param name="leg">腿材质。</param><param name="foot">脚材质。</param><param name="pivot">腿根位置。</param><param name="radius">腿宽。</param>
    private static void AddLeg(MeshBuilder mesh, int parent, string name, int leg, int foot, Vec3 pivot, float radius)
    {
        mesh.Part(name, parent, pivot);
        mesh.Capsule(leg, pivot, new Vec3(pivot.X, pivot.Y, 0.09f), radius, radius * 0.8f, 4);
        mesh.Box(foot, new Vec3(pivot.X, pivot.Y - 0.025f, 0.04f), radius, radius * 1.4f, 0.04f);
    }

    /// <summary>增加由原生双足动作驱动的左右手臂。</summary>
    /// <param name="mesh">网格生成器。</param><param name="body">主体。</param><param name="slot">材质。</param><param name="sides">圆周面数。</param>
    /// <param name="width">肩宽的一半。</param><param name="y">肩部前后位置。</param><param name="z">肩部高度。</param><param name="secondary">主翅膀存在时使用第二组部件。</param>
    private static void AddArms(MeshBuilder mesh, int body, int slot, int sides, float width = 0.16f, float y = 0f, float z = 0.72f, bool secondary = false)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            mesh.Part(side < 0 ? (secondary ? "wing_l2" : "wing_l") : (secondary ? "wing_r2" : "wing_r"), body, new Vec3(side * width, y, z));
            mesh.Capsule(slot, new Vec3(side * width, y, z), new Vec3(side * (width + 0.05f), y - 0.02f, z - 0.32f), 0.055f, 0.04f, sides);
        }
    }

    /// <summary>增加有厚度的左右翅膀。</summary>
    /// <param name="mesh">网格生成器。</param><param name="body">主体。</param><param name="slot">材质。</param><param name="span">翼展的一半。</param><param name="z">翼根高度。</param>
    private static void AddWings(MeshBuilder mesh, int body, int slot, float span, float z)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            mesh.Part(side < 0 ? "wing_l" : "wing_r", body, new Vec3(side * 0.12f, 0f, z));
            mesh.Extrude(slot, new Vec3(0f, 0f, z), new Vec3(side, 0f, 0f), new Vec3(0f, 1f, 0f), 0.016f,
                0.12f, -0.12f, span * 0.67f, -0.25f, span, 0.27f, span * 0.48f, 0.13f, 0.12f, 0.19f);
        }
    }

    /// <summary>增加正面的成对眼睛。</summary>
    /// <param name="mesh">网格生成器。</param><param name="parent">头部。</param><param name="slot">材质。</param><param name="at">眼睛中点。</param>
    /// <param name="spacing">眼间距的一半。</param><param name="detail">细节等级。</param>
    private static void AddEyes(MeshBuilder mesh, int parent, int slot, Vec3 at, float spacing, int detail)
    {
        if (detail > 0) return;
        mesh.Part("eyes", parent, at);
        for (int side = -1; side <= 1; side += 2) mesh.Box(slot, new Vec3(side * spacing, at.Y, at.Z), 0.018f, 0.012f, 0.018f);
    }

    /// <summary>增加成对尖角或带分叉的鹿角。</summary>
    /// <param name="mesh">网格生成器。</param><param name="head">头部。</param><param name="slot">材质。</param><param name="at">角根中点。</param>
    /// <param name="width">角间距的一半。</param><param name="height">角高度。</param><param name="sides">圆周面数。</param><param name="branched">是否分叉。</param>
    private static void AddHorns(MeshBuilder mesh, int head, int slot, Vec3 at, float width, float height, int sides, bool branched = false)
    {
        mesh.Part("horns", head, at);
        for (int side = -1; side <= 1; side += 2)
        {
            Vec3 root = new(side * width * 0.65f, at.Y, at.Z);
            Vec3 tip = new(side * width, at.Y + 0.10f, at.Z + height);
            mesh.Tube(slot, root, tip, 0.04f, 0.003f, sides, false);
            if (branched) mesh.Tube(slot, new Vec3(side * width * 0.85f, at.Y + 0.04f, at.Z + height * 0.5f), new Vec3(side * width * 1.5f, at.Y - 0.04f, at.Z + height * 0.9f), 0.027f, 0.003f, sides, false);
        }
    }

    /// <summary>保留动作部件，仅去掉远处看不到的五官和装饰。</summary>
    /// <param name="model">近景模型。</param><param name="name">新模型名称。</param><param name="far">是否为最远档。</param>
    /// <returns>仍能用原生人形动作的精简模型。</returns>
    internal static MeshModel Reduced(MeshModel model, string name, bool far)
    {
        var parts = model.Parts.Select(part => new MeshPart
        {
            Name = part.Name, Parent = part.Parent, Pivot = part.Pivot, Axis = part.Axis,
            Pos = part.Pos, Nrm = part.Nrm, Tri = part.Tri, Slot = part.Slot
        }).ToArray();
        foreach (MeshPart part in parts)
        {
            if (part.Name != "face_details" && part.Name != "beard" && (!far || part.Name != "armour")) continue;
            part.Pos = []; part.Nrm = []; part.Tri = []; part.Slot = []; part.Axis = [];
        }
        return model.Derive(name, parts);
    }

    /// <summary>同时调整顶点、部件原点、法线和总边界。</summary>
    /// <param name="model">自有模型。</param><param name="x">横向比例。</param><param name="y">深度比例。</param><param name="z">高度比例。</param>
    internal static void Scale(MeshModel model, float x, float y, float z)
    {
        foreach (MeshPart part in model.Parts)
        {
            part.Pivot = new Vec3(part.Pivot.X * x, part.Pivot.Y * y, part.Pivot.Z * z);
            for (int i = 0; i < part.Pos.Length; i += 3)
            {
                part.Pos[i] *= x; part.Pos[i + 1] *= y; part.Pos[i + 2] *= z;
                Vec3 normal = new Vec3(part.Nrm[i] / x, part.Nrm[i + 1] / y, part.Nrm[i + 2] / z).Normalized();
                part.Nrm[i] = normal.X; part.Nrm[i + 1] = normal.Y; part.Nrm[i + 2] = normal.Z;
            }
        }
        model.Min = new Vec3(model.Min.X * x, model.Min.Y * y, model.Min.Z * z);
        model.Max = new Vec3(model.Max.X * x, model.Max.Y * y, model.Max.Z * z);
    }
}
#endif