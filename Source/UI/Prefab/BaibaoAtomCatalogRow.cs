using System;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>法宝 atom 目录中的可复用候选条目。</summary>
public sealed class BaibaoAtomCatalogRow : APrefabPreview<BaibaoAtomCatalogRow>
{
    private UiListRowChrome _chrome;
    private Button _focus;
    private Image _icon;
    private Text _title;
    private Text _detail;
    private Button _action;

    protected override void OnInit()
    {
        _chrome = UiListRowChrome.From(gameObject);
        _focus = GetComponent<Button>();
        _icon = transform.Find("Icon").GetComponent<Image>();
        _title = transform.Find("Labels/Title").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _action = transform.Find("Action").GetComponent<Button>();
    }

    public void Setup(
        ArtifactAtomAsset atom,
        float strength,
        bool selected,
        bool focused,
        Action focus,
        Action toggle)
    {
        Init();
        string title = BaibaoPresentation.GetAtomName(atom);
        string description = BaibaoPresentation.GetAtomDescription(atom);
        string traitSummary = BaibaoPresentation.GetAtomTraitSummary(atom, strength, 2);
        _icon.sprite = BaibaoPresentation.GetAtomIcon(atom);
        _icon.preserveAspect = true;
        _title.text = title;
        _detail.text = string.IsNullOrEmpty(traitSummary)
            ? BaibaoPresentation.GetAtomCategoryName(atom.category)
            : traitSummary;
        _chrome.SetState(focused ? UiControlState.Selected : UiControlState.Normal);

        _focus.onClick.RemoveAllListeners();
        _focus.onClick.AddListener(focus.Invoke);
        _action.onClick.RemoveAllListeners();
        _action.interactable = atom.category != ArtifactAtomCategory.Shape || !selected;
        if (toggle != null) _action.onClick.AddListener(toggle.Invoke);
        UiElements.SetButtonIcon(_action, selected ? UiIcons.Confirm : UiIcons.Add);
        UiStateStyle.SetSelected(_action, selected);

        UiTooltip.Set(gameObject, title, description,
            BaibaoPresentation.GetAtomTooltipDetail(atom, strength));
        string actionTitle = selected
            ? "Cultiway.Baibao.UI.Action.Selected".Localize()
            : atom.category == ArtifactAtomCategory.Shape
                ? "Cultiway.Baibao.UI.Action.ReplaceAtom".Localize()
                : "Cultiway.Baibao.UI.Action.AddAtom".Localize();
        UiTooltip.Set(_action.gameObject, actionTitle, description);
    }

    private static void _init()
    {
        GameObject root = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(BaibaoAtomCatalogRow), true,
            232f, 42f, 4f, TextAnchor.MiddleLeft);
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(5, 4, 3, 3);
        UiListRowChrome.Attach(root, true);

        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(root.transform, false);
        UiLayout.SetSize(icon.transform, 32f, 32f);
        icon.GetComponent<Image>().preserveAspect = true;

        GameObject labels = UiLayout.Create(root.transform, "Labels", false, 155f, 36f, 0f);
        UiElements.CreateText(labels.transform, "Title", string.Empty, 155f, 20f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 155f, 16f, 6,
            TextAnchor.MiddleLeft);
        UiElements.CreateIconButton(root.transform, "Action", UiIcons.Add, 28f, 26f, () => { });
        Prefab = root.AddComponent<BaibaoAtomCatalogRow>();
    }
}
