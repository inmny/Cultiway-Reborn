using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>不同材料共用空间粒子的时间和运动资料。</summary>
internal enum SpatialParticleMaterial : byte
{
    /// <summary>稀疏灵光。</summary>
    Energy,
    /// <summary>生长和恢复时的叶片。</summary>
    Leaf,
    /// <summary>兵器、屏障和地面变化抛出的实体碎屑。</summary>
    Fragment,
    /// <summary>随真实积水变动的小水珠。</summary>
    Water,
}

/// <summary>只保存已发生作用的短暂表现，按世界时间在两个视角中生成同一份粒子和连接。</summary>
internal static class SpatialVisualEffects
{
    /// <summary>短时装饰的存储上限；满时省略装饰，不影响任何玩法实体。</summary>
    private const int Capacity = 768;
    /// <summary>当前世界的短时表现。</summary>
    private static readonly List<Effect> effects = new(Capacity);
    /// <summary>本世界内递增的纯视觉种子，不消耗战斗随机数。</summary>
    private static int serial;
    /// <summary>可选空间视角最近一次提交的平面位置。</summary>
    private static Vector2 eye;
    /// <summary>可选空间视角的实际收集范围。</summary>
    private static float reach;
    /// <summary>最近一次空间收集所在显示帧。</summary>
    private static int viewFrame = -10;
    /// <summary>供验收检查回收是否完成。</summary>
    internal static int Count => effects.Count;

    /// <summary>由显示边界提供当前实际视点，核心不依赖外部模组类型。</summary>
    /// <param name="position">当前视点的地图坐标。</param><param name="distance">当前收集距离。</param>
    internal static void SetView(Vector2 position, float distance) { eye = position; reach = distance; viewFrame = Time.frameCount; }

    /// <summary>仅用于省略不可见装饰，不参与作用范围和命中。</summary>
    /// <param name="position">作用位置。</param><param name="radius">装饰外缘。</param>
    internal static bool IsRelevant(Vector3 position, float radius = 0f)
    {
        if (Time.frameCount - viewFrame <= 2) return Vector2.Distance(position, eye) <= reach + radius;
        if (!MapBox.isRenderGameplay()) return false;
        Camera camera = World.world.camera;
        Vector3 p = camera.WorldToViewportPoint(new Vector3(position.x, position.y + position.z));
        float margin = radius / Mathf.Max(1f, camera.orthographicSize) + .05f;
        return p.x >= -margin && p.x <= 1f + margin && p.y >= -margin && p.y <= 1f + margin;
    }

    /// <summary>创建可组合的空间粒子，升高始终写入独立高度。</summary>
    /// <param name="position">起点。</param><param name="velocity">平面速度与升降速度。</param><param name="color">颜色。</param>
    /// <param name="size">世界尺寸。</param><param name="duration">世界秒数。</param><param name="material">实际材料对应的形体和亮度。</param>
    /// <param name="gravity">独立高度方向的加速度。</param>
    internal static void Particle(Vector3 position, Vector3 velocity, Color color, float size, float duration,
        SpatialParticleMaterial material = SpatialParticleMaterial.Energy, float gravity = 0f)
    {
        if (effects.Count >= Capacity || !IsRelevant(position, size)) return;
        effects.Add(new Effect { Kind = Kind.Particle, Material = material, Start = position, End = velocity, Color = color,
            Size = size, Duration = duration, Started = World.world.getCurWorldTime(), Seed = ++serial, Gravity = gravity });
    }

    /// <summary>保存真实两端，不把连锁、光束或牵引改成飞行弹丸。</summary>
    /// <param name="start">实际起点。</param><param name="end">实际终点。</param><param name="color">颜色。</param>
    /// <param name="width">宽度。</param><param name="duration">世界秒数。</param>
    internal static void Link(Vector3 start, Vector3 end, Color color, float width, float duration)
    {
        if (effects.Count >= Capacity || !IsRelevant((start + end) * .5f, Vector3.Distance(start, end))) return;
        effects.Add(new Effect { Kind = Kind.Link, Start = start, End = end, Color = color, Size = width,
            Duration = duration, Started = World.world.getCurWorldTime(), Seed = ++serial });
    }

    /// <summary>在已发生作用的位置展开波纹，允许表达向内汇聚。</summary>
    /// <param name="center">实际中心。</param><param name="radius">真实范围。</param><param name="color">颜色。</param>
    /// <param name="duration">世界秒数。</param><param name="inward">是否向内汇聚。</param><param name="delay">装饰延迟。</param>
    internal static void Ring(Vector3 center, float radius, Color color, float duration, bool inward = false, float delay = 0f)
    {
        if (effects.Count >= Capacity || !IsRelevant(center, radius)) return;
        effects.Add(new Effect { Kind = Kind.Ring, Start = center, Color = color, Size = radius, Duration = duration,
            Started = World.world.getCurWorldTime() + delay, Inward = inward, Seed = ++serial });
    }

    /// <summary>按世界时间采样，暂停和更换镜头不会推进粒子。</summary>
    /// <param name="output">本帧共用快照。</param><param name="details">全帧装饰预算。</param>
    internal static void Append(List<SpatialVisualMeshInstance> output, ref int details)
    {
        double now = World.world.getCurWorldTime();
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            Effect effect = effects[i];
            float age = (float)(now - effect.Started);
            if (age >= effect.Duration) { effects.RemoveAt(i); continue; }
            if (age < 0f || !IsRelevant(effect.Start, effect.Size)) continue;
            float progress = age / effect.Duration;
            Color color = effect.Color; color.a *= (1f - progress) * (1f - progress);
            if (effect.Kind == Kind.Link)
            {
                // 分支形状固定在此次作用上，镜头切换不重新随机。
                Vector3 delta = effect.End - effect.Start;
                Vector3 side = Vector3.Cross(delta.normalized, Vector3.forward);
                Vector3 previous = effect.Start;
                for (int n = 1; n <= 6; n++)
                {
                    Vector3 next = Vector3.Lerp(effect.Start, effect.End, n / 6f);
                    if (n < 6) next += side * Mathf.Sin(effect.Seed * 3.17f + n * 7.71f) * Mathf.Min(.12f, delta.magnitude * .025f);
                    SpatialVisualDraw.Rod(output, previous, next, effect.Size, color, true); previous = next;
                }
            }
            else if (effect.Kind == Kind.Ring)
            {
                if (details <= 0) continue;
                details--;
                float scale = effect.Inward ? 1f - progress * .9f : .1f + progress * .9f;
                SpatialVisualDraw.Ring(output, effect.Start, effect.Size * scale, color, Quaternion.identity);
            }
            else if (details > 0)
            {
                details--;
                Vector3 position = effect.Start + effect.End * age + Vector3.forward * effect.Gravity * age * age * .5f;
                SpatialVisualMesh model = effect.Material switch
                {
                    SpatialParticleMaterial.Leaf => SpatialVisualGeometry.Leaf,
                    SpatialParticleMaterial.Fragment => SpatialVisualGeometry.Shard,
                    SpatialParticleMaterial.Water => SpatialVisualGeometry.Stone,
                    _ => SpatialVisualGeometry.Spark,
                };
                bool energy = effect.Material == SpatialParticleMaterial.Energy;
                Vector3 scale = Vector3.one * effect.Size * (1f - progress * .55f);
                if (effect.Material == SpatialParticleMaterial.Water) scale.z *= 1.45f;
                SpatialVisualDraw.Mesh(output, model, position,
                    Quaternion.Euler(effect.Seed * 47f, age * 120f, effect.Seed * 71f), scale, color, energy,
                    additive: energy);
            }
        }
    }

    /// <summary>世界切换时释放短时资料与视点，不保留旧世界时间。</summary>
    internal static void ClearWorldState() { effects.Clear(); serial = 0; viewFrame = -10; }

    /// <summary>短时装饰的实际形式。</summary>
    private enum Kind
    {
        /// <summary>短暂碎屑。</summary>
        Particle,
        /// <summary>真实连锁两端。</summary>
        Link,
        /// <summary>短暂作用波纹。</summary>
        Ring,
    }
    /// <summary>一次作用的冻结起点和纯显示参数。</summary>
    private sealed class Effect
    {
        /// <summary>实际形式。</summary>
        internal Kind Kind;
        /// <summary>粒子的实体材质；连接不使用此项。</summary>
        internal SpatialParticleMaterial Material;
        /// <summary>起点，以及连接终点或粒子速度。</summary>
        internal Vector3 Start, End;
        /// <summary>语义颜色。</summary>
        internal Color Color;
        /// <summary>尺寸、存活时间和高度加速度。</summary>
        internal float Size, Duration, Gravity;
        /// <summary>开始世界时间。</summary>
        internal double Started;
        /// <summary>固定形状种子。</summary>
        internal int Seed;
        /// <summary>波纹是否收拢。</summary>
        internal bool Inward;
    }
}
