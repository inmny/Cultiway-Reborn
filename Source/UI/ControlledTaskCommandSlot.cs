using System;
using Cultiway.Core.ControlledTasks;
using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

internal sealed class ControlledTaskCommandSlot : MonoBehaviour
{
    private Button button;
    private Image icon;
    private Text label;
    private GameObject statusIcon;
    private Action<string> onSelected;
    private string commandId;

    internal static ControlledTaskCommandSlot CreateTemplate(Transform parent)
    {
        var root = new GameObject("ControlledTaskCommandSlot", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement), typeof(UiListRowRootMarker), typeof(ControlledTaskCommandSlot));
        root.transform.SetParent(parent, false);
        UiLayout.SetSize(root.transform, 145f, 28f);
        UiResources.ApplySurface(root.GetComponent<Image>(), UiSurface.Button);

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(5f, 0f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        iconObject.GetComponent<Image>().raycastTarget = false;

        Text label = UiElements.CreateText(root.transform, "Label", string.Empty, 104f, 26f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(27f, 1f);
        labelRect.offsetMax = new Vector2(-18f, -1f);
        label.GetComponent<LayoutElement>().ignoreLayout = true;
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 5;
        label.resizeTextMaxSize = 7;

        var status = new GameObject("Status", typeof(RectTransform), typeof(Image));
        status.transform.SetParent(root.transform, false);
        RectTransform statusRect = status.GetComponent<RectTransform>();
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(1f, 0.5f);
        statusRect.pivot = new Vector2(1f, 0.5f);
        statusRect.anchoredPosition = new Vector2(-4f, 0f);
        statusRect.sizeDelta = new Vector2(12f, 12f);
        UiResources.SetImage(status.GetComponent<Image>(), "ui/icons/iconWarning");

        return root.GetComponent<ControlledTaskCommandSlot>();
    }

    internal void Initialize(Action<string> selectionAction)
    {
        button = GetComponent<Button>();
        icon = transform.Find("Icon").GetComponent<Image>();
        label = transform.Find("Label").GetComponent<Text>();
        statusIcon = transform.Find("Status").gameObject;
        onSelected = selectionAction;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(commandId)) onSelected?.Invoke(commandId);
        });
    }

    internal void Setup(ControlledTaskCommandAsset command, ControlledTaskAvailability availability,
        bool selected)
    {
        commandId = command.id;
        label.text = command.NameLocaleKey.Localize();
        label.color = availability.Enabled ? UiTheme.Current.Palette.PrimaryText : UiTheme.Current.Palette.MutedText;
        UiResources.SetImage(icon, command.IconPath);
        UiStateStyle.ApplyVisual(button, selected
            ? UiControlState.Selected
            : availability.Enabled ? UiControlState.Normal : UiControlState.Disabled);

        statusIcon.SetActive(!availability.Enabled);
        if (!availability.Enabled)
        {
            string reason = availability.ReasonLocaleKey.Localize();
            UiTooltip.Set(statusIcon,
                "Cultiway.ControlledTask.UI.Unavailable".Localize(),
                reason);
            Button warningButton = statusIcon.GetComponent<Button>();
            warningButton.onClick.RemoveAllListeners();
            warningButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(commandId)) onSelected?.Invoke(commandId);
            });
        }
        else
        {
            UiTooltip.Clear(statusIcon);
        }
    }

    internal void Clear()
    {
        commandId = null;
        if (label != null) label.text = string.Empty;
        if (statusIcon != null)
        {
            UiTooltip.Clear(statusIcon);
            statusIcon.SetActive(false);
        }
    }
}
