using System.Collections.Generic;
using Cultiway.Core.SubWorlds;
using Cultiway.Core.SubWorlds.Runtime;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.SubWorlds;

/// <summary>提供主世界、小世界、局部时间与 Pawn 聚焦的底栏入口。</summary>
internal sealed class SubWorldNavigationBar
{
    private const float ButtonSize = 26f;
    private const string PauseIcon = "ui/icons/iconPause";
    private const string SpeedOneIcon = "ui/icons/iconClockX1";
    private const string SpeedTwoIcon = "ui/icons/iconClockX2";
    private const string SpeedFourIcon = "ui/icons/iconClockX4";

    private readonly SubWorldManager manager;
    private readonly SortedDictionary<long, PowerButton> runtimeButtons = new();
    private GameObject root;
    private Transform controls;
    private PowerButton mainWorldButton;
    private PowerButton pauseButton;
    private PowerButton speedOneButton;
    private PowerButton speedTwoButton;
    private PowerButton speedFourButton;
    private PowerButton focusPawnButton;

    internal SubWorldNavigationBar(SubWorldManager manager)
    {
        this.manager = manager;
    }

    internal void AddRuntime(SubWorldRuntime runtime)
    {
        EnsureBuilt();
        long instanceId = runtime.InstanceId;
        PowerButton button = CreatePowerButton(root.transform, $"Runtime_{instanceId}",
            runtime.VisualProfile.navigation_icon_path, () => manager.Focus(instanceId));
        UiTooltip.Set(button.gameObject,
            string.Format("Cultiway.SubWorld.Navigation.Runtime".Localize(), instanceId),
            "Cultiway.SubWorld.Navigation.Runtime.Description".Localize());
        runtimeButtons.Add(instanceId, button);
        ReorderRuntimeButtons();
        root.SetActive(true);
        Refresh();
    }

    internal void RemoveRuntime(long instanceId)
    {
        if (!runtimeButtons.TryGetValue(instanceId, out PowerButton button)) return;
        runtimeButtons.Remove(instanceId);
        Object.Destroy(button.gameObject);
        if (root != null) root.SetActive(runtimeButtons.Count != 0);
        Refresh();
    }

    internal void Refresh()
    {
        if (root == null || !root.activeSelf) return;
        long? focused = manager.FocusedInstanceId;
        SetSelected(mainWorldButton, !focused.HasValue);
        foreach (KeyValuePair<long, PowerButton> pair in runtimeButtons)
        {
            SetSelected(pair.Value, focused == pair.Key);
        }

        bool hasFocusedRuntime = focused.HasValue;
        controls.gameObject.SetActive(hasFocusedRuntime);
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
        runtimeButtons.Clear();
        if (root != null) Object.Destroy(root);
        root = null;
        controls = null;
        mainWorldButton = null;
        pauseButton = null;
        speedOneButton = null;
        speedTwoButton = null;
        speedFourButton = null;
        focusPawnButton = null;
    }

    private void EnsureBuilt()
    {
        if (root != null) return;
        Transform parent = CanvasMain.instance.canvas_ui.transform.Find("CanvasBottom");
        root = new GameObject("SubWorldNavigationBar", typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 54f);
        rect.sizeDelta = new Vector2(0f, 34f);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiResources.ApplySurface(root.GetComponent<Image>(), UiSurface.WindowInner);

        mainWorldButton = CreatePowerButton(root.transform, "MainWorld", UiIcons.World,
            manager.FocusMainWorld);
        UiTooltip.Set(mainWorldButton.gameObject,
            "Cultiway.SubWorld.Navigation.MainWorld".Localize(),
            "Cultiway.SubWorld.Navigation.MainWorld.Description".Localize());

        controls = new GameObject("FocusedRuntimeControls", typeof(RectTransform), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)).transform;
        controls.SetParent(root.transform, false);
        UiLayout.SetSize(controls, ButtonSize * 5f + 12f, ButtonSize);
        HorizontalLayoutGroup controlsLayout = controls.GetComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 3f;
        controlsLayout.childControlWidth = false;
        controlsLayout.childControlHeight = false;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childForceExpandHeight = false;

        pauseButton = CreateControlButton("Pause", PauseIcon,
            () => IssueFocused(runtime => new PauseCommand(runtime.InstanceId, !runtime.Clock.IsPaused)),
            "Cultiway.SubWorld.Navigation.Pause");
        speedOneButton = CreateSpeedButton("Speed1", SpeedOneIcon, 1f);
        speedTwoButton = CreateSpeedButton("Speed2", SpeedTwoIcon, 2f);
        speedFourButton = CreateSpeedButton("Speed4", SpeedFourIcon, 4f);
        focusPawnButton = CreateControlButton("FocusPawn", UiIcons.Select,
            manager.FocusPawn, "Cultiway.SubWorld.Navigation.FocusPawn");
        root.SetActive(false);
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
        PowerButton button = CreatePowerButton(controls, name, icon, action);
        UiTooltip.Set(button.gameObject, localeKey.Localize(), $"{localeKey}.Description".Localize());
        return button;
    }

    private static PowerButton CreatePowerButton(Transform parent, string name, string icon,
        UnityEngine.Events.UnityAction action)
    {
        PowerButton button = PowerButtonCreator.CreateSimpleButton(name, action,
            SpriteTextureLoader.getSprite(icon));
        button.transform.SetParent(parent, false);
        button.transform.localScale = Vector3.one;
        UiLayout.SetSize(button.transform, ButtonSize, ButtonSize);
        return button;
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

    private void ReorderRuntimeButtons()
    {
        int siblingIndex = mainWorldButton.transform.GetSiblingIndex() + 1;
        foreach (PowerButton button in runtimeButtons.Values)
        {
            button.transform.SetSiblingIndex(siblingIndex++);
        }
        controls.SetSiblingIndex(siblingIndex);
    }
}
