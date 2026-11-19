using Cultiway.Abstract;
using Cultiway.Content.Territories;
using Cultiway.UI;
using NeoModLoader.General;

namespace Cultiway.Content;

/// <summary>沿用原版地图笔刷提供定制地区工具。</summary>
public partial class GodPowers
{
    /// <summary>在点击位置新建一块地区。</summary>
    public static GodPower CreateTerritory { get; private set; }
    /// <summary>选择已有地区。</summary>
    public static GodPower SelectTerritory { get; private set; }
    /// <summary>连续绘制当前地区。</summary>
    [CloneSource(PowerLibrary.TEMPLATE_TERRAFORM_TILES)]
    public static GodPower DrawTerritory { get; private set; }
    /// <summary>连续擦除当前地区的空闲部分。</summary>
    [CloneSource(PowerLibrary.TEMPLATE_TERRAFORM_TILES)]
    public static GodPower EraseTerritory { get; private set; }
    /// <summary>明确删除整块定制范围。</summary>
    public static GodPower DeleteTerritory { get; private set; }
    /// <summary>点击地区后编辑名称。</summary>
    public static GodPower RenameTerritory { get; private set; }

    /// <summary>使用已有世界工具分区和原版按钮，绘制保留原版笔刷大小。</summary>
    private static void SetupTerritoryPowers()
    {
        ConfigureTerritoryPower(CreateTerritory, "create", "iconCityZones", TerritoryTools.Create, false);
        ConfigureTerritoryPower(SelectTerritory, "select", "iconVillages", TerritoryTools.Select, false);
        ConfigureTerritoryPower(DrawTerritory, "draw", "iconBorderBrush", TerritoryTools.Paint, true);
        ConfigureTerritoryPower(EraseTerritory, "erase", "iconEraser", TerritoryTools.Erase, true);
        ConfigureTerritoryPower(DeleteTerritory, "delete", "iconDemolish", TerritoryTools.Delete, false);
        ConfigureTerritoryPower(RenameTerritory, "rename", "iconEdit", TerritoryTools.Rename, false);
        TerritoryTools.PaintButton = PowerButtonCreator.CreateGodPowerButton(DrawTerritory.id,
            SpriteTextureLoader.getSprite("ui/icons/iconBorderBrush"));
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, PowerTabSections.WorldTools, 1900,
            "world.territory.select",
            PowerButtonCreator.CreateGodPowerButton(CreateTerritory.id, SpriteTextureLoader.getSprite("ui/icons/iconCityZones")),
            PowerButtonCreator.CreateGodPowerButton(SelectTerritory.id, SpriteTextureLoader.getSprite("ui/icons/iconVillages")));
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, PowerTabSections.WorldTools, 1910,
            "world.territory.paint", TerritoryTools.PaintButton,
            PowerButtonCreator.CreateGodPowerButton(EraseTerritory.id, SpriteTextureLoader.getSprite("ui/icons/iconEraser")));
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, PowerTabSections.WorldTools, 1920,
            "world.territory.delete",
            PowerButtonCreator.CreateGodPowerButton(DeleteTerritory.id, SpriteTextureLoader.getSprite("ui/icons/iconDemolish")),
            PowerButtonCreator.CreateGodPowerButton(RenameTerritory.id, SpriteTextureLoader.getSprite(UiIcons.Edit)));
    }

    /// <summary>配置免费世界工具，清除地形模板的破坏动作。</summary>
    /// <param name="power">新注册的原版神力。</param>
    /// <param name="name">工具名称后缀。</param>
    /// <param name="icon">原版光标图标。</param>
    /// <param name="action">领域操作。</param>
    /// <param name="brush">是否使用连续笔刷。</param>
    private static void ConfigureTerritoryPower(GodPower power, string name, string icon, PowerActionWithID action, bool brush)
    {
        power.name = "cultiway_territory_" + name;
        power.path_icon = icon;
        power.force_map_mode = MetaType.None;
        power.terraform = false;
        power.hold_action = brush;
        power.draw_lines = brush;
        power.show_tool_sizes = brush;
        power.force_brush = brush ? string.Empty : "sqr_0";
        power.click_action = action;
        power.click_brush_action = brush ? TerritoryTools.ApplyBrush : null;
        power.click_power_action = null;
        power.click_power_brush_action = null;
        power.unselect_when_window = true;
        power.can_drag_map = false;
        power.requires_premium = false;
        power.rank = PowerRank.Rank0_free;
    }
}
