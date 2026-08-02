using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using NeoModLoader.api.attributes;
using UnityEngine;
using Cultiway.Utils.Extension;

namespace Cultiway.UI.CreatureInfoPages;

public sealed class SkillPage : MonoBehaviour
{
    private Actor _actor;
    private MonoObjPool<SkillImportRow> _rowPool;
    private GameObject _importAll;

    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<SkillPage>();
        var root = UiLayout.Create(page.transform, "SkillPageRoot", false, 246f, 208f, 4f);
        var importAll = UiElements.CreateIconTextButton(root.transform, "ImportAll", UiIcons.Import,
            "Cultiway.Wanfa.UI.Action.ImportAll".Localize(), 132f, 22f, component.ImportAll);
        component._importAll = importAll.gameObject;
        UiTooltip.Set(importAll.gameObject, "Cultiway.Wanfa.UI.Action.ImportAll",
            "Cultiway.Wanfa.UI.Tooltip.ImportAll");
        UiScrollPane skills = UiScrollPane.CreateVertical(root.transform, "Skills", 246f, 180f);
        component._rowPool = new MonoObjPool<SkillImportRow>(SkillImportRow.Prefab, skills.Content);
    }

    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var component = page.GetComponent<SkillPage>();
        component._actor = actor;
        component.Refresh();
    }

    private void Refresh()
    {
        _rowPool.Clear();
        ActorExtend actorExtend = _actor.GetExtend();
        var learnedSkills = actorExtend.GetLearnedSkillsInOrder();
        _importAll.SetActive(learnedSkills.Count > 0);
        foreach (var container in learnedSkills)
        {
            var current = container;
            _rowPool.GetNext().Setup(current,
                () => ImportOne(current),
                () => EditImported(current));
        }
        using var grantedSkills = new ListPool<SourceGrantedSkillPresentation>();
        SourceGrantedSkillService.Collect(actorExtend, grantedSkills);
        for (var i = 0; i < grantedSkills.Count; i++)
        {
            SourceGrantedSkillPresentation granted = grantedSkills[i];
            if (granted.SkillContainer.IsNull) continue;
            string detail = string.IsNullOrEmpty(granted.DetailLocaleKey)
                ? string.Empty
                : granted.DetailLocaleKey.Localize();
            _rowPool.GetNext().SetupReadOnly(granted.SkillContainer, detail);
        }
    }

    private void ImportOne(Friflo.Engine.ECS.Entity container)
    {
        var result = WanfaPavilionService.Instance.Import(_actor, container);
        if (result.Status == WanfaPavilionSaveStatus.Saved)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.Imported".Localize(), false, "top", 2f);
        }
        else if (result.Status == WanfaPavilionSaveStatus.Invalid)
        {
            var error = result.Validation.Issues.First(issue =>
                issue.Severity == Core.SkillLibV3.Editor.SkillValidationSeverity.Error);
            WorldTip.showNow(error.Message, false, "top", 3f);
        }
        Refresh();
    }

    private void ImportAll()
    {
        var skills = _actor.GetExtend().GetLearnedSkillsInOrder().ToArray();
        var imported = 0;
        var failed = 0;
        foreach (var container in skills)
        {
            var signature = SkillContainerSignature.Build(container);
            if (WanfaPavilionService.Instance.ContainsSignature(signature)) continue;
            var result = WanfaPavilionService.Instance.Import(_actor, container);
            if (result.Status == WanfaPavilionSaveStatus.Saved)
            {
                imported++;
            }
            else
            {
                failed++;
            }
        }
        var message = failed == 0
            ? string.Format("Cultiway.Wanfa.UI.Format.ImportSuccess".Localize(), imported)
            : string.Format("Cultiway.Wanfa.UI.Format.ImportPartial".Localize(), imported, failed);
        WorldTip.showNow(message, false, "top", 3f);
        Refresh();
    }

    private void EditImported(Friflo.Engine.ECS.Entity container)
    {
        var blueprint = WanfaPavilionService.Instance.FindBySignature(SkillContainerSignature.Build(container));
        if (blueprint != null) WindowWanfaSkillEditor.OpenForActor(blueprint, _actor, container);
    }
}
