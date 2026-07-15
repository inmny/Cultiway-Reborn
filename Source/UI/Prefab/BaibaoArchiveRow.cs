using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public sealed class BaibaoArchiveRow : APrefabPreview<BaibaoArchiveRow>
{
    private Entity _artifact;
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _archive;
    private Image _archivedMarker;

    protected override void OnInit()
    {
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _archive = transform.Find("Archive").GetComponent<Button>();
        _archivedMarker = transform.Find("Archive/Archived").GetComponent<Image>();
    }

    public void Setup(Entity artifact, bool archived, Action archive)
    {
        Init();
        _artifact = artifact;
        ItemShape shape = artifact.GetComponent<ItemShape>();
        string shapeName = shape.Type.ingredient_name_candidates.FirstOrDefault() ?? shape.shape_id.Localize();
        string state = archived
            ? "Cultiway.Baibao.UI.State.Archived".Localize()
            : "Cultiway.Baibao.UI.State.NotArchived".Localize();

        _name.text = artifact.GetComponent<EntityName>().value;
        _detail.text = string.Format("Cultiway.Baibao.UI.Format.ArchiveItemDetail".Localize(), shapeName,
            artifact.GetComponent<ItemLevel>().GetName(), state);
        _icon.sprite = artifact.GetComponent<SpecialItem>().GetSprite();
        _icon.preserveAspect = true;
        WanfaUiFactory.SetTooltip(_icon.gameObject, ShowArtifactTooltip);

        _archive.interactable = !archived;
        _archive.onClick.RemoveAllListeners();
        _archive.onClick.AddListener(archive.Invoke);
        _archivedMarker.gameObject.SetActive(archived);
        WanfaUiFactory.SetTooltip(_archive.gameObject,
            archived ? "Cultiway.Baibao.UI.Action.Archived" : "Cultiway.Baibao.UI.Action.Archive",
            archived ? "Cultiway.Baibao.UI.Tooltip.Archived" : "Cultiway.Baibao.UI.Tooltip.Archive");
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
        GameObject obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(BaibaoArchiveRow), true,
            500f, 40f, 4f);
        Image background = obj.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;

        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(obj.transform, false);
        WanfaUiFactory.SetLayout(iconObject.transform, 36f, 36f);
        iconObject.GetComponent<Image>().preserveAspect = true;

        GameObject labels = WanfaUiFactory.CreateLayout(obj.transform, "Labels", false, 408f, 36f, 0f);
        WanfaUiFactory.CreateText(labels.transform, "Name", string.Empty, 408f, 18f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(labels.transform, "Detail", string.Empty, 408f, 18f, 6);
        Button archiveButton = WanfaUiFactory.CreateIconButton(obj.transform, "Archive", BaibaoUiIcons.Archive,
            36f, 26f, () => { });
        GameObject marker = new("Archived", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(archiveButton.transform, false);
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(1f, 0f);
        markerRect.sizeDelta = new Vector2(11f, 11f);
        markerRect.anchoredPosition = new Vector2(-6f, 5f);
        Image markerImage = marker.GetComponent<Image>();
        markerImage.sprite = SpriteTextureLoader.getSprite("ui/icons/IconOn");
        markerImage.color = ColorStyleLibrary.m.getSelectorColor();
        markerImage.raycastTarget = false;
        marker.SetActive(false);
        Prefab = obj.AddComponent<BaibaoArchiveRow>();
    }
}
