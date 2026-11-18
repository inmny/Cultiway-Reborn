using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>同组资源按已有真实分件建立共享视图，运行中不再访问网格。</summary>
public sealed partial class FormResource
{
    /// <summary>分件视图保持原资源存活，使同组释放观察包含这些实际使用者。</summary>
    private readonly FormResource whole;
    /// <summary>只在编译需要分件时建立的共享视图。</summary>
    private FormResource[] pieces;
    /// <summary>加载边界取得的该分件模型空间中心。</summary>
    internal readonly Vector3 Centre;
    /// <summary>分件绑定空间边界，只用于没有攻击形状的装饰碎片着地。</summary>
    private readonly Bounds pieceBounds;
    /// <summary>中心的主要控制，仅用于刚体转动中心，几何仍保留完整蒙皮。</summary>
    private readonly int centreControl = -1;

    /// <summary>在编译主线程读取一次原网格，返回本组共用的真实分件。</summary>
    /// <param name="part">已经核对的原部件索引。</param>
    internal FormResource Part(int part)
    {
        pieces ??= new FormResource[Meshes.Length];
        return pieces[part] ??= new FormResource(this, part);
    }

    /// <summary>当前分件的模型空间中心；不读取Unity对象。</summary>
    /// <param name="pose">源体当时的共同骨架姿态。</param>
    internal Vector3 PosedCentre(FormPose pose) => centreControl < 0 ? Centre : pose.Skin[centreControl].MultiplyPoint3x4(Centre);

    /// <summary>装饰碎片的近似运动边界，不增加人物或法术作用形状。</summary>
    /// <param name="pose">源体当前姿态。</param>
    internal Bounds PosedBounds(FormPose pose)
    {
        if (centreControl < 0) return pieceBounds;
        Bounds bounds = default;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = pieceBounds.center + Vector3.Scale(pieceBounds.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            point = pose.Skin[centreControl].MultiplyPoint3x4(point);
            if (corner == 0) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
        }
        return bounds;
    }

    /// <summary>主要控制的当前方向，完整几何仍使用原有全部蒙皮权重。</summary>
    /// <param name="pose">源体当时的共同姿态。</param>
    internal Quaternion PosedRotation(FormPose pose) => centreControl < 0 ? Quaternion.identity : pose.Skin[centreControl].rotation;

    /// <summary>保留原部件几何、表面与绑定，不制作缩小整物作为碎片。</summary>
    /// <param name="source">本版本原资源。</param>
    /// <param name="part">真实部件。</param>
    private FormResource(FormResource source, int part)
    {
        whole = source;
        Generation = source.Generation;
        ContactVolume = source.ContactVolume;
        Builds = source.Description.Parts[part].HasBuildData;
        Rig = source.Rig;
        Facing = source.Facing;
        FormDescription original = source.Description;
        int[] contacts = Enumerable.Range(0, original.Contacts.Length).Where(i => original.Contacts[i].Part == part).ToArray();
        int[] anchors = Enumerable.Range(0, original.Anchors.Length).Where(i => original.Anchors[i].Part == part).ToArray();
        Description = new FormDescription
        {
            Version = original.Version,
            Id = original.Id + ":" + original.Parts[part].Id,
            Structure = new FormStructureDescription
            {
                Kind = original.Structure.Kind,
                UnitInTiles = original.Structure.UnitInTiles,
                Generation = original.Structure.Generation,
                Changes = original.Structure.Changes.Where(change => change != "separate" && change != "crack" && change != "ground" &&
                    (change != "material" || original.Parts[part].UseMatterSurface) &&
                    (change != "build" || original.Parts[part].HasBuildData)).ToArray()
            },
            Forward = (float[])original.Forward.Clone(),
            Up = (float[])original.Up.Clone(),
            Parts = new[] { original.Parts[part] },
            Controls = original.Controls,
            Motions = original.Motions,
            Grips = original.Grips.Where(grip => anchors.Any(i => original.Anchors[i].Id == grip.Anchor)).ToArray(),
            Contacts = contacts.Select(i => new FormContactDescription
            {
                Id = original.Contacts[i].Id,
                Part = 0,
                A = original.Contacts[i].A,
                B = original.Contacts[i].B,
                Radius = original.Contacts[i].Radius,
                HalfExtents = original.Contacts[i].HalfExtents,
                AControl = original.Contacts[i].AControl,
                BControl = original.Contacts[i].BControl,
                AInfluences = original.Contacts[i].AInfluences,
                BInfluences = original.Contacts[i].BInfluences,
                BuildRange = original.Contacts[i].BuildRange,
                BuildAlong = original.Contacts[i].BuildAlong
            }).ToArray(),
            Anchors = anchors.Select(i => new FormAnchorDescription
            {
                Id = original.Anchors[i].Id,
                Part = 0,
                Position = original.Anchors[i].Position,
                Direction = original.Anchors[i].Direction,
                Control = original.Anchors[i].Control,
                Influences = original.Anchors[i].Influences,
                BuildAt = original.Anchors[i].BuildAt
            }).ToArray()
        };
        Meshes = new[] { source.Meshes[part] };
        detailMeshes = new[] { source.detailMeshes[part] };
        PartControls = new[] { source.PartControls[part] };
        Contacts = contacts.Select(i => source.Contacts[i]).ToArray();
        Anchors = anchors.Select(i => source.Anchors[i]).ToArray();
        LoadGrips();
        Structure = new FormStructure(Description);
        pieceBounds = Meshes[0].bounds;
        RestBounds = pieceBounds;
        RestSize = pieceBounds.size.magnitude;
        Centre = pieceBounds.center;
        centreControl = PartControls[0];
        if (!original.Parts[part].Skin) return;
        var controls = new List<Vector4>(); var weights = new List<Vector4>();
        Meshes[0].GetUVs(1, controls); Meshes[0].GetUVs(2, weights);
        var sums = new float[Rig.Count];
        for (int vertex = 0; vertex < controls.Count; vertex++)
            for (int entry = 0; entry < 4; entry++) sums[(int)controls[vertex][entry]] += weights[vertex][entry];
        centreControl = Array.IndexOf(sums, sums.Max());
    }
}
