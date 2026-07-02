using Cultiway.Abstract;
using Cultiway.UI;
using UnityEngine;

namespace Cultiway.Content;

internal class Hotkeys : ExtendLibrary<HotkeyAsset, Hotkeys>
{
    public const string ToggleControlledFlightId = "hotkey_cultiway_control_toggle_flight";

    private const string FlightOnLabelKey = "cultiway_control_action_flight_on";
    private const string FlightOffLabelKey = "cultiway_control_action_flight_off";

    [AssetId(ToggleControlledFlightId)]
    public static HotkeyAsset ToggleControlledFlight { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        ControlledCultivatorFlightControls.Init();

        WorldboxGame.Hotkeys.ConfigureUnitControlHotkey(ToggleControlledFlight, KeyCode.C,
            _ => ControlledCultivatorFlightControls.ToggleManualFlight());

        ControlledCultivatorPossessionUi.Register(
            ToggleControlledFlightId,
            () => ControlledCultivatorFlightControls.GetState().CanToggleFlight,
            () => ControlledCultivatorFlightControls.GetState().IsManualFlight ? FlightOffLabelKey : FlightOnLabelKey,
            () => WorldboxGame.Hotkeys.GetHotkeyText(ToggleControlledFlight, "C"));

        AssetManager.hotkey_library.linkAssets();
    }
}
