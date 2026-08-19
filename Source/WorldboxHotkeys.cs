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
        public const string IssueControlledTaskId = "hotkey_cultiway_control_issue_task";
        public const string SelectControlledSkill1Id = "hotkey_cultiway_control_select_skill_1";
        public const string SelectControlledSkill2Id = "hotkey_cultiway_control_select_skill_2";
        public const string SelectControlledSkill3Id = "hotkey_cultiway_control_select_skill_3";
        public const string SelectControlledSkill4Id = "hotkey_cultiway_control_select_skill_4";
        public const string SelectControlledSkill5Id = "hotkey_cultiway_control_select_skill_5";
        public const string SelectControlledSkill6Id = "hotkey_cultiway_control_select_skill_6";
        public const string SelectControlledSkill7Id = "hotkey_cultiway_control_select_skill_7";
        public const string SelectControlledSkill8Id = "hotkey_cultiway_control_select_skill_8";
        public const string SelectControlledSkill9Id = "hotkey_cultiway_control_select_skill_9";
        public const string SelectControlledSkill10Id = "hotkey_cultiway_control_select_skill_10";

        private const string CastLabelKey = "cultiway_control_action_cast_skill";
        private const string CastHintKey = "cultiway_control_action_cast_skill_hint";
        private const string CycleLabelKey = "cultiway_control_action_cycle_skill";

        [AssetId(CastControlledSkillId)]
        public static HotkeyAsset CastControlledSkill { get; private set; }
        [AssetId(CycleControlledSkillId)]
        public static HotkeyAsset CycleControlledSkill { get; private set; }
        [AssetId(IssueControlledTaskId)]
        public static HotkeyAsset IssueControlledTask { get; private set; }
        [AssetId(SelectControlledSkill1Id)]
        public static HotkeyAsset SelectControlledSkill1 { get; private set; }
        [AssetId(SelectControlledSkill2Id)]
        public static HotkeyAsset SelectControlledSkill2 { get; private set; }
        [AssetId(SelectControlledSkill3Id)]
        public static HotkeyAsset SelectControlledSkill3 { get; private set; }
        [AssetId(SelectControlledSkill4Id)]
        public static HotkeyAsset SelectControlledSkill4 { get; private set; }
        [AssetId(SelectControlledSkill5Id)]
        public static HotkeyAsset SelectControlledSkill5 { get; private set; }
        [AssetId(SelectControlledSkill6Id)]
        public static HotkeyAsset SelectControlledSkill6 { get; private set; }
        [AssetId(SelectControlledSkill7Id)]
        public static HotkeyAsset SelectControlledSkill7 { get; private set; }
        [AssetId(SelectControlledSkill8Id)]
        public static HotkeyAsset SelectControlledSkill8 { get; private set; }
        [AssetId(SelectControlledSkill9Id)]
        public static HotkeyAsset SelectControlledSkill9 { get; private set; }
        [AssetId(SelectControlledSkill10Id)]
        public static HotkeyAsset SelectControlledSkill10 { get; private set; }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            ConfigureUnitControlHotkey(CastControlledSkill, KeyCode.R, _ =>
            {
                if (!ControlledPossessionInputGate.BlocksPossessionActions)
                    ControlledSkillTargetSelection.Begin(CastControlledSkill);
            });
            ControlledSkillTargetSelection.Configure(CastControlledSkill);
            ControlledSkillTargetSelection.InstallCameraZoomGate();
            ConfigureUnitControlHotkey(CycleControlledSkill, KeyCode.E, _ =>
            {
                if (!ControlledPossessionInputGate.BlocksPossessionActions)
                    ControlledCultivatorSkillControls.CycleSelectedSkill();
            });
            ConfigureAbilitySelectionHotkey(SelectControlledSkill1, KeyCode.Alpha1, KeyCode.Keypad1, 0);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill2, KeyCode.Alpha2, KeyCode.Keypad2, 1);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill3, KeyCode.Alpha3, KeyCode.Keypad3, 2);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill4, KeyCode.Alpha4, KeyCode.Keypad4, 3);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill5, KeyCode.Alpha5, KeyCode.Keypad5, 4);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill6, KeyCode.Alpha6, KeyCode.Keypad6, 5);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill7, KeyCode.Alpha7, KeyCode.Keypad7, 6);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill8, KeyCode.Alpha8, KeyCode.Keypad8, 7);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill9, KeyCode.Alpha9, KeyCode.Keypad9, 8);
            ConfigureAbilitySelectionHotkey(SelectControlledSkill10, KeyCode.Alpha0, KeyCode.Keypad0, 9);
            ConfigureUnitControlHotkey(IssueControlledTask, KeyCode.B,
                _ => ControlledTaskCommandPalette.ToggleFromHotkey());

            ControlledCultivatorPossessionUi.Register(
                CastControlledSkillId,
                () => ControlledCultivatorSkillControls.GetState().HasSkill,
                () => CastLabelKey,
                () => GetHotkeyText(CastControlledSkill, "R"),
                () => CastHintKey);
            ControlledCultivatorPossessionUi.Register(
                CycleControlledSkillId,
                () => ControlledCultivatorSkillControls.GetState().CanCycleSkill,
                () => CycleLabelKey,
                () => GetHotkeyText(CycleControlledSkill, "E"));
            ControlledActiveAbilityBar.Ensure();
            ControlledPossessionMiniMap.Ensure();
            ControlledPossessionInfoButtons.Ensure();
            ControlledTaskCommandPalette.Ensure();
            ControlledTaskTargetSelection.Ensure();
            ControlledTaskOrderTracker.Ensure();
            ControlledPossessionInputGate.InstallControlUnitHotkeyGate();

            AssetManager.hotkey_library.linkAssets();
        }

        private static void ConfigureAbilitySelectionHotkey(
            HotkeyAsset hotkey,
            KeyCode numberKey,
            KeyCode keypadKey,
            int index)
        {
            ConfigureUnitControlHotkey(hotkey, numberKey, _ =>
            {
                if (!ControlledPossessionInputGate.BlocksPossessionActions)
                    ControlledCultivatorSkillControls.SelectAbilityAtIndex(index);
            });
            if (hotkey != null) hotkey.default_key_2 = keypadKey;
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
