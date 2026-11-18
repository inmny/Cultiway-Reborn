using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>从真实生成部位打开映像规则；每次改动通过完整构造检查后才进入撤销记录。</summary>
    /// <param name="node">当前选中的部位声明。</param>
    private void CorrespondenceRows(ConstructionNode node)
    {
        var link = node.Correspondence;
        Row("CorrespondenceRules", T("CorrespondenceHint"));
        Switch("Correspondence", link != null, (part, enabled) => part.Correspondence = enabled
            ? new ConstructionCorrespondence { Id = "image", Target = ConstructionCorrespondenceTarget.Source }
            : null);
        if (link == null) return;
        ConnectionText("CorrespondenceId", link.Id, value => Edit(copy => copy.Correspondence.Id = value), draftChange: false);
        Choice("CorrespondenceTarget", T("CorrespondenceTarget." + link.Target), () => menu.Show(T("CorrespondenceTarget"),
            ((ConstructionCorrespondenceTarget[])Enum.GetValues(typeof(ConstructionCorrespondenceTarget))).Select(target => new UiOptionMenuOption
            {
                Label = T("CorrespondenceTarget." + target), Selected = target == link.Target,
                Select = () => Edit(copy => copy.Correspondence.Target = target)
            }).ToArray()));
        Number("CorrespondenceDamage", link.DamageFraction, (copy, value) => copy.DamageFraction = value);
        foreach (var effect in new[] { ConstructionCorrespondenceEffects.Restoration, ConstructionCorrespondenceEffects.Status, ConstructionCorrespondenceEffects.Force })
            Switch("CorrespondenceEffect." + effect, (link.Effects & effect) != 0,
                (copy, enabled) => copy.Correspondence.Effects = enabled ? copy.Correspondence.Effects | effect : copy.Correspondence.Effects & ~effect);
        Number("CorrespondenceFrom", link.ActiveFrom, (copy, value) => copy.ActiveFrom = value);
        Switch("CorrespondenceHasEnd", link.ActiveUntil.HasValue, (copy, enabled) =>
            copy.Correspondence.ActiveUntil = enabled ? session.Draft.Lifetime : null);
        if (link.ActiveUntil is float until)
            Number("CorrespondenceUntil", until, (copy, value) => copy.ActiveUntil = value);

        void Edit(Action<ConstructionNode> edit) => ApplyConstructionEdit(() =>
        {
            string path = ConstructionContactEdits.Parts(session.Draft.Root).Single(part => ReferenceEquals(part.Node, node)).Path;
            AbilityDefinition candidate = ConstructionEditing.Copy(session.Draft);
            edit(ConstructionContactEdits.Parts(candidate.Root).Single(part => part.Path == path).Node);
            AbilityCompiler.Compile(candidate, false);
            return candidate;
        });
        void Switch(string key, bool value, Action<ConstructionNode, bool> set)
        {
            var toggle = UiElements.CreateIconToggle(Row(key).UseInlineControls(32f), key, UiIcons.Options, value, 28f, 22f);
            toggle.onValueChanged.AddListener(next => Edit(copy => set(copy, next)));
        }
        void Number(string key, float value, Action<ConstructionCorrespondence, float> set) =>
            FloatInput(Row(key).UseInlineControls(104f), key, value, next => Edit(copy => set(copy.Correspondence, next)),
                true, 100f, draftChange: false);
    }
}
