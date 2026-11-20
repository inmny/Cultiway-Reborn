using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>器口和时段组成完整候选。错误输入不写入草稿。</summary>
    private void WeaponChamberRows(ConstructionNode node)
    {
        var chamber = node.WeaponChamber;
        var toggle = UiElements.CreateIconToggle(Row("WeaponChamber", T("WeaponChamberHint")).UseInlineControls(32f),
            "WeaponChamber", UiIcons.Options, chamber != null, 28f, 22f);
        toggle.onValueChanged.AddListener(enabled => Edit(copy => copy.WeaponChamber = enabled ? new ConstructionWeaponChamber() : null));
        if (chamber == null) return;
        Number("WeaponChamberRadius", chamber.Radius, (copy, value) => copy.Radius = value);
        Number("WeaponChamberDepth", chamber.Depth, (copy, value) => copy.Depth = value);
        Number("WeaponChamberSpeed", chamber.ReleaseSpeed, (copy, value) => copy.ReleaseSpeed = value);

        void Number(string key, float value, Action<ConstructionWeaponChamber, float> set) =>
            FloatInput(Row(key).UseInlineControls(104f), key, value,
                next => Edit(copy => set(copy.WeaponChamber, next)), true, 100f, draftChange: false);
        void Edit(Action<ConstructionNode> edit) => ApplyConstructionEdit(() =>
        {
            string path = ConstructionContactEdits.Parts(session.Draft.Root).Single(part => ReferenceEquals(part.Node, node)).Path;
            AbilityDefinition candidate = ConstructionEditing.Copy(session.Draft);
            edit(ConstructionContactEdits.Parts(candidate.Root).Single(part => part.Path == path).Node);
            AbilityCompiler.Compile(candidate, false);
            return candidate;
        });
    }
}
