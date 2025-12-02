using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core.EventSystem;
using Cultiway.Content.Const;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 处理丹药效果生成完成的落地逻辑
/// </summary>
public class ElixirEffectGeneratedEventSystem : GenericEventSystem<ElixirEffectGeneratedEvent>
{
    protected override void HandleEvent(ElixirEffectGeneratedEvent evt)
    {
        if (string.IsNullOrEmpty(evt.ElixirId)) return;
        var asset = Libraries.Manager.ElixirLibrary.get(evt.ElixirId);
        if (asset == null) return;

        switch (evt.EffectType)
        {
            case ElixirEffectType.StatusGain:
                ElixirEffectGenerator.ApplyStatusDraft(asset, evt.StatusDraft);
                break;
            case ElixirEffectType.DataGain:
                ElixirEffectGenerator.ApplyDataGainDraft(asset, evt.DataGainDraft);
                break;
        }
    }
}
