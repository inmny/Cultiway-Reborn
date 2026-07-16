using Cultiway.Abstract;
using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>基础器形的世界表现档案。器形只配置通用运动参数，不参与能力结算。</summary>
public class ArtifactPresentations : ExtendLibrary<ArtifactPresentationAsset, ArtifactPresentations>
{
    public static ArtifactPresentationAsset Sword { get; private set; }
    public static ArtifactPresentationAsset Seal { get; private set; }
    public static ArtifactPresentationAsset Robe { get; private set; }
    public static ArtifactPresentationAsset Mirror { get; private set; }
    public static ArtifactPresentationAsset Ding { get; private set; }
    public static ArtifactPresentationAsset Banner { get; private set; }
    public static ArtifactPresentationAsset Bell { get; private set; }
    public static ArtifactPresentationAsset Gourd { get; private set; }
    public static ArtifactPresentationAsset Fan { get; private set; }
    public static ArtifactPresentationAsset Tower { get; private set; }
    public static ArtifactPresentationAsset Pearl { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ArtifactPresentation";

    protected override void OnInit()
    {
        Configure(Sword, 0.11f,
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.FacingSide,
                offset = new Vector2(0.36f, 0.57f),
                spacing = new Vector2(0.08f, 0.06f),
                world_size = 0.7f,
                side_rotation = -8f,
                spread_rotation = 7f,
                bob_amplitude = 0.05f,
                sway_amplitude = 4f,
                speed = 0.65f,
                phase_step = 0.79f,
                flip_with_actor = true,
            },
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.Orbit,
                offset = new Vector2(0f, 0.58f),
                world_size = 0.62f,
                base_rotation = 180f,
                orbit_radius = 0.34f,
                orbit_radius_activity = 0.16f,
                orbit_vertical_amplitude = 0.2f,
                speed = 0.75f,
                speed_activity = 1.05f,
                activity_height = 0.1f,
            });

        Configure(Seal, 0.18f,
            HoverLine(new Vector2(0f, 0.82f), 0.22f, 0.56f, 0.06f, 2f, 0.9f),
            HoverLine(new Vector2(0f, 0.82f), 0.22f, 0.56f, 0.16f, 9f, 0.9f, 0.65f));

        Configure(Robe, 0.24f,
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.Linear,
                offset = new Vector2(0f, 0.34f),
                spacing = new Vector2(0.08f, 0f),
                world_size = 0.78f,
                flip_with_actor = true,
            },
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.Linear,
                offset = new Vector2(0f, 0.36f),
                spacing = new Vector2(0.09f, 0f),
                world_size = 0.82f,
                bob_amplitude = 0.025f,
                speed = 1.2f,
                flip_with_actor = true,
            });

        Configure(Mirror, 0.14f,
            SideHover(0.43f, 0.62f, 0.5f, 0.07f, 4f, 0.65f),
            SideHover(0.43f, 0.62f, 0.5f, 0.19f, 14f, 0.65f, 0.45f));

        Configure(Ding, 0.22f,
            HoverLine(new Vector2(0f, 0.38f), 0.2f, 0.62f, 0.04f, 1f, 0.8f),
            HoverLine(new Vector2(0f, 0.38f), 0.2f, 0.62f, 0.11f, 4f, 0.8f, 0.55f, 0.18f));

        Configure(Banner, 0.18f,
            SideHover(0.46f, 0.66f, 0.72f, 0.05f, 7f, 0.75f),
            SideHover(0.48f, 0.7f, 0.82f, 0.12f, 16f, 1.15f, 0.8f));

        Configure(Bell, 0.17f,
            HoverLine(new Vector2(0f, 0.76f), 0.18f, 0.54f, 0.055f, 3f, 0.8f),
            HoverLine(new Vector2(0f, 0.78f), 0.2f, 0.68f, 0.1f, 13f, 1.25f, 0.75f));

        Configure(Gourd, 0.18f,
            SideHover(0.42f, 0.62f, 0.58f, 0.08f, 5f, 0.72f),
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.Orbit,
                offset = new Vector2(0f, 0.64f),
                world_size = 0.7f,
                orbit_radius = 0.38f,
                orbit_radius_activity = 0.18f,
                orbit_vertical_amplitude = 0.22f,
                sway_amplitude = 8f,
                speed = 0.85f,
                speed_activity = 1.1f,
            });

        Configure(Fan, 0.16f,
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.FacingSide,
                offset = new Vector2(0.4f, 0.58f),
                spacing = new Vector2(0.08f, 0.04f),
                world_size = 0.64f,
                side_rotation = -18f,
                spread_rotation = 9f,
                bob_amplitude = 0.045f,
                sway_amplitude = 6f,
                speed = 0.8f,
                flip_with_actor = true,
            },
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.FacingSide,
                offset = new Vector2(0.48f, 0.62f),
                spacing = new Vector2(0.1f, 0.06f),
                world_size = 0.78f,
                side_rotation = -28f,
                spread_rotation = 14f,
                bob_amplitude = 0.1f,
                sway_amplitude = 15f,
                speed = 1.25f,
                flip_with_actor = true,
            });

        Configure(Tower, 0.24f,
            HoverLine(new Vector2(0f, 0.84f), 0.2f, 0.58f, 0.035f, 1f, 0.65f),
            HoverLine(new Vector2(0f, 0.86f), 0.22f, 0.76f, 0.08f, 5f, 0.95f, 0.55f, 0.12f));

        Configure(Pearl, 0.1f,
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.Orbit,
                offset = new Vector2(0f, 0.58f),
                world_size = 0.42f,
                orbit_radius = 0.44f,
                orbit_vertical_amplitude = 0.25f,
                speed = 0.65f,
                phase_step = 1.1f,
            },
            new ArtifactMotionProfile
            {
                layout = ArtifactMotionLayout.Orbit,
                offset = new Vector2(0f, 0.62f),
                world_size = 0.58f,
                orbit_radius = 0.54f,
                orbit_radius_activity = 0.2f,
                orbit_vertical_amplitude = 0.3f,
                speed = 1.2f,
                speed_activity = 1.5f,
                phase_step = 1.25f,
            });

        Sword.vehicle_motion = VehiclePose(90f, -0.13f);
        Seal.vehicle_motion = VehiclePose(0f, -0.12f);
        Robe.vehicle_motion = VehiclePose(0f, -0.1f);
        Mirror.vehicle_motion = VehiclePose(0f, -0.11f);
        Ding.vehicle_motion = VehiclePose(0f, -0.1f);
        Banner.vehicle_motion = VehiclePose(90f, -0.12f);
        Bell.vehicle_motion = VehiclePose(0f, -0.12f);
        Gourd.vehicle_motion = VehiclePose(90f, -0.12f);
        Fan.vehicle_motion = VehiclePose(-10f, -0.12f);
        Tower.vehicle_motion = VehiclePose(0f, -0.08f);
        Pearl.vehicle_motion = VehiclePose(0f, -0.12f);
    }

    private static ArtifactMotionProfile VehiclePose(float rotation, float y)
    {
        return new ArtifactMotionProfile
        {
            layout = ArtifactMotionLayout.Linear,
            offset = new Vector2(0f, y),
            world_size = 1f,
            base_rotation = rotation,
            bob_amplitude = 0.012f,
            speed = 1.8f,
            flip_with_actor = false,
        };
    }

    private static void Configure(
        ArtifactPresentationAsset presentation,
        float bodyRadius,
        ArtifactMotionProfile idle,
        ArtifactMotionProfile active)
    {
        presentation.body_radius = bodyRadius;
        presentation.active_pixel_scale = 1f;
        presentation.idle_motion = idle;
        presentation.active_motion = active;
    }

    private static ArtifactMotionProfile HoverLine(
        Vector2 offset,
        float spacing,
        float size,
        float bob,
        float sway,
        float speed,
        float speedActivity = 0f,
        float activityHeight = 0f)
    {
        return new ArtifactMotionProfile
        {
            layout = ArtifactMotionLayout.Linear,
            offset = offset,
            spacing = new Vector2(spacing, 0f),
            world_size = size,
            bob_amplitude = bob,
            sway_amplitude = sway,
            speed = speed,
            speed_activity = speedActivity,
            activity_height = activityHeight,
        };
    }

    private static ArtifactMotionProfile SideHover(
        float x,
        float y,
        float size,
        float bob,
        float sway,
        float speed,
        float speedActivity = 0f)
    {
        return new ArtifactMotionProfile
        {
            layout = ArtifactMotionLayout.AlternatingSides,
            offset = new Vector2(x, y),
            spacing = new Vector2(0.08f, 0.04f),
            world_size = size,
            bob_amplitude = bob,
            sway_amplitude = sway,
            speed = speed,
            speed_activity = speedActivity,
            phase_step = 1.17f,
        };
    }
}
