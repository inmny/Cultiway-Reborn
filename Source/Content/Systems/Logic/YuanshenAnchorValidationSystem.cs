using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;

namespace Cultiway.Content.Systems.Logic;

/// <summary>每秒校验已登记设施锚点的物质建筑、归属、位置与受损状态。</summary>
public sealed class YuanshenAnchorValidationSystem : ThrottledSystem
{
    /// <summary>低频更新使用的锚点句柄快照。</summary>
    private readonly List<YuanshenAnchorHandle> anchors = new();

    /// <summary>每秒校验一轮全部锚点。</summary>
    protected override float IntervalSeconds => 1f;

    /// <summary>世界切换时丢弃句柄快照。</summary>
    protected override void OnThrottleWorldStateCleared()
    {
        anchors.Clear();
    }

    /// <summary>逐项校验全部已登记锚点，不查询其他世界设施。</summary>
    protected override void OnThrottledUpdate()
    {
        anchors.Clear();
        YuanshenAnchorNetworkService.CollectRegistered(anchors);
        for (var i = 0; i < anchors.Count; i++)
            YuanshenAnchorNetworkService.UpdateMaterialState(anchors[i]);
    }
}
