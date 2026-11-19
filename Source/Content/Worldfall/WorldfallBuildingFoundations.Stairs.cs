#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

internal static partial class WorldfallBuildingFoundations
{
    /// <summary>石阶顶端接入口，底端埋入实际地面，沿用主体的朝向与镜像。</summary>
    /// <param name="foundation">本座建筑复用的附件。</param><param name="body">已经放置的主体。</param>
    /// <param name="scale">主体按真实占地使用的比例。</param><param name="terrain">实际地面。</param>
    /// <param name="meshes">本帧绘制列表。</param>
    private static void AddStairs(Foundation foundation, MeshInstance body, float scale, TerrainCache terrain, List<MeshInstance> meshes)
    {
        FootingShape shape = foundation.Shape;
        if (body.ShadowOnly) return;
        while (foundation.Stairs.Count < shape.Entrances.Count) foundation.Stairs.Add(new MeshInstance());
        for (int i = 0; i < shape.Entrances.Count; i++)
            AddEntranceStairs(shape.Entrances[i], foundation.Stairs[i], foundation.Tag, body, scale, terrain, meshes);
    }

    /// <summary>每个入口分别接地，背面的入口同时反转阶梯前后和左右，保留正确面向。</summary>
    /// <param name="entrance">实际门面。</param><param name="stairs">该入口复用的石阶。</param>
    /// <param name="tag">主体点击编号。</param><param name="body">已经放置的主体。</param>
    /// <param name="scale">主体真实比例。</param><param name="terrain">实际地面。</param><param name="meshes">绘制列表。</param>
    private static void AddEntranceStairs(Entrance entrance, MeshInstance stairs, int tag, MeshInstance body,
        float scale, TerrainCache terrain, List<MeshInstance> meshes)
    {
        Vec3 top = body.Position + body.Basis.Mul(entrance.Position);
        Vec3 forward = body.Basis.C1 * (-entrance.Side / scale);
        float nearGround = terrain.SampleHeight(top.X - forward.X * 0.4f, top.Y - forward.Y * 0.4f);
        float run = Math.Max(0.3f, Math.Min(6f, (top.Z - nearGround) * 1.75f));
        Vec3 foot = top - forward * run;
        foot.Z = terrain.SampleHeight(foot.X, foot.Y) - 0.03f;
        float height = top.Z - foot.Z;
        if (height <= 0.12f) return;
        float width = Math.Max(0.8f, Math.Min(1.4f, scale * 0.1f));
        Vec3 size = stairs.Model == null ? new Vec3(0, 0, 0) : stairs.Model.Max - stairs.Model.Min;
        if (stairs.Model == null || Math.Abs(height - size.Z) > 0.02f ||
            Math.Abs(run - size.Y) > 0.02f || Math.Abs(width - size.X) > 0.02f)
        {
            stairs.Model = CreateStairs(width, run, height);
            size = stairs.Model.Max - stairs.Model.Min;
            stairs.Palette = stairs.Model.SlotColor;
        }
        stairs.EnsurePose();
        stairs.Position = foot;
        stairs.Basis = new Mat3(body.Basis.C0 * (-entrance.Side * width / (scale * size.X)),
            forward * (run / size.Y), new Vec3(0, 0, height / size.Z));
        stairs.Static = true;
        stairs.KeepSmall = true;
        stairs.Tag = tag;
        meshes.Add(stairs);
    }

    /// <summary>先铺踏步和立面，再封两侧、背面与底面，内部不堆叠重复石块。</summary>
    /// <param name="width">石阶总宽。</param><param name="run">底端到入口的水平距离。</param>
    /// <param name="height">地面到入口的高差。</param><returns>闭合的灰色石阶。</returns>
    private static MeshModel CreateStairs(float width, float run, float height)
    {
        MeshBuilder builder = new("cultiway_entrance_stairs");
        int stone = builder.Slot("stone", 4, TerrainCache.Rgb(133, 137, 141));
        int tread = builder.Slot("stone_tread", 4, TerrainCache.Rgb(163, 165, 162));
        builder.Part("stairs", -1, new Vec3(0, 0, 0));
        int count = Math.Max(1, (int)Math.Ceiling(height / 0.18f));
        float half = width * 0.5f, depth = run / count, rise = height / count;
        Vec3[] profile = new Vec3[2 * count + 2];
        profile[0] = new Vec3(-half, run, 0);
        profile[1] = new Vec3(-half, 0, 0);
        for (int i = 0; i < count; i++)
        {
            float front = depth * i, back = depth * (i + 1), bottom = rise * i, top = rise * (i + 1);
            builder.Face(tread, [new(-half, front, top), new(half, front, top), new(half, back, top), new(-half, back, top)]);
            builder.Face(stone, [new(-half, front, bottom), new(half, front, bottom), new(half, front, top), new(-half, front, top)]);
            profile[2 + i * 2] = new Vec3(-half, front, top);
            profile[3 + i * 2] = new Vec3(-half, back, top);
        }
        builder.Face(stone, [new(half, run, 0), new(-half, run, 0), new(-half, run, height), new(half, run, height)]);
        builder.Face(stone, [new(-half, 0, 0), new(-half, run, 0), new(half, run, 0), new(half, 0, 0)]);
        // 两侧都以靠门的底角扇切，避免从凹形阶梯的顶角切出跨越踏步的三角形。
        for (int i = 1; i < profile.Length - 1; i++)
        {
            Vec3 a = profile[0], b = profile[i], c = profile[i + 1];
            builder.Face(stone, [a, b, c]);
            a.X = b.X = c.X = half;
            builder.Face(stone, [a, c, b]);
        }
        return builder.Build();
    }
}
#endif
