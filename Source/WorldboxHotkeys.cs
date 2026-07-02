using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.UI;
using UnityEngine;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Hotkeys : ExtendLibrary<HotkeyAsset, Hotkeys>
    {
        public const string CastControlledSkillId = "hotkey_cultiway_control_cast_skill";
        public const string CycleControlledSkillId = "hotkey_cultiway_control_cycle_skill";

        private const string CastLabelKey = "cultiway_control_action_cast_skill";
        private const string CycleLabelKey = "cultiway_control_action_cycle_skill";

        [AssetId(CastControlledSkillId)]
        public static HotkeyAsset CastControlledSkill { get; private set; }
        [AssetId(CycleControlledSkillId)]
        public static HotkeyAsset CycleControlledSkill { get; private set; }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            ConfigureUnitControlHotkey(CastControlledSkill, KeyCode.R,
                _ => ControlledCultivatorSkillControls.CastSelectedSkill());
            ConfigureUnitControlHotkey(CycleControlledSkill, KeyCode.E,
                _ => ControlledCultivatorSkillControls.CycleSelectedSkill());

            ControlledCultivatorPossessionUi.Register(
                CastControlledSkillId,
                () => ControlledCultivatorSkillControls.GetState().HasSkill,
                () => CastLabelKey,
                () => GetHotkeyText(CastControlledSkill, "R"));
            ControlledCultivatorPossessionUi.Register(
                CycleControlledSkillId,
                () => ControlledCultivatorSkillControls.GetState().CanCycleSkill,
                () => CycleLabelKey,
                () => GetHotkeyText(CycleControlledSkill, "E"));

            AssetManager.hotkey_library.linkAssets();
        }

        public static void ConfigureUnitControlHotkey(HotkeyAsset hotkey, KeyCode key, HotkeyAction action)
        {
            if (hotkey == null) return;

            hotkey.default_key_1 = key;
            hotkey.ignore_same_key_diagnostic = true;
            hotkey.check_window_not_active = true;
            hotkey.check_controls_locked = true;
            hotkey.check_only_controllable_unit = true;
            hotkey.allow_unit_control = true;
            hotkey.just_pressed_action = action;
        }

        public static string GetHotkeyText(HotkeyAsset hotkey, string fallback)
        {
            var text = hotkey?.getLocalizedKeys();
            return string.IsNullOrEmpty(text) ? fallback : text;
        }
    }
}
