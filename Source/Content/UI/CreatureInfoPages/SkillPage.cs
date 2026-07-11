using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.UI.Prefab;
using Cultiway.Content.WanfaPavilion;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.UI.Prefab;
using NeoModLoader.api.attributes;
using UnityEngine;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.UI.CreatureInfoPages;

public sealed class SkillPage : MonoBehaviour
{
    private Actor _actor;
    private MonoObjPool<SkillImportRow> _rowPool;

    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<SkillPage>();
        var root = WanfaUiFactory.CreateLayout(page.transform, "SkillPageRoot", false, 246f, 208f, 4f);
        WanfaUiFactory.CreateButton(root.transform, "ImportAll",
            "Cultiway.Wanfa.UI.Action.ImportAll".Localize(), 118f, 22f, component.ImportAll);
        var content = WanfaUiFactory.CreateScrollContent(root.transform, "Skills", 246f, 180f);
        component._rowPool = new MonoObjPool<SkillImportRow>(SkillImportRow.Prefab, content);
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
        foreach (var container in _actor.GetExtend().GetLearnedSkillsInOrder())
        {
            var current = container;
            _rowPool.GetNext().Setup(current,
                () => ImportOne(current),
                () => EditImported(current));
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
