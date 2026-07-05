using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

public class SectTraitsEditor : TraitsEditor<SectTrait, SectTraitButton, SectTraitEditorButton, SectTraitGroupAsset, SectTraitGroupElement>
{
    private const string SectIconPath = "cultiway/icons/iconSect";
    private const string TraitTabTitleKey = "tab_sect_trait_editor";
    private const string TraitTabDescriptionKey = "tab_sect_trait_editor_description";
    private const string TraitTitleKey = "traits_sect";
    private const string TraitEditorDescriptionKey = "traits_sect_editor_description";

    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    public override List<SectTraitGroupAsset> augmentation_groups_list => ModClass.L.SectTraitGroupLibrary.list;

    public override List<SectTrait> all_augmentations_list => ModClass.L.SectTraitLibrary.list;

    internal static void Setup(SectWindow window, Transform content)
    {
        Transform traitsContent = content.Find("content_traits_editor")
                                  ?? throw new InvalidOperationException("SectWindow 缺少原版 content_traits_editor");
        var replacedButtons = new List<KingdomTraitButton>();

        ConfigureTraitTab(window);
        ConfigureTraitTitle(traitsContent);
        ConfigureTraitLocalizedTexts(window);
        ReplaceEditors(window, replacedButtons);
        ReplaceContainers(window, replacedButtons);
        ReplaceSelectedContainers(window, replacedButtons);
        DestroyReplacedButtons(replacedButtons);
    }

    private static void ConfigureTraitTab(SectWindow window)
    {
        WindowMetaTab[] tabs = window.GetComponentsInChildren<WindowMetaTab>(true);
        for (int i = 0; i < tabs.Length; i++)
        {
            WindowMetaTab tab = tabs[i];
            TipButton tipButton = tab.GetComponent<TipButton>();
            if (tipButton == null || tipButton.textOnClick != "tab_kingdom_trait_editor") continue;

            tipButton.textOnClick = TraitTabTitleKey;
            tipButton.textOnClickDescription = TraitTabDescriptionKey;
            tipButton.text_description_2 = string.Empty;
            UpdateTabWorldTip(tab);
        }
    }

    private static void ConfigureTraitTitle(Transform traitsContent)
    {
        Transform title = traitsContent.Find("tab_title_container_kingdom")
                          ?? traitsContent.Find("tab_title_container_traits");
        if (title == null) return;

        title.name = "tab_title_container_sect_traits";
        title.Find("title_tab")?.GetComponent<LocalizedText>()?.setKeyAndUpdate(TraitTitleKey);

        Sprite icon = SpriteTextureLoader.getSprite(SectIconPath);
        SetImageSprite(title.Find("icon_left"), icon);
        SetImageSprite(title.Find("icon_right"), icon);
    }

    private static void ConfigureTraitLocalizedTexts(SectWindow window)
    {
        LocalizedText[] localizedTexts = window.GetComponentsInChildren<LocalizedText>(true);
        for (int i = 0; i < localizedTexts.Length; i++)
        {
            LocalizedText localizedText = localizedTexts[i];
            switch (localizedText.key)
            {
                case "traits_kingdom":
                case "traits_culture":
                    localizedText.setKeyAndUpdate(TraitTitleKey);
                    break;
                case "tab_kingdom_trait_editor":
                    localizedText.setKeyAndUpdate(TraitTabTitleKey);
                    break;
                case "traits_kingdom_editor_description":
                    localizedText.setKeyAndUpdate(TraitEditorDescriptionKey);
                    break;
            }
        }
    }

    private static void ReplaceEditors(SectWindow window, List<KingdomTraitButton> replacedButtons)
    {
        KingdomTraitsEditor[] editors = window.GetComponentsInChildren<KingdomTraitsEditor>(true);
        for (int i = 0; i < editors.Length; i++)
        {
            ReplaceEditor(editors[i], replacedButtons);
        }
    }

    private static void ReplaceEditor(KingdomTraitsEditor source, List<KingdomTraitButton> replacedButtons)
    {
        SectTraitsEditor target = source.GetComponent<SectTraitsEditor>() ?? source.gameObject.AddComponent<SectTraitsEditor>();
        source.CopyCompatibleSerializedFieldsTo(target);

        target.prefab_augmentation = ReplaceButton(source.prefab_augmentation, replacedButtons);
        target.prefab_editor_augmentation = ReplaceEditorButton(source.prefab_editor_augmentation, replacedButtons);
        target.prefab_augmentation_group = ReplaceGroup(source.prefab_augmentation_group);
        target.window_title_text?.setKeyAndUpdate(TraitTabTitleKey);

        Object.DestroyImmediate(source);
    }

    private static void ReplaceContainers(SectWindow window, List<KingdomTraitButton> replacedButtons)
    {
        KingdomTraitsContainer[] containers = window.GetComponentsInChildren<KingdomTraitsContainer>(true);
        for (int i = 0; i < containers.Length; i++)
        {
            ReplaceContainer(containers[i], replacedButtons);
        }
    }

    private static void ReplaceContainer(KingdomTraitsContainer source, List<KingdomTraitButton> replacedButtons)
    {
        SectTraitsContainer target = source.GetComponent<SectTraitsContainer>() ?? source.gameObject.AddComponent<SectTraitsContainer>();
        source.CopyCompatibleSerializedFieldsTo(target);

        target._prefab_trait = ReplaceButton(source._prefab_trait, replacedButtons);

        Object.DestroyImmediate(source);
    }

    private static void ReplaceSelectedContainers(SectWindow window, List<KingdomTraitButton> replacedButtons)
    {
        KingdomSelectedContainerTraits[] containers = window.GetComponentsInChildren<KingdomSelectedContainerTraits>(true);
        for (int i = 0; i < containers.Length; i++)
        {
            ReplaceSelectedContainer(containers[i], replacedButtons);
        }
    }

    private static void ReplaceSelectedContainer(KingdomSelectedContainerTraits source, List<KingdomTraitButton> replacedButtons)
    {
        SectSelectedContainerTraits target = source.GetComponent<SectSelectedContainerTraits>() ?? source.gameObject.AddComponent<SectSelectedContainerTraits>();
        source.CopyCompatibleSerializedFieldsTo(target);

        target._prefab_trait = ReplaceButton(source._prefab_trait, replacedButtons);

        Object.DestroyImmediate(source);
    }

    private static SectTraitButton ReplaceButton(KingdomTraitButton source, List<KingdomTraitButton> replacedButtons)
    {
        if (source == null) return null;

        SectTraitButton target = source.GetComponent<SectTraitButton>() ?? source.gameObject.AddComponent<SectTraitButton>();
        if (!replacedButtons.Contains(source))
        {
            replacedButtons.Add(source);
        }

        return target;
    }

    private static SectTraitEditorButton ReplaceEditorButton(KingdomTraitEditorButton source, List<KingdomTraitButton> replacedButtons)
    {
        if (source == null) return null;

        SectTraitEditorButton target = source.GetComponent<SectTraitEditorButton>() ?? source.gameObject.AddComponent<SectTraitEditorButton>();
        target.selected_icon = source.selected_icon;
        target.augmentation_button = ReplaceButton(source.augmentation_button, replacedButtons);

        Object.DestroyImmediate(source);
        return target;
    }

    private static SectTraitGroupElement ReplaceGroup(KingdomTraitGroupElement source)
    {
        if (source == null) return null;

        SectTraitGroupElement target = source.GetComponent<SectTraitGroupElement>() ?? source.gameObject.AddComponent<SectTraitGroupElement>();
        source.CopyCompatibleSerializedFieldsTo(target);

        Object.DestroyImmediate(source);
        return target;
    }

    private static void DestroyReplacedButtons(List<KingdomTraitButton> replacedButtons)
    {
        for (int i = 0; i < replacedButtons.Count; i++)
        {
            KingdomTraitButton button = replacedButtons[i];
            if (button != null)
            {
                Object.DestroyImmediate(button);
            }
        }
    }

    private static void SetImageSprite(Transform transform, Sprite sprite)
    {
        Image image = transform?.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }
    }

    private static void UpdateTabWorldTip(WindowMetaTab tab)
    {
        TipButton tipButton = tab.GetComponent<TipButton>();
        string worldTip = LocalizedTextManager.getText(tipButton.textOnClick);
        if (!string.IsNullOrEmpty(tipButton.textOnClickDescription))
        {
            worldTip = worldTip + "\n<size=9>" + LocalizedTextManager.getText(tipButton.textOnClickDescription) + "</size>";
        }

        tab._worldtip_text = worldTip;
    }
}
