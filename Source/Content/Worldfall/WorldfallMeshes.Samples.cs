#if S3COND2_WORLDFALL
using System;
using System.Reflection;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

/// <summary>由自有贴图和占地约定生成可复用的三维网格。</summary>
internal static partial class WorldfallMeshes
{
    /// <summary>宗门大殿模型的名称。</summary>
    internal const string SectHallFamily = "cultiway_sect_hall";
    /// <summary>生成低多边形宗门大殿，屋檐和台基使用可复用的 Cultiway 材质槽。</summary>
    internal static MeshModel BuildSectHall()
    {
        MeshBuilder mesh = new(SectHallFamily);
        int stone = mesh.Slot("stone", 4, TerrainCache.Rgb(132, 126, 116));
        int wall = mesh.Slot("wall", 3, TerrainCache.Rgb(148, 102, 60));
        int timber = mesh.Slot("timber", 3, TerrainCache.Rgb(92, 60, 40));
        int door = mesh.Slot("door", 3, TerrainCache.Rgb(92, 60, 40));
        int roof = mesh.Slot("roof", MeshKind.Roof, TerrainCache.Rgb(154, 74, 48));
        int roofDark = mesh.Slot("roof_dark", MeshKind.Roof, TerrainCache.Rgb(100, 48, 38));
        int gold = mesh.Slot("gold", 10, TerrainCache.Rgb(214, 174, 68));

        int basePart = mesh.Part("base", -1, new Vec3(0f, 0f, 0f));
        mesh.Box(stone, new Vec3(0f, 0f, 0.055f), 0.5f, 0.24f, 0.055f);
        mesh.Box(stone, new Vec3(0f, -0.155f, 0.13f), 0.38f, 0.075f, 0.035f);

        int hallPart = mesh.Part("hall", basePart, new Vec3(0f, 0f, 0f));
        mesh.Box(wall, new Vec3(0f, 0.015f, 0.31f), 0.33f, 0.16f, 0.17f);
        mesh.Box(timber, new Vec3(0f, -0.165f, 0.31f), 0.34f, 0.022f, 0.19f);
        mesh.Box(timber, new Vec3(0f, 0.18f, 0.31f), 0.34f, 0.022f, 0.19f);

        mesh.Part("door", hallPart, new Vec3(0f, -0.17f, 0.21f));
        mesh.Box(door, new Vec3(0f, -0.171f, 0.28f), 0.075f, 0.012f, 0.12f);
        mesh.Box(gold, new Vec3(0f, -0.185f, 0.28f), 0.012f, 0.006f, 0.018f);

        mesh.Part("columns", hallPart, new Vec3(0f, 0f, 0f));
        for (int side = -1; side <= 1; side += 2)
        {
            for (int depth = -1; depth <= 1; depth += 2)
            {
                mesh.Box(timber, new Vec3(side * 0.27f, depth * 0.12f, 0.37f), 0.026f, 0.026f, 0.2f);
            }
        }
        mesh.Box(timber, new Vec3(0f, -0.17f, 0.49f), 0.37f, 0.03f, 0.025f);

        AddRoofTier(mesh, hallPart, "lower_roof", roof, roofDark, 0.52f, 0.28f, 0.49f, 0.69f);

        mesh.Part("middle_storey", hallPart, new Vec3(0f, 0f, 0f));
        mesh.Box(wall, new Vec3(0f, 0f, 0.73f), 0.19f, 0.13f, 0.04f);
        mesh.Box(timber, new Vec3(0f, -0.132f, 0.73f), 0.09f, 0.012f, 0.022f);
        AddRoofTier(mesh, hallPart, "middle_roof", roof, roofDark, 0.34f, 0.20f, 0.78f, 0.94f);

        mesh.Part("upper_storey", hallPart, new Vec3(0f, 0f, 0f));
        mesh.Box(wall, new Vec3(0f, 0f, 0.97f), 0.11f, 0.075f, 0.03f);
        mesh.Box(timber, new Vec3(0f, -0.077f, 0.97f), 0.055f, 0.008f, 0.018f);
        AddRoofTier(mesh, hallPart, "upper_roof", roof, roofDark, 0.21f, 0.13f, 1.02f, 1.15f);

        mesh.Part("finial", hallPart, new Vec3(0f, 0f, 0f));
        mesh.Box(gold, new Vec3(0f, 0f, 1.16f), 0.08f, 0.025f, 0.025f);
        mesh.Box(gold, new Vec3(0f, 0f, 1.22f), 0.025f, 0.025f, 0.045f);

        return SetInfo(mesh.Build(), "kind=hall;width=1.1");
    }

    /// <summary>生成一层四面屋顶和外沿檐口，供大殿三层屋顶共用。</summary>
    /// <param name="mesh">大殿网格生成器。</param>
    /// <param name="parent">屋顶所属的建筑部件。</param>
    /// <param name="name">本层屋顶的部件名称。</param>
    /// <param name="roof">屋顶主体材质槽。</param>
    /// <param name="trim">屋檐和翘角材质槽。</param>
    /// <param name="halfWidth">屋顶一半宽度。</param>
    /// <param name="halfDepth">屋顶一半深度。</param>
    /// <param name="eavesZ">屋檐高度。</param>
    /// <param name="peakZ">屋脊顶点高度。</param>
    private static void AddRoofTier(MeshBuilder mesh, int parent, string name, int roof, int trim,
        float halfWidth, float halfDepth, float eavesZ, float peakZ)
    {
        mesh.Part(name, parent, new Vec3(0f, 0f, 0f));
        Vec3 peak = new(0f, 0f, peakZ);
        Vec3 frontLeft = new(-halfWidth, -halfDepth, eavesZ);
        Vec3 frontRight = new(halfWidth, -halfDepth, eavesZ);
        Vec3 backRight = new(halfWidth, halfDepth, eavesZ);
        Vec3 backLeft = new(-halfWidth, halfDepth, eavesZ);

        mesh.Face(roof, frontLeft, frontRight, peak);
        mesh.Face(roof, frontRight, backRight, peak);
        mesh.Face(roof, backRight, backLeft, peak);
        mesh.Face(roof, backLeft, frontLeft, peak);

        mesh.Box(trim, new Vec3(0f, -halfDepth, eavesZ), halfWidth, 0.02f, 0.018f);
        mesh.Box(trim, new Vec3(0f, halfDepth, eavesZ), halfWidth, 0.02f, 0.018f);
        mesh.Box(trim, new Vec3(-halfWidth, 0f, eavesZ), 0.02f, halfDepth, 0.018f);
        mesh.Box(trim, new Vec3(halfWidth, 0f, eavesZ), 0.02f, halfDepth, 0.018f);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int depth = -1; depth <= 1; depth += 2)
            {
                mesh.Box(trim, new Vec3(side * halfWidth, depth * halfDepth, eavesZ + 0.02f),
                    0.03f, 0.03f, 0.035f);
            }
        }
    }

    /// <summary>生成有可动骨架部件的人形修士，并按东人动画贴图取色。</summary>
    internal static MeshModel BuildEasternHuman(string name, bool female = false, bool armoured = false)
    {
        MeshBuilder mesh = new(name);
        int skin = mesh.Slot("skin", 8, TerrainCache.Rgb(222, 180, 148));
        int hair = mesh.Slot("hair", 3, TerrainCache.Rgb(46, 34, 28));
        int eyes = mesh.Slot("eyes", 10, TerrainCache.Rgb(30, 26, 24));
        int shirt = mesh.Slot("shirt", 7, TerrainCache.Rgb(70, 96, 142));
        int shirtDark = mesh.Slot("shirt_dark", 7, TerrainCache.Rgb(48, 68, 104));
        int pants = mesh.Slot("pants", 7, TerrainCache.Rgb(52, 62, 78));
        int shoes = mesh.Slot("shoes", 3, TerrainCache.Rgb(82, 58, 42));
        int belt = mesh.Slot("belt", 3, TerrainCache.Rgb(108, 74, 44));
        mesh.Slot("mouth", 7, TerrainCache.Rgb(160, 86, 82));
        mesh.Slot("tusk", 8, TerrainCache.Rgb(230, 220, 192));
        int accent = mesh.Slot("accent", 10, TerrainCache.Rgb(198, 156, 58));

        int hips = mesh.Part("hips", -1, new Vec3(0f, 0f, 0.42f));
        mesh.Box(pants, new Vec3(0f, 0f, 0.42f), 0.105f, 0.075f, 0.09f);
        mesh.Part("robe_skirt", hips, new Vec3(0f, 0f, 0.42f));
        mesh.Loft(shirt,
            [new Vec3(0f, 0f, 0.15f), new Vec3(0f, 0f, 0.50f)],
            [0.185f, 0.13f], [0.15f, 0.09f],
            new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), 8, false, (float)Math.PI / 8f);
        mesh.Box(shirtDark, new Vec3(0f, -0.14f, 0.165f), 0.145f, 0.008f, 0.014f);

        int torso = mesh.Part("torso", hips, new Vec3(0f, 0f, 0.59f));
        mesh.Loft(shirt,
            [new Vec3(0f, 0f, 0.49f), new Vec3(0f, 0f, 0.89f)],
            [0.12f, 0.145f], [0.085f, 0.075f],
            new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), 8, false, (float)Math.PI / 8f);
        mesh.Box(shirtDark, new Vec3(0f, -0.086f, 0.62f), 0.035f, 0.012f, 0.17f);
        mesh.Box(belt, new Vec3(0f, 0f, 0.49f), 0.14f, 0.09f, 0.028f);
        mesh.Box(accent, new Vec3(0f, -0.097f, 0.49f), 0.025f, 0.008f, 0.025f);

        int head = mesh.Part("head", torso, new Vec3(0f, 0f, 0.99f));
        mesh.Blob(skin, new Vec3(0f, 0f, 1f), 0.085f, 0.075f, 0.105f, 6);
        mesh.Part("hair_short", head, new Vec3(0f, 0f, 1.07f));
        mesh.Blob(hair, new Vec3(0f, 0f, 1.09f), 0.092f, 0.082f, 0.045f, 6);
        mesh.Part("hair_long", head, new Vec3(0f, 0f, 0.98f));
        mesh.Box(hair, new Vec3(0f, 0.06f, 0.98f), 0.065f, 0.035f, 0.13f);
        mesh.Part("beard", head, new Vec3(0f, 0f, 0.91f));
        mesh.Blob(hair, new Vec3(0f, -0.01f, 0.91f), 0.046f, 0.05f, 0.055f, 5);
        mesh.Part("face_details", head, new Vec3(0f, 0f, 1f));
        mesh.Box(eyes, new Vec3(-0.03f, -0.073f, 1.02f), 0.015f, 0.006f, 0.008f);
        mesh.Box(eyes, new Vec3(0.03f, -0.073f, 1.02f), 0.015f, 0.006f, 0.008f);

        int upperArmL = mesh.Part("upper_arm_L", torso, new Vec3(-0.14f, 0f, 0.82f));
        mesh.Box(shirt, new Vec3(-0.16f, 0f, 0.73f), 0.05f, 0.055f, 0.14f);
        mesh.Part("lower_arm_L", upperArmL, new Vec3(-0.17f, 0f, 0.61f));
        mesh.Box(shirt, new Vec3(-0.17f, 0f, 0.60f), 0.045f, 0.05f, 0.12f);
        mesh.Blob(skin, new Vec3(-0.17f, 0f, 0.48f), 0.04f, 0.04f, 0.045f, 5);

        int upperArmR = mesh.Part("upper_arm_R", torso, new Vec3(0.14f, 0f, 0.82f));
        mesh.Box(shirt, new Vec3(0.16f, 0f, 0.73f), 0.05f, 0.055f, 0.14f);
        mesh.Part("lower_arm_R", upperArmR, new Vec3(0.17f, 0f, 0.61f));
        mesh.Box(shirt, new Vec3(0.17f, 0f, 0.60f), 0.045f, 0.05f, 0.12f);
        mesh.Blob(skin, new Vec3(0.17f, 0f, 0.48f), 0.04f, 0.04f, 0.045f, 5);

        int upperLegL = mesh.Part("upper_leg_L", hips, new Vec3(-0.06f, 0f, 0.39f));
        mesh.Box(pants, new Vec3(-0.06f, 0f, 0.27f), 0.052f, 0.06f, 0.14f);
        mesh.Part("lower_leg_L", upperLegL, new Vec3(-0.06f, 0f, 0.15f));
        mesh.Box(pants, new Vec3(-0.06f, 0f, 0.14f), 0.048f, 0.05f, 0.13f);
        mesh.Box(shoes, new Vec3(-0.06f, -0.018f, 0.035f), 0.055f, 0.075f, 0.035f);

        int upperLegR = mesh.Part("upper_leg_R", hips, new Vec3(0.06f, 0f, 0.39f));
        mesh.Box(pants, new Vec3(0.06f, 0f, 0.27f), 0.052f, 0.06f, 0.14f);
        mesh.Part("lower_leg_R", upperLegR, new Vec3(0.06f, 0f, 0.15f));
        mesh.Box(pants, new Vec3(0.06f, 0f, 0.14f), 0.048f, 0.05f, 0.13f);
        mesh.Box(shoes, new Vec3(0.06f, -0.018f, 0.035f), 0.055f, 0.075f, 0.035f);

        if (armoured)
        {
            mesh.Part("armour", torso, new Vec3(0f, 0f, 0.7f));
            mesh.Box(accent, new Vec3(0f, -0.074f, 0.71f), 0.104f, 0.02f, 0.13f);
            mesh.Box(shirtDark, new Vec3(-0.16f, 0f, 0.85f), 0.058f, 0.062f, 0.035f);
            mesh.Box(shirtDark, new Vec3(0.16f, 0f, 0.85f), 0.058f, 0.062f, 0.035f);
        }
        MeshModel model = SetInfo(mesh.Build(), "robe=1;arm=bell");
        if (female) Scale(model, 0.94f, 0.98f, 1f);
        return model;
    }

    /// <summary>为模型附加 Worldfall 读取的简要渲染信息。</summary>
    /// <param name="model">已构建完成的网格。</param>
    /// <param name="info">Worldfall 用于缩放和建筑类别判断的信息。</param>
    /// <returns>附加信息后的网格。</returns>
    internal static MeshModel SetInfo(MeshModel model, string info)
    {
        MethodInfo parser = typeof(MeshModel).GetMethod("ParseInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        parser.Invoke(model, [info]);
        return model;
    }
}
#endif
