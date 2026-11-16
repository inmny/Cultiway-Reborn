using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.UI;

/// <summary>范围及介质分送编辑复用普通接触结果，不引入专用法术。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前选中的独立分送条目，只属于窗口。</summary>
    private int contactSpreadSelection;

    /// <summary>先选择分送，再编辑实际范围和人物结果。</summary>
    /// <param name="impact">当前作用。</param><param name="prefix">作用所属。</param>
    private void ContactSpreadRows(ConstructionImpact impact, string prefix)
    {
        var spreads = impact.Spreads ?? Array.Empty<ConstructionContactSpread>();
        if (spreads.Length < 8) ActionRow(prefix + "AddContactSpread", "", () => Change(() =>
        {
            impact.Spreads = spreads.Concat(new[] { new ConstructionContactSpread { Radius = 3f, Outcome = new ConstructionContactOutcome() } }).ToArray();
            contactSpreadSelection = spreads.Length;
        }));
        if (spreads.Length == 0) return;
        contactSpreadSelection = Math.Min(contactSpreadSelection, spreads.Length - 1);
        Choice(prefix + "ContactSpread", (contactSpreadSelection + 1).ToString(), () => menu.Show(T(prefix + "ContactSpread"),
            Enumerable.Range(0, spreads.Length).Select(index => new UiOptionMenuOption
            {
                Label = (index + 1).ToString(), Selected = index == contactSpreadSelection,
                Select = () => { contactSpreadSelection = index; Refresh(); }
            }).ToArray()));
        ActionRow(prefix + "RemoveContactSpread", "", () => Change(() => impact.Spreads = spreads.Where((_, index) => index != contactSpreadSelection).ToArray()));
        var spread = spreads[contactSpreadSelection];
        Toggle(prefix + "SpreadMedium", spread.Medium != null, next =>
        {
            spread.Radius = next ? 0f : 3f;
            spread.Medium = next ? new ConstructionSpreadMedium { Channel = "contact" } : null;
        });
        if (spread.Medium == null) Number(prefix + "SpreadRadius", spread.Radius, next => spread.Radius = next);
        else SpreadMediumRows(spread.Medium, prefix);
        Number(prefix + "SpreadBuildingDamage", spread.BuildingDamagePerForce, next => spread.BuildingDamagePerForce = next);
        Number(prefix + "SpreadVisualSeconds", spread.VisualSeconds, next => spread.VisualSeconds = next);
        Toggle(prefix + "SpreadOutcome", spread.Outcome != null, next => spread.Outcome = next ? new ConstructionContactOutcome() : null);
        if (spread.Outcome != null) ContactOutcomeRows(spread.Outcome, prefix);
    }

    /// <summary>介质按明确通道及真实连通形状选取，不用半径冒充介质。</summary>
    /// <param name="medium">独立介质选择。</param><param name="prefix">作用所属。</param>
    private void SpreadMediumRows(ConstructionSpreadMedium medium, string prefix)
    {
        ConnectionText(prefix + "SpreadChannel", medium.Channel, next => medium.Channel = next);
        ContactInteger(prefix + "SpreadTargets", medium.MaximumTargets, next => medium.MaximumTargets = next);
        Toggle(prefix + "SpreadOnce", medium.OncePerCast, next => medium.OncePerCast = next);
        Toggle(prefix + "SpreadInherit", medium.InheritContactEffects, next => medium.InheritContactEffects = next);
        Toggle(prefix + "SpreadMatterFilter", medium.Matter != null, next => { medium.Matter = next ? "water" : null; medium.State = null; });
        if (medium.Matter != null)
        {
            var matter = AbilityMatters.Resolve(medium.Matter, medium.State, "编辑介质");
            Choice(prefix + "SpreadMatter", MaterialName(matter), () => menu.Show(T(prefix + "SpreadMatter"), AbilityMatters.All.Select(item => new UiOptionMenuOption
            {
                Label = MaterialName(item), Selected = item.Id == matter.Id && item.State == matter.State,
                Select = () => Change(() => { medium.Matter = item.Id; medium.State = item.State; })
            }).ToArray()));
        }
        string[] links = FormResources.Ids.Where(id => FormResources.Load(id).Span != null).ToArray();
        if (links.Length == 0) return;
        Toggle(prefix + "SpreadLink", medium.LinkForm != null, next => medium.LinkForm = next ? links[0] : null);
        if (medium.LinkForm == null) return;
        Choice(prefix + "SpreadLinkForm", medium.LinkForm, () => menu.Show(T(prefix + "SpreadLinkForm"), links.Select(id => new UiOptionMenuOption
        {
            Label = id, Selected = medium.LinkForm == id, Select = () => Change(() => medium.LinkForm = id)
        }).ToArray()));
        Number(prefix + "SpreadLinkRadius", medium.LinkRadius, next => medium.LinkRadius = next);
    }
}
