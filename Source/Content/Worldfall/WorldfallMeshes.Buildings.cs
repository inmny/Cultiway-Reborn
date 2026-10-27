#if S3COND2_WORLDFALL
using System;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

internal static partial class WorldfallMeshes
{
    /// <summary>生成建筑的主体与台基；台基宽度为一，深度来自真实占地。</summary>
    /// <param name="name">模型族名称。</param><param name="asset">建筑资产。</param><param name="shape">建筑类别。</param><param name="detail">细节等级。</param>
    /// <returns>可直接接入原生建筑状态和点击处理的网格。</returns>
    internal static MeshModel BuildBuilding(string name, BuildingAsset asset, WorldfallBuilding shape, int detail)
    {
        float depth = (float)asset.fundament.height / asset.fundament.width * 0.5f;
        if (asset == Buildings.SectHall)
        {
            MeshModel hall = BuildSectHall();
            hall.Name = name;
            Scale(hall, 1f, depth / 0.24f, 0.54f);
            if (detail > 0)
                foreach (MeshPart part in hall.Parts)
                    if (part.Name == "door" || part.Name == "columns" || (detail == 2 && part.Name == "finial")) Empty(part);
            return SetInfo(hall, "kind=hall;width=1");
        }
        MeshBuilder mesh = new(name);
        int stone = mesh.Slot("stone", 4, TerrainCache.Rgb(130, 125, 119));
        int wall = mesh.Slot("wall", 3, asset == Buildings.SectTreasurePavilion || asset == Buildings.SectScripturePavilion
            ? TerrainCache.Rgb(148, 102, 60) : TerrainCache.Rgb(215, 198, 160));
        int wood = mesh.Slot("timber", 3, TerrainCache.Rgb(86, 56, 40));
        int door = mesh.Slot("door", 3, TerrainCache.Rgb(61, 45, 34));
        int roof = mesh.Slot("roof", MeshKind.Roof, RoofColor(asset.id));
        int trim = mesh.Slot("roof_dark", MeshKind.Roof, TerrainCache.Scale(RoofColor(asset.id), 0.65f));
        int gold = mesh.Slot("gold", 10, TerrainCache.Rgb(216, 177, 69));
        int crystal = mesh.Slot("crystal", 10, asset == Buildings.CrimsonCore ? TerrainCache.Rgb(193, 36, 56) : TerrainCache.Rgb(61, 178, 197));
        int leaf = mesh.Slot("leaf", 7, TerrainCache.Rgb(76, 141, 69));
        int banner = asset.id == "hall_Cultiway.EasternHuman_3" ? mesh.Slot("banner", 7, TerrainCache.Rgb(255, 0, 255)) : -1;
        int root = mesh.Part("base", -1, new Vec3(0f, 0f, 0f));
        bool flora = shape == WorldfallBuilding.Tree || shape == WorldfallBuilding.Plant;
        if (!flora) mesh.Box(stone, new Vec3(0f, 0f, 0.025f), 0.5f, depth, 0.025f);
        string kind = "hall";
        switch (shape)
        {
            case WorldfallBuilding.Hall:
            case WorldfallBuilding.Pagoda:
                int floors = shape == WorldfallBuilding.Pagoda ? 2 : asset.id.Contains("hall_") || asset.id.Contains("temple_") ? 2 : 1;
                for (int floor = 0; floor < floors; floor++)
                {
                    float inset = floor == 0 ? 1f : 0.67f;
                    float baseZ = 0.06f + floor * 0.30f;
                    mesh.Part("storey_" + floor, root, new Vec3(0f, 0f, baseZ));
                    mesh.Box(wall, new Vec3(0f, 0f, baseZ + 0.105f), 0.36f * inset, depth * 0.72f * inset, 0.105f);
                    mesh.Box(wood, new Vec3(0f, -depth * 0.74f * inset, baseZ + 0.10f), 0.37f * inset, 0.017f, 0.10f);
                    if (detail < 2)
                        for (int side = -1; side <= 1; side += 2)
                            mesh.Box(wood, new Vec3(side * 0.29f * inset, -depth * 0.74f * inset, baseZ + 0.11f), 0.022f, 0.024f, 0.11f);
                    AddRoofTier(mesh, root, "roof_" + floor, roof, trim, 0.51f * inset, (depth + 0.025f) * inset, baseZ + 0.22f, baseZ + 0.35f);
                }
                mesh.Part("door", root, new Vec3(0f, -depth * 0.74f, 0.06f));
                mesh.Box(door, new Vec3(0f, -depth * 0.75f, 0.15f), 0.075f, 0.02f, 0.095f);
                if (asset == Buildings.SectTreasurePavilion && detail == 0)
                {
                    mesh.Box(gold, new Vec3(0f, -depth * 0.78f, 0.15f), 0.015f, 0.01f, 0.028f);
                    mesh.Box(gold, new Vec3(0f, -depth * 0.77f, 0.27f), 0.11f, 0.012f, 0.025f);
                }
                break;
            case WorldfallBuilding.Tower:
            case WorldfallBuilding.Windmill:
                mesh.Part("tower", root, new Vec3(0f, 0f, 0.05f));
                mesh.Loft(wall, [new Vec3(0f, 0f, 0.05f), new Vec3(0f, 0f, 0.65f)], [0.27f, 0.20f], [depth * 0.80f, depth * 0.62f], new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), detail == 2 ? 4 : 8, false, (float)Math.PI / 8f);
                for (int level = 0; level < 3; level++) mesh.Box(stone, new Vec3(0f, 0f, 0.18f + level * 0.20f), 0.29f - level * 0.018f, depth * 0.84f, 0.025f);
                AddRoofTier(mesh, root, "spire", roof, trim, 0.31f, depth * 0.95f, 0.66f, 0.84f);
                mesh.Part("door", root, new Vec3(0f, -depth * 0.82f, 0.12f));
                mesh.Box(door, new Vec3(0f, -depth * 0.82f, 0.14f), 0.07f, 0.012f, 0.09f);
                if (shape == WorldfallBuilding.Windmill)
                {
                    mesh.Part("sails", root, new Vec3(0f, -depth * 0.98f, 0.59f));
                    mesh.Box(wood, new Vec3(0f, -depth * 0.98f, 0.59f), 0.045f, 0.06f, 0.045f);
                    mesh.Box(wall, new Vec3(0f, -depth * 1.02f, 0.59f), 0.46f, 0.022f, 0.034f);
                    mesh.Box(wall, new Vec3(0f, -depth * 1.02f, 0.59f), 0.034f, 0.022f, 0.46f);
                }
                else if (detail == 0)
                {
                    mesh.Part("crest", root, new Vec3(0f, 0f, 0.80f));
                    if (asset == Buildings.RobotTower) mesh.Box(crystal, new Vec3(0f, 0f, 0.83f), 0.12f, 0.12f, 0.07f);
                    else mesh.Blob(asset == Buildings.NurgleTower ? leaf : crystal, new Vec3(0f, 0f, 0.88f), 0.08f, 0.08f, 0.09f, 6);
                }
                break;
            case WorldfallBuilding.Castle:
                mesh.Part("keep", root, new Vec3(0f, 0f, 0.05f));
                mesh.Box(wall, new Vec3(0f, 0f, 0.26f), 0.30f, depth * 0.74f, 0.21f);
                AddRoofTier(mesh, root, "main_roof", roof, trim, 0.35f, depth * 0.84f, 0.48f, 0.67f);
                for (int side = -1; side <= 1; side += 2)
                    for (int back = -1; back <= 1; back += 2)
                    {
                        mesh.Part("turret_" + side + "_" + back, root, new Vec3(side * 0.38f, back * depth * 0.6f, 0.05f));
                        mesh.Box(stone, new Vec3(side * 0.38f, back * depth * 0.6f, 0.28f), 0.09f, depth * 0.23f, 0.23f);
                        mesh.Tube(roof, new Vec3(side * 0.38f, back * depth * 0.6f, 0.53f), new Vec3(side * 0.38f, back * depth * 0.6f, 0.76f), 0.13f, 0.001f, 4, false, (float)Math.PI / 4f);
                    }
                mesh.Part("door", root, new Vec3(0f, -depth * 0.75f, 0.05f));
                mesh.Box(door, new Vec3(0f, -depth * 0.76f, 0.15f), 0.11f, 0.018f, 0.1f);
                break;
            case WorldfallBuilding.Pyramid:
                for (int level = 0; level < (detail == 2 ? 4 : 7); level++)
                {
                    int count = detail == 2 ? 4 : 7;
                    float share = 1f - (float)level / count;
                    mesh.Part("tier_" + level, root, new Vec3(0f, 0f, 0f));
                    mesh.Box(wall, new Vec3(0f, 0f, 0.05f + (level + 0.5f) * 0.65f / count), 0.48f * share, depth * 0.94f * share, 0.325f / count);
                }
                mesh.Part("door", root, new Vec3(0f, -depth * 0.95f, 0.05f));
                mesh.Box(door, new Vec3(0f, -depth * 0.96f, 0.13f), 0.055f, 0.01f, 0.085f);
                break;
            case WorldfallBuilding.Portal:
                kind = "circle";
                mesh.Part("runes", root, new Vec3(0f, 0f, 0.05f));
                int points = detail == 2 ? 8 : 16;
                for (int i = 0; i < points; i++)
                {
                    float a = i * (float)Math.PI * 2f / points;
                    float b = (i + 1) * (float)Math.PI * 2f / points;
                    mesh.Capsule(crystal, new Vec3((float)Math.Cos(a) * 0.39f, (float)Math.Sin(a) * depth * 0.77f, 0.075f), new Vec3((float)Math.Cos(b) * 0.39f, (float)Math.Sin(b) * depth * 0.77f, 0.075f), 0.014f, 0.014f, 4);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    mesh.Part("pylon_" + side, root, new Vec3(side * 0.43f, 0f, 0.05f));
                    mesh.Box(stone, new Vec3(side * 0.43f, 0f, 0.18f), 0.045f, depth * 0.16f, 0.13f);
                    mesh.Blob(crystal, new Vec3(side * 0.43f, 0f, 0.32f), 0.055f, 0.05f, 0.08f, 4);
                }
                break;
            case WorldfallBuilding.Station:
            case WorldfallBuilding.Docks:
                kind = shape == WorldfallBuilding.Docks ? "docks" : "hall";
                mesh.Part("deck", root, new Vec3(0f, 0f, 0.05f));
                mesh.Box(wood, new Vec3(0f, 0f, 0.09f), 0.47f, depth * 0.95f, 0.04f);
                if (detail == 0) for (int i = -4; i <= 4; i++) mesh.Box(trim, new Vec3(i * 0.1f, 0f, 0.135f), 0.012f, depth * 0.96f, 0.004f);
                if (shape == WorldfallBuilding.Station)
                {
                    mesh.Part("pillars", root, new Vec3(0f, 0f, 0.05f));
                    for (int side = -1; side <= 1; side += 2) for (int back = -1; back <= 1; back += 2)
                        mesh.Box(wood, new Vec3(side * 0.39f, back * depth * 0.55f, 0.24f), 0.025f, 0.025f, 0.15f);
                    AddRoofTier(mesh, root, "canopy", roof, trim, 0.49f, depth * 0.82f, 0.39f, 0.52f);
                }
                else
                {
                    mesh.Part("posts", root, new Vec3(0f, 0f, 0f));
                    for (int side = -1; side <= 1; side += 2) mesh.Box(stone, new Vec3(side * 0.4f, depth * 0.7f, 0.06f), 0.04f, 0.04f, 0.11f);
                }
                break;
            case WorldfallBuilding.Throne:
                mesh.Part("throne", root, new Vec3(0f, 0f, 0.05f));
                mesh.Box(gold, new Vec3(0f, 0f, 0.18f), 0.23f, depth * 0.70f, 0.13f);
                mesh.Box(gold, new Vec3(0f, depth * 0.55f, 0.43f), 0.28f, 0.05f, 0.35f);
                mesh.Box(roof, new Vec3(0f, 0f, 0.32f), 0.15f, depth * 0.56f, 0.025f);
                for (int side = -1; side <= 1; side += 2) mesh.Box(stone, new Vec3(side * 0.39f, 0f, 0.28f), 0.075f, depth * 0.4f, 0.23f);
                break;
            case WorldfallBuilding.Crystal:
                mesh.Part("core", root, new Vec3(0f, 0f, 0.05f));
                mesh.Blob(crystal, new Vec3(0f, 0f, 0.38f), 0.23f, depth * 0.75f, 0.32f, detail == 2 ? 4 : 8);
                for (int side = -1; side <= 1; side += 2) mesh.Box(stone, new Vec3(side * 0.34f, 0f, 0.20f), 0.065f, depth * 0.4f, 0.15f);
                break;
            case WorldfallBuilding.Tree:
            case WorldfallBuilding.Plant:
                kind = "tree";
                BuildFlora(mesh, asset.id, root, wood, stone, leaf, crystal, gold, shape == WorldfallBuilding.Tree, detail);
                break;
        }
        if (banner >= 0)
        {
            mesh.Part("banner", root, new Vec3(0f, -depth * 0.77f, 0.20f));
            mesh.Box(banner, new Vec3(0f, -depth * 0.77f, 0.20f), 0.035f, 0.006f, 0.025f);
        }
        MeshModel model = mesh.Build();
        // 自然物件的原色已有独立色块；不套用建筑砖石、木板等程序纹理。
        if (flora)
        {
            for (int i = 0; i < model.SlotKind.Length; i++) model.SlotKind[i] = MeshKind.Plain;
        }
        return SetInfo(model, $"kind={kind};width=1;cultiway_building={shape}");
    }

    /// <summary>制作完整建筑之外的破损墙、断柱和屋瓦。</summary>
    /// <param name="name">废墟模型名称。</param><param name="asset">建筑资产。</param>
    /// <returns>保持台基范围的低矮废墟。</returns>
    internal static MeshModel BuildRuin(string name, BuildingAsset asset)
    {
        MeshBuilder mesh = new(name);
        int stone = mesh.Slot("stone", 4, TerrainCache.Rgb(91, 86, 78));
        int timber = mesh.Slot("timber", 3, TerrainCache.Rgb(57, 43, 33));
        int roof = mesh.Slot("roof", MeshKind.Roof, TerrainCache.Scale(RoofColor(asset.id), 0.55f));
        int banner = asset.id == "hall_Cultiway.EasternHuman_3" ? mesh.Slot("banner", 7, TerrainCache.Rgb(255, 0, 255)) : -1;
        int wall = asset == Buildings.SectHall || asset == Buildings.SectScripturePavilion || asset == Buildings.SectTreasurePavilion
            ? mesh.Slot("wall", 3, TerrainCache.Rgb(148, 102, 60)) : stone;
        float depth = (float)asset.fundament.height / asset.fundament.width * 0.5f;
        int root = mesh.Part("foundation", -1, new Vec3(0f, 0f, 0f));
        mesh.Box(stone, new Vec3(0f, 0f, 0.02f), 0.5f, depth, 0.02f);
        mesh.Part("broken_walls", root, new Vec3(0f, 0f, 0f));
        mesh.Box(wall, new Vec3(-0.3f, 0f, 0.11f), 0.06f, depth * 0.75f, 0.1f);
        mesh.Box(wall, new Vec3(0.19f, depth * 0.63f, 0.08f), 0.20f, 0.05f, 0.06f);
        mesh.Part("rubble", root, new Vec3(0f, 0f, 0f));
        for (int i = 0; i < 7; i++)
        {
            float x = (i % 4 - 1.5f) * 0.18f;
            float y = (i / 4 - 0.4f) * depth;
            mesh.Blob(i % 2 == 0 ? stone : roof, new Vec3(x, y, 0.06f), 0.10f, 0.06f, 0.05f, 4);
        }
        mesh.Part("fallen_beam", root, new Vec3(0f, 0f, 0f));
        mesh.Capsule(timber, new Vec3(-0.31f, -depth * 0.45f, 0.07f), new Vec3(0.33f, depth * 0.4f, 0.09f), 0.035f, 0.035f, 4);
        if (banner >= 0) mesh.Box(banner, new Vec3(0.13f, -depth * 0.3f, 0.07f), 0.035f, 0.025f, 0.006f);
        return SetInfo(mesh.Build(), "kind=hall;ruin=1;width=1");
    }

    /// <summary>按 Worldfall 的四个进度阶段组织地基、墙架、屋架和瓦面。</summary>
    /// <param name="name">施工模型名称。</param><param name="asset">建筑资产。</param>
    /// <returns>由原生进度选择部件的施工模型。</returns>
    internal static MeshModel BuildSite(string name, BuildingAsset asset)
    {
        MeshBuilder mesh = new(name);
        int stone = mesh.Slot("stone", 4, TerrainCache.Rgb(132, 126, 117));
        int wood = mesh.Slot("timber", 3, TerrainCache.Rgb(111, 80, 50));
        int wall = mesh.Slot("wall", 3, asset == Buildings.SectHall || asset == Buildings.SectScripturePavilion || asset == Buildings.SectTreasurePavilion
            ? TerrainCache.Rgb(148, 102, 60) : TerrainCache.Rgb(190, 175, 144));
        int roof = mesh.Slot("roof", MeshKind.Roof, RoofColor(asset.id));
        int banner = asset.id == "hall_Cultiway.EasternHuman_3" ? mesh.Slot("banner", 7, TerrainCache.Rgb(255, 0, 255)) : -1;
        float depth = (float)asset.fundament.height / asset.fundament.width * 0.5f;
        mesh.Part("base@0-3", -1, new Vec3(0f, 0f, 0f));
        mesh.Box(stone, new Vec3(0f, 0f, 0.025f), 0.5f, depth, 0.025f);
        mesh.Part("supplies~@0-2", -1, new Vec3(0f, 0f, 0f));
        mesh.Box(wood, new Vec3(0.32f, depth * 0.3f, 0.08f), 0.13f, depth * 0.32f, 0.03f);
        mesh.Part("frames@1-3", -1, new Vec3(0f, 0f, 0f));
        for (int side = -1; side <= 1; side += 2) for (int back = -1; back <= 1; back += 2)
            mesh.Box(wood, new Vec3(side * 0.31f, back * depth * 0.65f, 0.17f), 0.02f, 0.02f, 0.12f);
        mesh.Part("walls@2-3", -1, new Vec3(0f, 0f, 0f));
        mesh.Box(wall, new Vec3(0f, depth * 0.63f, 0.17f), 0.31f, 0.025f, 0.11f);
        mesh.Part("rafters@2-3", -1, new Vec3(0f, 0f, 0f));
        for (int i = -1; i <= 1; i++)
        {
            mesh.Capsule(wood, new Vec3(i * 0.27f, -depth * 0.7f, 0.29f), new Vec3(i * 0.27f, 0f, 0.43f), 0.016f, 0.016f, 4);
            mesh.Capsule(wood, new Vec3(i * 0.27f, 0f, 0.43f), new Vec3(i * 0.27f, depth * 0.7f, 0.29f), 0.016f, 0.016f, 4);
        }
        mesh.Part("tiles@3", -1, new Vec3(0f, 0f, 0f));
        mesh.Face(roof, new Vec3(-0.38f, -depth * 0.8f, 0.3f), new Vec3(0.05f, -depth * 0.8f, 0.3f), new Vec3(0.05f, 0f, 0.44f), new Vec3(-0.38f, 0f, 0.44f));
        if (banner >= 0)
        {
            mesh.Part("banner@3", -1, new Vec3(0f, depth * 0.60f, 0.20f));
            mesh.Box(banner, new Vec3(0f, depth * 0.60f, 0.20f), 0.035f, 0.006f, 0.025f);
        }
        return SetInfo(mesh.Build(), "kind=hall;width=1");
    }

    /// <summary>制作文明共用的木船、甲板、舱室和有厚度的分节船帆。</summary>
    /// <param name="name">船只模型名称。</param><param name="trading">是否为较小的商船。</param><param name="detail">细节等级。</param>
    /// <returns>可由 Worldfall 驾驶、浮动和选择的船体。</returns>
    internal static MeshModel BuildBoat(string name, bool trading, int detail)
    {
        MeshBuilder mesh = new(name);
        int wood = mesh.Slot("wood", 3, TerrainCache.Rgb(105, 66, 40));
        int deck = mesh.Slot("deck", 3, TerrainCache.Rgb(176, 131, 76));
        int sail = mesh.Slot("sail", 7, TerrainCache.Rgb(204, 183, 139));
        int banner = mesh.Slot("banner", 7, TerrainCache.Rgb(139, 52, 48));
        float length = trading ? 2.1f : 2.8f;
        int hull = mesh.Part("hull", -1, new Vec3(0f, 0f, 0f));
        mesh.Loft(wood, [new Vec3(0f, -length * 0.5f, 0.25f), new Vec3(0f, -length * 0.32f, 0.05f), new Vec3(0f, length * 0.32f, 0.08f), new Vec3(0f, length * 0.5f, 0.22f)],
            [0.045f, 0.43f, 0.43f, 0.20f], [0.16f, 0.22f, 0.22f, 0.17f], new Vec3(1f, 0f, 0f), new Vec3(0f, 0f, 1f), detail == 2 ? 4 : 8, false);
        mesh.Part("deck", hull, new Vec3(0f, 0f, 0.27f));
        mesh.Box(deck, new Vec3(0f, 0f, 0.27f), 0.37f, length * 0.37f, 0.03f);
        mesh.Part("cabin", hull, new Vec3(0f, length * 0.22f, 0.28f));
        mesh.Box(wood, new Vec3(0f, length * 0.22f, 0.46f), 0.27f, 0.30f, 0.17f);
        for (int i = 0; i < (trading ? 1 : 2); i++)
        {
            float y = -length * 0.25f + i * length * 0.32f;
            mesh.Part("mast_" + i, hull, new Vec3(0f, y, 0.28f));
            mesh.Tube(wood, new Vec3(0f, y, 0.28f), new Vec3(0f, y, 1.73f), 0.025f, 0.02f, 4, false);
            mesh.Part("sail_" + i, hull, new Vec3(0f, y, 1.2f));
            mesh.Box(sail, new Vec3(0f, y + 0.025f, 1.15f), 0.56f, 0.023f, 0.46f);
            if (detail == 0) for (int batten = -2; batten <= 2; batten++) mesh.Box(wood, new Vec3(0f, y - 0.006f, 1.15f + batten * 0.18f), 0.57f, 0.012f, 0.012f);
        }
        mesh.Part("flag", hull, new Vec3(0f, length * 0.35f, 1.0f));
        mesh.Box(banner, new Vec3(0.14f, length * 0.35f, 1.0f), 0.14f, 0.012f, 0.07f);
        return SetInfo(mesh.Build(), "kind=boat;length=" + length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>根据原群系物件的辨识特征制作树、花草、墓碑、珊瑚或晶体。</summary>
    /// <param name="mesh">网格生成器。</param><param name="id">资产名称。</param><param name="root">根部。</param><param name="wood">枝干。</param>
    /// <param name="stone">石材。</param><param name="leaf">叶片。</param><param name="crystal">晶体。</param><param name="gold">穗或火焰。</param><param name="tree">是否为大型物件。</param><param name="detail">细节等级。</param>
    private static void BuildFlora(MeshBuilder mesh, string id, int root, int wood, int stone, int leaf, int crystal, int gold, bool tree, int detail)
    {
        float height = tree ? 1.5f : 0.45f;
        if (id.Contains("Cemetery") || id.Contains("Titans"))
        {
            mesh.Part("monument", root, new Vec3(0f, 0f, 0f));
            mesh.Box(stone, new Vec3(0f, 0f, height * 0.4f), 0.16f, 0.12f, height * 0.4f);
            mesh.Blob(stone, new Vec3(0f, 0f, height * 0.8f), 0.17f, 0.13f, 0.16f, 6);
            if (detail == 0) mesh.Box(wood, new Vec3(0f, -0.132f, height * 0.5f), 0.08f, 0.01f, 0.08f);
        }
        else if (id.Contains("Coral") || id.Contains("Candle") || id.Contains("Knowledge") || id.Contains("Tzeentch"))
        {
            for (int i = -1; i <= 1; i++)
            {
                mesh.Part("branch_" + i, root, new Vec3(0f, 0f, 0f));
                mesh.Capsule(id.Contains("Candle") ? gold : crystal, new Vec3(i * 0.12f, 0f, 0.04f), new Vec3(i * 0.28f, 0f, height * (1f - Math.Abs(i) * 0.25f)), 0.07f, 0.04f, detail == 2 ? 4 : 6);
            }
        }
        else if (id.Contains("Bamboo") || id.Contains("Rice"))
        {
            for (int i = -1; i <= 1; i++)
            {
                mesh.Part("stem_" + i, root, new Vec3(0f, 0f, 0f));
                mesh.Capsule(wood, new Vec3(i * 0.14f, 0f, 0.045f), new Vec3(i * 0.18f, 0.04f, height), 0.035f, 0.025f, 4);
                mesh.Blob(id.Contains("Rice") ? gold : leaf, new Vec3(i * 0.18f, 0.04f, height * 0.8f), 0.11f, 0.1f, 0.20f, detail == 2 ? 4 : 6);
            }
        }
        else if (id.Contains("FleshBlood") || id.Contains("Nurgle"))
        {
            mesh.Part("growth", root, new Vec3(0f, 0f, 0f));
            mesh.Blob(id.Contains("FleshBlood") ? gold : leaf, new Vec3(0f, 0f, height * 0.4f), 0.3f, 0.22f, height * 0.4f, detail == 2 ? 4 : 8);
            mesh.Blob(stone, new Vec3(0.07f, -0.2f, height * 0.6f), 0.10f, 0.07f, 0.13f, 4);
        }
        else
        {
            mesh.Part("trunk", root, new Vec3(0f, 0f, 0f));
            mesh.Capsule(wood, new Vec3(0f, 0f, 0.09f), new Vec3(0f, 0f, height * 0.78f), 0.075f, 0.045f, detail == 2 ? 4 : 6);
            mesh.Part("crown", root, new Vec3(0f, 0f, height * 0.65f));
            mesh.Blob(leaf, new Vec3(0f, 0f, height * 0.78f), 0.34f, 0.28f, height * 0.22f, detail == 2 ? 4 : 8);
            if (detail == 0)
                for (int i = -1; i <= 1; i++) mesh.Blob(leaf, new Vec3(i * 0.2f, 0.02f, height * 0.65f), 0.2f, 0.2f, height * 0.18f, 6);
        }
    }

    /// <summary>为宗门、两种文明和各主题建筑设置各自的屋顶底色。</summary>
    /// <param name="id">资产名称。</param><returns>原生城邦配色未覆盖时使用的颜色。</returns>
    private static uint RoofColor(string id)
    {
        if (id.Contains("Nurgle")) return TerrainCache.Rgb(95, 116, 55);
        if (id.Contains("Slaanesh")) return TerrainCache.Rgb(143, 63, 137);
        if (id.Contains("Tzeentch") || id.Contains("Magic") || id.Contains("FishPeople")) return TerrainCache.Rgb(65, 117, 159);
        if (id.Contains("Gui")) return TerrainCache.Rgb(72, 86, 107);
        if (id.Contains("Treasure") || id.Contains("Golden")) return TerrainCache.Rgb(180, 136, 57);
        return TerrainCache.Rgb(146, 63, 48);
    }

    /// <summary>保留部件序号，移除只在近景需要的小装饰。</summary>
    /// <param name="part">要精简的部件。</param>
    private static void Empty(MeshPart part)
    {
        part.Pos = []; part.Nrm = []; part.Tri = []; part.Slot = []; part.Axis = [];
    }
}
#endif
