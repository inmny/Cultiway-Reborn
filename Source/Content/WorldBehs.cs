using Cultiway.Abstract;
using Cultiway.Const;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

/// <summary>记录当前世界处于灵气涨潮还是落潮，以及距离下次切换的时间。</summary>
public struct WakanTideStatus : IComponent
{
    public float switch_timer;
    public bool rise;
}

public class WorldBehs : ExtendLibrary<WorldBehaviourAsset, WorldBehs>
{
    public static WorldBehaviourAsset WakanTide { get; private set; }

    protected override bool AutoRegisterAssets() => false;

    protected override void OnInit()
    {
        WakanTide = Add(new WorldBehaviourAsset
        {
            id = nameof(WakanTide),
            interval = 1f,
            interval_random = 0.5f,
            action = UpdateWakanTide
        });
    }

    protected override WorldBehaviourAsset Add(WorldBehaviourAsset asset)
    {
        WorldBehaviourAsset result = base.Add(asset);
        result.manager = new WorldBehaviour(result);
        return result;
    }

    /// <summary>潮汐只切换世界倍率，具体灵气来源由灵脉月度结算负责。</summary>
    [Hotfixable]
    private static void UpdateWakanTide()
    {
        Entity worldRecord = ModClass.I.WorldRecord.E;
        if (!worldRecord.HasComponent<WakanTideStatus>())
        {
            worldRecord.AddComponent(new WakanTideStatus
            {
                switch_timer = 500 * 12 * TimeScales.SecPerMonth,
                rise = true
            });
        }

        ref WakanTideStatus status = ref worldRecord.GetComponent<WakanTideStatus>();
        status.switch_timer -= WakanTide.interval;
        while (status.switch_timer < 0f)
        {
            status.switch_timer += 500 * 12 * TimeScales.SecPerMonth;
            status.rise = !status.rise;
        }
    }
}
