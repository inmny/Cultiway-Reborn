using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

internal enum KnightTrailStyle
{
    None,
    Guardian,
    Lancer,
    Duelist,
    DuelistFinisher,
    Movement,
}

/// <summary>生成真实武器运动、短拖尾和不含武器轮廓的自制环境帧。</summary>
internal static class KnightTechniqueVisuals
{
    private static readonly Dictionary<long, WeaponDisplayLease> Leases = new();
    private static readonly Dictionary<long, Entity> Stances = new();

    public static void Init()
    {
        PatchActor.RegisterHideHandItemPredicate(IsWeaponDetached);
    }

    public static Entity SpawnWeaponMotion(
        KnightTechniqueContext context,
        EquippedWeaponMotionKind kind,
        Vector3 direction,
        float duration,
        float reach,
        float startAngle,
        float endAngle,
        KnightTrailStyle trailStyle,
        float scaleMultiplier = 1f,
        float motionDuration = 0f)
    {
        Actor owner = context.Caster.Base;
        Sprite sprite = EquippedWeaponVisualService.ResolveSprite(owner, context.WeaponAsset);
        if (sprite == null) return default;

        direction.z = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;
        direction.Normalize();
        Vector3 targetPosition = owner.GetSimPos() + direction * Mathf.Max(1f, reach);
        Entity execution = ModClass.I.SkillV3.SpawnSkill(
            KnightTechniqueSkills.GetContainer(context.Technique),
            owner,
            context.Target,
            targetPosition,
            owner.stats[S.damage],
            attack_kingdom: owner.kingdom);
        ref AnimData animation = ref execution.GetComponent<AnimData>();
        animation.frames = new[] { sprite };
        animation.frame_idx = 0;
        animation.frame_timer = 0f;
        float actorScale = Mathf.Max(0.1f, owner.stats[S.scale]);
        execution.GetComponent<Scale>().value = Vector3.one * actorScale * 1.85f * scaleMultiplier;
        execution.GetComponent<VisualRotation>() = VisualRotation.FollowRotation(
            EquippedWeaponVisualService.ResolveSpriteAngle(sprite));

        ref EquippedWeaponMotionState state = ref execution.GetComponent<EquippedWeaponMotionState>();
        state.Weapon = context.Weapon;
        state.Kind = kind;
        state.Elapsed = 0f;
        state.Duration = duration;
        state.MotionDuration = motionDuration;
        state.Reach = reach;
        state.StartAngle = startAngle;
        state.EndAngle = endAngle;
        state.Direction = direction;
        state.CollisionEnabled = false;
        execution.GetComponent<ColliderConfig>().Enabled = false;
        execution.GetComponent<AliveTimeLimit>().value = duration + 0.12f;
        AnimAfterimage afterimage = CreateAfterimage(kind, trailStyle, reach);
        if (execution.HasComponent<AnimAfterimage>()) execution.GetComponent<AnimAfterimage>() = afterimage;
        if (execution.HasComponent<AnimAfterimageOverride>())
            execution.GetComponent<AnimAfterimageOverride>().Value = afterimage;
        execution.GetComponent<MotionRibbonTrail>() = CreateTrail(
            owner,
            context.WeaponAsset,
            kind,
            trailStyle,
            reach);
        if (kind == EquippedWeaponMotionKind.GuardHold) TrackStance(owner, execution);
        LeaseWeapon(owner, context.Weapon, duration + 0.2f);
        return execution;
    }

    public static void StopStance(Actor actor)
    {
        long actorId = actor.getID();
        if (Stances.TryGetValue(actorId, out Entity stance) && stance.IsAvailable())
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(stance.Id);
        Stances.Remove(actorId);
        Leases.Remove(actorId);
    }

    public static bool IsWeaponDetached(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        long actorId = actor.getID();
        if (!Leases.TryGetValue(actorId, out WeaponDisplayLease lease)) return false;
        if (Time.time < lease.Until && EquippedWeaponVisualService.IsCurrent(actor, lease.Weapon)) return true;
        Leases.Remove(actorId);
        return false;
    }

    public static void ClearWorldState()
    {
        Leases.Clear();
        Stances.Clear();
    }

    private static void TrackStance(Actor actor, Entity stance)
    {
        StopStance(actor);
        Stances[actor.getID()] = stance;
    }

    private static void LeaseWeapon(Actor actor, Item weapon, float duration)
    {
        long actorId = actor.getID();
        float until = Time.time + duration;
        if (Leases.TryGetValue(actorId, out WeaponDisplayLease current) &&
            ReferenceEquals(current.Weapon, weapon) && current.Until > until) return;
        Leases[actorId] = new WeaponDisplayLease(weapon, until);
    }

    private static MotionRibbonTrail CreateTrail(
        Actor owner,
        EquipmentAsset weaponAsset,
        EquippedWeaponMotionKind kind,
        KnightTrailStyle style,
        float reach)
    {
        if (style == KnightTrailStyle.None) return default;
        EquippedWeaponVisualService.ResolveTrailColors(owner, weaponAsset, out Color core, out Color glow);
        TintForStyle(style, ref core, ref glow);
        float worldScale = EquippedWeaponVisualService.ResolveWorldVisualScale(owner);
        float width = style switch
        {
            KnightTrailStyle.Guardian => 0.42f,
            KnightTrailStyle.Lancer => 0.32f,
            KnightTrailStyle.DuelistFinisher => 0.4f,
            KnightTrailStyle.Movement => 0.34f,
            _ => 0.28f,
        } * worldScale;
        MotionRibbonShape shape = kind switch
        {
            EquippedWeaponMotionKind.Thrust => MotionRibbonShape.AxialThrust,
            EquippedWeaponMotionKind.Sweep => MotionRibbonShape.RadialSweep,
            _ => MotionRibbonShape.Path,
        };
        if (shape == MotionRibbonShape.AxialThrust)
            width = Mathf.Clamp(reach * 0.3f, 0.72f * worldScale, 1.4f * worldScale);
        return new MotionRibbonTrail
        {
            Enabled = true,
            Shape = shape,
            HistorySeconds = kind == EquippedWeaponMotionKind.Crush ? 0.3f : 0.24f,
            MinSampleDistance = 0.045f * worldScale,
            MaxPoints = 36,
            CoreWidth = width,
            GlowWidth = width * 1.85f,
            CoreColor = core,
            GlowColor = glow,
            SourceOrigin = owner.GetSimPos(),
            SweepInnerRadiusRatio = style == KnightTrailStyle.Guardian ? 0.18f : 0.27f,
            SweepOuterExtension = width * 0.75f,
            SweepGlowExpansion = width * 0.4f,
            ThrustStartOffset = 0.28f * worldScale,
            ThrustTipExtension = Mathf.Min(0.75f * worldScale, reach * 0.32f),
            CoreAlpha = style == KnightTrailStyle.DuelistFinisher ? 0.9f : 0.78f,
            GlowAlpha = 0.22f,
            TileLength = 0.32f * worldScale,
            FlowSpeed = shape == MotionRibbonShape.AxialThrust ? 2.1f : 1.35f,
        };
    }

    private static AnimAfterimage CreateAfterimage(
        EquippedWeaponMotionKind kind,
        KnightTrailStyle style,
        float reach)
    {
        if (style == KnightTrailStyle.None || kind == EquippedWeaponMotionKind.GuardHold) return default;
        if (kind is EquippedWeaponMotionKind.Thrust or EquippedWeaponMotionKind.ForwardAnchor)
        {
            return new AnimAfterimage
            {
                Count = kind == EquippedWeaponMotionKind.ForwardAnchor ? 6 : 4,
                Layout = AnimAfterimageLayout.Linear,
                SpacingRatio = 0.09f,
                MinSpacing = 0.18f,
                NewestAlpha = 0.34f,
                OldestAlpha = 0.035f,
                LocalDirection = Vector2.left,
                Tint = Color.white,
            };
        }

        return new AnimAfterimage
        {
            Count = kind == EquippedWeaponMotionKind.Crush || style == KnightTrailStyle.DuelistFinisher ? 7 : 4,
            Layout = AnimAfterimageLayout.Angular,
            NewestAlpha = 0.36f,
            OldestAlpha = 0.035f,
            Tint = Color.white,
            ArcRadius = reach,
            ArcDegreesPerLayer = kind == EquippedWeaponMotionKind.Crush ? 11f : 8f,
            ArcDirection = 1f,
        };
    }

    private static void TintForStyle(KnightTrailStyle style, ref Color core, ref Color glow)
    {
        Color accent = style switch
        {
            KnightTrailStyle.Guardian => new Color(0.58f, 0.43f, 0.2f),
            KnightTrailStyle.Lancer or KnightTrailStyle.Movement => new Color(0.55f, 0.72f, 0.88f),
            KnightTrailStyle.DuelistFinisher => new Color(0.58f, 0.08f, 0.08f),
            _ => new Color(0.86f, 0.88f, 0.92f),
        };
        core = Color.Lerp(core, accent, 0.22f);
        glow = Color.Lerp(glow, accent, 0.2f);
    }

    private readonly struct WeaponDisplayLease
    {
        public readonly Item Weapon;
        public readonly float Until;

        public WeaponDisplayLease(Item weapon, float until)
        {
            Weapon = weapon;
            Until = until;
        }
    }
}
