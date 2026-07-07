using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.Pathfinding;

public sealed class PathRequest
{
    public PathRequest(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int regionLimit)
    {
        Actor = actor;
        var start = actor?.current_tile;
        Target = target;
        PathOnWater = pathOnWater;
        WalkOnBlocks = walkOnBlocks;
        WalkOnLava = walkOnLava;
        RegionLimit = regionLimit;
        StartTileId = TileTraversalInfo.TileIdOf(start);
        TargetTileId = TileTraversalInfo.TileIdOf(Target);
        var movementSnapshot = SnapshotActorMovement(actor);
        ActorIgnoresBlocks = movementSnapshot.IgnoresBlocks;
        ActorDiesOnBlocks = movementSnapshot.DiesOnBlocks;
        ActorIsBoat = movementSnapshot.IsBoat;
        ActorIsWaterCreature = movementSnapshot.IsWaterCreature;
        ActorIsFlying = movementSnapshot.IsFlying;
        ActorIsFireImmune = movementSnapshot.IsFireImmune;
        ActorIsDamagedByOcean = movementSnapshot.IsDamagedByOcean;
        ActorHasFastSwimming = movementSnapshot.HasFastSwimming;
        ActorIsLavaDamaging = movementSnapshot.IsLavaDamaging;
        ActorCurrentStamina = movementSnapshot.CurrentStamina;
        ActorMaxStamina = movementSnapshot.MaxStamina;
        ActorCurrentHealth = movementSnapshot.CurrentHealth;
        ActorMaxHealth = movementSnapshot.MaxHealth;
        ActorBaseSpeed = movementSnapshot.BaseSpeed;
        ActorWaterDamagePerSecond = movementSnapshot.WaterDamagePerSecond;
        StaminaRegenPerSecond = movementSnapshot.StaminaRegenPerSecond;
        var extendSnapshot = SnapshotActorExtend(actor);
        ActorPowerLevel = extendSnapshot.PowerLevel;
        ActorHasXianCultisys = extendSnapshot.HasXianCultisys;
    }

    public Actor Actor { get; }
    public WorldTile Target { get; }
    public bool PathOnWater { get; }
    public bool WalkOnBlocks { get; }
    public bool WalkOnLava { get; }
    public int RegionLimit { get; }
    public int StartTileId { get; }
    public int TargetTileId { get; }
    public bool ActorIgnoresBlocks { get; }
    public bool ActorDiesOnBlocks { get; }
    public bool ActorIsBoat { get; }
    public bool ActorIsWaterCreature { get; }
    public bool ActorIsFlying { get; }
    public bool ActorIsFireImmune { get; }
    public bool ActorIsDamagedByOcean { get; }
    public bool ActorHasFastSwimming { get; }
    public bool ActorIsLavaDamaging { get; }
    public float ActorCurrentStamina { get; }
    public float ActorMaxStamina { get; }
    public float ActorCurrentHealth { get; }
    public float ActorMaxHealth { get; }
    public float ActorBaseSpeed { get; }
    public float ActorWaterDamagePerSecond { get; }
    public float StaminaRegenPerSecond { get; }
    public float ActorPowerLevel { get; }
    public bool ActorHasXianCultisys { get; }

    public bool HasSameTargetAndOptions(WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int regionLimit)
    {
        return TargetTileId == TileTraversalInfo.TileIdOf(target) &&
               PathOnWater == pathOnWater &&
               WalkOnBlocks == walkOnBlocks &&
               WalkOnLava == walkOnLava &&
               RegionLimit == regionLimit;
    }

    private static ActorMovementSnapshot SnapshotActorMovement(Actor actor)
    {
        var staminaRegen = SimGlobals.m != null
            ? SimGlobals.m.stamina_change / Math.Max(SimGlobals.m.interval_stamina, 0.01f)
            : 0.5f;
        if (actor == null)
        {
            return ActorMovementSnapshot.Default(staminaRegen);
        }

        try
        {
            var isFireImmune = actor.isImmuneToFire();
            var maxHealth = actor.getMaxHealth();
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
                var ae = actor.GetExtend();
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
        public ActorExtendSnapshot(float powerLevel, bool hasXianCultisys)
        {
            PowerLevel = powerLevel;
            HasXianCultisys = hasXianCultisys;
        }

        public float PowerLevel { get; }
        public bool HasXianCultisys { get; }
    }

    private readonly struct ActorMovementSnapshot
    {
        public ActorMovementSnapshot(bool ignoresBlocks, bool diesOnBlocks, bool isBoat, bool isWaterCreature,
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

        public bool IgnoresBlocks { get; }
        public bool DiesOnBlocks { get; }
        public bool IsBoat { get; }
        public bool IsWaterCreature { get; }
        public bool IsFlying { get; }
        public bool IsFireImmune { get; }
        public bool IsDamagedByOcean { get; }
        public bool HasFastSwimming { get; }
        public bool IsLavaDamaging { get; }
        public float CurrentStamina { get; }
        public float MaxStamina { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public float BaseSpeed { get; }
        public float WaterDamagePerSecond { get; }
        public float StaminaRegenPerSecond { get; }

        public static ActorMovementSnapshot Default(float staminaRegenPerSecond)
        {
            return new ActorMovementSnapshot(false, false, false, false, false, false, false, false, false,
                0f, 1f, 1f, 1f, 5f, 0.3333f, staminaRegenPerSecond);
        }
    }
}
