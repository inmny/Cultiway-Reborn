using System;
using System.IO;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>普通参数页里的整法改造，先填本次参数，再明确应用到实际草稿。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>复用现有参数行和输入框，不增加词条叠加器或固定多招组合。</summary>
    private void MagnitudeRows()
    {
        Multiplier("ResizeConstruction", 1.5f, value => ConstructionMagnitudeEdits.Resize(session.Draft, value));
        Multiplier("HasteConstruction", 1.5f, value => ConstructionMagnitudeEdits.Haste(session.Draft, value));
        Multiplier("DurationConstruction", 2f, value => ConstructionMagnitudeEdits.Duration(session.Draft, value));
        Multiplier("SustainConstruction", 1f, value => ConstructionMagnitudeEdits.Sustain(session.Draft, value), "SustainSeconds");
        float count = 3, spacing = 1f, interval = 0f;
        WanfaEditorRow row = rows.GetNext();
        row.Setup(T("RepeatConstruction"), T("RepeatConstructionHint"), T("ApplyChange"), true, () => ApplyConstructionEdit(() =>
        {
            if (count != Mathf.Floor(count)) throw new InvalidDataException(T("IntegerCountRequired"));
            if (count < 1 || count > 64) throw new InvalidDataException(T("IntegerCountRequired"));
            return ConstructionMagnitudeEdits.Repeat(session.Draft, (int)count, spacing, interval);
        }));
        Transform controls = row.UseInlineControls(244f);
        FloatInput(controls, "CopyCount", count, value => count = value, true, 70f, false);
        FloatInput(controls, "CopySpacing", spacing, value => spacing = value, true, 70f, false);
        FloatInput(controls, "CopyInterval", interval, value => interval = value, true, 70f, false);
        Row("MagnitudeRules", T("MagnitudeRulesHint"));
    }

    /// <summary>倍率是本次命令参数，输入不会先制造一份空撤销记录。</summary>
    /// <param name="key">改造名称和解释。</param>
    /// <param name="initial">本次默认倍率。</param>
    /// <param name="edit">使用该数值建立独立候选。</param>
    /// <param name="parameter">本次参数名称和单位。</param>
    private void Multiplier(string key, float initial, Func<float, AbilityDefinition> edit, string parameter = "ChangeFactor")
    {
        float amount = initial;
        WanfaEditorRow row = rows.GetNext();
        row.Setup(T(key), T(key + "Hint"), T("ApplyChange"), true, () => ApplyConstructionEdit(() => edit(amount)));
        FloatInput(row.UseInlineControls(104f), parameter, initial, value => amount = value, true, 100f, false);
    }
}
