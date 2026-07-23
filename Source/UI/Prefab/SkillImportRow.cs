using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using Cultiway.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public sealed class SkillImportRow : APrefabPreview<SkillImportRow>
{
    private UiListRowChrome _chrome;
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _import;
    private Button _edit;

    protected override void OnInit()
    {
        _chrome = UiListRowChrome.From(gameObject);
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _import = transform.Find("Import").GetComponent<Button>();
        _edit = transform.Find("Edit").GetComponent<Button>();
    }

    public void Setup(Entity container, Action import, Action edit)
    {
        Init();
        var skill = container.GetComponent<SkillContainer>();
        _name.text = container.HasName ? container.Name.value : skill.SkillEntityAssetID.Localize();
        var trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(container);
        var modifierCount = container.GetComponentTypes().Count(type =>
        {
            if (!typeof(IModifier).IsAssignableFrom(type) || type == typeof(Trajectory)) return false;
            var modifier = ModClass.I.SkillV3.ModifierLib.GetByComponentType(type);
            return modifier != null && !modifier.EditorDerived;
        });
        var itemLevel = container.GetComponent<ItemLevel>();
        _detail.text = string.Format("Cultiway.Wanfa.UI.Format.ImportDetail".Localize(),
            SkillCastResourceFormatter.FormatItemLevel(skill.CastResourceRequirement, itemLevel),
            trajectory.Localize(), modifierCount);
        var frames = skill.Asset.GetAnimation(skill.AnimationIndex).Runtime.Frames;
        _icon.sprite = frames.Length == 0 ? null : frames[0];
        UiTooltip.Set(_icon.gameObject, () => SkillTooltip.Show(_icon.gameObject, container));

        var signature = SkillContainerSignature.Build(container);
        var imported = WanfaPavilionService.Instance.ContainsSignature(signature);
        _chrome.SetState(imported ? UiControlState.Selected : UiControlState.Normal);
        _import.interactable = !imported;
        _import.onClick.RemoveAllListeners();
        _import.onClick.AddListener(import.Invoke);
        UiTooltip.Set(_import.gameObject,
            imported ? "Cultiway.Wanfa.UI.Action.Imported" : "Cultiway.Wanfa.UI.Action.Import",
            imported ? "Cultiway.Wanfa.UI.Tooltip.Imported" : "Cultiway.Wanfa.UI.Tooltip.Import");
        _edit.interactable = imported;
        _edit.onClick.RemoveAllListeners();
        _edit.onClick.AddListener(edit.Invoke);
        UiTooltip.Set(_edit.gameObject, "Cultiway.Wanfa.UI.Action.Edit",
            imported ? "Cultiway.Wanfa.UI.Tooltip.EditActorSkill" : "Cultiway.Wanfa.UI.Tooltip.EditRequiresImport");
    }

    private static void _init()
    {
        var obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(SkillImportRow), true, 238f, 38f, 3f);
        UiListRowChrome.Attach(obj, false);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(icon.transform, 34f, 34f);
        icon.GetComponent<Image>().preserveAspect = true;
        var labels = UiLayout.Create(obj.transform, "Labels", false, 132f, 34f, 0f);
        UiElements.CreateText(labels.transform, "Name", string.Empty, 132f, 18f, 7, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 132f, 16f, 6);
        UiElements.CreateIconButton(obj.transform, "Import", UiIcons.Import, 28f, 24f, () => { });
        UiElements.CreateIconButton(obj.transform, "Edit", UiIcons.Edit, 28f, 24f, () => { });
        Prefab = obj.AddComponent<SkillImportRow>();
    }
}
