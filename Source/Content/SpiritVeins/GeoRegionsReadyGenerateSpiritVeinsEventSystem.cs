using System;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Performance;

namespace Cultiway.Content.SpiritVeins;

/// <summary>地区首次完成后分批读取地形，并在后台生成当前世界的灵脉。</summary>
internal sealed class GeoRegionsReadyGenerateSpiritVeinsEventSystem :
    GenericEventSystem<GeoRegionsReadyEvent>,
    ICooperativeSystemStep,
    IWorldStateClearable
{
    private const int CaptureBatchSize = 4096;
    private static GeoRegionsReadyGenerateSpiritVeinsEventSystem instance;
    private GenerationWork work;

    internal GeoRegionsReadyGenerateSpiritVeinsEventSystem()
    {
        instance = this;
    }

    internal static bool BlocksSimulation => instance?.work != null;

    string ICooperativeSystemStep.CooperativePhaseName
    {
        get
        {
            if (work == null) return "spirit_vein.dequeue";
            if (work.CaptureIndex < work.Cells.Length) return "spirit_vein.capture";
            if (work.GenerationTask == null || !work.GenerationTask.IsCompleted) return "spirit_vein.generate";
            return "spirit_vein.install";
        }
    }

    bool ICooperativeSystemStep.StepCooperatively()
    {
        if (work == null)
        {
            base.OnUpdateGroup();
            if (work == null) return true;
        }

        GenerationWork current = work;
        if (!IsCurrentWorld(current))
        {
            CancelWork();
            return true;
        }

        try
        {
            if (current.CaptureIndex < current.Cells.Length)
            {
                int end = Math.Min(current.Cells.Length, current.CaptureIndex + CaptureBatchSize);
                for (int tileId = current.CaptureIndex; tileId < end; tileId++)
                {
                    current.Cells[tileId] = SpiritVeinTerrainSnapshot.CaptureCell(current.Tiles[tileId], tileId);
                }
                current.CaptureIndex = end;
                if (current.CaptureIndex < current.Cells.Length) return false;

                current.Terrain = new SpiritVeinTerrainSnapshot(
                    current.WorldSeedId,
                    current.Width,
                    current.Height,
                    current.Cells);
                current.GenerationTask = StartGeneration(current.Terrain, current.Cancellation.Token);
                return false;
            }

            if (current.GenerationTask == null || !current.GenerationTask.IsCompleted) return true;
            SpiritVeinGenerationResult result = current.GenerationTask.GetAwaiter().GetResult();
            if (!IsCurrentWorld(current))
            {
                CancelWork();
                return true;
            }

            SpiritVeinManager manager = WorldboxGame.I?.SpiritVeins ??
                                         throw new InvalidOperationException("SpiritVeinManager 尚未初始化");
            manager.Install(result, current.Terrain);
            ModClass.I.TileExtendManager.CompleteWorldInitialization(current.Tiles);
            current.Cancellation.Dispose();
            work = null;
            return true;
        }
        catch (OperationCanceledException)
        {
            CancelWork();
            return true;
        }
        catch (Exception exception)
        {
            current.Cancellation.Cancel();
            current.Cancellation.Dispose();
            work = null;
            WorldboxGame.I?.SpiritVeins?.clear();
            ModClass.I?.TileExtendManager?.FailWorldInitialization(current.Tiles);
            ModClass.LogErrorConcurrent("灵脉世界初始化失败，已停止本轮初始化: " + exception);
            throw;
        }
    }

    protected override void HandleEvent(GeoRegionsReadyEvent evt)
    {
        if (evt.WorldSeedId != MapBox.current_world_seed_id ||
            evt.Width != MapBox.width || evt.Height != MapBox.height)
        {
            return;
        }

        WorldTile[] tiles = World.world?.tiles_list;
        try
        {
            if (tiles == null || tiles.Length != checked(evt.Width * evt.Height))
            {
                throw new InvalidOperationException("地区完成后没有可用于生成灵脉的完整地块数组");
            }

            CancelWork();
            WorldboxGame.I?.SpiritVeins?.clear();
            work = new GenerationWork(evt.WorldSeedId, evt.Width, evt.Height, tiles);
        }
        catch (Exception exception)
        {
            CancelWork();
            WorldboxGame.I?.SpiritVeins?.clear();
            if (tiles == null)
                ModClass.I?.TileExtendManager?.CancelFitNewWorld();
            else
                ModClass.I?.TileExtendManager?.FailWorldInitialization(tiles);
            ModClass.LogErrorConcurrent("灵脉初始化任务创建失败: " + exception);
            throw;
        }
    }

    public void ClearWorldState()
    {
        CancelWork();
    }

    private static Task<SpiritVeinGenerationResult> StartGeneration(
        SpiritVeinTerrainSnapshot terrain,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SpiritVeinGenerationResult result = SpiritVeinGenerator.Generate(terrain, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);
    }

    private static bool IsCurrentWorld(GenerationWork current)
    {
        return current != null &&
               current.WorldSeedId == MapBox.current_world_seed_id &&
               current.Width == MapBox.width &&
               current.Height == MapBox.height &&
               ReferenceEquals(World.world?.tiles_list, current.Tiles);
    }

    private void CancelWork()
    {
        GenerationWork current = work;
        work = null;
        if (current == null) return;
        current.Cancellation.Cancel();
        current.Cancellation.Dispose();
    }

    private sealed class GenerationWork
    {
        internal GenerationWork(int worldSeedId, int width, int height, WorldTile[] tiles)
        {
            WorldSeedId = worldSeedId;
            Width = width;
            Height = height;
            Tiles = tiles;
            Cells = new SpiritVeinTerrainCell[checked(width * height)];
            Cancellation = new CancellationTokenSource();
        }

        internal int WorldSeedId { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal WorldTile[] Tiles { get; }
        internal SpiritVeinTerrainCell[] Cells { get; }
        internal CancellationTokenSource Cancellation { get; }
        internal int CaptureIndex { get; set; }
        internal SpiritVeinTerrainSnapshot Terrain { get; set; }
        internal Task<SpiritVeinGenerationResult> GenerationTask { get; set; }
    }
}
