using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.WanfaPavilion;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Prefab;

public sealed class SkillImportRow : APrefabPreview<SkillImportRow>
{
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _import;
    private Button _edit;

    protected override void OnInit()
    {
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
        _detail.text = string.Format("Cultiway.Wanfa.UI.Format.ImportDetail".Localize(), trajectory.Localize(),
            modifierCount);
        var frames = skill.Asset.PrefabEntity.GetComponent<AnimData>().frames;
        _icon.sprite = frames.Length == 0 ? null : frames[0];
        WanfaUiFactory.SetTooltip(_icon.gameObject, () => WanfaSkillTooltip.Show(_icon.gameObject, container));

        var signature = SkillContainerSignature.Build(container);
        var imported = WanfaPavilionService.Instance.ContainsSignature(signature);
        _import.interactable = !imported;
        _import.onClick.RemoveAllListeners();
        _import.onClick.AddListener(import.Invoke);
        WanfaUiFactory.SetTooltip(_import.gameObject,
            imported ? "Cultiway.Wanfa.UI.Action.Imported" : "Cultiway.Wanfa.UI.Action.Import",
            imported ? "Cultiway.Wanfa.UI.Tooltip.Imported" : "Cultiway.Wanfa.UI.Tooltip.Import");
        _edit.interactable = imported;
        _edit.onClick.RemoveAllListeners();
        _edit.onClick.AddListener(edit.Invoke);
        WanfaUiFactory.SetTooltip(_edit.gameObject, "Cultiway.Wanfa.UI.Action.Edit",
            imported ? "Cultiway.Wanfa.UI.Tooltip.EditActorSkill" : "Cultiway.Wanfa.UI.Tooltip.EditRequiresImport");
    }

    private static void _init()
    {
        var obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(SkillImportRow), true, 238f, 38f, 3f);
        obj.AddComponent<Image>().sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        obj.GetComponent<Image>().type = Image.Type.Sliced;
        obj.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(obj.transform, false);
        WanfaUiFactory.SetLayout(icon.transform, 34f, 34f);
        icon.GetComponent<Image>().preserveAspect = true;
        var labels = WanfaUiFactory.CreateLayout(obj.transform, "Labels", false, 132f, 34f, 0f);
        WanfaUiFactory.CreateText(labels.transform, "Name", string.Empty, 132f, 18f, 7, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(labels.transform, "Detail", string.Empty, 132f, 16f, 6);
        WanfaUiFactory.CreateIconButton(obj.transform, "Import", WanfaUiIcons.Import, 28f, 24f, () => { });
        WanfaUiFactory.CreateIconButton(obj.transform, "Edit", WanfaUiIcons.Edit, 28f, 24f, () => { });
        Prefab = obj.AddComponent<SkillImportRow>();
    }
}
