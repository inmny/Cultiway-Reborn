using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>使用原版 Tooltip 的标签-数值行展示单个核心构成特殊效果。</summary>
internal static class CoreFormationEffectTooltip
{
    private static CoreFormationEffectTooltipModel pending;

    /// <summary>同步打开指定核心构成的 Tooltip，并在回调结束后释放临时上下文。</summary>
    public static void Show(GameObject source, CoreFormationEffectTooltipModel model)
    {
        if (model == null) return;
        pending = model;
        try
        {
            Tooltip.show(
                source,
                Tooltips.CoreFormationEffect.id,
                new TooltipData());
        }
        finally
        {
            pending = null;
        }
    }

    /// <summary>由 TooltipAsset 回调读取当前展示模型并填充正文和标签-数值行。</summary>
    internal static void SetupPending(Tooltip tooltip)
    {
        CoreFormationEffectTooltipModel model = pending;
        if (model == null) return;
        tooltip.name.text = model.Title;
        tooltip.setDescription(model.Description);
        for (var i = 0; i < model.Lines.Count; i++)
        {
            CoreFormationEffectTooltipLine line = model.Lines[i];
            tooltip.addLineText(
                line.Label,
                line.Value,
                pLocalize: false,
                pLimitValue: 80);
        }
    }
}
