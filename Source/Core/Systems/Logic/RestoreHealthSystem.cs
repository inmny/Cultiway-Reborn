using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Core.Components;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Core.Systems.Logic;

public class RestoreHealthSystem : QuerySystem<ActorBinder>
{
    private float _restore_timer = TimeScales.SecPerMonth;
    [Hotfixable]
    protected override void OnUpdate()
    {
        _restore_timer -= Tick.deltaTime;
        if (_restore_timer > 0) return;
        _restore_timer = TimeScales.SecPerMonth;
        Query.ForEach([Hotfixable](binders, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var a = binders[i].Actor;
                if (a == null || !a.isAlive()) return;
                a.restoreHealth((int)a.stats[WorldboxGame.BaseStats.HealthRegen.id]);
            }
        }).RunParallel();
    }
}