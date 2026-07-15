using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>角色法宝库存中的可检查、多选收录条目。</summary>
public sealed class BaibaoArchiveRow : APrefabPreview<BaibaoArchiveRow>
{
    private Entity _artifact;
    private Button _row;
    private UiListRowChrome _chrome;
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _select;

    protected override void OnInit()
    {
        _row = GetComponent<Button>();
        _chrome = UiListRowChrome.From(gameObject);
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _select = transform.Find("Select").GetComponent<Button>();
    }

    public void Setup(Entity artifact, bool archived, bool active, bool selected, Action inspect, Action toggle)
    {
        Init();
        _artifact = artifact;
        ItemShape shape = artifact.GetComponent<ItemShape>();
        string shapeName = shape.Type.ingredient_name_candidates.FirstOrDefault() ?? shape.shape_id.Localize();
        string state = archived
            ? "Cultiway.Baibao.UI.State.Archived".Localize()
            : "Cultiway.Baibao.UI.State.NotArchived".Localize();

        _name.text = artifact.GetComponent<EntityName>().value;
        _detail.text = $"{shapeName}  ·  {artifact.GetComponent<ItemLevel>().GetName()}  ·  {state}";
        _icon.sprite = artifact.GetComponent<SpecialItem>().GetSprite();
        _icon.preserveAspect = true;
        UiTooltip.Set(_icon.gameObject, ShowArtifactTooltip);

        _row.onClick.RemoveAllListeners();
        _row.onClick.AddListener(inspect.Invoke);
        _chrome.SetState(active ? UiControlState.Selected : UiControlState.Normal);
        _select.interactable = !archived;
        _select.onClick.RemoveAllListeners();
        if (!archived) _select.onClick.AddListener(toggle.Invoke);
        UiElements.SetButtonIcon(_select, archived ? UiIcons.Confirm : BaibaoUiIcons.Archive);
        UiStateStyle.SetSelected(_select, selected || archived);
        UiTooltip.Set(_select.gameObject,
            archived ? "Cultiway.Baibao.UI.Action.Archived" :
                selected ? "Cultiway.Baibao.UI.Action.Deselect" : "Cultiway.Baibao.UI.Action.Select",
            archived ? "Cultiway.Baibao.UI.Tooltip.Archived" : "Cultiway.Baibao.UI.Tooltip.ArchiveSelection");
    }

    private void ShowArtifactTooltip()
    {
        Tooltip.show(_icon.gameObject, WorldboxGame.Tooltips.SpecialItem.id, new TooltipData
        {
            tip_name = _artifact.Id.ToString(),
        });
    }

    private static void _init()
    {
        GameObject obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(BaibaoArchiveRow), true,
            280f, 46f, 3f);
        UiListRowChrome.Attach(obj, true);
        obj.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 3, 3);

        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(iconObject.transform, 40f, 40f);
        iconObject.GetComponent<Image>().preserveAspect = true;

        GameObject labels = UiLayout.Create(obj.transform, "Labels", false, 196f, 40f, 0f);
        UiElements.CreateText(labels.transform, "Name", string.Empty, 196f, 21f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 196f, 19f, 6,
            TextAnchor.MiddleLeft);
        UiElements.CreateIconButton(obj.transform, "Select", BaibaoUiIcons.Archive, 28f, 25f, () => { }, 4f);
        Prefab = obj.AddComponent<BaibaoArchiveRow>();
    }
}
