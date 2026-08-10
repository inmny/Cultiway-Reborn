using Cultiway.Abstract;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core;

/// <summary>通过 SystemRoot 统一清除所有系统持有的当前世界状态。</summary>
internal static class WorldSystemLifecycle
{
    internal static void ClearWorldState()
    {
        ClearSystemStates(ModClass.I.GeneralLogicSystems);
        ClearSystemStates(ModClass.I.GeneralRenderSystems);
        ClearSystemStates(ModClass.I.TileLogicSystems);
        ClearSystemStates(ModClass.I.TileRenderSystems);
    }

    private static void ClearSystemStates(SystemGroup group)
    {
        foreach (BaseSystem system in group.ChildSystems)
        {
            if (system is IWorldStateClearable clearable) clearable.ClearWorldState();
            if (system is SystemGroup childGroup) ClearSystemStates(childGroup);
        }
    }
}
