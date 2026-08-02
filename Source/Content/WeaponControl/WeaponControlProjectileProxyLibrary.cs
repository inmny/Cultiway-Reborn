using System;
using System.Collections.Generic;
using Cultiway.Core.Combat;

namespace Cultiway.Content.WeaponControl;

/// <summary>
/// 按原投射物和御器形态缓存轻量代理。代理保留贴图、物理和落地行为，只关闭高频声音并登记伤害倍率。
/// </summary>
internal static class WeaponControlProjectileProxyLibrary
{
    private const float ProjectileDamageMultiplier = 0.22f;
    private static readonly Dictionary<string, string> ProxyIds = new(StringComparer.Ordinal);

    /// <summary>返回适合当前御器形态的代理投射物资产 ID。</summary>
    public static string Resolve(string sourceAssetId, WeaponControlCastMode mode)
    {
        if (string.IsNullOrWhiteSpace(sourceAssetId)) return string.Empty;
        string key = $"{sourceAssetId}|{mode}";
        if (ProxyIds.TryGetValue(key, out string existing)) return existing;

        ProjectileAsset source = AssetManager.projectiles.get(sourceAssetId);
        if (source == null) return string.Empty;
        string suffix = mode == WeaponControlCastMode.ArrowRain ? "high_arc" : "sky";
        string proxyId = $"Cultiway.WeaponControl.Projectile.{suffix}.{sourceAssetId}";
        ProjectileAsset registered = AssetManager.projectiles.get(proxyId);
        if (registered == null)
        {
            registered = Clone(source, proxyId, mode == WeaponControlCastMode.ArrowRain);
            AssetManager.projectiles.add(registered);
        }

        AttackDamageScaleContext.RegisterProjectile(proxyId, ProjectileDamageMultiplier);
        ProxyIds.Add(key, proxyId);
        return proxyId;
    }

    /// <summary>完整复制原投射物玩法字段，并只调整弧线与声音语义。</summary>
    private static ProjectileAsset Clone(ProjectileAsset source, string id, bool forceHighArc)
    {
        return new ProjectileAsset
        {
            id = id,
            texture = source.texture,
            animated = source.animated,
            animation_speed = source.animation_speed,
            speed = source.speed,
            speed_random = source.speed_random,
            terraform_option = source.terraform_option,
            terraform_range = source.terraform_range,
            end_effect = source.end_effect,
            end_effect_scale = source.end_effect_scale,
            sound_launch = string.Empty,
            sound_impact = string.Empty,
            look_at_target = source.look_at_target,
            trail_effect_enabled = source.trail_effect_enabled,
            trail_effect_id = source.trail_effect_id,
            trail_effect_scale = source.trail_effect_scale,
            trail_effect_timer = source.trail_effect_timer,
            hit_freeze = source.hit_freeze,
            hit_shake = source.hit_shake,
            shake_duration = source.shake_duration,
            shake_interval = source.shake_interval,
            shake_intensity = source.shake_intensity,
            shake_x = source.shake_x,
            shake_y = source.shake_y,
            frames = source.frames,
            scale_start = source.scale_start,
            scale_target = source.scale_target,
            texture_shadow = source.texture_shadow,
            world_actions = source.world_actions,
            impact_actions = source.impact_actions,
            draw_light_area = source.draw_light_area,
            draw_light_area_offset_x = source.draw_light_area_offset_x,
            draw_light_area_offset_y = source.draw_light_area_offset_y,
            draw_light_size = source.draw_light_size,
            trigger_on_collision = source.trigger_on_collision,
            can_be_collided = source.can_be_collided,
            can_be_left_on_ground = source.can_be_left_on_ground,
            can_be_blocked = source.can_be_blocked,
            use_min_angle_height = forceHighArc ? false : source.use_min_angle_height,
            mass = source.mass,
            size = source.size,
        };
    }
}
