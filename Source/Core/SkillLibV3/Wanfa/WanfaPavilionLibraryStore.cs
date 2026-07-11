using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Wanfa.Persistence;

namespace Cultiway.Core.SkillLibV3.Wanfa;

/// <summary>
/// 管理万法阁领域数据，物理读写和版本迁移由通用持久化文档负责。
/// </summary>
internal sealed class WanfaPavilionLibraryStore
{
    private readonly SaveDocument<WanfaPavilionData> _document;
    private WanfaPavilionData Data => _document.Data;

    public WanfaPavilionLibraryStore(ModSaveManager saveManager)
    {
        _document = saveManager.Register(WanfaPavilionSaveDefinition.Create());
    }

    public IReadOnlyList<SkillBlueprint> Blueprints => Data.Blueprints;
    public IReadOnlyList<string> SelectedBlueprintIds => Data.SelectedBlueprintIds;

    public SkillBlueprint Get(string id)
    {
        return Data.Blueprints.FirstOrDefault(item => item.Id == id);
    }

    public SkillBlueprint FindBySignature(string signature, string excludedId = null)
    {
        return Data.Blueprints.FirstOrDefault(item => item.Id != excludedId &&
            SkillBlueprintSignature.Build(item) == signature);
    }

    public void Add(SkillBlueprint blueprint)
    {
        blueprint.SortOrder = Data.Blueprints.Count == 0
            ? 0
            : Data.Blueprints.Max(item => item.SortOrder) + 1;
        Data.Blueprints.Add(blueprint);
        Save();
    }

    public void Replace(SkillBlueprint blueprint)
    {
        var index = Data.Blueprints.FindIndex(item => item.Id == blueprint.Id);
        if (index < 0)
        {
            throw new InvalidOperationException(string.Format(
                "Cultiway.Wanfa.Exception.BlueprintMissing".Localize(), blueprint.Id));
        }
        Data.Blueprints[index] = blueprint;
        Save();
    }

    public bool Remove(string id)
    {
        var removed = Data.Blueprints.RemoveAll(item => item.Id == id) > 0;
        if (!removed) return false;
        Save();
        return true;
    }

    public void SaveOrder(IReadOnlyList<string> ids)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(ids[i])) continue;
            if (!positions.ContainsKey(ids[i])) positions[ids[i]] = i;
        }
        foreach (var blueprint in Data.Blueprints)
        {
            if (!string.IsNullOrWhiteSpace(blueprint.Id) &&
                positions.TryGetValue(blueprint.Id, out var position)) blueprint.SortOrder = position;
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
        WanfaPavilionSaveDefinition.NormalizeSortOrder(Data);
        _document.Save();
    }
}
