using Cultiway.Abstract;
using Cultiway.Core;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 血脉资产：骑士修到 9 级时对其个人属性的快照。挂在 BloodlineLibrary（DynamicAssetLibrary）中。
/// 后代通过 ActorExtend.Master 持有；当无任何后裔持有时（Current 归零）由 RecycleUnknownAssetsSystem 自动回收。
/// </summary>
public class BloodlineAsset : Asset, IDeleteWhenUnknown
{
    /// <summary>始祖（9 级骑士）的 ActorData id。</summary>
    public long ancestor_actor_id;

    // 始祖 9 级时的 7 项战斗属性快照（已剔除其自身继承的血脉加成，防雪崩）
    public float snapshot_health;
    public float snapshot_armor;
    public float snapshot_HealthRegen;
    public float snapshot_attack_speed;
    public float snapshot_critical_chance;
    public float snapshot_KnightEvasion;

    /// <summary>始祖 9 级时的战力等级，用作血脉强弱的比较基准。</summary>
    public float snapshot_power_level;

    public int Current { get; set; }

    public void OnDelete()
    {
        ModClass.LogInfo($"Bloodline of ancestor {ancestor_actor_id} faded (no descendants).");
    }
}

/// <summary>血脉资产库（动态资产）。照 CultibookLibrary 抄。</summary>
public class BloodlineLibrary : DynamicAssetLibrary<BloodlineAsset>
{
}
