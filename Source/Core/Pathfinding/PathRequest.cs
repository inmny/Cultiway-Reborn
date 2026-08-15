using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Performance;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.Pathfinding;

/// <summary>由模拟线程创建、供寻路线程只读的一次纯数据请求。</summary>
public sealed class PathRequest
{
    private PathRequest(
        PathAgentKey agentKey,
        int startTileId,
        int targetTileId,
        PathNavigationGrid navigationGrid,
        PathTraversalProfile profile,
        PathSearchRules searchRules,
        bool pathOnWater,
        bool walkOnBlocks,
        bool walkOnLava,
        int regionLimit)
    {
        AgentKey = agentKey;
        StartTileId = startTileId;
        TargetTileId = targetTileId;
        NavigationGrid = navigationGrid;
        WorldGeneration = navigationGrid?.Generation ?? agentKey.World.Generation;
        NavigationRevision = navigationGrid?.Revision ?? -1;
        Profile = profile;
        SearchRules = searchRules;
        PathOnWater = pathOnWater;
        WalkOnBlocks = walkOnBlocks;
        WalkOnLava = walkOnLava;
        RegionLimit = regionLimit;
    }

    public PathAgentKey AgentKey { get; }
    public long ActorId => AgentKey.AgentId;
    public bool PathOnWater { get; }
    public bool WalkOnBlocks { get; }
    public bool WalkOnLava { get; }
    public int RegionLimit { get; }
    public int StartTileId { get; }
    public int TargetTileId { get; }
    internal PathNavigationGrid NavigationGrid { get; }
    internal int WorldGeneration { get; }
    internal long NavigationRevision { get; }
    internal PathTraversalProfile Profile { get; }
    internal PathSearchRules SearchRules { get; }

    public bool ActorIgnoresBlocks => Profile.IgnoresBlocks;
    public bool ActorDiesOnBlocks => Profile.DiesOnBlocks;
    public bool ActorIsBoat => Profile.IsBoat;
    public bool ActorIsWaterCreature => Profile.IsWaterCreature;
    public bool ActorIsFlying => Profile.IsFlying;
    public bool ActorIsFireImmune => Profile.IsFireImmune;
    public bool ActorIsDamagedByOcean => Profile.IsDamagedByOcean;
    public bool ActorHasFastSwimming => Profile.HasFastSwimming;
    public bool ActorIsLavaDamaging => Profile.IsLavaDamaging;
    public float ActorCurrentStamina => Profile.CurrentStamina;
    public float ActorMaxStamina => Profile.MaxStamina;
    public float ActorCurrentHealth => Profile.CurrentHealth;
    public float ActorMaxHealth => Profile.MaxHealth;
    public float ActorBaseSpeed => Profile.BaseSpeed;
    public float ActorWaterDamagePerSecond => Profile.WaterDamagePerSecond;
    public float StaminaRegenPerSecond => Profile.StaminaRegenPerSecond;
    public float ActorPowerLevel => Profile.PowerLevel;
    public bool ActorHasXianCultisys => Profile.HasXianCultisys;

    internal bool IsValid => AgentKey.IsValid && NavigationGrid != null &&
                             NavigationGrid.WorldKey == AgentKey.World &&
                             StartTileId >= 0 && TargetTileId >= 0;

    internal static PathRequest CreateMainWorld(Actor actor, WorldTile target, bool pathOnWater,
        bool walkOnBlocks, bool walkOnLava, int regionLimit)
    {
        PathNavigationGrid grid = PathNavigationGridService.Current;
        PathWorldKey worldKey = grid?.WorldKey ?? PathWorldKey.MainWorld(SimulationTime.Generation);
        var agentKey = new PathAgentKey(worldKey, actor?.data?.id ?? 0);
        ActorMovementSnapshot movement = SnapshotActorMovement(actor);
        ActorExtendSnapshot extend = SnapshotActorExtend(actor);
        var profile = new PathTraversalProfile(
            movement.IgnoresBlocks,
            movement.DiesOnBlocks,
            movement.IsBoat,
            movement.IsWaterCreature,
            movement.IsFlying,
            movement.IsFireImmune,
            movement.IsDamagedByOcean,
            movement.HasFastSwimming,
            movement.IsLavaDamaging,
            movement.CurrentStamina,
            movement.MaxStamina,
            movement.CurrentHealth,
            movement.MaxHealth,
            movement.BaseSpeed,
            movement.WaterDamagePerSecond,
            movement.StaminaRegenPerSecond,
            extend.PowerLevel,
            extend.HasXianCultisys);
        return new PathRequest(
            agentKey,
            TileTraversalInfo.TileIdOf(actor?.current_tile),
            TileTraversalInfo.TileIdOf(target),
            grid,
            profile,
            PathSearchRules.MainWorld,
            pathOnWater,
            walkOnBlocks,
            walkOnLava,
            regionLimit);
    }

    internal static PathRequest CreateSubWorld(
        PathAgentKey agentKey,
        int startTileId,
        int targetTileId,
        PathNavigationGrid navigationGrid,
        PathTraversalProfile profile)
    {
        return new PathRequest(
            agentKey,
            startTileId,
            targetTileId,
            navigationGrid,
            profile,
            PathSearchRules.ForSubWorld(navigationGrid?.TileCount ?? 0),
            false,
            false,
            false,
            0);
    }

    /// <summary>为下一局部分段绑定起点和资源估值，世界快照保持不变。</summary>
    internal PathRequest WithStart(int startTileId, float currentStamina, float currentHealth)
    {
        var profile = new PathTraversalProfile(
            Profile.IgnoresBlocks,
            Profile.DiesOnBlocks,
            Profile.IsBoat,
            Profile.IsWaterCreature,
            Profile.IsFlying,
            Profile.IsFireImmune,
            Profile.IsDamagedByOcean,
            Profile.HasFastSwimming,
            Profile.IsLavaDamaging,
            currentStamina,
            Profile.MaxStamina,
            currentHealth,
            Profile.MaxHealth,
            Profile.BaseSpeed,
            Profile.WaterDamagePerSecond,
            Profile.StaminaRegenPerSecond,
            Profile.PowerLevel,
            Profile.HasXianCultisys);
        return new PathRequest(
            AgentKey,
            startTileId,
            TargetTileId,
            NavigationGrid,
            profile,
            SearchRules,
            PathOnWater,
            WalkOnBlocks,
            WalkOnLava,
            RegionLimit);
    }

    internal bool HasSameTargetAndOptions(int targetTileId, bool pathOnWater, bool walkOnBlocks,
        bool walkOnLava, int regionLimit)
    {
        return TargetTileId == targetTileId &&
               PathOnWater == pathOnWater &&
               WalkOnBlocks == walkOnBlocks &&
               WalkOnLava == walkOnLava &&
               RegionLimit == regionLimit;
    }

    private static ActorMovementSnapshot SnapshotActorMovement(Actor actor)
    {
        float staminaRegen = SimGlobals.m != null
            ? SimGlobals.m.stamina_change / Math.Max(SimGlobals.m.interval_stamina, 0.01f)
            : 0.5f;
        if (actor == null)
        {
            return ActorMovementSnapshot.Default(staminaRegen);
        }

        try
        {
            bool isFireImmune = actor.isImmuneToFire();
            float maxHealth = actor.getMaxHealth();
            return new ActorMovementSnapshot(
                actor.ignoresBlocks(),
                actor.asset?.die_on_blocks ?? false,
                actor.asset?.is_boat ?? false,
                actor.isWaterCreature(),
                actor.isFlying(),
                isFireImmune,
                actor.isDamagedByOcean(),
                actor.hasTag("fast_swimming"),
                actor.asset != null && actor.asset.die_in_lava && !isFireImmune,
                actor.getStamina(),
                actor.getMaxStamina(),
                actor.getHealth(),
                maxHealth,
                actor.stats?["speed"] ?? 5f,
                actor.getWaterDamage() * 3.333f,
                staminaRegen);
        }
        catch (Exception e)
        {
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            return ActorMovementSnapshot.Default(staminaRegen);
        }
    }

    private static ActorExtendSnapshot SnapshotActorExtend(Actor actor)
    {
        if (actor == null)
        {
            return default;
        }

        try
        {
            lock (EntityStoreLock.GlobalLock)
            {
                ActorExtend ae = actor.GetExtend();
                return new ActorExtendSnapshot(ae?.GetPowerLevel() ?? 0f, ae != null && ae.HasCultisys<Xian>());
            }
        }
        catch (Exception e)
        {
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            return default;
        }
    }

    private readonly struct ActorExtendSnapshot
    {
        internal ActorExtendSnapshot(float powerLevel, bool hasXianCultisys)
        {
            PowerLevel = powerLevel;
            HasXianCultisys = hasXianCultisys;
        }

        internal float PowerLevel { get; }
        internal bool HasXianCultisys { get; }
    }

    private readonly struct ActorMovementSnapshot
    {
        internal ActorMovementSnapshot(bool ignoresBlocks, bool diesOnBlocks, bool isBoat, bool isWaterCreature,
            bool isFlying, bool isFireImmune, bool isDamagedByOcean, bool hasFastSwimming, bool isLavaDamaging,
            float currentStamina, float maxStamina, float currentHealth, float maxHealth, float baseSpeed,
            float waterDamagePerSecond, float staminaRegenPerSecond)
        {
            IgnoresBlocks = ignoresBlocks;
            DiesOnBlocks = diesOnBlocks;
            IsBoat = isBoat;
            IsWaterCreature = isWaterCreature;
            IsFlying = isFlying;
            IsFireImmune = isFireImmune;
            IsDamagedByOcean = isDamagedByOcean;
            HasFastSwimming = hasFastSwimming;
            IsLavaDamaging = isLavaDamaging;
            CurrentStamina = currentStamina;
            MaxStamina = maxStamina;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            BaseSpeed = baseSpeed;
            WaterDamagePerSecond = waterDamagePerSecond;
            StaminaRegenPerSecond = staminaRegenPerSecond;
        }

        internal bool IgnoresBlocks { get; }
        internal bool DiesOnBlocks { get; }
        internal bool IsBoat { get; }
        internal bool IsWaterCreature { get; }
        internal bool IsFlying { get; }
        internal bool IsFireImmune { get; }
        internal bool IsDamagedByOcean { get; }
        internal bool HasFastSwimming { get; }
        internal bool IsLavaDamaging { get; }
        internal float CurrentStamina { get; }
        internal float MaxStamina { get; }
        internal float CurrentHealth { get; }
        internal float MaxHealth { get; }
        internal float BaseSpeed { get; }
        internal float WaterDamagePerSecond { get; }
        internal float StaminaRegenPerSecond { get; }

        internal static ActorMovementSnapshot Default(float staminaRegenPerSecond)
        {
            return new ActorMovementSnapshot(false, false, false, false, false, false, false, false, false,
                0f, 1f, 1f, 1f, 5f, 0.3333f, staminaRegenPerSecond);
        }
    }
}
