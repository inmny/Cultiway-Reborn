namespace Cultiway.Core.Pathfinding;

/// <summary>worker 使用的完整移动能力快照。</summary>
public readonly struct PathTraversalProfile
{
    public PathTraversalProfile(
        bool ignoresBlocks,
        bool diesOnBlocks,
        bool isBoat,
        bool isWaterCreature,
        bool isFlying,
        bool isFireImmune,
        bool isDamagedByOcean,
        bool hasFastSwimming,
        bool isLavaDamaging,
        float currentStamina,
        float maxStamina,
        float currentHealth,
        float maxHealth,
        float baseSpeed,
        float waterDamagePerSecond,
        float staminaRegenPerSecond,
        float powerLevel,
        bool hasXianCultisys)
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
        PowerLevel = powerLevel;
        HasXianCultisys = hasXianCultisys;
    }

    internal static PathTraversalProfile StandardGround(float baseSpeed)
    {
        return new PathTraversalProfile(
            ignoresBlocks: false,
            diesOnBlocks: false,
            isBoat: false,
            isWaterCreature: false,
            isFlying: false,
            isFireImmune: false,
            isDamagedByOcean: false,
            hasFastSwimming: false,
            isLavaDamaging: false,
            currentStamina: 1f,
            maxStamina: 1f,
            currentHealth: 1f,
            maxHealth: 1f,
            baseSpeed: baseSpeed,
            waterDamagePerSecond: 0f,
            staminaRegenPerSecond: 0f,
            powerLevel: 0f,
            hasXianCultisys: false);
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
    public float PowerLevel { get; }
    public bool HasXianCultisys { get; }
}
