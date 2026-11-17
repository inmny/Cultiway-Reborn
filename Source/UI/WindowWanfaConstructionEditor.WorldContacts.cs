using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>世界操作使用真实接触形状与共同次数规则，整招、身体和部位共用表单。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前选择只属于窗口，不写入法术。</summary>
    private int worldContactSelection;

    /// <summary>先选择世界操作，再编辑地块范围、频率及原理支持的持续参数。</summary>
    /// <param name="impact">当前独立作用。</param><param name="prefix">作用所属。</param>
    private void WorldContactRows(ConstructionImpact impact, string prefix)
    {
        ConstructionWorldAction[] actions = ConstructionWorldActions.All.OrderBy(action => action.Id, StringComparer.Ordinal).ToArray();
        var effects = impact.WorldEffects ?? Array.Empty<ConstructionWorldEffect>();
        if (effects.Length < 8 && actions.Length > 0) ActionRow(prefix + "AddWorldContact", "", () => Change(() =>
        {
            impact.WorldEffects = effects.Concat(new[] { new ConstructionWorldEffect { Action = actions[0].Id } }).ToArray();
            worldContactSelection = effects.Length;
        }));
        if (effects.Length == 0) return;
        worldContactSelection = Math.Min(worldContactSelection, effects.Length - 1);
        Choice(prefix + "WorldContact", (worldContactSelection + 1).ToString(), () => menu.Show(T(prefix + "WorldContact"),
            Enumerable.Range(0, effects.Length).Select(index => new UiOptionMenuOption
            {
                Label = (index + 1) + " · " + T("WorldAction." + effects[index].Action), Selected = index == worldContactSelection,
                Select = () => { worldContactSelection = index; Refresh(); }
            }).ToArray()));
        ActionRow(prefix + "RemoveWorldContact", "", () => Change(() => impact.WorldEffects = effects.Where((_, index) => index != worldContactSelection).ToArray()));
        ConstructionWorldEffect effect = effects[worldContactSelection];
        Choice(prefix + "WorldAction", T("WorldAction." + effect.Action), () => menu.Show(T(prefix + "WorldAction"), actions.Select(action => new UiOptionMenuOption
        {
            Label = T("WorldAction." + action.Id), Selected = action.Id == effect.Action,
            Select = () => Change(() => { effect.Action = action.Id; effect.Duration = null; })
        }).ToArray()));
        Toggle(prefix + "WorldTileCentre", effect.TileCentreOnly, next => effect.TileCentreOnly = next);
        ContactInteger(prefix + "WorldMaximumTiles", effect.MaximumTiles, next => effect.MaximumTiles = next);
        Number(prefix + "WorldMaterialVolume", effect.MaterialVolume, next => effect.MaterialVolume = next);
        Row(prefix + "WorldMaterialRules", T("WorldMaterialHint"));
        if (actions.Any(action => action.Id == effect.Action && action.DefaultDuration > 0f))
        {
            float duration = actions.Single(action => action.Id == effect.Action).DefaultDuration;
            Toggle(prefix + "WorldDurationOverride", effect.Duration.HasValue, next => effect.Duration = next ? duration : null);
            if (effect.Duration.HasValue) Number(prefix + "WorldDuration", effect.Duration.Value, next => effect.Duration = next);
        }
        Toggle(prefix + "WorldContactPolicy", effect.Contact != null, next => effect.Contact = next ? new ConstructionContactPolicy() : null);
        if (effect.Contact == null) return;
        ConstructionContactPolicy contact = effect.Contact;
        ImpactOption(prefix, "WorldContactMode", contact.Mode, next => contact.Mode = next);
        if (contact.Mode is ConstructionContactMode.Interval or ConstructionContactMode.Pulse)
            Number(prefix + "WorldContactInterval", contact.Interval, next => contact.Interval = next);
        ContactInteger(prefix + "WorldPerTile", contact.MaximumPerTarget, next => contact.MaximumPerTarget = next);
        ContactInteger(prefix + "WorldTotal", contact.MaximumTotal, next => contact.MaximumTotal = next);
    }
}
