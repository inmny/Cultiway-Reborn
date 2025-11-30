using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
///     记录境界突破演出的临时状态，避免重复触发并用于渲染。
/// </summary>
public struct XianBreakthroughState : IComponent
{
    public byte last_level;
    public byte visual_level;
    public float visual_timer;
    public int rng_seed;
    public byte flags;

    public bool HasFlag(byte mask) => (flags & mask) != 0;
    public void SetFlag(byte mask) => flags |= mask;

    public const byte FlagSpecialTriggered = 1 << 0;
    public const byte FlagShockwaveTriggered = 1 << 1;
    public const byte FlagWeatherRolled = 1 << 2;
}
