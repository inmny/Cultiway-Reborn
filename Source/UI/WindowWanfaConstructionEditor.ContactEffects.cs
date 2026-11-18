using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>接触状态与恢复复用原作用页，不用整招词条代替普通作用资料。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>只展开当前状态，避免把整张状态表堆进窗口。</summary>
    private int contactStatusSelection;

    /// <summary>只展开当前属性；固定值及目标比例分别保留。</summary>
    private int contactAttributeSelection;

    /// <summary>整招、身体和部位共用同一套目标、伤害、恢复及状态参数。</summary>
    /// <param name="impact">独立草稿里的实际接触作用。</param>
    /// <param name="prefix">原有作用行的所属前缀。</param>
    private void ContactEffectRows(ConstructionImpact impact, string prefix)
    {
        Toggle(prefix + "SelectedTargetOnly", impact.SelectedTargetOnly, v => impact.SelectedTargetOnly = v);
        ImpactOption(prefix, "DamageBasis", impact.DamageBasis, v => impact.DamageBasis = v);
        Toggle(prefix + "ExplicitDamageElement", impact.Element.HasValue,
            v => impact.Element = v ? new ElementComposition { iron = 1f } : null);
        if (impact.Element.HasValue) ContactElementRows(prefix, "DamageElement", impact.Element.Value, v => impact.Element = v);
    }

    /// <summary>普通接触与有限结果共用恢复表单，不转换成临时作用对象。</summary>
    /// <param name="restoration">当前恢复。</param><param name="set">替换声明。</param><param name="prefix">作用所属。</param>
    private void ContactRestorationRows(ConstructionRestoration restoration, Action<ConstructionRestoration> set, string prefix)
    {
        Toggle(prefix + "Restoration", restoration != null,
            v => set(v ? new ConstructionRestoration { Health = 1f } : null));
        if (restoration != null)
        {
            ImpactOption(prefix, "RestorationTarget", restoration.Target, v => restoration.Target = v);
            Number(prefix + "RestoreHealth", restoration.Health, v => restoration.Health = v);
            Number(prefix + "RestorePerForce", restoration.HealthPerForce, v => restoration.HealthPerForce = v);
            Number(prefix + "RestorePerSecond", restoration.HealthPerSecond, v => restoration.HealthPerSecond = v);
            Number(prefix + "RestoreDuration", restoration.Duration, v => restoration.Duration = v);
            ContactInteger(prefix + "CleanseCount", restoration.CleanseCount, v => restoration.CleanseCount = v);
            ContactRemovalRows(restoration, prefix);
        }

    }

    /// <summary>普通接触与有限结果共用状态列表、属性及周期伤害。</summary>
    /// <param name="statuses">当前状态数组。</param><param name="set">替换数组。</param><param name="prefix">作用所属。</param>
    private void ContactStatusRows(ConstructionStatus[] statuses, Action<ConstructionStatus[]> set, string prefix)
    {
        statuses ??= Array.Empty<ConstructionStatus>();
        if (statuses.Length < 16)
            ActionRow(prefix + "AddContactStatus", T("ContactStatusHint"), () => menu.Show(T(prefix + "AddContactStatus"),
                ModClass.L.StatusEffectLibrary.list.OrderBy(asset => asset.id, StringComparer.Ordinal).Select(asset =>
                    new UiOptionMenuOption
                    {
                        Label = asset.GetName(), SearchText = asset.id + " " + asset.GetDescription(),
                        Select = () => Change(() =>
                        {
                            set(statuses.Concat(new[] { new ConstructionStatus
                            {
                                Id = asset.id, Duration = 2f, Application = ConstructionStatusApplication.Refresh
                            } }).ToArray());
                            contactStatusSelection = statuses.Length;
                        })
                    }).ToArray()));
        if (statuses.Length == 0) return;
        contactStatusSelection = Math.Min(contactStatusSelection, statuses.Length - 1);
        ConstructionStatus status = statuses[contactStatusSelection];
        string StatusName(ConstructionStatus value) => ModClass.L.StatusEffectLibrary.get(value.Id)?.GetName() ?? value.Id;
        Choice(prefix + "ContactStatus", StatusName(status), () => menu.Show(T(prefix + "ContactStatus"),
            statuses.Select((value, index) => new UiOptionMenuOption
            {
                Label = (index + 1) + ". " + StatusName(value), Selected = index == contactStatusSelection,
                Select = () => { contactStatusSelection = index; Refresh(); }
            }).ToArray()));
        ActionRow(prefix + "RemoveContactStatus", "", () => Change(() =>
            set(statuses.Where((_, index) => index != contactStatusSelection).ToArray())));
        Number(prefix + "StatusDuration", status.Duration, v => status.Duration = v);
        Number(prefix + "StatusPotency", status.Potency, v => status.Potency = v);
        ImpactOption(prefix, "StatusApplication", status.Application, v =>
        {
            status.Application = v;
            status.StackLimit = v == ConstructionStatusApplication.SourceStack ? Math.Max(1, status.StackLimit) : 0;
        });
        if (status.Application == ConstructionStatusApplication.SourceStack)
            ContactInteger(prefix + "StatusStackLimit", status.StackLimit, v => status.StackLimit = v);
        Toggle(prefix + "StatusWait", status.WaitForDuration, v => status.WaitForDuration = v);
        ContactAttributes(prefix, "StatusStats", status.Stats, v => status.Stats = v);
        ContactAttributes(prefix, "StatusRatios", status.TargetStatRatios, v => status.TargetStatRatios = v);
        Toggle(prefix + "StatusTick", status.Tick != null, v => status.Tick = v ? new ConstructionStatusTick
        {
            ForcePerSecond = .1f, Element = new ElementComposition { fire = 1f }
        } : null);
        if (status.Tick != null)
        {
            Number(prefix + "TickPerForce", status.Tick.ForcePerSecond, v => status.Tick.ForcePerSecond = v);
            ContactElementRows(prefix, "TickElement", status.Tick.Element, v => status.Tick.Element = v);
        }
    }

    /// <summary>固定属性与目标比例保留独立字典，选一项修改，不生成状态专用类。</summary>
    /// <param name="prefix">作用所属前缀。</param><param name="key">固定属性或目标比例。</param>
    /// <param name="values">当前可选字典。</param><param name="set">替换当前字典。</param>
    private void ContactAttributes(string prefix, string key, Dictionary<string, float> values,
        Action<Dictionary<string, float>> set)
    {
        string label = prefix + key;
        string Name(string id)
        {
            string locale = AssetManager.base_stats_library.get(id).getLocaleID();
            return LocalizedTextManager.instance.contains(locale) ? LocalizedTextManager.getText(locale) + " · " + id : id;
        }
        Toggle(label, values != null, enabled => set(enabled ? new Dictionary<string, float>() : null));
        if (values == null) return;
        if (values.Count < 64)
            ActionRow(label + "Add", "", () => menu.Show(T(label + "Add"), AssetManager.base_stats_library.list
                .Where(asset => !asset.ignore && !values.ContainsKey(asset.id)).OrderBy(asset => asset.id, StringComparer.Ordinal)
                .Select(asset => new UiOptionMenuOption
                {
                    Label = Name(asset.id), SearchText = asset.id,
                    Select = () => Change(() => values.Add(asset.id, 0f))
                }).ToArray()));
        if (values.Count == 0) return;
        string[] keys = values.Keys.ToArray();
        contactAttributeSelection = Math.Min(contactAttributeSelection, keys.Length - 1);
        string selected = keys[contactAttributeSelection];
        Choice(label + "Attribute", Name(selected), () => menu.Show(T(label + "Attribute"), keys.Select((id, index) =>
            new UiOptionMenuOption
            {
                Label = Name(id), Selected = index == contactAttributeSelection,
                Select = () => { contactAttributeSelection = index; Refresh(); }
            }).ToArray()));
        Number(label + "Value", values[selected], v => values[selected] = v);
        ActionRow(label + "Remove", "", () => Change(() => values.Remove(selected)));
    }

    /// <summary>八维元素直接保存权重，合法性及归一化由普通编译入口处理。</summary>
    /// <param name="prefix">作用所属前缀。</param><param name="key">主伤害或周期伤害。</param>
    /// <param name="element">当前值类型构成。</param><param name="set">替换实际构成。</param>
    private void ContactElementRows(string prefix, string key, ElementComposition element, Action<ElementComposition> set)
    {
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            Number(prefix + key + i, element[i], value => { var next = element; next[index] = value; set(next); });
        }
    }

    /// <summary>本页的枚举字段复用同一个选项菜单及对应本地化。</summary>
    /// <typeparam name="TValue">已经实现的普通枚举。</typeparam>
    /// <param name="prefix">作用所属前缀。</param><param name="key">字段名。</param>
    /// <param name="value">当前选择。</param><param name="set">修改独立草稿。</param>
    private void ImpactOption<TValue>(string prefix, string key, TValue value, Action<TValue> set) where TValue : struct, Enum
    {
        Choice(prefix + key, T(key + "." + value), () => menu.Show(T(prefix + key),
            Enum.GetValues(typeof(TValue)).Cast<TValue>().Select(option => new UiOptionMenuOption
            {
                Label = T(key + "." + option), Selected = option.Equals(value),
                Select = () => Change(() => set(option))
            }).ToArray()));
    }

    /// <summary>计数不能把小数悄悄截断；范围仍交给正式编译器判断。</summary>
    /// <param name="key">字段及本地化名。</param><param name="value">当前计数。</param><param name="set">草稿写入。</param>
    /// <param name="draftChange">直接写草稿，或交给完整候选命令处理。</param>
    private void ContactInteger(string key, int value, Action<int> set, bool draftChange = true)
    {
        WanfaEditorRow row = Row(key);
        FloatInput(row.UseInlineControls(104f), key, value, next =>
        {
            if (next != Mathf.Floor(next) || next >= int.MaxValue || next < int.MinValue)
            {
                state.text = T("ContactIntegerRequired");
                return;
            }
            if (draftChange) Change(() => set((int)next));
            else set((int)next);
        }, true, 100f, false);
    }
}
