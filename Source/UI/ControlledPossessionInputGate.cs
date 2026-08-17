namespace Cultiway.UI;

/// <summary>集中定义附体 HUD 何时拥有世界输入，避免各控件分别遗漏动作入口。</summary>
internal static class ControlledPossessionInputGate
{
    private static bool controlHotkeyGateInstalled;
    private static HotkeyAction originalControlAction;

    internal static bool BlocksPossessionActions =>
        ControlledTaskCommandPalette.IsOpen || ControlledTaskTargetSelection.IsActive;

    internal static bool ShouldSuppressPossessionAction()
    {
        return BlocksPossessionActions || ConsumesPointerInput();
    }

    internal static bool ConsumesPointerInput()
    {
        return ControlledActiveAbilityBar.ConsumesPointerInput() ||
               ControlledTaskCommandPalette.ConsumesPointerInput() ||
               ControlledTaskTargetSelection.ConsumesPointerInput() ||
               ControlledTaskOrderTracker.ConsumesPointerInput();
    }

    internal static void InstallControlUnitHotkeyGate()
    {
        if (controlHotkeyGateInstalled || HotkeyLibrary.control_unit == null) return;
        controlHotkeyGateInstalled = true;
        originalControlAction = HotkeyLibrary.control_unit.just_pressed_action;
        HotkeyLibrary.control_unit.just_pressed_action = hotkey =>
        {
            if (!BlocksPossessionActions) originalControlAction?.Invoke(hotkey);
        };
    }
}
