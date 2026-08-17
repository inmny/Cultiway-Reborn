using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.SubWorlds.Natural.Generation;
using Cultiway.Content.SubWorlds.Ruins.Generation;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Content.SubWorlds;

/// <summary>注册内容层的小世界模板。</summary>
[Dependency(typeof(SubWorldGenerators), typeof(SubWorldVisualProfiles))]
public sealed class SubWorldTemplates : ExtendLibrary<SubWorldTemplateAsset, SubWorldTemplates>
{
    /// <summary>原版大陆风格自然模板。</summary>
    public static SubWorldTemplateAsset NaturalWorld { get; private set; }

    /// <summary>方形世界模板。</summary>
    public static SubWorldTemplateAsset BoxWorld { get; private set; }

    /// <summary>群岛模板。</summary>
    public static SubWorldTemplateAsset Islands { get; private set; }

    /// <summary>平原模板。</summary>
    public static SubWorldTemplateAsset BoringPlains { get; private set; }

    /// <summary>环形岛模板。</summary>
    public static SubWorldTemplateAsset Donut { get; private set; }

    /// <summary>吐司形模板。</summary>
    public static SubWorldTemplateAsset Toast { get; private set; }

    /// <summary>煎饼形模板。</summary>
    public static SubWorldTemplateAsset Pancake { get; private set; }

    /// <summary>休眠火山模板。</summary>
    public static SubWorldTemplateAsset DormantVolcano { get; private set; }

    /// <summary>奶酪孔洞模板。</summary>
    public static SubWorldTemplateAsset Cheese { get; private set; }

    /// <summary>坏苹果模板。</summary>
    public static SubWorldTemplateAsset BadApple { get; private set; }

    /// <summary>混沌珍珠模板。</summary>
    public static SubWorldTemplateAsset ChaosPearl { get; private set; }

    /// <summary>千层面模板。</summary>
    public static SubWorldTemplateAsset Lasagna { get; private set; }

    /// <summary>蚁丘模板。</summary>
    public static SubWorldTemplateAsset Anthill { get; private set; }

    /// <summary>棋盘模板。</summary>
    public static SubWorldTemplateAsset Checkerboard { get; private set; }

    /// <summary>方格模板。</summary>
    public static SubWorldTemplateAsset Cubicles { get; private set; }

    /// <summary>空地图模板。</summary>
    public static SubWorldTemplateAsset Empty { get; private set; }

    /// <summary>残破古修道场模板。</summary>
    public static SubWorldTemplateAsset RuinedDaoGround { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SubWorld.Template";

    protected override void ActionAfterCreation(PropertyInfo property, SubWorldTemplateAsset asset)
    {
        asset.clock_profile_id = SubWorldClockProfileLibrary.StandardId;
        if (property.Name == nameof(RuinedDaoGround))
        {
            asset.width = RuinedDaoGroundGeneratorAsset.MapWidth;
            asset.height = RuinedDaoGroundGeneratorAsset.MapHeight;
            asset.generator_id = SubWorldGenerators.RuinedDaoGround.id;
            asset.visual_profile_id = SubWorldVisualProfiles.RuinedDaoGround.id;
            return;
        }

        ConfigureNaturalTemplate(asset, property.Name);
    }

    private static void ConfigureNaturalTemplate(SubWorldTemplateAsset asset, string propertyName)
    {
        asset.allow_custom_size = true;
        asset.allow_user_creation = true;
        asset.width = NaturalWorldGeneratorAsset.MapWidth;
        asset.height = NaturalWorldGeneratorAsset.MapHeight;
        asset.generator_id = SubWorldGenerators.NaturalWorld.id;
        asset.visual_profile_id = SubWorldVisualProfileLibrary.StandardId;
        asset.generation_profile_id = GetProfileId(propertyName);
        asset.icon_path = $"ui/new_world_templates_icons/template_{asset.generation_profile_id}";
        asset.display_name_key = $"Cultiway.SubWorld.Template.{GetDisplayName(propertyName)}";
        asset.description_key = $"Cultiway.SubWorld.Template.{GetDisplayName(propertyName)}.Description";
        asset.display_order = GetDisplayOrder(propertyName);
        asset.generation_settings = NaturalWorldGeneratorAsset.CreateDefaultSettings(asset.generation_profile_id);
    }

    private static string GetProfileId(string propertyName)
    {
        return propertyName switch
        {
            nameof(NaturalWorld) => "continent",
            nameof(BoxWorld) => "box_world",
            nameof(Islands) => "islands",
            nameof(BoringPlains) => "boring_plains",
            nameof(Donut) => "donut",
            nameof(Toast) => "toast",
            nameof(Pancake) => "pancake",
            nameof(DormantVolcano) => "dormant_volcano",
            nameof(Cheese) => "cheese",
            nameof(BadApple) => "bad_apple",
            nameof(ChaosPearl) => "chaos_pearl",
            nameof(Lasagna) => "lasagna",
            nameof(Anthill) => "anthill",
            nameof(Checkerboard) => "checkerboard",
            nameof(Cubicles) => "cubicles",
            nameof(Empty) => "empty",
            _ => "continent"
        };
    }

    private static string GetDisplayName(string propertyName)
    {
        return propertyName == nameof(NaturalWorld) ? "Continent" : propertyName;
    }

    private static int GetDisplayOrder(string propertyName)
    {
        return propertyName switch
        {
            nameof(NaturalWorld) => 0,
            nameof(BoxWorld) => 1,
            nameof(Islands) => 2,
            nameof(Toast) => 3,
            nameof(Pancake) => 4,
            nameof(BoringPlains) => 5,
            nameof(Checkerboard) => 6,
            nameof(Cubicles) => 7,
            nameof(DormantVolcano) => 8,
            nameof(Cheese) => 9,
            nameof(BadApple) => 10,
            nameof(Donut) => 11,
            nameof(Lasagna) => 12,
            nameof(ChaosPearl) => 13,
            nameof(Anthill) => 14,
            nameof(Empty) => 15,
            _ => 100
        };
    }

    protected override void OnInit()
    {
    }
}
