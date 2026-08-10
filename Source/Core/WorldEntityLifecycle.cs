using System;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

/// <summary>负责在原版世界销毁后同步移除两个长期 Store 的运行期实体。</summary>
internal static class WorldEntityLifecycle
{
    internal static void ClearWorldState()
    {
        lock (EntityStoreLock.GlobalLock)
        {
            ClearRuntimeEntities(ModClass.I.W);
            ClearRuntimeEntities(ModClass.I.TileExtendManager.World);
            AssertNoRuntimeEntities();
        }
    }

    private static void ClearRuntimeEntities(EntityStore store)
    {
        var entities = store.Entities.ToEntityList();
        Entity storeRoot = store.StoreRoot;
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            Entity entity = entities[i];
            if (entity.IsNull || IsApplicationEntity(entity, storeRoot)) continue;

            if (entity.TryGetComponent(out AnimBindRenderer bindRenderer) && bindRenderer.value != null)
            {
                bindRenderer.value.Return();
            }
            entity.DeleteEntity();
        }
    }

    private static void AssertNoRuntimeEntities()
    {
        var mainCount = CountRuntimeEntities(ModClass.I.W);
        var tileCount = CountRuntimeEntities(ModClass.I.TileExtendManager.World);
        if (mainCount != 0 || tileCount != 0)
        {
            throw new InvalidOperationException(
                $"世界清理后仍有运行期 ECS 实体: main={mainCount}, tile={tileCount}");
        }
    }

    private static int CountRuntimeEntities(EntityStore store)
    {
        var count = 0;
        Entity storeRoot = store.StoreRoot;
        foreach (Entity entity in store.Entities)
        {
            if (!IsApplicationEntity(entity, storeRoot)) count++;
        }
        return count;
    }

    private static bool IsApplicationEntity(Entity entity, Entity storeRoot)
    {
        return entity == storeRoot ||
               entity.Tags.Has<TagPrefab>() ||
               entity.HasComponent<SourceGrantedSkill>() ||
               entity.HasComponent<SourceGrantedSkillRoot>();
    }
}
