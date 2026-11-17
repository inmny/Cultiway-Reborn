using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>根连接的完整候选编辑，位置、转移接触和真实输入共用原执行。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>手部要求真实身体，两点位置使用明确比例；无效候选不进入草稿。</summary>
    private void BindingRows()
    {
        ConstructionBinding binding = session.Draft.Binding ?? new ConstructionBinding();
        Choice("Anchor", T("Anchor." + binding.Anchor), () => menu.Show(T("Anchor"),
            new[] { AbilityAnchor.Source, AbilityAnchor.Target, AbilityAnchor.Between, AbilityAnchor.Hand, AbilityAnchor.Effector, AbilityAnchor.Carrier }
                .Where(anchor => anchor is not (AbilityAnchor.Hand or AbilityAnchor.Effector) || session.Draft.Body != null)
                .Select(anchor => new UiOptionMenuOption
                {
                    Label = T("Anchor." + anchor), Selected = binding.Anchor == anchor,
                    Select = () => EditBinding(next =>
                    {
                        next.Anchor = anchor;
                        if (anchor == AbilityAnchor.Between) next.Fraction ??= .5f;
                    })
                }).ToArray()));
        if (binding.Anchor == AbilityAnchor.Between)
        {
            Row("BetweenRules", T("BetweenRulesHint"));
            BindingNumber("BetweenFraction", binding.Fraction ?? .5f, (next, value) => next.Fraction = value);
        }
        BindingToggle("Follow", binding.Follow, (next, value) => next.Follow = value);
        if (binding.Follow && binding.Anchor is AbilityAnchor.Source or AbilityAnchor.Target or AbilityAnchor.Between)
        {
            Row("DestinationContactRules", T("DestinationContactHint"));
            BindingToggle("DestinationContact", binding.ContactAtDestination, (next, value) => next.ContactAtDestination = value);
        }
        BindingToggle("FaceTarget", binding.FaceTarget, (next, value) => next.FaceTarget = value);
        BindingToggle("ScaleByBody", binding.ScaleByBody, (next, value) => next.ScaleByBody = value);
        BindingNumber("BindingHeight", binding.Height, (next, value) => next.Height = value);
    }

    /// <summary>完整连接通过共同编译后才替换草稿；不静默关掉原有转移规则。</summary>
    /// <param name="edit">本次明确修改。</param>
    private void EditBinding(Action<ConstructionBinding> edit) => ApplyConstructionEdit(() =>
    {
        AbilityDefinition candidate = ConstructionEditing.Copy(session.Draft);
        candidate.Binding ??= new ConstructionBinding();
        edit(candidate.Binding);
        AbilityCompiler.Compile(candidate, false);
        return candidate;
    });

    /// <summary>开关自身负责一次候选命令，不套入第二次草稿历史。</summary>
    /// <param name="key">本地化与行名。</param><param name="value">当前数值。</param><param name="set">候选修改。</param>
    private void BindingToggle(string key, bool value, Action<ConstructionBinding, bool> set)
    {
        var row = Row(key);
        var toggle = UiElements.CreateIconToggle(row.UseInlineControls(32f), key, UiIcons.Options, value, 28f, 22f);
        toggle.onValueChanged.AddListener(next => EditBinding(candidate => set(candidate, next)));
    }

    /// <summary>数值不提前写入草稿。</summary>
    /// <param name="key">本地化与输入名。</param><param name="value">当前数值。</param><param name="set">候选修改。</param>
    private void BindingNumber(string key, float value, Action<ConstructionBinding, float> set)
    {
        var row = Row(key);
        FloatInput(row.UseInlineControls(104f), key, value, next => EditBinding(candidate => set(candidate, next)),
            true, 100f, draftChange: false);
    }
}
