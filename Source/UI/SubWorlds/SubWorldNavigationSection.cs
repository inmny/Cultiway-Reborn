using System.Collections.Generic;
using Cultiway.Core.SubWorlds;
using Cultiway.Core.SubWorlds.Runtime;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine.UI;

namespace Cultiway.UI.SubWorlds;

/// <summary>在 WORLD 神力分区中提供主世界、小世界、局部时间与 Pawn 聚焦入口。</summary>
internal sealed class SubWorldNavigationSection
{
    private const string PauseIcon = "ui/icons/iconPause";
    private const string SpeedOneIcon = "ui/icons/iconClockX1";
    private const string SpeedTwoIcon = "ui/icons/iconClockX2";
    private const string SpeedFourIcon = "ui/icons/iconClockX4";
    private const string SeparatorEntryId = "subworld.separator";
    private const string MainWorldEntryId = "subworld.main_world";
    private const string PauseSpeedEntryId = "subworld.controls.pause_speed1";
    private const string SpeedEntryId = "subworld.controls.speed2_speed4";
    private const string FocusPawnEntryId = "subworld.controls.focus_pawn";

    private readonly SubWorldManager manager;
    private readonly SortedDictionary<long, PowerButton> runtimeButtons = new();
    private PowerButton mainWorldButton;
    private PowerButton pauseButton;
    private PowerButton speedOneButton;
    private PowerButton speedTwoButton;
    private PowerButton speedFourButton;
    private PowerButton focusPawnButton;
    private bool built;
    private bool navigationVisible;
    private bool controlsVisible;

    internal SubWorldNavigationSection(SubWorldManager manager)
    {
        this.manager = manager;
    }

    internal void AddRuntime(SubWorldRuntime runtime)
    {
        EnsureBuilt();
        long instanceId = runtime.InstanceId;
        string entryId = RuntimeEntryId(instanceId);
        PowerButton button = CreatePowerButton($"Runtime_{instanceId}",
            runtime.VisualProfile.navigation_icon_path, () => manager.Focus(instanceId));
        UiTooltip.Set(button.gameObject,
            string.Format("Cultiway.SubWorld.Navigation.Runtime".Localize(), instanceId),
            "Cultiway.SubWorld.Navigation.Runtime.Description".Localize());
        runtimeButtons.Add(instanceId, button);
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD, PowerTabSections.WorldSubWorlds, 200, entryId, button);
        SetNavigationVisible(true);
        Refresh();
    }

    internal void RemoveRuntime(long instanceId)
    {
        if (!runtimeButtons.TryGetValue(instanceId, out PowerButton button)) return;
        runtimeButtons.Remove(instanceId);
        Cultiway.UI.Manager.RemoveEntry(TabButtonType.WORLD, RuntimeEntryId(instanceId));
        if (runtimeButtons.Count == 0)
        {
            SetControlsVisible(false);
            SetNavigationVisible(false);
        }
        Refresh();
    }

    internal void Refresh()
    {
        if (!built || !navigationVisible) return;
        long? focused = manager.FocusedInstanceId;
        SetSelected(mainWorldButton, !focused.HasValue);
        foreach (KeyValuePair<long, PowerButton> pair in runtimeButtons)
        {
            SetSelected(pair.Value, focused == pair.Key);
        }

        bool hasFocusedRuntime = focused.HasValue && runtimeButtons.ContainsKey(focused.Value);
        SetControlsVisible(hasFocusedRuntime);
        if (!hasFocusedRuntime) return;

        SubWorldRuntime runtime = manager.Get(focused.Value);
        float speed = runtime.Clock.LocalSpeed;
        SetSelected(pauseButton, runtime.Clock.IsPaused);
        SetSelected(speedOneButton, speed == 1f);
        SetSelected(speedTwoButton, speed == 2f);
        SetSelected(speedFourButton, speed == 4f);
    }

    internal void Clear()
    {
        long[] instanceIds = new long[runtimeButtons.Count];
        runtimeButtons.Keys.CopyTo(instanceIds, 0);
        for (int i = 0; i < instanceIds.Length; i++)
        {
            Cultiway.UI.Manager.RemoveEntry(TabButtonType.WORLD, RuntimeEntryId(instanceIds[i]));
        }
        runtimeButtons.Clear();
        SetControlsVisible(false);
        SetNavigationVisible(false);
    }

    private void EnsureBuilt()
    {
        if (built) return;

        Cultiway.UI.Manager.AddSeparator(TabButtonType.WORLD, PowerTabSections.WorldSubWorlds, 0,
            SeparatorEntryId);
        mainWorldButton = CreatePowerButton("MainWorld", UiIcons.World,
            manager.FocusMainWorld);
        UiTooltip.Set(mainWorldButton.gameObject,
            "Cultiway.SubWorld.Navigation.MainWorld".Localize(),
            "Cultiway.SubWorld.Navigation.MainWorld.Description".Localize());
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD, PowerTabSections.WorldSubWorlds, 100,
            MainWorldEntryId, mainWorldButton);

        pauseButton = CreateControlButton("Pause", PauseIcon,
            () => IssueFocused(runtime => new PauseCommand(runtime.InstanceId, !runtime.Clock.IsPaused)),
            "Cultiway.SubWorld.Navigation.Pause");
        speedOneButton = CreateSpeedButton("Speed1", SpeedOneIcon, 1f);
        speedTwoButton = CreateSpeedButton("Speed2", SpeedTwoIcon, 2f);
        speedFourButton = CreateSpeedButton("Speed4", SpeedFourIcon, 4f);
        focusPawnButton = CreateControlButton("FocusPawn", UiIcons.Select,
            manager.FocusPawn, "Cultiway.SubWorld.Navigation.FocusPawn");

        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, PowerTabSections.WorldSubWorlds, 1000,
            PauseSpeedEntryId, pauseButton, speedOneButton);
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, PowerTabSections.WorldSubWorlds, 1100,
            SpeedEntryId, speedTwoButton, speedFourButton);
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD, PowerTabSections.WorldSubWorlds, 1200,
            FocusPawnEntryId, focusPawnButton);

        built = true;
        navigationVisible = true;
        controlsVisible = true;
        SetControlsVisible(false);
        SetNavigationVisible(false);
    }

    private PowerButton CreateSpeedButton(string name, string icon, float speed)
    {
        return CreateControlButton(name, icon,
            () => IssueFocused(runtime => new SetLocalSpeedCommand(runtime.InstanceId, speed)),
            $"Cultiway.SubWorld.Navigation.Speed{speed:0}");
    }

    private PowerButton CreateControlButton(string name, string icon, UnityEngine.Events.UnityAction action,
        string localeKey)
    {
        PowerButton button = CreatePowerButton(name, icon, action);
        UiTooltip.Set(button.gameObject, localeKey.Localize(), $"{localeKey}.Description".Localize());
        return button;
    }

    private static PowerButton CreatePowerButton(string name, string icon,
        UnityEngine.Events.UnityAction action)
    {
        return PowerButtonCreator.CreateSimpleButton(name, action,
            SpriteTextureLoader.getSprite(icon));
    }

    private static void SetSelected(PowerButton button, bool selected)
    {
        UiStateStyle.SetSelected(button.GetComponent<Button>(), selected);
    }

    private void IssueFocused(System.Func<SubWorldRuntime, ISubWorldCommand> createCommand)
    {
        long instanceId = manager.FocusedInstanceId.Value;
        manager.IssueCommand(instanceId, createCommand(manager.Get(instanceId)));
    }

    private void SetNavigationVisible(bool visible)
    {
        if (!built || navigationVisible == visible) return;
        navigationVisible = visible;
        Cultiway.UI.Manager.SetEntryActive(TabButtonType.WORLD, SeparatorEntryId, visible);
        Cultiway.UI.Manager.SetEntryActive(TabButtonType.WORLD, MainWorldEntryId, visible);
    }

    private void SetControlsVisible(bool visible)
    {
        if (!built || controlsVisible == visible) return;
        controlsVisible = visible;
        Cultiway.UI.Manager.SetEntryActive(TabButtonType.WORLD, PauseSpeedEntryId, visible);
        Cultiway.UI.Manager.SetEntryActive(TabButtonType.WORLD, SpeedEntryId, visible);
        Cultiway.UI.Manager.SetEntryActive(TabButtonType.WORLD, FocusPawnEntryId, visible);
    }

    private static string RuntimeEntryId(long instanceId)
    {
        return $"subworld.runtime.{instanceId:D20}";
    }
}
