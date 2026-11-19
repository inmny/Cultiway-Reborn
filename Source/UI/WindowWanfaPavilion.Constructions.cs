using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.UI;

public sealed partial class WindowWanfaPavilion
{
    private static string ShareText(string key) => ConstructionShareNotice.Text(key);

    private void ClearConstructionMenus() => _optionMenu?.Clear();

    /// <summary>收藏显示已保存的独立修订，换世界仍从同一份本机资料读取。</summary>
    private void ShowConstructionLibrary()
    {
        var options = new List<UiOptionMenuOption>
        {
            new() { Label = ShareText("SharePaste"), IconPath = UiIcons.Add, Select = ReadConstructionClipboard }
        };
        foreach (AbilityDefinition saved in WanfaPavilionService.Instance.ConstructionFavorites
                     .OrderBy(item => item.Name, System.StringComparer.Ordinal).ThenByDescending(item => item.Revision))
        {
            options.Add(new UiOptionMenuOption
            {
                Label = ShareLabel(saved), SearchText = ShareLabel(saved) + " " + saved.Id, IconPath = UiIcons.Favorite,
                Select = () => ShowConstructionFavorite(saved)
            });
        }
        _optionMenu.Show(ShareText(options.Count == 1 ? "ShareEmptyLibrary" : "ConstructionLibrary"), options, true);
    }

    /// <summary>收藏可重新载入、分享或移出收藏；移出不删除世界法术。</summary>
    private void ShowConstructionFavorite(AbilityDefinition saved) => _optionMenu.Show(ShareLabel(saved), new[]
    {
        new UiOptionMenuOption
        {
            Label = ShareText("ShareOpenFavorite"), IconPath = UiIcons.Edit,
            Select = () => OpenSharedConstruction(ConstructionEditing.Copy(saved))
        },
        new UiOptionMenuOption
        {
            Label = ShareText("ShareCopyText"),
            Select = () => ConstructionShareNotice.Run(() => ConstructionShareNotice.Copy(saved))
        },
        new UiOptionMenuOption
        {
            Label = ShareText("ShareRemoveFavorite"), IconPath = UiIcons.Favorite,
            Select = () => ConstructionShareNotice.Run(() =>
            {
                WanfaPavilionService.Instance.SetConstructionFavorite(saved, false);
                ConstructionShareNotice.Show("ShareFavoriteRemoved");
                ShowConstructionLibrary();
            })
        }
    });

    /// <summary>读取粘贴时的一份资料，确认阶段不再读取可能变化的剪贴板。</summary>
    private void ReadConstructionClipboard() => ConstructionShareNotice.Run(() =>
    {
        AbilityDefinition candidate = ConstructionShare.Read(GUIUtility.systemCopyBuffer);
        _optionMenu.Show(ShareLabel(candidate), new[]
        {
            new UiOptionMenuOption
            {
                Label = ShareText("ShareConfirmImport"), IconPath = UiIcons.Confirm,
                Select = () => OpenSharedConstruction(candidate)
            }
        });
    });

    /// <summary>成功后打开正式修订；导入没有隐含的学习或覆盖动作。</summary>
    private void OpenSharedConstruction(AbilityDefinition definition) => ConstructionShareNotice.Run(() =>
    {
        bool added = ConstructionEditing.Access.Import(definition);
        WindowWanfaConstructionEditor.Open(definition.Id,
            ControllableUnit.isControllingUnit() ? ControllableUnit.getControllableUnit() : null);
        ConstructionShareNotice.Show(added ? "ShareImported" : "ShareAlreadyPresent");
    });

    private static string ShareLabel(AbilityDefinition definition) =>
        string.Format(ShareText("ShareRevisionLabel"), definition.Name, definition.Revision);
}
