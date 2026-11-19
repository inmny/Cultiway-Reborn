using Cultiway.Abstract;
using Cultiway.Content.Railways;
using Cultiway.UI;
using NeoModLoader.General;

namespace Cultiway.Content;

/// <summary>铁路造物笔刷，沿用原版选择、连续绘制和取消操作。</summary>
public partial class GodPowers
{
    /// <summary>玩家免费绘制安全陆地铁路。</summary>
    [CloneSource(PowerLibrary.TEMPLATE_TERRAFORM_TILES)]
    public static GodPower DrawRailway { get; private set; }
    /// <summary>玩家明确拆除轨道，不破坏地面和建筑。</summary>
    [CloneSource(PowerLibrary.TEMPLATE_TERRAFORM_TILES)]
    public static GodPower EraseRailway { get; private set; }
    /// <summary>复用底栏真实神力按钮来选择绘制工具。</summary>
    private static PowerButton railwayPaintButton;
    /// <summary>复用底栏真实神力按钮来选择擦除工具。</summary>
    private static PowerButton railwayEraseButton;

    /// <summary>使用已有世界工具分区及原版图标，不新增平行工具栏。</summary>
    private static void SetupRailwayPowers()
    {
        ConfigureRailwayPower(DrawRailway, "cultiway_draw_rails", "iconBoat", RailwayManualTrackService.Paint);
        ConfigureRailwayPower(EraseRailway, "cultiway_erase_rails", "iconDemolish", RailwayManualTrackService.Erase);
        var paint = railwayPaintButton = PowerButtonCreator.CreateGodPowerButton(DrawRailway.id,
            SpriteTextureLoader.getSprite("cultiway/icons/plots/iconTrainNet"));
        var erase = railwayEraseButton = PowerButtonCreator.CreateGodPowerButton(EraseRailway.id,
            SpriteTextureLoader.getSprite(UiIcons.Delete));
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, PowerTabSections.WorldTools, 1800,
            "world.railway.paint", paint, erase);
    }

    /// <summary>详情页入口沿用原版神力的选择、光标和取消行为。</summary>
    /// <param name="erase">是否选择擦除。</param>
    internal static void SelectRailwayTool(bool erase) => (erase ? railwayEraseButton : railwayPaintButton).clickButton();

    /// <summary>固定为单格连续笔刷，避免形成无法行驶的宽块或继承地形破坏动作。</summary>
    /// <param name="power">新登记的原版神力。</param>
    /// <param name="name">本地化名称。</param>
    /// <param name="icon">原版光标图标。</param>
    /// <param name="action">只处理合法铁轨的动作。</param>
    private static void ConfigureRailwayPower(GodPower power, string name, string icon, PowerActionWithID action)
    {
        power.name = name; power.path_icon = icon;
        power.force_brush = "sqr_0"; power.show_tool_sizes = false;
        power.terraform = false; power.draw_lines = true;
        power.click_brush_action = null; power.click_action = action;
        power.requires_premium = false; power.rank = PowerRank.Rank0_free;
    }
}
