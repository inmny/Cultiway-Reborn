using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>
/// 法宝炼制窗口。器形和 atom 均从注册表读取，非器形 atom 可以任意组合。
/// </summary>
public sealed class WindowBaibaoForge : AbstractWideWindow<WindowBaibaoForge>
{
    public const string Id = "Cultiway.UI.WindowBaibaoForge";
    public static readonly Vector2 WindowSize = new(600f, 360f);
    private const float RootHeight = 318f;
    private static bool _resetRequested;

    private ArtifactShapeAsset[] _shapes = [];
    private ArtifactAtomAsset[] _atoms = [];
    private Toggle[] _atomToggles = [];
    private Image _previewImage;
    private Text _previewName;
    private Text _previewSummary;
    private Text _shapeValue;
    private Text _qualityValue;
    private Text _atomTitle;
    private int _shapeIndex;
    private int _qualityValueIndex;
    private bool _suppressRefresh;

    public static void Open()
    {
        _resetRequested = true;
        ScrollWindow.showWindow(Id);
    }

    protected override void Init()
    {
        BackgroundTransform.Find("Scroll View").gameObject.SetActive(false);
        GameObject root = WanfaUiFactory.CreateLayout(BackgroundTransform, "BaibaoForgeRoot", false, 520f,
            RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        GameObject preview = WanfaUiFactory.CreateLayout(root.transform, "Preview", true, 520f, 66f, 6f);
        GameObject imageObject = new("Image", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        imageObject.transform.SetParent(preview.transform, false);
        WanfaUiFactory.SetLayout(imageObject.transform, 64f, 64f);
        _previewImage = imageObject.GetComponent<Image>();
        _previewImage.preserveAspect = true;
        GameObject labels = WanfaUiFactory.CreateLayout(preview.transform, "Labels", false, 450f, 64f, 0f);
        _previewName = WanfaUiFactory.CreateText(labels.transform, "Name", string.Empty, 450f, 22f, 10,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        _previewSummary = WanfaUiFactory.CreateText(labels.transform, "Summary", string.Empty, 450f, 42f, 7,
            TextAnchor.UpperLeft);

        GameObject selectors = WanfaUiFactory.CreateLayout(root.transform, "Selectors", true, 520f, 24f, 4f);
        CreateSelector(selectors.transform, "Shape", "Cultiway.Baibao.UI.Label.Shape", 165f,
            () => CycleShape(-1), () => CycleShape(1), out _shapeValue);
        CreateSelector(selectors.transform, "Quality", "Cultiway.Baibao.UI.Label.Quality", 133f,
            () => CycleQuality(-1), () => CycleQuality(1), out _qualityValue);

        _atomTitle = WanfaUiFactory.CreateText(root.transform, "AtomTitle", string.Empty, 520f, 17f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        Transform atomContent = WanfaUiFactory.CreateScrollContent(root.transform, "Atoms", 520f, 166f);

        _shapes = ModClass.L.ItemShapeLibrary.list
            .OfType<ArtifactShapeAsset>()
            .OrderBy(shape => shape.id, StringComparer.Ordinal)
            .ToArray();
        _atoms = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category != ArtifactAtomCategory.Shape)
            .OrderBy(atom => atom.category)
            .ThenByDescending(atom => atom.priority)
            .ThenBy(atom => atom.id, StringComparer.Ordinal)
            .ToArray();
        _atomToggles = new Toggle[_atoms.Length];
        for (int i = 0; i < _atoms.Length; i++)
        {
            ArtifactAtomAsset atom = _atoms[i];
            string label = string.Format("Cultiway.Baibao.UI.Format.AtomOption".Localize(),
                GetCategoryName(atom.category), GetAtomName(atom));
            Toggle toggle = WanfaUiFactory.CreateToggle(atomContent, $"Atom_{atom.id}", label, false, 500f, 24f);
            WanfaUiFactory.SetTooltip(toggle, GetAtomName(atom), "Cultiway.Baibao.UI.Tooltip.Atom");
            toggle.onValueChanged.AddListener(_ => OnAtomChanged());
            _atomToggles[i] = toggle;
        }

        GameObject footer = WanfaUiFactory.CreateLayout(root.transform, "Footer", true, 520f, 25f, 4f,
            TextAnchor.MiddleRight);
        WanfaUiFactory.CreateText(footer.transform, "Spacer", string.Empty, 436f, 25f);
        Button save = WanfaUiFactory.CreateIconTextButton(footer.transform, "Save", BaibaoUiIcons.Save,
            "Cultiway.Baibao.UI.Action.Save".Localize(), 48f, 23f, Save);
        WanfaUiFactory.SetTooltip(save.gameObject, "Cultiway.Baibao.UI.Action.Save",
            "Cultiway.Baibao.UI.Tooltip.Save");
        Button cancel = WanfaUiFactory.CreateIconButton(footer.transform, "Cancel", BaibaoUiIcons.Cancel, 28f,
            23f, CloseToPrevious);
        WanfaUiFactory.SetTooltip(cancel.gameObject, "Cultiway.Baibao.UI.Action.Cancel",
            "Cultiway.Baibao.UI.Tooltip.Cancel");
    }

    public override void OnNormalEnable()
    {
        if (_resetRequested)
        {
            ResetDesign();
            _resetRequested = false;
        }
        RefreshPreview();
    }

    private static void CreateSelector(
        Transform parent,
        string name,
        string labelKey,
        float valueWidth,
        UnityAction previous,
        UnityAction next,
        out Text value)
    {
        WanfaUiFactory.CreateText(parent, name + "Label", labelKey.Localize(), 42f, 24f, 7,
            TextAnchor.MiddleRight);
        Button previousButton = WanfaUiFactory.CreateIconButton(parent, name + "Previous", BaibaoUiIcons.Previous,
            24f, 22f, previous, 4f);
        WanfaUiFactory.SetTooltip(previousButton.gameObject, "Cultiway.Baibao.UI.Action.Previous",
            "Cultiway.Baibao.UI.Tooltip.PreviousOption");
        value = WanfaUiFactory.CreateText(parent, name + "Value", string.Empty, valueWidth, 24f, 7,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        Button nextButton = WanfaUiFactory.CreateIconButton(parent, name + "Next", BaibaoUiIcons.Next, 24f, 22f,
            next, 4f);
        WanfaUiFactory.SetTooltip(nextButton.gameObject, "Cultiway.Baibao.UI.Action.Next",
            "Cultiway.Baibao.UI.Tooltip.NextOption");
    }

    private void ResetDesign()
    {
        _shapeIndex = 0;
        _qualityValueIndex = 0;
        _suppressRefresh = true;
        for (int i = 0; i < _atomToggles.Length; i++) _atomToggles[i].SetIsOnWithoutNotify(false);
        SelectFirstAtom(ArtifactAtomCategory.Material);
        SelectFirstAtom(ArtifactAtomCategory.Finish);
        _suppressRefresh = false;
    }

    private void SelectFirstAtom(ArtifactAtomCategory category)
    {
        int index = Array.FindIndex(_atoms, atom => atom.category == category);
        if (index >= 0) _atomToggles[index].SetIsOnWithoutNotify(true);
    }

    private void CycleShape(int offset)
    {
        _shapeIndex = (_shapeIndex + offset + _shapes.Length) % _shapes.Length;
        RefreshPreview();
    }

    private void CycleQuality(int offset)
    {
        _qualityValueIndex = (_qualityValueIndex + offset + 36) % 36;
        RefreshPreview();
    }

    private void OnAtomChanged()
    {
        if (_suppressRefresh) return;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_shapes.Length == 0) return;
        ArtifactComposeResult result = ArtifactComposer.ComposeDesign(BuildDesign());
        ArtifactBlueprint blueprint = ArtifactBlueprintCodec.FromComposeResult(result);
        _previewImage.sprite = BaibaoPavilionService.Instance.GetPreviewIcon(blueprint);
        _previewName.text = result.Name;
        _previewSummary.text = string.Format("Cultiway.Baibao.UI.Format.ForgePreview".Localize(),
            GetShapeName(result.Shape), result.Level.GetName(), result.Atoms.Length, result.AbilitySet.abilities.Length);
        _shapeValue.text = GetShapeName(_shapes[_shapeIndex]);
        _qualityValue.text = ItemLevel.FromValue(_qualityValueIndex).GetName();
        int selectedAtoms = _atomToggles.Count(toggle => toggle.isOn);
        _atomTitle.text = string.Format("Cultiway.Baibao.UI.Format.AtomTitle".Localize(), selectedAtoms);
        WanfaUiFactory.SetTooltip(_previewImage.gameObject, result.Name, _previewSummary.text);
    }

    private ArtifactDesignRequest BuildDesign()
    {
        return new ArtifactDesignRequest
        {
            Shape = _shapes[_shapeIndex],
            Level = ItemLevel.FromValue(_qualityValueIndex),
            Atoms = _atoms.Where((_, index) => _atomToggles[index].isOn).ToArray(),
        };
    }

    private void Save()
    {
        BaibaoSaveResult result = BaibaoPavilionService.Instance.Forge(BuildDesign());
        WindowBaibaoPavilion.ShowSaveResult(result, "Cultiway.Baibao.UI.Tip.Forged");
        if (result.Status != BaibaoSaveStatus.Invalid) CloseToPrevious();
    }

    private void CloseToPrevious()
    {
        WindowHistory.clickBack();
    }

    private static string GetShapeName(ArtifactShapeAsset shape)
    {
        return shape.ingredient_name_candidates.FirstOrDefault() ?? shape.id.Localize();
    }

    private static string GetAtomName(ArtifactAtomAsset atom)
    {
        return atom.name_stems.FirstOrDefault() ?? atom.tag ?? atom.id;
    }

    private static string GetCategoryName(ArtifactAtomCategory category)
    {
        return category switch
        {
            ArtifactAtomCategory.Material => "Cultiway.Baibao.UI.AtomCategory.Material".Localize(),
            ArtifactAtomCategory.Finish => "Cultiway.Baibao.UI.AtomCategory.Finish".Localize(),
            _ => "Cultiway.Baibao.UI.AtomCategory.Other".Localize(),
        };
    }
}
