using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

public sealed partial class SpiritVeinManager
{
    private CancellationTokenSource reflowCancellation;
    private Task<SpiritVeinGenerationResult> reflowTask;

    /// <summary>接收稳定地形变化。普通地表变化只调整元素，山水结构变化会在长期积累后重流。</summary>
    internal void ApplyTerrainChanges(int[] changedTileIds, bool topologyChanged)
    {
        if (!IsReady || changedTileIds == null || terrain == null) return;
        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null || tiles.Length != terrain.CellCount) return;
        var affectedSections = new HashSet<int>();
        bool addedTopology = false;
        for (int i = 0; i < changedTileIds.Length; i++)
        {
            int tileId = changedTileIds[i];
            if ((uint)tileId >= (uint)terrain.CellCount) continue;
            terrain.Cells[tileId] = SpiritVeinTerrainSnapshot.CaptureCell(tiles[tileId], tileId);
            if (!IsNearField(tileId, SpiritVeinSettings.TerrainChangeRadius)) continue;
            int sectionId = field.SectionByTile[tileId];
            if (sectionId >= 0) affectedSections.Add(sectionId);
            int secondarySectionId = field.SecondarySectionByTile[tileId];
            if (secondarySectionId >= 0) affectedSections.Add(secondarySectionId);
            if (topologyChanged) addedTopology |= pendingTerrainTileIds.Add(tileId);
        }

        if (topologyChanged)
        {
            if (addedTopology && pendingTerrainTileIds.Count == 1) pendingTerrainMonths = 0;
            return;
        }

        foreach (int sectionId in affectedSections)
        {
            SpiritVeinSection section = GetSection(sectionId);
            if (section == null) continue;
            ElementComposition currentTerrain = SpiritVeinGenerator.CalculateComposition(terrain, section.TileIds);
            section.Composition = BlendComposition(section.Composition, currentTerrain, 0.1f);
        }
        if (affectedSections.Count > 0) displayRevision = NextRevision(displayRevision);
    }

    /// <summary>在主线程安装后台完成的山水重流结果。</summary>
    internal void UpdateRerouteTask()
    {
        Task<SpiritVeinGenerationResult> task = reflowTask;
        if (task == null || !task.IsCompleted) return;
        reflowTask = null;
        CancellationTokenSource cancellation = reflowCancellation;
        reflowCancellation = null;
        try
        {
            SpiritVeinGenerationResult result = task.GetAwaiter().GetResult();
            if (!IsReady || result.WorldSeedId != worldSeedId || result.Width != width || result.Height != height) return;
            ApplyReflowResult(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ModClass.LogErrorConcurrent("龙脉地势重流失败: " + exception);
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void AdvancePendingTerrainChanges()
    {
        if (pendingTerrainTileIds.Count == 0 || reflowTask != null) return;
        pendingTerrainMonths++;
        if (pendingTerrainMonths < SpiritVeinSettings.RerouteDelayYears * 12) return;
        var snapshot = new SpiritVeinTerrainSnapshot(
            worldSeedId,
            width,
            height,
            (SpiritVeinTerrainCell[])terrain.Cells.Clone());
        pendingTerrainTileIds.Clear();
        pendingTerrainMonths = 0;
        reflowCancellation = new CancellationTokenSource();
        CancellationToken token = reflowCancellation.Token;
        reflowTask = Task.Run(() => SpiritVeinGenerator.Generate(snapshot, token), token);
    }

    private void ApplyReflowResult(SpiritVeinGenerationResult result)
    {
        Dictionary<int, SpiritVein> oldByNewVein = PreserveRuntimeIdentity(result);
        new SpiritVeinNameService(worldSeedId, width, height).AssignNames(result, terrain);
        List<SpiritVein> reconciledVeins = Reconcile(result.Veins, oldByNewVein);
        ReplaceState(result, reconciledVeins);
        RebuildDictionaries();
        RebuildFieldTileIds();
        RefreshGroundsAndEyes();
        topologyRevision = NextRevision(topologyRevision);
        displayRevision = NextRevision(displayRevision);
    }

    private Dictionary<int, SpiritVein> PreserveRuntimeIdentity(SpiritVeinGenerationResult result)
    {
        var oldVeins = new List<SpiritVein>(veins);
        var oldBranches = new List<SpiritVeinBranch>(branches);
        var oldSections = new List<SpiritVeinSection>(sections);
        var oldGrounds = new List<GatheringGround>(grounds);
        var oldEyes = new List<SpiritVeinEye>(eyes);
        var matchedOldVeins = new HashSet<int>();
        var oldByNewVein = new Dictionary<int, SpiritVein>();

        for (int i = 0; i < result.Veins.Count; i++)
        {
            SpiritVeinDraft next = result.Veins[i];
            SpiritVein old = FindNearestVein(oldVeins, matchedOldVeins, next.SourceCenterTileId);
            if (old == null) continue;
            matchedOldVeins.Add(old.Id);
            oldByNewVein[next.Id] = old;
            next.Name = old.Name;
        }

        for (int i = 0; i < result.Sections.Count; i++)
        {
            SpiritVeinSection next = result.Sections[i];
            if (!oldByNewVein.TryGetValue(next.VeinId, out SpiritVein oldVein)) continue;
            SpiritVeinSection old = FindNearestSection(oldSections, oldVein.Id, next.Kind, next.CenterTileId);
            if (old == null) continue;
            next.CurrentAmount = next.Capacity * old.FillRatio;
            next.Purity = old.Purity;
            next.RefreshStatus();
        }

        for (int i = 0; i < result.Branches.Count; i++)
        {
            SpiritVeinBranch next = result.Branches[i];
            if (!oldByNewVein.TryGetValue(next.VeinId, out SpiritVein oldVein)) continue;
            SpiritVeinBranch old = FindNearestBranch(oldBranches, oldVein.Id, next.SourceCenterTileId);
            if (old != null) next.Name = old.Name;
        }

        var matchedOldGrounds = new HashSet<int>();
        for (int i = 0; i < result.Grounds.Count; i++)
        {
            GatheringGround next = result.Grounds[i];
            if (!oldByNewVein.TryGetValue(next.PrimaryVeinId, out SpiritVein oldVein)) continue;
            GatheringGround old = FindNearestGround(oldGrounds, matchedOldGrounds, oldVein.Id, next.Kind, next.CenterTileId);
            if (old == null || TileDistance(old.CenterTileId, next.CenterTileId) > SpiritVeinSettings.GroundMinimumDistance)
                continue;
            matchedOldGrounds.Add(old.Id);
            next.Name = old.Name;
            SpiritVeinEye oldEye = FindEyeByGround(oldEyes, old.Id);
            SpiritVeinEye nextEye = FindEyeByGround(result.Eyes, next.Id);
            if (oldEye != null && nextEye != null) nextEye.Name = oldEye.Name;
        }

        AddRemnantGrounds(result, oldByNewVein, oldGrounds, oldEyes, matchedOldGrounds);
        SpiritVeinGenerator.ValidateGeneratedState(
            terrain,
            result.Veins,
            result.Branches,
            result.Sections,
            result.Grounds,
            result.Eyes,
            result.Field);
        return oldByNewVein;
    }

    private void AddRemnantGrounds(
        SpiritVeinGenerationResult result,
        Dictionary<int, SpiritVein> oldByNewVein,
        List<GatheringGround> oldGrounds,
        List<SpiritVeinEye> oldEyes,
        HashSet<int> matchedOldGrounds)
    {
        int nextGroundId = result.Grounds.Count + 1;
        int nextEyeId = result.Eyes.Count + 1;
        foreach (KeyValuePair<int, SpiritVein> pair in oldByNewVein)
        {
            SpiritVeinDraft nextVein = null;
            for (int i = 0; i < result.Veins.Count; i++)
            {
                if (result.Veins[i].Id == pair.Key)
                {
                    nextVein = result.Veins[i];
                    break;
                }
            }
            if (nextVein == null) continue;
            for (int i = 0; i < oldGrounds.Count; i++)
            {
                GatheringGround old = oldGrounds[i];
                if (old.PrimaryVeinId != pair.Value.Id || old.Kind != GatheringGroundKind.Main ||
                    matchedOldGrounds.Contains(old.Id)) continue;
                int center = old.CenterTileId;
                if ((uint)center >= (uint)result.Field.FieldStrength.Length ||
                    result.Field.PrimaryVeinByTile[center] != nextVein.Id ||
                    result.Field.FieldStrength[center] < 0.12f) continue;
                int sectionId = result.Field.SectionByTile[center];
                if (sectionId < 0) continue;
                SpiritVeinEye oldEye = FindEyeByGround(oldEyes, old.Id);
                if (oldEye == null) continue;
                var area = new List<int>();
                var hall = new List<int>();
                for (int tileIndex = 0; tileIndex < old.TileIds.Length; tileIndex++)
                {
                    int tileId = old.TileIds[tileIndex];
                    if ((uint)tileId >= (uint)result.Field.FieldStrength.Length ||
                        result.Field.PrimaryVeinByTile[tileId] != nextVein.Id ||
                        result.Field.GroundByTile[tileId] >= 0) continue;
                    result.Field.GroundByTile[tileId] = nextGroundId;
                    area.Add(tileId);
                    if (ContainsTile(old.HallTileIds, tileId)) hall.Add(tileId);
                }
                if (area.Count == 0) continue;
                GatheringGroundQuality quality = old.Quality > GatheringGroundQuality.Lower
                    ? old.Quality - 1
                    : GatheringGroundQuality.Lower;
                var remnant = new GatheringGround(
                    nextGroundId,
                    nextVein.Id,
                    -1,
                    sectionId,
                    -1,
                    GatheringGroundKind.Remnant,
                    quality,
                    center,
                    area.ToArray(),
                    hall.ToArray(),
                    result.Field.Convergence[center],
                    result.Field.Shelter[center],
                    result.Field.Leakage[center])
                {
                    Name = old.Name
                };
                int remnantEyeTile = area.Contains(oldEye.TileId) ? oldEye.TileId : center;
                var remnantEye = new SpiritVeinEye(
                    nextEyeId,
                    nextVein.Id,
                    nextGroundId,
                    sectionId,
                    remnantEyeTile,
                    oldEye.Manifestation,
                    oldEye.BaseConcentration * 0.55f,
                    oldEye.Composition)
                {
                    Name = oldEye.Name
                };
                remnant.EyeId = nextEyeId;
                result.Eyes.Add(remnantEye);
                nextVein.EyeIds.Add(nextEyeId);
                nextEyeId++;
                result.Grounds.Add(remnant);
                nextVein.GroundIds.Add(nextGroundId);
                nextGroundId++;
            }
        }
    }

    private bool IsNearField(int tileId, int radius)
    {
        if ((uint)tileId >= (uint)(field?.PrimaryVeinByTile.Length ?? 0)) return false;
        int centerX = tileId % width;
        int centerY = tileId / width;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                if (field.PrimaryVeinByTile[y * width + x] >= 0) return true;
            }
        }
        return false;
    }

    private SpiritVein FindNearestVein(
        List<SpiritVein> values,
        HashSet<int> excluded,
        int tileId)
    {
        SpiritVein result = null;
        int distance = int.MaxValue;
        for (int i = 0; i < values.Count; i++)
        {
            if (excluded.Contains(values[i].Id)) continue;
            int current = TileDistance(values[i].SourceCenterTileId, tileId);
            if (current >= distance) continue;
            result = values[i];
            distance = current;
        }
        return result;
    }

    private SpiritVeinSection FindNearestSection(
        List<SpiritVeinSection> values,
        int topologyId,
        VeinSectionKind kind,
        int tileId)
    {
        SpiritVeinSection result = null;
        int distance = int.MaxValue;
        for (int i = 0; i < values.Count; i++)
        {
            SpiritVeinSection value = values[i];
            if (value.VeinId != topologyId || value.Kind != kind) continue;
            int current = TileDistance(value.CenterTileId, tileId);
            if (current >= distance) continue;
            result = value;
            distance = current;
        }
        return result;
    }

    private SpiritVeinBranch FindNearestBranch(
        List<SpiritVeinBranch> values,
        int topologyId,
        int tileId)
    {
        SpiritVeinBranch result = null;
        int distance = int.MaxValue;
        for (int i = 0; i < values.Count; i++)
        {
            SpiritVeinBranch value = values[i];
            if (value.VeinId != topologyId) continue;
            int current = TileDistance(value.SourceCenterTileId, tileId);
            if (current >= distance) continue;
            result = value;
            distance = current;
        }
        return result;
    }

    private GatheringGround FindNearestGround(
        List<GatheringGround> values,
        HashSet<int> excluded,
        int topologyId,
        GatheringGroundKind kind,
        int tileId)
    {
        GatheringGround result = null;
        int distance = int.MaxValue;
        for (int i = 0; i < values.Count; i++)
        {
            GatheringGround value = values[i];
            if (excluded.Contains(value.Id) || value.PrimaryVeinId != topologyId || value.Kind != kind) continue;
            int current = TileDistance(value.CenterTileId, tileId);
            if (current >= distance) continue;
            result = value;
            distance = current;
        }
        return result;
    }

    private static SpiritVeinEye FindEyeByGround(List<SpiritVeinEye> values, int groundId)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i].GroundId == groundId) return values[i];
        }
        return null;
    }

    private void CancelReflowTask()
    {
        CancellationTokenSource cancellation = reflowCancellation;
        Task<SpiritVeinGenerationResult> task = reflowTask;
        reflowCancellation = null;
        reflowTask = null;
        if (cancellation == null) return;
        cancellation.Cancel();
        if (task == null || task.IsCompleted)
        {
            if (task?.IsFaulted == true) _ = task.Exception;
            cancellation.Dispose();
            return;
        }
        task.ContinueWith(
            completed =>
            {
                if (completed.IsFaulted) _ = completed.Exception;
                cancellation.Dispose();
            },
            TaskScheduler.Default);
    }
}
