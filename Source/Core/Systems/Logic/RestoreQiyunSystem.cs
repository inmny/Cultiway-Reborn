using Cultiway.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Logic;

public class RestoreQiyunSystem : QuerySystem<Qiyun>
{
    private float _restore_timer = TimeScales.SecPerMonth;
    protected override void OnUpdate()
    {
        _restore_timer -= Tick.deltaTime;
        if (_restore_timer > 0) return;
        _restore_timer = TimeScales.SecPerMonth;
        Query.ForEach((qiyuns, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                ref var q = ref qiyuns[i];
                q.Value = Mathf.Min(q.Value + q.MaxValue / 12, q.MaxValue);
            }
        }).RunParallel();
    }
}