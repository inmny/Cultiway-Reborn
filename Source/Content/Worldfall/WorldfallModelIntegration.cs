#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Const;
using FirstPerson.Core;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>把修仙模组的自有网格接到核对过的 Worldfall 模型入口。</summary>
[Dependency(typeof(Actors), typeof(Buildings))]
public sealed partial class WorldfallModelIntegration : ICanInit
{
    /// <summary>当前接入所对应的渲染器版本。</summary>
    private static readonly Version SupportedVersion = new(1, 0, 0, 0);
    /// <summary>版本和入口核对通过后才创建的独立补丁组。</summary>
    private static Harmony ModelHarmony;
    /// <summary>按模型名称缓存的网格，不按游戏对象重复生成。</summary>
    private static readonly Dictionary<string, MeshModel> CustomModels = new(StringComparer.Ordinal);
    /// <summary>单位资产和身体模型的对应关系。</summary>
    private static readonly Dictionary<string, string> ActorFamilies = new(StringComparer.Ordinal);
    /// <summary>建筑资产和模型的对应关系。</summary>
    private static readonly Dictionary<string, string> BuildingFamilies = new(StringComparer.Ordinal);
    /// <summary>船只资产和模型的对应关系。</summary>
    private static readonly Dictionary<string, string> BoatFamilies = new(StringComparer.Ordinal);
    /// <summary>三维上帝视角是否正在显示。</summary>
    private static FieldInfo GodViewActive;
    /// <summary>机车使用自有车头帧提供取色和尺寸，不调用缺失的普通人物动画。</summary>
    private static Sprite TrainReference;
    /// <summary>原生建筑记录中的模型缩放缓存。</summary>
    private static FieldInfo BuildingModelScale;
    /// <summary>建筑破损后沿用的最后一次国家色。</summary>
    private static FieldInfo BuildingKingdomColor;
    /// <summary>人物缓存中的原生衣色记录。</summary>
    private static FieldInfo PersonPalette;
    /// <summary>只跟踪仍在渲染器里的人物记录，换国家时刷新衣色。</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Tuple<ColorAsset, string>> PersonKingdomColors = new();
    /// <summary>沿用渲染器的图像缓存读取人物原图。</summary>
    private static FieldInfo OriginalPersonSprites;
    /// <summary>缓存读取入口，仅在衣色或皮肤改变时调用。</summary>
    private static MethodInfo ReadOriginalPersonSprite;

    /// <summary>先确认版本和接口，再准备网格并安装外观补丁。</summary>
    public void Init()
    {
        Assembly assembly = typeof(FirstPerson.WorldBoxMod).Assembly;
        if (!SupportsVersion(assembly.GetName().Version))
        {
            ModClass.LogWarning("Worldfall 模型接入需要程序集 1.0.0.0；当前版本未启用修仙专属网格。");
            return;
        }
        Type models = assembly.GetType("FirstPerson.Models");
        Type collector = assembly.GetType("FirstPerson.SceneCollector");
        Type people = assembly.GetType("FirstPerson.People");
        MethodInfo addCreature = AccessTools.Method(assembly.GetType("FirstPerson.Creatures"), "Add");
        MethodInfo scale = AccessTools.Method(collector, "KeptScale");
        MethodInfo position = AccessTools.Method(collector, "Position", [typeof(Building)]);
        MethodInfo get = Find(models, "Get", typeof(string));
        MethodInfo building = Find(models, "BuildingFamily", typeof(BuildingAsset));
        MethodInfo person = Find(models, "PersonFamily", typeof(Actor));
        MethodInfo creature = Find(models, "CreatureFamily", typeof(Actor));
        MethodInfo boat = Find(models, "BoatFamily", typeof(Actor));
        MethodInfo plant = Find(models, "PlantFamily", typeof(BuildingAsset));
        MethodInfo tree = AccessTools.Method(models, "TreeFamily", [typeof(string), typeof(int)]);
        if (get == null || building == null || person == null || creature == null || boat == null || plant == null || tree == null || scale == null || position == null || addCreature == null)
        {
            ModClass.LogWarning("Worldfall 模型入口与 1.0.0 约定不符；未启用修仙专属网格。");
            return;
        }
        ModelHarmony = new Harmony("inmny.cultiway.worldfall.models");
        GodViewActive = assembly.GetType("FirstPerson.GodView").GetField("Active", BindingFlags.Public | BindingFlags.Static);
        BuildingModelScale = AccessTools.Field(scale.GetParameters()[0].ParameterType, "ModelScale");
        BuildingKingdomColor = AccessTools.Field(scale.GetParameters()[0].ParameterType, "KingdomColor");
        MethodInfo figure = AccessTools.Method(people, "Figure");
        PersonPalette = AccessTools.Field(figure.ReturnType, "Palette");
        OriginalPersonSprites = AccessTools.Field(typeof(FirstPerson.WorldBoxMod), "_sprites");
        ReadOriginalPersonSprite = AccessTools.Method(OriginalPersonSprites.FieldType, "Get", [typeof(Sprite)]);
        opaqueCreatureWidth = (Func<SpriteImage, float>)AccessTools.Method(models, "OpaqueWidth")
            .CreateDelegate(typeof(Func<SpriteImage, float>));
        var loading = WorldfallModelFiles.ReadAsync(Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Worldfall", "Models"));
        RegisterActors();
        RegisterBuildings();
        WorldfallModelFiles.Install(loading, CustomModels, InstallEntries);

        // 原生入口必须在三档、状态和变体全部接入后才开放，避免缓存不完整的外观。
        void InstallEntries()
        {
            InstallCivilBuildingVariants(assembly, collector);
            InstallTreeVariants(assembly, collector);
            InstallPlantVariants(collector);
            WorldfallBiomeSizes.Install(collector, ModelHarmony, TreeVariants.Keys, PlantVariants.Count > 0);
            ModelHarmony.Patch(AccessTools.Method(assembly.GetType("FirstPerson.WorldSampler"), "MaterialOf"),
                postfix: Postfix(nameof(UseRiceGroundMaterial)));
            WorldfallLeafCards.Install(collector, ModelHarmony, ReadLeafCardSprite);
            WorldfallGroundPits.Install(collector, ModelHarmony, CustomModels);
            PrepareBuildingPalettes();
            ModelHarmony.Patch(get, postfix: Postfix(nameof(ProvideCustomModel)));
            ModelHarmony.Patch(building, postfix: Postfix(nameof(SelectBuildingFamily)));
            ModelHarmony.Patch(person, postfix: Postfix(nameof(SelectPersonFamily)));
            ModelHarmony.Patch(creature, postfix: Postfix(nameof(SelectCreatureFamily)));
            ModelHarmony.Patch(boat, postfix: Postfix(nameof(SelectBoatFamily)));
            ModelHarmony.Patch(plant, postfix: Postfix(nameof(SelectPlantFamily)));
            ModelHarmony.Patch(tree, postfix: Postfix(nameof(SelectTreeFamily)));
            ModelHarmony.Patch(AccessTools.Method(typeof(BuildingPalette), nameof(BuildingPalette.Whole)), postfix: Postfix(nameof(UseBuildingPalette)));
            ModelHarmony.Patch(AccessTools.Method(typeof(BuildingPalette), nameof(BuildingPalette.Ruin)), postfix: Postfix(nameof(UseRuinPalette)));
            ModelHarmony.Patch(scale, postfix: Postfix(nameof(UseBuildingScale)));
            ModelHarmony.Patch(position, postfix: Postfix(nameof(CenterBuilding)));
            ModelHarmony.Patch(AccessTools.Method(collector, "RoofColour"), postfix: Postfix(nameof(UseBuildingKingdom)));
            ModelHarmony.Patch(figure, postfix: Postfix(nameof(RefreshPersonKingdomColor)));
            ModelHarmony.Patch(AccessTools.Method(typeof(Humanoid), nameof(Humanoid.Extract),
                    [typeof(SpriteImage), typeof(string), typeof(uint)]),
                postfix: Postfix(nameof(UseGuiPalette)));
            ModelHarmony.Patch(AccessTools.Method(typeof(CreatureColors), nameof(CreatureColors.FromSideSprite)),
                postfix: Postfix(nameof(UseCreatureColors)));
            ModelHarmony.Patch(AccessTools.Method(typeof(Actor), nameof(Actor.getSpriteToRender)),
                prefix: Postfix(nameof(ProvideTrainReference)));
            ModelHarmony.Patch(AccessTools.Method(typeof(CreatureRig), nameof(CreatureRig.Pose)),
                postfix: Postfix(nameof(PoseCreature)));
            ModelHarmony.Patch(addCreature, prefix: Postfix(nameof(PrepareCreatureFrame)),
                postfix: Postfix(nameof(FinishCreatureFrame)));
            ModelHarmony.Patch(AccessTools.Method(addCreature.DeclaringType, "AddFigure"),
                prefix: Postfix(nameof(EnterCreatureFigure)), finalizer: Postfix(nameof(LeaveCreatureFigure)));
            ModelHarmony.Patch(AccessTools.Method(addCreature.DeclaringType, "DrawnAs"),
                postfix: Postfix(nameof(ReportCreatureLift)));
            ModelHarmony.Patch(AccessTools.Method(addCreature.DeclaringType, "Airborne"),
                postfix: Postfix(nameof(ReportCreatureAirborne)));
            ModelHarmony.Patch(AccessTools.Method(typeof(BeastEye), nameof(BeastEye.Of)),
                postfix: Postfix(nameof(CalibrateCreatureEye)));
            ModelHarmony.Patch(AccessTools.Method(typeof(BeastEye), nameof(BeastEye.Muzzle)),
                postfix: Postfix(nameof(ClearPossessedMuzzle)));
            ModelHarmony.Patch(AccessTools.Method(assembly.GetType("FirstPerson.BeastView"), "Update"),
                postfix: Postfix(nameof(CorrectAirborneEye)));
            ModelHarmony.Patch(AccessTools.Method(typeof(PersonRig), nameof(PersonRig.WeaponPlace)),
                postfix: Postfix(nameof(FacePersonHeldItemForward)));
            ModClass.LogInfo($"[Worldfall] 已接入 {ActorFamilies.Count} 类单位、{BoatFamilies.Count} 类船只、{BuildingFamilies.Count} 类建筑与植被。");
        }
    }

    /// <summary>只有核对过接口的程序集版本才启用接入。</summary>
    /// <param name="version">渲染器实际报告的程序集版本。</param>
    /// <returns>是否为已支持的版本。</returns>
    internal static bool SupportsVersion(Version version) => version == SupportedVersion;

    /// <summary>查找已授权使用的模型选择入口。</summary>
    /// <param name="type">Worldfall 的模型库类型。</param>
    /// <param name="name">方法名称。</param>
    /// <param name="argument">唯一参数类型。</param>
    /// <returns>匹配的入口；接口不匹配时为空。</returns>
    private static MethodInfo Find(Type type, string name, Type argument) =>
        type?.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, [argument], null);

    /// <summary>取得本类中的后置补丁。</summary>
    /// <param name="name">补丁方法名称。</param>
    /// <returns>补丁描述。</returns>
    private static HarmonyMethod Postfix(string name) => new(AccessTools.Method(typeof(WorldfallModelIntegration), name));

    /// <summary>按实际注册的单位准备人形、异兽与船只模型。</summary>
    private static void RegisterActors()
    {
        foreach (PropertyInfo property in typeof(Actors).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType != typeof(ActorAsset) || property.GetValue(null) is not ActorAsset asset) continue;
            if (asset.is_boat)
            {
                string family = "boat_cultiway_" + asset.id;
                BoatFamilies.Add(asset.id, family);
                AddLevels(family, detail => WorldfallMeshes.BuildBoat(family, asset.boat_type == "trading", detail));
                continue;
            }
            string name = asset == Actors.EasternHuman ? "person_cultiway_eastern_human" :
                asset == Actors.Gui ? "person_cultiway_gui" : "creature_cultiway_" + asset.id;
            ActorFamilies.Add(asset.id, name);
            if (asset.is_humanoid)
            {
                AddPerson(name, false, false);
                AddPerson(name + "_fem", true, false);
                AddPerson(name + "_warrior", false, true);
                AddPerson(name + "_warrior_fem", true, true);
            }
            else
            {
                WorldfallBody shape = WorldfallModelCatalog.BodyOf(asset.id);
                AddLevels(name, detail => WorldfallMeshes.BuildCreature(name, asset.id, shape, detail));
            }
        }
    }

    /// <summary>准备一个共享原生人物动作的服装变体。</summary>
    /// <param name="name">模型名称。</param>
    /// <param name="female">是否使用较窄的女性体形。</param>
    /// <param name="armoured">是否显示战士护甲。</param>
    private static void AddPerson(string name, bool female, bool armoured)
    {
        MeshModel model = WorldfallMeshes.BuildEasternHuman(name, female, armoured);
        CustomModels.Add(name, model);
        CustomModels.Add(name + "_m", WorldfallMeshes.Reduced(model, name + "_m", false));
        CustomModels.Add(name + "_f", WorldfallMeshes.Reduced(model, name + "_f", true));
    }

    /// <summary>为全部当前启用的修仙建筑和两种文明建筑准备模型。</summary>
    private static void RegisterBuildings()
    {
        HashSet<BuildingAsset> assets = new();
        foreach (PropertyInfo property in typeof(Buildings).GetProperties(BindingFlags.Public | BindingFlags.Static))
            if (property.PropertyType == typeof(BuildingAsset) && property.GetValue(null) is BuildingAsset asset) assets.Add(asset);
        foreach (BuildingAsset asset in AssetManager.buildings.list)
            if (asset.id.Contains("Cultiway.")) assets.Add(asset);
        foreach (BuildingAsset asset in assets)
        {
            string family = asset == Buildings.SectHall ? WorldfallMeshes.SectHallFamily : "building_cultiway_" + asset.id;
            BuildingFamilies.Add(asset.id, family);
            WorldfallBuilding shape = WorldfallModelCatalog.BuildingOf(asset);
            AddLevels(family, detail => WorldfallMeshes.BuildBuilding(family, asset, shape, detail));
            if (asset.has_ruin_state)
                CustomModels.Add("ruin_" + family, WorldfallMeshes.BuildRuin("ruin_" + family, asset));
            if (asset.has_sprite_construction)
                CustomModels.Add("site_" + family, WorldfallMeshes.BuildSite("site_" + family, asset));
            foreach (string name in new[] { family, family + "_m", family + "_f", "site_" + family, "ruin_" + family })
                if (CustomModels.TryGetValue(name, out MeshModel model))
                    WorldfallMeshes.SetInfo(model, "cultiway_building=1");
        }
    }

    /// <summary>按已确认的材质分区设置国家色槽，并让施工和废墟沿用主体颜色。</summary>
    internal static void PrepareBuildingPalettes()
    {
        foreach (KeyValuePair<string, string> pair in BuildingFamilies)
        {
            MeshModel whole = CustomModels[pair.Value];
            if (whole.Info("cultiway_tree") == "1") continue;
            string slots = whole.Info("kingdom_slots") ?? KingdomSlotsOf(pair.Key);
            foreach (string name in new[] { pair.Value, pair.Value + "_m", pair.Value + "_f", "site_" + pair.Value, "ruin_" + pair.Value })
            {
                if (!CustomModels.TryGetValue(name, out MeshModel model)) continue;
                if (model.Info("kingdom_slots") == null)
                    WorldfallMeshes.SetInfo(model, "kingdom_slots=" + slots);
                if (!name.StartsWith("site_", StringComparison.Ordinal) && !name.StartsWith("ruin_", StringComparison.Ordinal)) continue;
                // 文明建筑文件已经分别绘制施工和熄灯配色，不用完整建筑覆盖。
                if (model.Info("cultiway_frame") != null) continue;
                for (int i = 0; i < model.SlotNames.Length; i++)
                {
                    int source = whole.SlotIndex(model.SlotNames[i]);
                    if (source >= 0) model.SlotColor[i] = whole.SlotColor[source];
                }
            }
        }
    }

    /// <summary>大殿等文明木构建筑用屋顶表达国家色，其余建筑按已确认的区域取色。</summary>
    /// <param name="id">已注册的建筑名称。</param><returns>国家色区域对应的材质槽。</returns>
    private static string KingdomSlotsOf(string id) => id switch
    {
        "hall_Cultiway.EasternHuman_3" => "banner",
        "Cultiway.SectHall" or "Cultiway.SectTreasurePavilion" or
        "Cultiway.SectScripturePavilion" or "Cultiway.TrainStation" or
        "watch_tower_Cultiway.EasternHuman" or "docks_Cultiway.EasternHuman" or
        "barracks_Cultiway.EasternHuman" or "temple_Cultiway.EasternHuman" or
        "library_Cultiway.EasternHuman" or "market_Cultiway.EasternHuman" or
        "house_Cultiway.EasternHuman_2" or "house_Cultiway.EasternHuman_3" or
        "house_Cultiway.EasternHuman_4" or "house_Cultiway.EasternHuman_5" or
        "hall_Cultiway.EasternHuman_1" or "hall_Cultiway.EasternHuman_2" => "roof,roof_dark",
        _ => "none"
    };

    /// <summary>生成近、中、远三档网格，三档使用相同的尺度信息。</summary>
    /// <param name="family">模型族名称。</param>
    /// <param name="build">按细节等级生成网格的方法。</param>
    private static void AddLevels(string family, Func<int, MeshModel> build)
    {
        MeshModel model = build(0);
        CustomModels.Add(family, model);
        for (int detail = 1; detail <= 2; detail++)
        {
            string name = family + (detail == 1 ? "_m" : "_f");
            MeshModel level = build(detail);
            level.Name = name;
            if (model.Info("height") != null)
                WorldfallMeshes.SetInfo(level, $"height={model.Info("height")};length={model.Info("length")}");
            CustomModels.Add(name, level);
        }
    }

    /// <summary>为 Worldfall 的模型查找提供自有网格。</summary>
    /// <param name="name">请求的模型名称。</param>
    /// <param name="__result">原结果或本模组网格。</param>
    private static void ProvideCustomModel(string name, ref MeshModel __result)
    {
        if (CustomModels.TryGetValue(name, out MeshModel model)) __result = model;
    }

    /// <summary>按建筑资产选择模型；树木和小型植被使用各自的原生入口。</summary>
    /// <param name="asset">正在绘制的建筑资产。</param><param name="__result">对应的模型族。</param>
    private static void SelectBuildingFamily(BuildingAsset asset, ref string __result)
    {
        if (asset != null && asset.type is not ("type_tree" or "type_vegetation") &&
            BuildingFamilies.TryGetValue(asset.id, out string family)) __result = family;
    }

    /// <summary>只替换明确标出的国家色槽，木墙、木柱、金饰和灰石保持各自颜色。</summary>
    /// <param name="model">当前建筑或施工模型。</param><param name="kingdom">实际所属国家的主色。</param><param name="__result">与 Blender 材质槽顺序一致的颜色。</param>
    private static void UseBuildingPalette(MeshModel model, uint kingdom, ref uint[] __result)
    {
        if (model.Info("cultiway_building") != "1") return;
        __result = (uint[])model.SlotColor.Clone();
        foreach (string name in model.Info("kingdom_slots").Split(','))
        {
            int slot = model.SlotIndex(name);
            if (slot < 0) continue;
            float shade = name switch { "roof_dark" => 0.51f, "banner" => 0f, _ => 0.13f };
            __result[slot] = TerrainCache.Lerp(kingdom, TerrainCache.Rgb(30, 30, 30), shade);
        }
    }

    /// <summary>宗门建筑优先用所在城市或宗门所属城市的国家色，避免固定野生王国或随机屋顶色。</summary>
    /// <param name="nb">原生建筑记录。</param><param name="b">正在绘制的自有建筑。</param><param name="__result">实际归属提供的主色。</param>
    private static void UseBuildingKingdom(object nb, Building b, ref uint __result)
    {
        if (!BuildingFamilies.ContainsKey(b.asset.id)) return;
        Kingdom kingdom = b.kingdom;
        if (kingdom?.isCiv() != true && (b.isRuin() || b.isAbandoned()) && (uint)BuildingKingdomColor.GetValue(nb) != 0) return;
        if (b.asset.kingdom == KingdomAssets.SectBuildings.id && kingdom?.isCiv() != true)
        {
            City city = b.city;
            if (city == null)
            {
                b.data.get(BuildingDataKeys.SectID_Long, out long sectId, -1);
                city = WorldboxGame.I.Sects.get(sectId)?.GetHomeCity();
            }
            kingdom = city?.kingdom ?? kingdom;
        }
        if (kingdom == null) return;
        ColorAsset colors = kingdom.getColor();
        colors.initColor();
        Color32 color = colors.k_color_0;
        __result = TerrainCache.Rgb(color.r, color.g, color.b);
        BuildingKingdomColor.SetValue(nb, __result);
    }

    /// <summary>人物初次沿用原生取色，换国家后只刷新衣色，保留肤色和人物动作。</summary>
    /// <param name="a">正在绘制的人物。</param><param name="img">原版已替换紫色模板的当前贴图。</param><param name="__result">渲染器已有的人物记录。</param>
    private static void RefreshPersonKingdomColor(Actor a, SpriteImage img, object __result)
    {
        if (!a.asset.is_humanoid || !ActorFamilies.ContainsKey(a.asset.id)) return;
        ColorAsset color = a.kingdom.getColor();
        string skin = a.getUnitTexturePath();
        if (PersonKingdomColors.TryGetValue(__result, out Tuple<ColorAsset, string> previous) &&
            previous.Item1 == color && previous.Item2 == skin) return;
        Humanoid.Palette palette = (Humanoid.Palette)PersonPalette.GetValue(__result);
        palette.Shirt = Humanoid.Extract(img, a.asset.id, palette.Skin).Shirt;
        if (a.asset == Actors.EasternHuman)
        {
            // 甲胄比紫色衣料面积大时，原生衣色采样会选到金属色；改从未染色原图确定国家色档。
            SpriteImage original = (SpriteImage)ReadOriginalPersonSprite.Invoke(
                OriginalPersonSprites.GetValue(FirstPerson.WorldBoxMod.Instance), [a.calculateMainSprite()]);
            uint template = original.Pixels
                .Where(pixel => pixel is 0xFFFF00FFu or 0xFFDE00DEu or 0xFFA700A7u or 0xFF7F007Fu or 0xFF580058u)
                .GroupBy(pixel => pixel)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault();
            if (template != 0)
            {
                color.initColor();
                Color32 shirt = template switch
                {
                    0xFFFF00FFu => color.k_color_0,
                    0xFFDE00DEu => color.k_color_1,
                    0xFFA700A7u => color.k_color_2,
                    0xFF7F007Fu => color.k_color_3,
                    _ => color.k_color_4
                };
                palette.Shirt = TerrainCache.Rgb(shirt.r, shirt.g, shirt.b);
            }
        }
        PersonPalette.SetValue(__result, palette);
        PersonKingdomColors.Remove(__result);
        PersonKingdomColors.Add(__result, Tuple.Create(color, skin));
    }

    /// <summary>废墟沿用自有材质，并统一压暗，避免被通用人类墙色覆盖。</summary>
    /// <param name="model">废墟或变暗主体模型。</param><param name="kingdom">当前王国的颜色。</param><param name="__result">压暗后的完整材质色表。</param>
    private static void UseRuinPalette(MeshModel model, uint kingdom, ref uint[] __result)
    {
        if (model.Info("cultiway_building") != "1") return;
        UseBuildingPalette(model, kingdom, ref __result);
        for (int i = 0; i < __result.Length; i++) __result[i] = TerrainCache.Scale(__result[i], 0.7f);
    }
    /// <summary>台基宽度为一，直接按真实格子数缩放，避免皮肤和动画原图尺寸改变建筑大小。</summary>
    /// <param name="nb">原生建筑显示记录。</param><param name="b">当前建筑。</param><param name="__result">完好、施工和废墟共用的缩放比例。</param>
    private static void UseBuildingScale(object nb, Building b, ref float __result)
    {
        if (b.asset.type is "type_tree" or "type_vegetation" || !BuildingFamilies.ContainsKey(b.asset.id)) return;
        __result = b.asset.fundament.width;
        BuildingModelScale.SetValue(nb, __result);
    }

    /// <summary>让偶数占地的几何中心落在占地中央，镜像废墟也保持同一位置。</summary>
    /// <param name="b">当前建筑。</param><param name="__result">原生地块位置加上半格中心偏移。</param>
    private static void CenterBuilding(Building b, ref Vector2 __result)
    {
        if (b.asset.type is "type_tree" or "type_vegetation" || !BuildingFamilies.ContainsKey(b.asset.id)) return;
        __result.x += (b.asset.fundament.right - b.asset.fundament.left) * 0.5f;
    }
    /// <summary>群系小型物件沿用原生落地和随机朝向，显示比例另按人物身高调整。</summary>
    /// <param name="asset">当前植被资产。</param><param name="__result">自制植被模型族。</param>
    private static void SelectPlantFamily(BuildingAsset asset, ref string __result)
    {
        if (BuildingFamilies.TryGetValue(asset.id, out string family)) __result = family;
    }

    /// <summary>树木保留原生外观和风动，尺寸与树冠遮挡共用群系比例。</summary>
    /// <param name="id">树木资产名称。</param><param name="__result">自制树木模型族。</param>
    private static void SelectTreeFamily(string id, ref string __result)
    {
        if (BuildingFamilies.TryGetValue(id, out string family)) __result = family;
    }
    /// <summary>保持人物种族映射，并选择男女和战士服装。</summary>
    /// <param name="a">当前人物。</param>
    /// <param name="__result">模型族。</param>
    private static void SelectPersonFamily(Actor a, ref string __result)
    {
        if (a?.asset == null || !ActorFamilies.TryGetValue(a.asset.id, out string name) || !a.asset.is_humanoid) return;
        if (a.isProfession(UnitProfession.Warrior)) name += "_warrior";
        if (a.isSexFemale()) name += "_fem";
        __result = name;
    }

    /// <summary>让非人形修仙单位使用各自的身体骨架。</summary>
    /// <param name="a">当前单位。</param>
    /// <param name="__result">模型族。</param>
    private static void SelectCreatureFamily(Actor a, ref string __result)
    {
        if (a?.asset != null && !a.asset.is_humanoid && ActorFamilies.TryGetValue(a.asset.id, out string name))
            __result = CamouflagedCreatureFamily(a, MovingCreatureFamily(a, name));
    }

    /// <summary>选择两种文明的商船或运输船。</summary>
    /// <param name="a">当前船只。</param>
    /// <param name="__result">模型族。</param>
    private static void SelectBoatFamily(Actor a, ref string __result)
    {
        if (a?.asset != null && BoatFamilies.TryGetValue(a.asset.id, out string name)) __result = name;
    }

    /// <summary>独立绘制的生物保留各部位原色，其余自制身体按整张原贴图取色。</summary>
    /// <param name="model">正在配色的模型。</param><param name="img">单位当前原贴图。</param><param name="__result">与自有材质槽对应的配色。</param>
    private static void UseCreatureColors(MeshModel model, SpriteImage img, ref uint[] __result)
    {
        if (model.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal))
            // 零值表示沿用模型自身颜色，避免泳姿的水色或同色器官被重新分配到其他部位。
            __result = model.Info("cultiway_native_unit") == "1"
                ? new uint[model.SlotColor.Length]
                : CreatureColors.FromSprite(model, img);
    }
    /// <summary>鬼人沿用原贴图的青蓝脸色，避免人物基因肤色覆盖灵体外观。</summary>
    /// <param name="img">当前皮肤的完整原贴图。</param><param name="species">人物种族。</param><param name="__result">保留衣色、头发和体型信息的配色。</param>
    private static void UseGuiPalette(SpriteImage img, string species, ref Humanoid.Palette __result)
    {
        if (species != Actors.Gui.id) return;
        uint skin = img.Pixels
            .Where(color => (color >> 24) >= 128 &&
                            ((color >> 8) & 255) >= 75 && ((color >> 16) & 255) >= 75 &&
                            (color & 255) * 3 < ((color >> 8) & 255) * 2 &&
                            (color & 255) * 3 < ((color >> 16) & 255) * 2)
            .GroupBy(color => color | 0xFF000000u)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
        if (skin == 0) return;
        __result.Skin = skin;
        __result.SkinSeen = true;
    }
    /// <summary>三维渲染机车时使用其自有车头帧，二维预制体仍沿用原处理。</summary>
    /// <param name="__instance">准备取图的单位。</param><param name="__result">三维渲染器使用的尺寸和配色参考。</param>
    /// <returns>是否继续调用普通人物取图方法。</returns>
    private static bool ProvideTrainReference(Actor __instance, ref Sprite __result)
    {
        if (__instance.asset != Actors.Train ||
            (!(bool)GodViewActive.GetValue(null) && FirstPerson.WorldBoxMod.Instance?.IsFirstPerson != true)) return true;
        if (TrainReference == null) TrainReference = SpriteTextureLoader.getSpriteList("actors/train/head/right")[0];
        __result = TrainReference;
        return false;
    }
    /// <summary>在原生姿势之后调整恐龙尾速，再补上异兽翅膀和软体触手动作。</summary>
    /// <param name="m">原生渲染器使用的同一个模型实例。</param>
    /// <param name="stride">当前步幅。</param>
    /// <param name="phase">当前走动周期。</param>
    /// <param name="time">渲染器时间。</param>
    /// <param name="flying">原生渲染器判断的飞行状态。</param>
    /// <param name="seed">不同个体的动作相位。</param>
    private static void PoseCreature(MeshInstance m, float stride, float phase, float time, bool flying, float seed)
    {
        if (!m.Model.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal)) return;
        if (PoseCamouflagedCreature(m)) return;
        if (m.Model.Info("cultiway_move_form") == "wolf") PoseRunningWolf(m, phase);
        string asset = m.Model.Info("cultiway_asset");
        if (asset is "Cultiway.Diplodocus" or "Cultiway.Dreadnoughtus" or "Cultiway.Triceratops" or
            "Cultiway.TyrannosaurusRex" or "Cultiway.Velociraptor" or "Cultiway.Pterodactyl")
        {
            // 长尾保持待机频率，只取消原生动作中随步幅增加的尾速，不牵动腿部步频。
            int tail = m.Model.PartIndex("tail");
            if (tail >= 0)
                m.PartRotation[tail] = Mat3.RotZ((0.18f + stride * 0.25f) *
                    Mathf.Sin(time * 2.2f + seed));
        }
        PoseFlyingCreature(m, stride, time, flying, seed);
        PoseFixedCreatureItems(m);
        AlignDaemonLegs(m);
        if (m.Model.Info("tentacles") == "1")
            for (int i = 1; i <= 8; i++)
            {
                int part = m.Model.PartIndex("leg_" + i);
                if (part >= 0) m.PartRotation[part] = Mat3.RotZ(0.2f * Mathf.Sin(phase + time * 1.7f + i));
            }
    }
}
#endif
