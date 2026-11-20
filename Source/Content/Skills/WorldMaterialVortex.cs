using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一个真实深口正在牵引的有限散件，保存只记录世界材料的当前姿态。</summary>
internal sealed class WorldVortexRun
{
    /// <summary>至多十二件，避免相邻世界地物造成无界工作。</summary>
    internal const int Limit = 12;

    /// <summary>本次接纳的独立世界材料。</summary>
    internal readonly List<Piece> Pieces = new();

    /// <summary>本次已经接纳的总体积，以及上轮推进和下轮选取时间。</summary>
    internal double Accepted, Previous, NextPick;

    /// <summary>保留已接纳的同一记录和它上轮的形变矩阵。</summary>
    internal sealed class Piece
    {
        /// <summary>仍由世界维护的、带真实身份和余款的材料。</summary>
        internal readonly WorldMaterialRecord Row;
        /// <summary>固定的内部形状。</summary>
        internal readonly FormPose Pose;
        /// <summary>上一轮保留的拉伸，防止逐轮叠加尺寸。</summary>
        internal Matrix4x4 Stretch = Matrix4x4.identity;

        /// <summary>用当前真实材料接入。</summary>
        /// <param name="row">已核对双重归属和实际受蚀的散件。</param>
        internal Piece(WorldMaterialRecord row)
        {
            Row = row;
            Pose = row.Material.CaptureSolidPose();
        }
    }

    /// <summary>已发生的位移保留在原记录上，解绑后马上允许普通世界规则接手。</summary>
    internal void Release()
    {
        foreach (var piece in Pieces)
            if (piece.Row.Vortex == this) piece.Row.Vortex = null;
        Pieces.Clear();
    }
}

public sealed partial class WorldMaterialService
{
    /// <summary>只在共同推进确认后更新原材料。一个记录同时只交给一个实际深口。</summary>
    /// <param name="run">本部位的运行关系。</param><param name="rule">实际口径和牵引能力。</param>
    /// <param name="owner">当前真实控制者。</param><param name="centre">当前外唇中心。</param>
    /// <param name="clock">已确认的世界时点。</param><param name="cast">本次施放。</param>
    /// <param name="ability">正式构造编号。</param>
    internal static void DriveVortex(WorldVortexRun run, CompiledMaterialVortex rule, long owner, Vector3 centre,
        double clock, long cast, string ability)
    {
        long epoch = instance.generation;
        float dt = run.Previous > 0 ? (float)(clock - run.Previous) : 0f;
        run.Previous = clock;
        if (clock >= run.NextPick && run.Pieces.Count < WorldVortexRun.Limit && run.Accepted < rule.MaximumVolume)
        {
            run.NextPick = clock + .12;
            instance.PickVortexPieces(run, rule, owner, centre, cast, ability);
            if (epoch != instance.generation) return;
        }
        if (dt <= 0f) return;

        Vector3 throat = centre - Vector3.forward * rule.Depth;
        for (int i = run.Pieces.Count - 1; i >= 0; i--)
        {
            var piece = run.Pieces[i];
            var row = piece.Row;
            if (row.Finished || row.Recovering || row.Provider != owner || row.Controller != owner ||
                row.Vortex != run || row.Material.Flow != null || row.Material.Matter.State != "solid")
            {
                if (row.Vortex == run) row.Vortex = null;
                run.Pieces.RemoveAt(i);
                continue;
            }
            instance.MoveVortexPiece(piece, rule, throat, dt);
            if (!instance.FitsThroat(row, throat, rule.Throat)) continue;

            // 到达并能通过才消解，扣除真正剩余的量和本金，不提交任何回能。
            var material = row.Material;
            double volume = material.Volume, resource = material.Resource;
            material.InterruptErosion();
            material.Retain(0f, recovery: false);
            material.Weathering.Add(volume, volume, resource);
            row.Finished = true;
            row.Vortex = null;
            run.Pieces.RemoveAt(i);
            Changed?.Invoke(new WorldMaterialChange(row, "vortex-dissolve", castId: cast, abilityId: ability,
                convertedVolume: volume));
            if (epoch != instance.generation) return;
        }
    }

    /// <summary>用实际范围筛出已经受蚀、与施放容器分开的固体。敌物受蚀不改变它的控制权。</summary>
    /// <param name="run">实际牵引关系。</param><param name="rule">选取范围及容量。</param>
    /// <param name="owner">需同时是原支付者和控制者。</param><param name="centre">当前外唇中心。</param>
    /// <param name="cast">本次施放。</param><param name="ability">实际编号。</param>
    private void PickVortexPieces(WorldVortexRun run, CompiledMaterialVortex rule, long owner, Vector3 centre,
        long cast, string ability)
    {
        if (owner <= 0) return;
        RebuildIndex();
        float radius = rule.Radius, vertical = rule.HalfHeight;
        index.Query(new Bounds(centre - Vector3.forward * rule.Depth * .5f, new Vector3(radius * 2, radius * 2, vertical * 2)), nearby);
        // 先复制稳定编号，再发布结果。回调可能使索引失效。
        long[] candidates = nearby.Select(i => indexed[i].Id).OrderBy(id => id).ToArray();
        long epoch = generation;
        foreach (long id in candidates)
        {
            if (run.Pieces.Count == WorldVortexRun.Limit) return;
            if (!rows.TryGetValue(id, out var row) || row.Vortex != null || row.Finished || row.Recovering ||
                row.Provider != owner || row.Controller != owner || row.WorldGrid) continue;
            var material = row.Material;
            if (material.Flow != null || material.Matter.State != "solid" || material.Weathering.LostVolume <= 0 ||
                material.Volume > rule.MaximumVolume - run.Accepted) continue;
            Vector3 delta = (Vector3)material.Transform.GetColumn(3) - centre;
            if (Mathf.Abs(delta.z + rule.Depth * .5f) > vertical || new Vector2(delta.x, delta.y).sqrMagnitude > radius * radius)
                continue;
            run.Accepted += material.Volume;
            run.Pieces.Add(new WorldVortexRun.Piece(row));
            row.Vortex = run;
            Changed?.Invoke(new WorldMaterialChange(row, "vortex-draw", castId: cast, abilityId: ability));
            if (epoch != generation) return;
        }
    }

    /// <summary>以实际体积决定速度，明确定下位置、螺旋入向以及无增量拉伸。</summary>
    /// <param name="piece">同一原散件的运行关系。</param><param name="rule">实际喉口与推进速度。</param>
    /// <param name="throat">当前内部喉口的位置。</param><param name="dt">共同已确认秒数。</param>
    private void MoveVortexPiece(WorldVortexRun.Piece piece, CompiledMaterialVortex rule, Vector3 throat, float dt)
    {
        var material = piece.Row.Material;
        Vector3 from = material.Transform.GetColumn(3), delta = from - throat;
        var planar = new Vector2(delta.x, delta.y);
        float distance = planar.magnitude;
        Bounds bounds = BoundsOf(piece.Row);
        float width = new Vector2(bounds.extents.x, bounds.extents.y).magnitude;
        float limit = width <= rule.Throat * .86f ? 0f : rule.Throat + width * .45f;
        float speed = rule.Speed / Mathf.Sqrt(Mathf.Max(1f, (float)material.Volume));
        float until = Mathf.MoveTowards(distance, limit, speed * dt);
        Vector3 direction = Quaternion.AngleAxis(64f * dt, Vector3.forward) *
                            (Vector3)(distance > .0001f ? planar / distance : Vector2.right);
        Vector3 to = throat + direction * until;
        to.x = Mathf.Clamp(to.x, 0f, MapBox.width - .01f);
        to.y = Mathf.Clamp(to.y, 0f, MapBox.height - .01f);
        to.z = Mathf.MoveTowards(from.z, throat.z + until * .28f, speed * dt);

        float stretch = 1f + .48f * (1f - Mathf.Clamp01(until / rule.Radius));
        Quaternion axis = Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, Vector3.forward);
        Matrix4x4 warp = Matrix4x4.Rotate(axis) * Matrix4x4.Scale(new Vector3(stretch, 1f / stretch, 1f)) *
                         Matrix4x4.Rotate(Quaternion.Inverse(axis));
        var matrix = Matrix4x4.Translate(to) * warp * piece.Stretch.inverse * Matrix4x4.Translate(-from) * material.Transform;
        material.MoveSolid(matrix, piece.Pose);
        piece.Stretch = warp;
        indexDirty = true;
    }

    /// <summary>实际形状和中心都已进入喉口，避免环外、过大或仍在高处的材料被提前扣除。</summary>
    /// <param name="row">实际世界材料。</param><param name="throat">实际喉口。</param>
    /// <param name="radius">该口能通过的半宽。</param>
    private bool FitsThroat(WorldMaterialRecord row, Vector3 throat, float radius)
    {
        Vector3 origin = row.Material.Transform.GetColumn(3);
        if (Vector2.Distance(origin, throat) > radius * .11f || Mathf.Abs(origin.z - throat.z) > radius * .25f) return false;
        var bounds = BoundsOf(row);
        Vector3 offset = bounds.center - throat;
        return new Vector2(Mathf.Abs(offset.x) + bounds.extents.x, Mathf.Abs(offset.y) + bounds.extents.y).magnitude <= radius &&
               Mathf.Abs(offset.z) + bounds.extents.z <= radius;
    }
}
