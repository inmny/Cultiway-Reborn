using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原版身体的自然帧细分，身体、衣甲及仍可见器官保留同一物理面。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>沿制作部位分配实际可见像素，不按全身范围替代掌足。</summary>
    private sealed partial class Native
    {
        /// <summary>同一图片的四种臂链遮挡布局，姿态改变只更新变换。</summary>
        private readonly Dictionary<BodyOccupancy, BodySurfacePart[]> armShadows = new();

        /// <summary>原身体与真正出招臂链的遮挡布局。</summary>
        private BodySurfaceComposition actionShadow;

        /// <summary>遮挡去掉原臂链，再与实际不透明skin臂链表面合并。</summary>
        /// <param name="body">本轮身体。</param><param name="actions">实际动作。</param>
        internal override BodySurfacePose IncludeActionOcclusion(BodySurfacePose body,
            IReadOnlyList<BodySurfacePose> actions)
        {
            if (body.Opaque == null) return body;
            var sources = new List<BodySurfacePose> { body.Opaque };
            foreach (BodySurfacePose action in actions)
                if (action.Opaque?.Parts.Length > 0) sources.Add(action.Opaque);
            if (sources.Count == 1) return body;
            if (actionShadow == null || !actionShadow.Matches(sources, BodyOccupancy.None, false))
                actionShadow = new BodySurfaceComposition(sources, BodyOccupancy.None, false);
            return OccludeHeld(body.WithOpaque(actionShadow.Sample(sources)));
        }

        /// <summary>只按实际接管裁切原图透明度，不让旧手臂留下隐形遮挡。</summary>
        /// <param name="basis">同轮原遮挡。</param><param name="arms">实际接管。</param>
        private BodySurfacePose ArmShadow(BodySurfacePose basis, BodyOccupancy arms)
        {
            if (basis == null || basis.Parts.Length == 0 || arms == BodyOccupancy.None) return basis;
            if (!armShadows.TryGetValue(arms, out BodySurfacePart[] parts))
            {
                byte[] alpha = NativeBodyRegions.CutAlpha(Actor, sprite, nativeFrame, bodyAlpha, arms);
                parts = OpaquePixels(sprite, Vector2.zero, basis.Height / Actor.actor_scale, alpha);
                armShadows.Add(arms, parts);
            }
            return new BodySurfacePose(parts, Array.ConvertAll(parts, _ => basis.Matrices[0]), basis.Foot,
                basis.Height, basis.Heading);
        }

        /// <summary>先沿原帧位置读取部位，再合并实际装备和器官选择；不增加顶点或复制表面。</summary>
        /// <param name="native">原动画帧。</param><param name="names">已有可见归属组。</param>
        /// <param name="owners">最终帧逐像素归属。</param>
        private void ClassifyRegions(Sprite native, ref string[][] names, ref int[] owners)
        {
            string[] regions = NativeBodyRegions.Get(Actor, native);
            if (regions == null) return;
            int width = Mathf.RoundToInt(sprite.rect.width), height = Mathf.RoundToInt(sprite.rect.height),
                sourceWidth = Mathf.RoundToInt(native.rect.width), sourceHeight = Mathf.RoundToInt(native.rect.height),
                dx = Mathf.RoundToInt(sprite.pivot.x - native.pivot.x),
                dy = Mathf.RoundToInt(sprite.pivot.y - native.pivot.y);
            owners = owners == null ? new int[bodyAlpha.Length] : (int[])owners.Clone();
            var groups = new List<string[]>(names);
            var known = new Dictionary<(int group, string region), int>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int pixel = y * width + x;
                if (bodyAlpha[pixel] == 0) continue;
                int group = owners[pixel];
                string[] original = names[group];
                if (Array.IndexOf(original, "head") >= 0 ||
                    Array.IndexOf(original, "torso") < 0 && Array.IndexOf(original, "armor.boots") < 0) continue;
                int nx = x - dx, ny = y - dy;
                if (nx < 0 || nx >= sourceWidth || ny < 0 || ny >= sourceHeight) continue;
                string region = regions[ny * sourceWidth + nx];
                var key = (group, region);
                if (!known.TryGetValue(key, out int assigned))
                {
                    assigned = groups.Count;
                    known.Add(key, assigned);
                    var selected = new List<string>();
                    selected.AddRange(original.Where(name => name.StartsWith("organ:", StringComparison.Ordinal)));
                    selected.Add(region);
                    string parent = NativeBodyRegions.Parent(region);
                    if (parent != null) selected.Add(parent);
                    selected.AddRange(original.Where(name => name != "torso" &&
                        !name.StartsWith("organ:", StringComparison.Ordinal)));
                    groups.Add(selected.Distinct(StringComparer.Ordinal).ToArray());
                }
                owners[pixel] = assigned;
            }
            names = groups.ToArray();
        }
    }
}
