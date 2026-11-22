using Cultiway.Core.Skills;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>地域和全图共用同一个实际条件。</summary>
    private void WorldConditionRows()
    {
        if (session.Draft.WorldCondition is not { } condition)
        {
            ActionRow("WorldFireAdd", T("WorldFireHint"),
                () => Change(() => session.Draft.WorldCondition = new ConstructionWorldCondition()));
            return;
        }

        Row("WorldFireRules", T("WorldFireHint"));
        Toggle("WorldFireEntire", condition.EntireWorld, value => condition.EntireWorld = value);
        if (!condition.EntireWorld) Number("WorldFireRadius", condition.Radius, value => condition.Radius = value);
        ActionRow("WorldFireRemove", "", () => Change(() => session.Draft.WorldCondition = null));
    }
}
