using System.Linq;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Libraries;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>百宝阁相关窗口共用的法宝预览，顶部显示方形图像与摘要，下方显示完整构件和能力信息。</summary>
internal sealed class BaibaoArtifactPreview
{
    private readonly Image _preview;
    private readonly Button _iconMode;
    private readonly Button _worldMode;
    private readonly Text _name;
    private readonly Text _subtitle;
    private readonly Text _metrics;
    private readonly Text _details;
    private readonly Text _status;
    private ArtifactBlueprint _blueprint;
    private bool _draftPreview;
    private bool _worldPreview;

    public BaibaoArtifactPreview(Transform parent, float width, float height)
    {
        const float modeHeight = 20f;
        const float summaryHeight = 80f;
        const float statusHeight = 24f;
        const float spacing = 3f;
        float innerWidth = width - 8f;
        float frameSize = width >= 180f ? 80f : 70f;
        float informationWidth = innerWidth - frameSize - spacing;
        float detailsHeight = Mathf.Max(38f,
            height - modeHeight - summaryHeight - statusHeight - spacing * 3f);

        GameObject root = WanfaUiFactory.CreateLayout(parent, "ArtifactPreviewContent", false, width, height,
            spacing, TextAnchor.UpperCenter);
        GameObject modes = WanfaUiFactory.CreateLayout(root.transform, "PreviewModes", true, innerWidth,
            modeHeight, spacing, TextAnchor.MiddleCenter);
        float modeWidth = (innerWidth - spacing) * 0.5f;
        _iconMode = WanfaUiFactory.CreateIconTextButton(modes.transform, "Icon", BaibaoUiIcons.Pavilion,
            "Cultiway.Baibao.UI.Preview.Icon".Localize(), modeWidth, 19f, () => SetPreviewMode(false));
        _worldMode = WanfaUiFactory.CreateIconTextButton(modes.transform, "World", BaibaoUiIcons.World,
            "Cultiway.Baibao.UI.Preview.World".Localize(), modeWidth, 19f, () => SetPreviewMode(true));

        GameObject summary = WanfaUiFactory.CreateLayout(root.transform, "Summary", true, innerWidth,
            summaryHeight, spacing, TextAnchor.UpperLeft);
        GameObject previewFrame = new("Preview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        previewFrame.transform.SetParent(summary.transform, false);
        WanfaUiFactory.SetLayout(previewFrame.transform, frameSize, frameSize);
        Image frame = previewFrame.GetComponent<Image>();
        frame.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        frame.type = Image.Type.Sliced;

        GameObject imageObject = new("Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(previewFrame.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(frameSize - 8f, frameSize - 8f);
        _preview = imageObject.GetComponent<Image>();
        _preview.preserveAspect = true;

        GameObject information = WanfaUiFactory.CreateLayout(summary.transform, "Information", false,
            informationWidth, summaryHeight, 0f, TextAnchor.UpperLeft);
        _name = WanfaUiFactory.CreateText(information.transform, "Name", string.Empty, informationWidth, 30f,
            8, TextAnchor.UpperLeft, FontStyle.Bold);
        _subtitle = WanfaUiFactory.CreateText(information.transform, "Subtitle", string.Empty, informationWidth,
            18f, 6, TextAnchor.MiddleLeft);
        _metrics = WanfaUiFactory.CreateText(information.transform, "Metrics", string.Empty, informationWidth,
            32f, 6, TextAnchor.UpperLeft);

        _details = WanfaUiFactory.CreateText(root.transform, "Details", string.Empty, innerWidth, detailsHeight,
            6, TextAnchor.UpperLeft);
        _status = WanfaUiFactory.CreateText(root.transform, "Status", string.Empty, innerWidth, statusHeight, 6,
            TextAnchor.UpperLeft);
        Clear();
    }

    public void Show(ArtifactBlueprint blueprint, bool draftPreview, string validStatus, Color validColor)
    {
        _blueprint = blueprint;
        _draftPreview = draftPreview;
        _name.text = blueprint.Name;
        _subtitle.text = $"{BaibaoPresentation.GetShapeName(blueprint)}  ·  {blueprint.Level.GetName()}";
        _metrics.text = string.Format("Cultiway.Baibao.UI.Format.InspectorMetrics".Localize(),
            blueprint.MaterialData.ingredient_count, blueprint.MaterialData.stability,
            blueprint.ControlProfile.complexity);

        string atoms = string.Join("、", (blueprint.AtomData.entries ?? [])
            .Select(entry => Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.get(entry.atom_id))
            .Where(atom => atom != null && atom.category != ArtifactAtomCategory.Shape)
            .Take(3)
            .Select(BaibaoPresentation.GetAtomName));
        string abilities = string.Join("、", (blueprint.AbilitySet.abilities ?? [])
            .Take(2)
            .Select(ability => BaibaoPresentation.GetAbilityName(ability.ability_id)));
        _details.text = string.Format("Cultiway.Baibao.UI.Format.InspectorEffects".Localize(),
            string.IsNullOrEmpty(atoms) ? "Cultiway.Baibao.UI.State.None".Localize() : atoms,
            string.IsNullOrEmpty(abilities) ? "Cultiway.Baibao.UI.State.None".Localize() : abilities);

        string error = BaibaoPavilionService.Instance.Validate(blueprint);
        _status.text = error ?? validStatus;
        _status.color = error == null ? validColor : new Color(1f, 0.45f, 0.38f, 1f);
        RefreshPreview();
    }

    public void Clear()
    {
        _blueprint = null;
        _preview.sprite = null;
        _name.text = "Cultiway.Baibao.UI.State.NoSelection".Localize();
        _subtitle.text = string.Empty;
        _metrics.text = "Cultiway.Baibao.UI.Detail.SelectArtifact".Localize();
        _details.text = string.Empty;
        _status.text = string.Empty;
        BaibaoUiFactory.SetSelected(_iconMode, !_worldPreview);
        BaibaoUiFactory.SetSelected(_worldMode, _worldPreview);
    }

    private void SetPreviewMode(bool world)
    {
        _worldPreview = world;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        BaibaoUiFactory.SetSelected(_iconMode, !_worldPreview);
        BaibaoUiFactory.SetSelected(_worldMode, _worldPreview);
        if (_blueprint == null) return;

        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        _preview.sprite = _draftPreview
            ? _worldPreview ? service.GetPreviewWorldSprite(_blueprint) : service.GetPreviewIcon(_blueprint)
            : _worldPreview ? service.GetWorldSprite(_blueprint) : service.GetIcon(_blueprint);
        WanfaUiFactory.SetTooltip(_preview.gameObject, _blueprint.Name,
            _worldPreview ? "Cultiway.Baibao.UI.Tooltip.WorldPreview" :
                "Cultiway.Baibao.UI.Tooltip.IconPreview");
    }
}
