using System;
using Cultiway.Content.Artifacts.Baibao;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

internal sealed class BaibaoBlueprintInspectorActions
{
    public Action Edit;
    public Action Copy;
    public Action Favorite;
    public Action Gift;
    public Action MoveUp;
    public Action MoveDown;
    public Action Delete;
    public Action Archive;
    public bool ArchiveVisible;
    public bool GiftSelected;
    public bool FavoriteSelected;
    public bool CanMove;
}

/// <summary>百宝阁目录与收录窗口共用的法宝详情检查器。</summary>
internal sealed class BaibaoBlueprintInspector
{
    private readonly BaibaoArtifactPreview _preview;
    private readonly Button _edit;
    private readonly Button _copy;
    private readonly Button _favorite;
    private readonly Button _gift;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private readonly Button _delete;
    private readonly Button _archive;
    public BaibaoBlueprintInspector(Transform parent, float width, float height)
    {
        GameObject root = BaibaoUiFactory.CreatePanel(parent, "ArtifactInspector", false, width, height, 3f,
            TextAnchor.UpperCenter);
        _preview = new BaibaoArtifactPreview(root.transform, width - 12f, height - 68f);

        GameObject primaryActions = WanfaUiFactory.CreateLayout(root.transform, "PrimaryActions", true,
            width - 8f, 22f, 3f, TextAnchor.MiddleCenter);
        _edit = CreateAction(primaryActions.transform, "Edit", BaibaoUiIcons.Edit);
        _copy = CreateAction(primaryActions.transform, "Copy", BaibaoUiIcons.Copy);
        _favorite = CreateAction(primaryActions.transform, "Favorite", BaibaoUiIcons.Favorite);
        _gift = CreateAction(primaryActions.transform, "Gift", BaibaoUiIcons.Grant);

        GameObject secondaryActions = WanfaUiFactory.CreateLayout(root.transform, "SecondaryActions", true,
            width - 8f, 22f, 3f, TextAnchor.MiddleCenter);
        _moveUp = CreateAction(secondaryActions.transform, "MoveUp", BaibaoUiIcons.MoveUp, 90f);
        _moveDown = CreateAction(secondaryActions.transform, "MoveDown", BaibaoUiIcons.MoveDown, 90f);
        _delete = CreateAction(secondaryActions.transform, "Delete", BaibaoUiIcons.Delete);
        _archive = CreateAction(secondaryActions.transform, "Archive", BaibaoUiIcons.Archive);
        Clear();
    }

    public void Show(ArtifactBlueprint blueprint, BaibaoBlueprintInspectorActions actions)
    {
        _preview.Show(blueprint, false, BaibaoPresentation.GetOrigin(blueprint), Color.white);

        Configure(_edit, actions.Edit, "Cultiway.Baibao.UI.Action.Edit",
            "Cultiway.Baibao.UI.Tooltip.Edit");
        Configure(_copy, actions.Copy, "Cultiway.Baibao.UI.Action.SaveCopy",
            "Cultiway.Baibao.UI.Tooltip.SaveCopy");
        Configure(_favorite, actions.Favorite,
            actions.FavoriteSelected ? "Cultiway.Baibao.UI.Action.Unfavorite" : "Cultiway.Baibao.UI.Action.Favorite",
            actions.FavoriteSelected ? "Cultiway.Baibao.UI.Tooltip.Unfavorite" : "Cultiway.Baibao.UI.Tooltip.Favorite");
        Configure(_gift, actions.Gift,
            actions.GiftSelected ? "Cultiway.Baibao.UI.Action.RemoveFromGift" : "Cultiway.Baibao.UI.Action.AddToGift",
            actions.GiftSelected ? "Cultiway.Baibao.UI.Tooltip.DeselectForGrant" :
                "Cultiway.Baibao.UI.Tooltip.SelectForGrant");
        BaibaoUiFactory.SetSelected(_favorite, actions.FavoriteSelected);
        BaibaoUiFactory.SetSelected(_gift, actions.GiftSelected);
        Configure(_moveUp, actions.CanMove ? actions.MoveUp : null, "Cultiway.Baibao.UI.Action.MoveUp",
            "Cultiway.Baibao.UI.Tooltip.MoveUp");
        Configure(_moveDown, actions.CanMove ? actions.MoveDown : null, "Cultiway.Baibao.UI.Action.MoveDown",
            "Cultiway.Baibao.UI.Tooltip.MoveDown");
        Configure(_delete, actions.Delete, "Cultiway.Baibao.UI.Action.Delete",
            "Cultiway.Baibao.UI.Tooltip.Delete");
        Configure(_archive, actions.Archive,
            actions.Archive == null ? "Cultiway.Baibao.UI.Action.Archived" : "Cultiway.Baibao.UI.Action.Archive",
            actions.Archive == null ? "Cultiway.Baibao.UI.Tooltip.Archived" : "Cultiway.Baibao.UI.Tooltip.Archive",
            actions.ArchiveVisible);
    }

    public void Clear()
    {
        _preview.Clear();
        Button[] actions = { _edit, _copy, _favorite, _gift, _moveUp, _moveDown, _delete, _archive };
        for (int i = 0; i < actions.Length; i++) actions[i].gameObject.SetActive(false);
    }

    private static Button CreateAction(Transform parent, string name, string icon, float rotation = 0f)
    {
        return WanfaUiFactory.CreateIconButton(parent, name, icon, 34f, 21f, () => { }, 4f, 1f, rotation);
    }

    private static void Configure(Button button, Action action, string titleKey, string descriptionKey,
        bool visible = true)
    {
        button.gameObject.SetActive(visible && (action != null || button.name == "Archive"));
        button.interactable = action != null;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(action.Invoke);
        WanfaUiFactory.SetTooltip(button.gameObject, titleKey, descriptionKey);
    }
}
