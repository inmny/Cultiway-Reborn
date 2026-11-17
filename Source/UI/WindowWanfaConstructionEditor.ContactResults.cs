using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>选择普通作用、随机候选或条件分支，共用同一组结果参数。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>每组作用独立选择当前展示的结果；只改变窗口，不写入法术。</summary>
    private readonly Dictionary<string, int> contactPanels = new();
    /// <summary>当前附加伤害、随机候选、条件条目及后续结果。</summary>
    private int contactDamageSelection, randomOutcomeSelection, conditionalSelection, conditionalResultSelection;

    /// <summary>原作用保持默认展示，其他分支按需展开，避免堆叠重复表单。</summary>
    /// <param name="impact">独立作用资料。</param><param name="prefix">整招、身体或部位。</param>
    private void ContactResultRows(ConstructionImpact impact, string prefix)
    {
        int selected = contactPanels.TryGetValue(prefix, out int value) ? value : 0;
        Choice(prefix + "ContactPanel", T("ContactPanel." + selected), () => menu.Show(T(prefix + "ContactPanel"),
            Enumerable.Range(0, 6).Select(index => new UiOptionMenuOption
            {
                Label = T("ContactPanel." + index), Selected = index == selected,
                Select = () => { contactPanels[prefix] = index; Refresh(); }
            }).ToArray()));
        if (selected == 0)
        {
            ContactRestorationRows(impact.Restoration, next => impact.Restoration = next, prefix);
            ContactStatusRows(impact.Statuses, next => impact.Statuses = next, prefix);
            ContactDisplacementRows(impact, prefix);
            ContactDamageRows(impact.AdditionalDamage, next => impact.AdditionalDamage = next, prefix);
        }
        else if (selected == 1) RandomContactRows(impact, prefix);
        else if (selected == 2) ConditionalContactRows(impact, prefix);
        else if (selected == 3) ContactSpreadRows(impact, prefix);
        else if (selected == 4) WorldContactRows(impact, prefix);
        else ContactAgingRows(impact, prefix);
    }

    /// <summary>选择和增删独立伤害，受伤者与攻击归属分别保存。</summary>
    /// <param name="damage">当前数组。</param><param name="set">替换数组。</param><param name="prefix">作用所属。</param>
    private void ContactDamageRows(ConstructionContactDamage[] damage, Action<ConstructionContactDamage[]> set, string prefix)
    {
        damage ??= Array.Empty<ConstructionContactDamage>();
        if (damage.Length < 8) ActionRow(prefix + "AddContactDamage", "", () => Change(() =>
        {
            set(damage.Concat(new[] { new ConstructionContactDamage { PerForce = .1f } }).ToArray());
            contactDamageSelection = damage.Length;
        }));
        if (damage.Length == 0) return;
        contactDamageSelection = Math.Min(contactDamageSelection, damage.Length - 1);
        Choice(prefix + "ContactDamage", (contactDamageSelection + 1).ToString(), () => menu.Show(T(prefix + "ContactDamage"),
            Enumerable.Range(0, damage.Length).Select(index => new UiOptionMenuOption
            {
                Label = (index + 1).ToString(), Selected = index == contactDamageSelection,
                Select = () => { contactDamageSelection = index; Refresh(); }
            }).ToArray()));
        ActionRow(prefix + "RemoveContactDamage", "", () => Change(() => set(damage.Where((_, index) => index != contactDamageSelection).ToArray())));
        ConstructionContactDamage item = damage[contactDamageSelection];
        ImpactOption(prefix, "ContactDamageTarget", item.Target, next => item.Target = next);
        ImpactOption(prefix, "ContactDamageSource", item.Source, next => item.Source = next);
        Number(prefix + "ContactDamagePerForce", item.PerForce, next => item.PerForce = next);
        Number(prefix + "ContactDamagePerHealth", item.PerHealth, next => item.PerHealth = next);
        Number(prefix + "ContactDamagePerMaxHealth", item.PerMaxHealth, next => item.PerMaxHealth = next);
        Number(prefix + "ContactDamageMinimum", item.MinimumBonus, next => item.MinimumBonus = next);
        Toggle(prefix + "ContactDamageIgnoreReduction", item.IgnoreDamageReduction, next => item.IgnoreDamageReduction = next);
        ContactElementRows(prefix, "ContactDamageElement", item.Element, next => item.Element = next);
    }

    /// <summary>真实命中时只抽取一个候选，窗口不代替游戏抽取。</summary>
    /// <param name="impact">独立作用。</param><param name="prefix">作用所属。</param>
    private void RandomContactRows(ConstructionImpact impact, string prefix)
    {
        Toggle(prefix + "RandomContact", impact.RandomContact != null, next => impact.RandomContact = next
            ? new ConstructionRandomContact { Outcomes = new[] { new ConstructionContactOutcome() } } : null);
        if (impact.RandomContact == null) return;
        ConstructionRandomContact random = impact.RandomContact;
        Number(prefix + "RandomChance", random.Chance, next => random.Chance = next);
        if (random.Outcomes.Length < 16) ActionRow(prefix + "AddRandomOutcome", "", () => Change(() =>
        {
            random.Outcomes = random.Outcomes.Concat(new[] { new ConstructionContactOutcome() }).ToArray();
            randomOutcomeSelection = random.Outcomes.Length - 1;
        }));
        randomOutcomeSelection = Math.Min(randomOutcomeSelection, random.Outcomes.Length - 1);
        Choice(prefix + "RandomOutcome", (randomOutcomeSelection + 1).ToString(), () => menu.Show(T(prefix + "RandomOutcome"),
            Enumerable.Range(0, random.Outcomes.Length).Select(index => new UiOptionMenuOption
            {
                Label = (index + 1).ToString(), Selected = index == randomOutcomeSelection,
                Select = () => { randomOutcomeSelection = index; Refresh(); }
            }).ToArray()));
        ActionRow(prefix + "RemoveRandomOutcome", "", () => Change(() =>
        {
            if (random.Outcomes.Length == 1) impact.RandomContact = null;
            else random.Outcomes = random.Outcomes.Where((_, index) => index != randomOutcomeSelection).ToArray();
        }));
        ContactOutcomeRows(random.Outcomes[randomOutcomeSelection], prefix);
    }

    /// <summary>生命条件和真实伤害后续只编辑普通结果，不建立固定连招。</summary>
    /// <param name="impact">独立作用。</param><param name="prefix">作用所属。</param>
    private void ConditionalContactRows(ConstructionImpact impact, string prefix)
    {
        ConstructionConditionalContact[] rules = impact.ConditionalContacts ?? Array.Empty<ConstructionConditionalContact>();
        if (rules.Length < 8) ActionRow(prefix + "AddConditionalContact", "", () => Change(() =>
        {
            impact.ConditionalContacts = rules.Concat(new[] { new ConstructionConditionalContact { MaximumHealthRatio = .5f, Outcome = new ConstructionContactOutcome() } }).ToArray();
            conditionalSelection = rules.Length;
        }));
        if (rules.Length == 0) return;
        conditionalSelection = Math.Min(conditionalSelection, rules.Length - 1);
        Choice(prefix + "ConditionalContact", (conditionalSelection + 1).ToString(), () => menu.Show(T(prefix + "ConditionalContact"),
            Enumerable.Range(0, rules.Length).Select(index => new UiOptionMenuOption
            {
                Label = (index + 1).ToString(), Selected = index == conditionalSelection,
                Select = () => { conditionalSelection = index; Refresh(); }
            }).ToArray()));
        ActionRow(prefix + "RemoveConditionalContact", "", () => Change(() => impact.ConditionalContacts = rules.Where((_, index) => index != conditionalSelection).ToArray()));
        ConstructionConditionalContact rule = rules[conditionalSelection];
        Toggle(prefix + "ContactHealthCondition", rule.MaximumHealthRatio.HasValue, next =>
        {
            rule.MaximumHealthRatio = next ? .5f : null;
            if (!next) rule.Otherwise = null;
        });
        if (rule.MaximumHealthRatio.HasValue) Number(prefix + "ContactHealthRatio", rule.MaximumHealthRatio.Value, next => rule.MaximumHealthRatio = next);
        int[] choices = rule.MaximumHealthRatio.HasValue ? new[] { 0, 1, 2, 3 } : new[] { 0, 2, 3 };
        if (!choices.Contains(conditionalResultSelection)) conditionalResultSelection = 0;
        Choice(prefix + "ConditionalResult", T("ConditionalResult." + conditionalResultSelection), () => menu.Show(T(prefix + "ConditionalResult"),
            choices.Select(index => new UiOptionMenuOption
            {
                Label = T("ConditionalResult." + index), Selected = index == conditionalResultSelection,
                Select = () => { conditionalResultSelection = index; Refresh(); }
            }).ToArray()));
        ConstructionContactOutcome outcome = conditionalResultSelection switch { 0 => rule.Outcome, 1 => rule.Otherwise, 2 => rule.OnKill, _ => rule.OnSurvive };
        if (conditionalResultSelection != 0) Toggle(prefix + "ConditionalResultEnabled", outcome != null, next =>
        {
            var result = next ? new ConstructionContactOutcome() : null;
            if (conditionalResultSelection == 1) rule.Otherwise = result;
            else if (conditionalResultSelection == 2) rule.OnKill = result;
            else rule.OnSurvive = result;
        });
        if (outcome != null) ContactOutcomeRows(outcome, prefix);
    }

    /// <summary>随机、生命分支及伤害后续复用同一套状态、恢复、击退和附加伤害。</summary>
    /// <param name="outcome">当前选定普通结果。</param><param name="prefix">作用所属。</param>
    private void ContactOutcomeRows(ConstructionContactOutcome outcome, string prefix)
    {
        ContactRestorationRows(outcome.Restoration, next => outcome.Restoration = next, prefix);
        ContactStatusRows(outcome.Statuses, next => outcome.Statuses = next, prefix);
        ContactKnockbackRows(outcome.Knockback, next => outcome.Knockback = next, prefix);
        ContactDamageRows(outcome.Damage, next => outcome.Damage = next, prefix);
    }
}
