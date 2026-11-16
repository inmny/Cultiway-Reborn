using System;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>人物接触规则按真实共用组修改，世界地块继续使用各自的规则。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>身体与部位共用一套频率和次数表单，候选成立才替换草稿。</summary>
    /// <param name="policy">当前声明的明确规则。</param><param name="node">实际部位，空值表示身体。</param>
    /// <param name="prefix">身体或部位前缀。</param>
    private void ContactPolicyRows(ConstructionContactPolicy policy, ConstructionNode node, string prefix)
    {
        // 入口同时修改真实共享范围；未启用时从实际继承规则建立副本。
        var row = Row(prefix + "ActorContactPolicy", T("ActorContactPolicyHint"));
        var toggle = UiElements.CreateIconToggle(row.UseInlineControls(32f), prefix + "ActorContactPolicy", UiIcons.Options, policy != null, 28f, 22f);
        toggle.onValueChanged.AddListener(enabled => ApplyConstructionEdit(() => ConstructionContactEdits.Set(session.Draft,
            enabled ? ConstructionContactEdits.Read(session.Draft, node) : null, node)));
        UiTooltip.Set(toggle, "Cultiway.Construction." + prefix + "ActorContactPolicy", "Cultiway.Construction.ActorContactPolicyHint");
        if (policy == null) return;
        Choice(prefix + "ActorContactMode", T("ActorContactMode." + policy.Mode), () => menu.Show(T(prefix + "ActorContactMode"),
            Array.ConvertAll((ConstructionContactMode[])Enum.GetValues(typeof(ConstructionContactMode)), mode => new UiOptionMenuOption
            {
                Label = T("ActorContactMode." + mode), Selected = mode == policy.Mode,
                Select = () => Edit(value => value.Mode = mode)
            })));
        if (policy.Mode is ConstructionContactMode.Interval or ConstructionContactMode.Pulse)
        {
            var interval = Row(prefix + "ActorContactInterval");
            FloatInput(interval.UseInlineControls(104f), prefix + "ActorContactInterval", policy.Interval,
                next => Edit(value => value.Interval = next), true, 100f, draftChange: false);
        }
        ContactInteger(prefix + "ActorContactPerTarget", policy.MaximumPerTarget, next => Edit(value => value.MaximumPerTarget = next), draftChange: false);
        ContactInteger(prefix + "ActorContactTotal", policy.MaximumTotal, next => Edit(value => value.MaximumTotal = next), draftChange: false);

        // 数值先写独立候选，非法输入不污染草稿或已经保存的同组声明。
        void Edit(Action<ConstructionContactPolicy> edit) => ApplyConstructionEdit(() =>
        {
            var next = new ConstructionContactPolicy
            {
                Mode = policy.Mode, Interval = policy.Interval,
                MaximumPerTarget = policy.MaximumPerTarget, MaximumTotal = policy.MaximumTotal
            };
            edit(next);
            return ConstructionContactEdits.Set(session.Draft, next, node);
        });
    }
}
