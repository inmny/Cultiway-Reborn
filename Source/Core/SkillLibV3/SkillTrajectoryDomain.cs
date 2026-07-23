using System;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 法术实体与轨迹之间的运行形态契约。该契约描述实体如何存在于战场，不负责贴图朝向。
/// </summary>
[Flags]
public enum SkillTrajectoryDomain
{
    None = 0,
    FlyingBody = 1 << 0,
    FlyingWave = 1 << 1,
    Ballistic = 1 << 2,
    Skyfall = 1 << 3,
    GroundTravel = 1 << 4,
    GroundManifest = 1 << 5,
    TargetManifest = 1 << 6,
    Beam = 1 << 7,
    Chain = 1 << 8,
    StationaryField = 1 << 9,
    MobileField = 1 << 10,
    Barrier = 1 << 11,
    Aura = 1 << 12,
    Melee = 1 << 13
}
