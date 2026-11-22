using System;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    private void SoulPactRows()
    {
        if (session.Draft.SoulPact is not { } pact)
        {
            ActionRow("SoulPactAdd", T("SoulPactHint"), () => EditSoulPact(candidate => candidate.SoulPact = new ConstructionSoulPact()));
            return;
        }
        Row("SoulPactHint", T("SoulPactHint"));
        SoulPactNumber("SoulPactFraction", pact.Fraction, (rule, value) => rule.Fraction = value);
        SoulPactNumber("SoulPactCapacity", pact.Capacity, (rule, value) => rule.Capacity = value);
        Toggle("SoulPactAllowOtherBearers", pact.AllowOtherBearers, value => pact.AllowOtherBearers = value);
        if (pact.AllowOtherBearers) Row("SoulPactSharedRules", T("SoulPactSharedHint"));
        ActionRow("SoulPactRemove", "", () => EditSoulPact(candidate => candidate.SoulPact = null));
    }

    private void SoulPactNumber(string key, float value, Action<ConstructionSoulPact, float> set)
    {
        var row = Row(key);
        FloatInput(row.UseInlineControls(104f), key, value, next => EditSoulPact(candidate => set(candidate.SoulPact, next)),
            true, 100f, draftChange: false);
    }

    private void EditSoulPact(Action<AbilityDefinition> edit) => ApplyConstructionEdit(() =>
    {
        AbilityDefinition candidate = ConstructionEditing.Copy(session.Draft);
        edit(candidate);
        AbilityCompiler.Compile(candidate, false);
        return candidate;
    });
}
