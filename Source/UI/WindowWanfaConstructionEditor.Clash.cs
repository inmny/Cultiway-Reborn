using System;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>共同交锋规则的编辑，完整候选成立后才替换当前草稿。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>用途切换不静默清除原接触作用，冲突由共同编译给出实际字段。</summary>
    /// <param name="node">选中的实际部位。</param>
    private void ClashRows(ConstructionNode node)
    {
        Row("ClashRules", T("ClashRulesHint"));
        Choice("ClashRole", T("ClashRole." + node.ContactRole), () => menu.Show(T("ClashRole"),
            Array.ConvertAll((ConstructionContactRole[])Enum.GetValues(typeof(ConstructionContactRole)), role => new UiOptionMenuOption
            {
                Label = T("ClashRole." + role), Selected = node.ContactRole == role,
                Select = () => Commit(role: role)
            })));
        if (node.ContactRole == ConstructionContactRole.Barrier)
        {
            Choice("ClashInterception", T("ClashInterception." + node.Interception), () => menu.Show(T("ClashInterception"),
                Array.ConvertAll((ConstructionInterception[])Enum.GetValues(typeof(ConstructionInterception)), rule => new UiOptionMenuOption
                {
                    Label = T("ClashInterception." + rule), Selected = node.Interception == rule,
                    Select = () => Commit(interception: rule)
                })));
            var row = Row("ClashGroundBlock", T("ClashGroundBlockHint"));
            var toggle = UiElements.CreateIconToggle(row.UseInlineControls(32f), "ClashGroundBlock", UiIcons.Options,
                node.BlocksGroundActors, 28f, 22f);
            toggle.onValueChanged.AddListener(value => Commit(blocks: value));
            UiTooltip.Set(toggle, "Cultiway.Construction.ClashGroundBlock", "Cultiway.Construction.ClashGroundBlockHint");
        }
        if (node.ContactRole != ConstructionContactRole.Decoration)
            ContactInteger("ClashReflectionLimit", node.ReflectionLimit, value => Commit(limit: value), draftChange: false);

        void Commit(ConstructionContactRole? role = null, ConstructionInterception? interception = null,
            bool? blocks = null, int? limit = null) => ApplyConstructionEdit(() => ConstructionContactEdits.SetClash(
                session.Draft, node, role ?? node.ContactRole, interception ?? node.Interception,
                blocks ?? node.BlocksGroundActors, limit ?? node.ReflectionLimit));
    }
}
