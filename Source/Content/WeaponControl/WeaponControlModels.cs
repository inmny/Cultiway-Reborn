namespace Cultiway.Content.WeaponControl;

/// <summary>御器对当前武器的运行时分类。</summary>
internal enum WeaponControlCategory
{
    Ranged,
    Sword,
    Spear,
    Axe,
    Hammer,
    Staff,
    Other,
}

/// <summary>一次御器序列采用的整体表现形式。</summary>
internal enum WeaponControlCastMode
{
    SkyVolley,
    ArrowRain,
    MeleeSweep,
    MeleeThrust,
    MeleeCrush,
}
