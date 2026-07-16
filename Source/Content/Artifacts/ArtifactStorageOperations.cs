using System;
using Cultiway.Content.Components;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>法器命名资源通道的配置与守恒存取原语。</summary>
public static class ArtifactStorageOperations
{
    public const string SoulEssence = "soul_essence";
    public const string Wakan = "wakan";
    public const string AbsorbedPower = "absorbed_power";

    public static void Configure(ref ArtifactStorageState storage, string key, float capacity)
    {
        int index = Find(storage.resources, key);
        if (index < 0)
        {
            int length = storage.resources.Length;
            Array.Resize(ref storage.resources, length + 1);
            storage.resources[length] = new ArtifactStoredResource
            {
                key = key,
                capacity = Mathf.Max(0f, capacity),
            };
            return;
        }

        storage.resources[index].capacity = Mathf.Max(storage.resources[index].capacity, Mathf.Max(0f, capacity));
        storage.resources[index].amount = Mathf.Min(
            storage.resources[index].amount,
            storage.resources[index].capacity);
    }

    /// <summary>存入资源并返回实际存入量。</summary>
    public static float Store(ref ArtifactStorageState storage, string key, float amount)
    {
        if (amount <= 0f) return 0f;
        int index = Require(storage.resources, key);
        ref ArtifactStoredResource resource = ref storage.resources[index];
        float stored = Mathf.Min(amount, Mathf.Max(0f, resource.capacity - resource.amount));
        resource.amount += stored;
        return stored;
    }

    /// <summary>取出资源并返回实际取出量。</summary>
    public static float Take(ref ArtifactStorageState storage, string key, float amount)
    {
        if (amount <= 0f) return 0f;
        int index = Require(storage.resources, key);
        ref ArtifactStoredResource resource = ref storage.resources[index];
        float taken = Mathf.Min(amount, Mathf.Max(0f, resource.amount));
        resource.amount -= taken;
        return taken;
    }

    public static float GetAmount(in ArtifactStorageState storage, string key)
    {
        int index = Find(storage.resources, key);
        return index < 0 ? 0f : storage.resources[index].amount;
    }

    public static float GetCapacity(in ArtifactStorageState storage, string key)
    {
        int index = Find(storage.resources, key);
        return index < 0 ? 0f : storage.resources[index].capacity;
    }

    private static int Require(ArtifactStoredResource[] resources, string key)
    {
        int index = Find(resources, key);
        if (index < 0) throw new InvalidOperationException($"法器储藏通道尚未配置: {key}");
        return index;
    }

    private static int Find(ArtifactStoredResource[] resources, string key)
    {
        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i].key == key) return i;
        }
        return -1;
    }
}
