using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 每月推进始祖骑士的雷铸待重生条目（KnightForge.TickPending）：
/// 村庄被摧毁→删除数据；冷却月数到→在所属村庄满血重生。
/// 月度节流照 KnightBreakthroughSystem；用 QuerySystem 仅为获取 Tick.deltaTime（查询本身不使用）。
/// </summary>
public sealed class ForgePendingSystem : QuerySystem<ActorBinder>
{
    private float _timer = TimeScales.SecPerMonth;

    public ForgePendingSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        _timer -= Tick.deltaTime;
        if (_timer > 0f) return;
        _timer = TimeScales.SecPerMonth;

        KnightForge.TickPending();
    }
}
