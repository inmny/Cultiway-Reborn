using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Artifacts.Baibao.Persistence;
using Cultiway.Core.Persistence;

namespace Cultiway.Content.Artifacts.Baibao;

/// <summary>
/// 管理百宝阁目录数据，实际读写和版本检查交给通用持久化文档。
/// </summary>
internal sealed class BaibaoPavilionStore
{
    private readonly SaveDocument<BaibaoPavilionData> _document;
    private BaibaoPavilionData Data => _document.Data;

    public BaibaoPavilionStore(ModSaveManager saveManager)
    {
        _document = saveManager.Register(BaibaoPavilionSaveDefinition.Create());
    }

    public IReadOnlyList<ArtifactBlueprint> Blueprints => Data.Blueprints;
    public IReadOnlyList<string> SelectedBlueprintIds => Data.SelectedBlueprintIds;

    public ArtifactBlueprint Get(string id)
    {
        return Data.Blueprints.FirstOrDefault(blueprint => blueprint.Id == id);
    }

    public ArtifactBlueprint FindBySignature(string signature, string excludedId = null)
    {
        return Data.Blueprints.FirstOrDefault(blueprint => blueprint.Id != excludedId &&
                                                               ArtifactBlueprintSignature.Build(blueprint) == signature);
    }

    public void Add(ArtifactBlueprint blueprint)
    {
        blueprint.SortOrder = Data.Blueprints.Count == 0
            ? 0
            : Data.Blueprints.Max(item => item.SortOrder) + 1;
        Data.Blueprints.Add(blueprint);
        Save();
    }

    public void Replace(ArtifactBlueprint blueprint)
    {
        int index = Data.Blueprints.FindIndex(item => item.Id == blueprint.Id);
        if (index < 0) throw new InvalidOperationException($"百宝阁法宝蓝图不存在: {blueprint.Id}");
        Data.Blueprints[index] = blueprint;
        Save();
    }

    public bool Remove(string id)
    {
        bool removed = Data.Blueprints.RemoveAll(blueprint => blueprint.Id == id) > 0;
        if (removed) Save();
        return removed;
    }

    public void SaveOrder(IReadOnlyList<string> ids)
    {
        Dictionary<string, int> positions = new(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!positions.ContainsKey(ids[i])) positions[ids[i]] = i;
        }
        foreach (ArtifactBlueprint blueprint in Data.Blueprints)
        {
            if (positions.TryGetValue(blueprint.Id, out int position)) blueprint.SortOrder = position;
        }
        Save();
    }

    public void SaveSelection(IEnumerable<string> ids)
    {
        Data.SelectedBlueprintIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _document.Save();
    }

    private void Save()
    {
        BaibaoPavilionSaveDefinition.NormalizeSortOrder(Data);
        _document.Save();
    }
}
