using System.Collections.Generic;
using Cultiway.Content.WeaponControl;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>由原版弹丸的实际出生和结束维护御器代理飞行，供发射容量和空间绘制共用。</summary>
internal static class NativeProjectileFlights
{
    private static readonly HashSet<Projectile> Active = new();

    /// <summary>原版每次启动都会重新确认。反弹复用同一对象，普通弹丸复用时立即解除旧登记。</summary>
    internal static void Started(Projectile projectile)
    {
        if (WeaponControlProjectileProxyLibrary.IsProxy(projectile.asset?.id)) Active.Add(projectile);
        else if (Active.Count != 0) Active.Remove(projectile);
    }

    /// <summary>保留还在原版飞行的实例。着地后留在地图上的箭也已经退出飞行。</summary>
    internal static int Prune()
    {
        Active.RemoveWhere(projectile => !IsFlyingProxy(projectile));
        return Active.Count;
    }

    internal static void Clear() => Active.Clear();

    /// <summary>有同一份空间显示可追加时，才接替 Worldfall 自带的投射物绘制。</summary>
    internal static bool UsesSpatialVisual(Projectile projectile) =>
        Active.Contains(projectile) && IsFlyingProxy(projectile) && projectile.asset.frames?.Length > 0;

    /// <summary>读取原版同一弹丸的真实轨道、转向与帧资料。</summary>
    internal static void AppendSpatial(List<SpatialVisualMeshInstance> meshes)
    {
        foreach (Projectile projectile in Active)
        {
            if (projectile.asset.frames?.Length is not > 0) continue;
            Vector3 point = projectile._current_position_3d;
            if (!SpatialVisualEffects.IsRelevant(point, 1f)) continue;
            Sprite sprite = projectile.asset.animated
                ? AnimationHelper.getSpriteFromList(projectile.GetHashCode(), projectile.asset.frames,
                    projectile.asset.animation_speed)
                : projectile.asset.frames[0];
            Vector3 direction = projectile._velocity;
            Quaternion rotation = direction.sqrMagnitude > .0001f
                ? Quaternion.FromToRotation(Vector3.right, direction.normalized)
                : Quaternion.identity;
            meshes.Add(new SpatialVisualMeshInstance(SpatialSpriteMesh.Get(sprite), point, rotation,
                Vector3.one * Mathf.Max(.05f, projectile.getCurrentScale()),
                new Color(1f, 1f, 1f, projectile.getAlpha()), emissive: projectile.asset.draw_light_area));
        }
    }

    private static bool IsFlyingProxy(Projectile projectile) => projectile != null && projectile.isAlive() &&
        !projectile.isFinished() && WeaponControlProjectileProxyLibrary.IsProxy(projectile.asset?.id);
}
