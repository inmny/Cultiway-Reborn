using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

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

/// <summary>近战执行体逐帧采用的空间运动。</summary>
internal enum WeaponControlMotionKind
{
    Sweep,
    Thrust,
    Crush,
}

/// <summary>每一道近战武器执行体携带的完整动作快照。</summary>
internal struct WeaponControlMotionState : IComponent
{
    /// <summary>施放开始时借用的真实武器；角色换装后当前执行体立即失效。</summary>
    public Item Weapon;

    /// <summary>本次执行体采用的运动形态。</summary>
    public WeaponControlMotionKind Kind;

    /// <summary>从生成到回收已经经过的模拟秒数。</summary>
    public float Elapsed;

    /// <summary>完整动作持续时间。</summary>
    public float Duration;

    /// <summary>武器主体相对角色中心能够达到的最远距离。</summary>
    public float Reach;

    /// <summary>扫掠相对初始目标方向的起始角度。</summary>
    public float StartAngle;

    /// <summary>扫掠相对初始目标方向的结束角度。</summary>
    public float EndAngle;

    /// <summary>原版攻击结算采用的伤害倍率。</summary>
    public float DamageMultiplier;

    /// <summary>施放步骤生成时已经包含随机偏移的基础方向。</summary>
    public Vector3 Direction;
}
