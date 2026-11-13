using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.CreatureCompositions.Visuals;

/// <summary>兽形原图的实际部位和关节制作表，不套用人类肩点或头图。</summary>
internal static class NativeCreatureRegions
{
    /// <summary>原物种及原帧对应的完整制作表，资源清理后重新读取。</summary>
    private static Catalog catalog;

    /// <summary>只采用同一物种、原帧和实际尺寸，未提供的身体不借用别的物种。</summary>
    /// <param name="actor">实际身体。</param><param name="sprite">未合成的原版帧。</param>
    internal static Frame Get(Actor actor, Sprite sprite)
    {
        if (actor.asset.is_humanoid || !actor.isAdult() || sprite == null) return null;
        if (catalog == null) Load();
        return catalog.Actors.TryGetValue(actor.asset.id, out var frames) && frames.TryGetValue(sprite.name, out Frame frame) &&
            frame.Width == sprite.rect.width && frame.Height == sprite.rect.height ? frame : null;
    }

    /// <summary>加载边界统一核对实际尺寸、区域和关节，随后只读展开的数值。</summary>
    private static void Load()
    {
        string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content/BodyVisuals/animal-regions.json");
        Catalog data = JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(path));
        foreach (var actor in data.Actors)
        foreach (var entry in actor.Value)
        {
            Frame frame = entry.Value;
            if (frame.Width <= 0 || frame.Height <= 0 || frame.Rows.Length != frame.Height ||
                frame.Rows.Any(row => row.Length != frame.Width || row.Any(c => !data.Roles.ContainsKey(c.ToString()))) ||
                frame.Joints.Any(joint => joint.Value.Length != 2 || joint.Value.Any(value => float.IsNaN(value) || float.IsInfinity(value)) ||
                    joint.Value[0] < 0f || joint.Value[0] > frame.Width || joint.Value[1] < 0f || joint.Value[1] > frame.Height) ||
                frame.Parents.Any(parent => !data.Roles.ContainsValue(parent.Key) || !data.Roles.ContainsValue(parent.Value)) ||
                frame.Supports.Any(part => !data.Roles.ContainsValue(part)))
                throw new InvalidDataException("兽形部位制作表无效：" + actor.Key + "/" + entry.Key);
            frame.Regions = new string[frame.Width * frame.Height];
            for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++) frame.Regions[y * frame.Width + x] = data.Roles[frame.Rows[frame.Height - 1 - y][x].ToString()];
        }
        catalog = data;
    }

    /// <summary>换世界及资源重载不保留旧制作表。</summary>
    internal static void Clear() => catalog = null;

    /// <summary>各原身体和帧的完整制作入口。</summary>
    private sealed class Catalog
    {
        /// <summary>文字像素对应的实际部位。</summary>
        public Dictionary<string, string> Roles { get; set; }
        /// <summary>原身体编号及各自原帧。</summary>
        public Dictionary<string, Dictionary<string, Frame>> Actors { get; set; }
    }

    /// <summary>一个真实兽形帧的部位及关节，像素尺寸与原图一致。</summary>
    internal sealed class Frame
    {
        /// <summary>原图实际像素宽和高。</summary>
        public int Width { get; set; }
        /// <summary>原图实际像素高。</summary>
        public int Height { get; set; }
        /// <summary>从上往下的部位文字行。</summary>
        public string[] Rows { get; set; }
        /// <summary>原图左下角坐标中的真实关节。</summary>
        public Dictionary<string, float[]> Joints { get; set; }
        /// <summary>明确制作的父部位；空表不补出任何手脚或骨段。</summary>
        public Dictionary<string, string> Parents { get; set; } = new();
        /// <summary>本帧实际可见的足底等承托部位。</summary>
        public string[] Supports { get; set; } = Array.Empty<string>();
        /// <summary>按原图片左下角展开的不可变部位名。</summary>
        [JsonIgnore] internal string[] Regions;
    }
}
