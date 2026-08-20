using System;
using System.Collections.Generic;

namespace Cultiway.Content.SpiritVeins;

/// <summary>统一管理当前世界的龙脉对象、内部结构和运行状态。</summary>
public sealed partial class SpiritVeinManager : CoreSystemManager<SpiritVein, SpiritVeinData>
{
    private readonly List<SpiritVein> veins = new();
    private readonly List<SpiritVeinBranch> branches = new();
    private readonly List<SpiritVeinSection> sections = new();
    private readonly List<GatheringGround> grounds = new();
    private readonly List<SpiritVeinEye> eyes = new();
    private readonly Dictionary<int, SpiritVein> veinByTopologyId = new();
    private readonly Dictionary<int, SpiritVeinBranch> branchById = new();
    private readonly Dictionary<int, SpiritVeinSection> sectionById = new();
    private readonly Dictionary<int, GatheringGround> groundById = new();
    private readonly Dictionary<int, SpiritVeinEye> eyeById = new();
    private readonly HashSet<int> pendingTerrainTileIds = new();

    private SpiritVeinTerrainSnapshot terrain;
    private SpiritVeinFieldSnapshot field;
    private int[] fieldTileIds = Array.Empty<int>();
    private long nextId = 1;
    private int worldSeedId = -1;
    private int width;
    private int height;
    private int pendingTerrainMonths;
    private int supplyCursor;
    private bool ready;
    private int topologyRevision;
    private int displayRevision;

    public bool IsReady => ready && worldSeedId == MapBox.current_world_seed_id;
    public int TopologyRevision => topologyRevision;
    public int DisplayRevision => displayRevision;
    public IReadOnlyList<SpiritVein> Veins => veins;
    public IReadOnlyList<SpiritVeinBranch> Branches => branches;
    public IReadOnlyList<SpiritVeinSection> Sections => sections;
    public IReadOnlyList<GatheringGround> Grounds => grounds;
    public IReadOnlyList<SpiritVeinEye> Eyes => eyes;

    /// <summary>安装一轮确定性生成结果，并建立由宽广脉域主导的初始天地灵气。</summary>
    internal void Install(
        SpiritVeinGenerationResult result,
        SpiritVeinTerrainSnapshot terrainSnapshot)
    {
        ValidateResult(result, terrainSnapshot);
        clear();
        worldSeedId = result.WorldSeedId;
        width = result.Width;
        height = result.Height;
        terrain = terrainSnapshot;
        new SpiritVeinNameService(worldSeedId, width, height).AssignNames(result, terrainSnapshot);
        List<SpiritVein> installedVeins = Reconcile(result.Veins, null);
        ReplaceState(result, installedVeins);
        RebuildDictionaries();
        RebuildFieldTileIds();
        InitializeWakanFromField();
        ready = true;
        topologyRevision = NextRevision(topologyRevision);
        displayRevision = NextRevision(displayRevision);
        WorldWakanService.PublishDisplayValues(true);
    }

    internal List<SpiritVein> Reconcile(
        IReadOnlyList<SpiritVeinDraft> layouts,
        IReadOnlyDictionary<int, SpiritVein> oldByNewTopologyId)
    {
        var result = new List<SpiritVein>(layouts.Count);
        var retained = new HashSet<SpiritVein>();
        for (int i = 0; i < layouts.Count; i++)
        {
            SpiritVeinDraft layout = layouts[i];
            SpiritVein vein;
            if (oldByNewTopologyId != null &&
                oldByNewTopologyId.TryGetValue(layout.Id, out SpiritVein existing) &&
                existing != null && existing.isAlive())
            {
                vein = existing;
                vein.ApplyLayout(layout);
            }
            else
            {
                vein = newObject(nextId++);
                vein.Setup(layout);
            }

            result.Add(vein);
            retained.Add(vein);
        }

        RemoveUnretained(retained);
        return result;
    }

    /// <summary>清空当前世界龙脉，不保存任何历史。</summary>
    public override void clear()
    {
        CancelReflowTask();
        if (SelectedObjects.getSelectedNanoObject() is SpiritVein)
        {
            SelectedObjects.unselectNanoObject();
        }
        if (WorldboxGame.I != null)
        {
            WorldboxGame.I.SelectedSpiritVein = null;
        }

        nextId = 1;
        base.clear();
        veins.Clear();
        branches.Clear();
        sections.Clear();
        grounds.Clear();
        eyes.Clear();
        veinByTopologyId.Clear();
        branchById.Clear();
        sectionById.Clear();
        groundById.Clear();
        eyeById.Clear();
        pendingTerrainTileIds.Clear();
        terrain = null;
        field = null;
        fieldTileIds = Array.Empty<int>();
        worldSeedId = -1;
        width = 0;
        height = 0;
        pendingTerrainMonths = 0;
        supplyCursor = 0;
        ready = false;
        topologyRevision = NextRevision(topologyRevision);
        displayRevision = NextRevision(displayRevision);
    }

    private void RemoveUnretained(HashSet<SpiritVein> retained)
    {
        var removed = new List<SpiritVein>();
        foreach (SpiritVein vein in this)
        {
            if (!retained.Contains(vein)) removed.Add(vein);
        }

        for (int i = 0; i < removed.Count; i++)
        {
            SpiritVein vein = removed[i];
            if (SelectedObjects.isNanoObjectSelected(vein))
            {
                SelectedObjects.unselectNanoObject();
            }
            if (ReferenceEquals(WorldboxGame.I?.SelectedSpiritVein, vein))
            {
                WorldboxGame.I.SelectedSpiritVein = null;
            }
            removeObject(vein);
        }
    }

    private void ReplaceState(
        SpiritVeinGenerationResult result,
        IReadOnlyList<SpiritVein> installedVeins)
    {
        veins.Clear();
        branches.Clear();
        sections.Clear();
        grounds.Clear();
        eyes.Clear();
        veins.AddRange(installedVeins);
        branches.AddRange(result.Branches);
        sections.AddRange(result.Sections);
        grounds.AddRange(result.Grounds);
        eyes.AddRange(result.Eyes);
        field = result.Field;
    }

    private void RebuildDictionaries()
    {
        veinByTopologyId.Clear();
        branchById.Clear();
        sectionById.Clear();
        groundById.Clear();
        eyeById.Clear();
        for (int i = 0; i < veins.Count; i++) veinByTopologyId[veins[i].Id] = veins[i];
        for (int i = 0; i < branches.Count; i++) branchById[branches[i].Id] = branches[i];
        for (int i = 0; i < sections.Count; i++) sectionById[sections[i].Id] = sections[i];
        for (int i = 0; i < grounds.Count; i++) groundById[grounds[i].Id] = grounds[i];
        for (int i = 0; i < eyes.Count; i++) eyeById[eyes[i].Id] = eyes[i];
    }

    private void RebuildFieldTileIds()
    {
        if (field == null)
        {
            fieldTileIds = Array.Empty<int>();
            return;
        }
        var ids = new List<int>();
        for (int tileId = 0; tileId < field.PrimaryVeinByTile.Length; tileId++)
        {
            if (field.PrimaryVeinByTile[tileId] >= 0 && field.FieldStrength[tileId] > 0f) ids.Add(tileId);
        }
        fieldTileIds = ids.ToArray();
        supplyCursor = 0;
    }

    private static void ValidateResult(
        SpiritVeinGenerationResult result,
        SpiritVeinTerrainSnapshot terrainSnapshot)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (terrainSnapshot == null) throw new ArgumentNullException(nameof(terrainSnapshot));
        if (result.WorldSeedId != terrainSnapshot.WorldSeedId ||
            result.Width != terrainSnapshot.Width ||
            result.Height != terrainSnapshot.Height ||
            result.WorldSeedId != MapBox.current_world_seed_id ||
            result.Width != MapBox.width ||
            result.Height != MapBox.height)
        {
            throw new InvalidOperationException("风水龙脉生成结果不属于当前世界");
        }
    }

    private static int NextRevision(int current)
    {
        return current == int.MaxValue ? 1 : current + 1;
    }
}
