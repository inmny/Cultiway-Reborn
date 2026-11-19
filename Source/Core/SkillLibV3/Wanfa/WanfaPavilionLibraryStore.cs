using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Wanfa.Persistence;
using Cultiway.Core.Skills;

namespace Cultiway.Core.SkillLibV3.Wanfa;

/// <summary>
/// 管理万法阁领域数据，物理读写和版本迁移由通用持久化文档负责。
/// </summary>
internal sealed class WanfaPavilionLibraryStore
{
    private readonly SaveDocument<WanfaPavilionData> _document;
    private readonly SaveDocument<WanfaConstructionFavorites> _constructions;
    private WanfaPavilionData Data => _document.Data;

    public WanfaPavilionLibraryStore(ModSaveManager saveManager)
    {
        _document = saveManager.Register(WanfaPavilionSaveDefinition.Create());
        _constructions = saveManager.Register(WanfaConstructionFavorites.Create());
    }

    public IReadOnlyList<SkillBlueprint> Blueprints => Data.Blueprints;
    public IReadOnlyList<string> SelectedBlueprintIds => Data.SelectedBlueprintIds;

    /// <summary>收藏以一份确认的修订为单位；返回供窗口读取的条目。</summary>
    internal IReadOnlyList<AbilityDefinition> ConstructionFavorites => _constructions.Data.Entries;

    /// <summary>同时保留旧收藏和世界目录，直到本机文件确实保存。</summary>
    internal bool SetConstructionFavorite(AbilityDefinition definition, bool favorite)
    {
        var previous = _constructions.Data;
        var entries = new List<AbilityDefinition>(previous.Entries);
        int index = entries.FindIndex(item => item.Id == definition.Id && item.Revision == definition.Revision);
        if (favorite)
        {
            if (index >= 0)
            {
                if (!ConstructionShare.Matches(entries[index], definition)) throw new ConstructionShareException("ShareConflictingId");
                return false;
            }
            entries.Add(ConstructionEditing.Copy(definition));
        }
        else
        {
            if (index < 0) return false;
            entries.RemoveAt(index);
        }
        _constructions.Data = new WanfaConstructionFavorites { Entries = entries };
        bool saved = false;
        try { _constructions.Save(); saved = true; }
        finally { if (!saved) _constructions.Data = previous; }
        return true;
    }

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
