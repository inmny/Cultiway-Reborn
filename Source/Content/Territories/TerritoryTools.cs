using Cultiway.Content.UI.Territories;
using UnityEngine;

namespace Cultiway.Content.Territories;

/// <summary>把地图点击交给地区规则，复用原版笔刷、神力选择和顶部提示。</summary>
internal static class TerritoryTools
{
    /// <summary>新建或选择之后自动切换的原版绘制按钮。</summary>
    internal static PowerButton PaintButton;
    /// <summary>连续笔刷冲突提示的下次显示时间。</summary>
    private static float nextMessage;

    /// <summary>点击地图开始一个新区域，随后继续绘制。</summary>
    /// <param name="tile">玩家点击的位置。</param>
    /// <param name="power">原版神力编号。</param>
    /// <returns>是否成功新建。</returns>
    internal static bool Create(WorldTile tile, string power)
    {
        if (tile == null) return false;
        ShowMap();
        if (!Report(TerritoryService.Create(tile.zone))) return false;
        TerritoryService.FlushDisplay();
        ShowSelection();
        PaintButton.clickButton();
        return true;
    }

    /// <summary>选择已有区域并继续绘制。</summary>
    /// <param name="tile">玩家点击的位置。</param>
    /// <param name="power">原版神力编号。</param>
    /// <returns>是否成功选中。</returns>
    internal static bool Select(WorldTile tile, string power)
    {
        if (tile == null) return false;
        ShowMap();
        if (!Report(TerritoryService.Select(tile.zone))) return false;
        ShowSelection();
        PaintButton.clickButton();
        return true;
    }

    /// <summary>点击地区打开名称编辑窗口。</summary>
    /// <param name="tile">玩家点击的位置。</param>
    /// <param name="power">原版神力编号。</param>
    /// <returns>是否打开编辑窗口。</returns>
    internal static bool Rename(WorldTile tile, string power)
    {
        if (tile == null) return false;
        ShowMap();
        if (!Report(TerritoryService.Select(tile.zone))) return false;
        WindowRegionName.Open(TerritoryService.Selected);
        return true;
    }

    /// <summary>按当前笔刷扩展区域，不改变地形。</summary>
    /// <param name="tile">原版笔刷选中的地块。</param>
    /// <param name="power">原版神力编号。</param>
    /// <returns>是否允许本次绘制。</returns>
    internal static bool Paint(WorldTile tile, string power)
    {
        if (tile == null) return false;
        ShowMap();
        return Report(TerritoryService.Paint(tile.zone));
    }

    /// <summary>按当前笔刷擦除空闲部分。</summary>
    /// <param name="tile">原版笔刷选中的地块。</param>
    /// <param name="power">原版神力编号。</param>
    /// <returns>是否允许本次擦除。</returns>
    internal static bool Erase(WorldTile tile, string power)
    {
        if (tile == null) return false;
        ShowMap();
        return Report(TerritoryService.Erase(tile.zone));
    }

    /// <summary>点击删除整块定制范围，原有村庄继续存在。</summary>
    /// <param name="tile">玩家明确点击的位置。</param>
    /// <param name="power">原版神力编号。</param>
    /// <returns>是否删除成功。</returns>
    internal static bool Delete(WorldTile tile, string power)
    {
        if (tile == null) return false;
        ShowMap();
        if (!Report(TerritoryService.Delete(tile.zone))) return false;
        TerritoryService.FlushDisplay();
        WorldTip.showNow(T("Deleted"), false, "top");
        return true;
    }

    /// <summary>使用工具时打开现有地图图层。</summary>
    private static void ShowMap()
    {
        if (!PlayerConfig.optionBoolEnabled(TerritoryMapOverlay.ToggleName))
            AssetManager.powers.get(TerritoryMapOverlay.ToggleName).toggle_action(TerritoryMapOverlay.ToggleName);
    }

    /// <summary>使用原版完整笔刷，再统一发布一次地区显示。</summary>
    /// <param name="tile">笔刷中心。</param>
    /// <param name="power">绘制或擦除神力编号。</param>
    /// <returns>原版笔刷执行结果。</returns>
    [ClickActionCaller]
    internal static bool ApplyBrush(WorldTile tile, string power)
    {
        GodPower tool = AssetManager.powers.get(power);
        World.world.loopWithBrush(tile, Config.current_brush_data, tool.click_action, power);
        TerritoryService.FlushDisplay();
        return true;
    }

    /// <summary>显示当前编号、所属村庄和面积。</summary>
    private static void ShowSelection()
    {
        CustomTerritory region = TerritoryService.Selected;
        WorldTip.showNow(string.Format(T("Selected"), TerritoryService.GetName(region),
            TerritoryService.GetOwner(region)?.name ?? T("Vacant"), region.Zones.Count), false, "top");
    }

    /// <summary>连续拖动时限频显示冲突原因。</summary>
    /// <param name="error">领域操作返回的提示后缀。</param>
    /// <returns>操作是否成功。</returns>
    private static bool Report(string error)
    {
        if (error == null) return true;
        if (Time.unscaledTime >= nextMessage)
        {
            nextMessage = Time.unscaledTime + 1f;
            WorldTip.showNow(T(error), false, "top");
        }
        return false;
    }

    /// <summary>读取地区工具文本。</summary>
    /// <param name="suffix">文本键后缀。</param>
    /// <returns>当前语言文本。</returns>
    private static string T(string suffix) => LocalizedTextManager.getText("Cultiway.Territories." + suffix);
}
