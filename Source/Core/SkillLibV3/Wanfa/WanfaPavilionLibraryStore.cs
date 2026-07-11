using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Wanfa;

[Serializable]
internal sealed class WanfaPavilionLibraryFile
{
    public int SchemaVersion = 1;
    public List<SkillBlueprint> Blueprints = new();
}

internal sealed class WanfaPavilionLibraryStore
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _directory = Path.Combine(Application.persistentDataPath, "Cultiway", "WanfaPavilion");
    private WanfaPavilionLibraryFile _file = new();

    private string LibraryPath => Path.Combine(_directory, "library.json");
    private string BackupPath => Path.Combine(_directory, "library.json.bak");
    private string TempPath => Path.Combine(_directory, "library.json.tmp");

    public IReadOnlyList<SkillBlueprint> Blueprints => _file.Blueprints;

    public void Load()
    {
        Directory.CreateDirectory(_directory);
        if (!File.Exists(LibraryPath))
        {
            _file = new WanfaPavilionLibraryFile();
            return;
        }

        var recovered = false;
        try
        {
            _file = Deserialize(File.ReadAllText(LibraryPath));
        }
        catch (Exception primaryError)
        {
            if (!File.Exists(BackupPath)) throw;
            ModClass.LogError(string.Format("Cultiway.Wanfa.Log.LibraryFallback".Localize(),
                primaryError.Message));
            _file = Deserialize(File.ReadAllText(BackupPath));
            recovered = true;
        }

        var beforeMigration = JsonConvert.SerializeObject(_file);
        Migrate();
        if (recovered || beforeMigration != JsonConvert.SerializeObject(_file))
        {
            Save();
            if (recovered) File.Copy(LibraryPath, BackupPath, true);
        }
    }

    public SkillBlueprint Get(string id)
    {
        return _file.Blueprints.FirstOrDefault(item => item.Id == id);
    }

    public SkillBlueprint FindBySignature(string signature, string excludedId = null)
    {
        return _file.Blueprints.FirstOrDefault(item => item.Id != excludedId &&
            SkillBlueprintSignature.Build(item) == signature);
    }

    public void Add(SkillBlueprint blueprint)
    {
        blueprint.SortOrder = _file.Blueprints.Count == 0
            ? 0
            : _file.Blueprints.Max(item => item.SortOrder) + 1;
        _file.Blueprints.Add(blueprint);
        NormalizeSortOrder();
        Save();
    }

    public void Replace(SkillBlueprint blueprint)
    {
        var index = _file.Blueprints.FindIndex(item => item.Id == blueprint.Id);
        if (index < 0)
        {
            throw new InvalidOperationException(string.Format(
                "Cultiway.Wanfa.Exception.BlueprintMissing".Localize(), blueprint.Id));
        }
        _file.Blueprints[index] = blueprint;
        NormalizeSortOrder();
        Save();
    }

    public bool Remove(string id)
    {
        var removed = _file.Blueprints.RemoveAll(item => item.Id == id) > 0;
        if (!removed) return false;
        NormalizeSortOrder();
        Save();
        return true;
    }

    public void Save()
    {
        Directory.CreateDirectory(_directory);
        var json = JsonConvert.SerializeObject(_file, Formatting.Indented);
        File.WriteAllText(TempPath, json);
        if (File.Exists(LibraryPath))
        {
            File.Replace(TempPath, LibraryPath, BackupPath, true);
        }
        else
        {
            File.Move(TempPath, LibraryPath);
        }
    }

    public void SaveOrder(IReadOnlyList<string> ids)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(ids[i])) continue;
            if (!positions.ContainsKey(ids[i])) positions[ids[i]] = i;
        }
        foreach (var blueprint in _file.Blueprints)
        {
            if (!string.IsNullOrWhiteSpace(blueprint.Id) &&
                positions.TryGetValue(blueprint.Id, out var position)) blueprint.SortOrder = position;
        }
        NormalizeSortOrder();
        Save();
    }

    private static WanfaPavilionLibraryFile Deserialize(string json)
    {
        var file = JsonConvert.DeserializeObject<WanfaPavilionLibraryFile>(json);
        if (file == null)
        {
            throw new InvalidDataException("Cultiway.Wanfa.Exception.LibraryEmpty".Localize());
        }
        return file;
    }

    private void Migrate()
    {
        if (_file.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException(string.Format(
                "Cultiway.Wanfa.Exception.LibraryVersionTooNew".Localize(), _file.SchemaVersion));
        }

        _file.Blueprints ??= new List<SkillBlueprint>();
        _file.Blueprints.RemoveAll(blueprint => blueprint == null);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var blueprint in _file.Blueprints)
        {
            blueprint.Modifiers ??= new List<SkillModifierSpec>();
            blueprint.Origin ??= new SkillBlueprintOriginData();
            if (string.IsNullOrWhiteSpace(blueprint.Id) || !seenIds.Add(blueprint.Id))
            {
                var oldId = blueprint.Id;
                blueprint.Id = Guid.NewGuid().ToString("N");
                seenIds.Add(blueprint.Id);
                if (!string.IsNullOrWhiteSpace(oldId))
                {
                    blueprint.Origin.SourceBlueprintId = oldId;
                }
            }
            if (blueprint.Revision < 1) blueprint.Revision = 1;
            if (blueprint.CreatedAtUtcTicks <= 0) blueprint.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
            if (blueprint.UpdatedAtUtcTicks <= 0) blueprint.UpdatedAtUtcTicks = blueprint.CreatedAtUtcTicks;
            for (var i = 0; i < blueprint.Modifiers.Count; i++)
            {
                var modifier = blueprint.Modifiers[i];
                if (modifier == null)
                {
                    modifier = new SkillModifierSpec();
                    blueprint.Modifiers[i] = modifier;
                }
                modifier.Parameters ??= new Dictionary<string, string>(StringComparer.Ordinal);
            }
            if (blueprint.SchemaVersion == 0)
            {
                blueprint.SchemaVersion = SkillBlueprint.CurrentSchemaVersion;
            }
        }
        _file.SchemaVersion = CurrentSchemaVersion;
        NormalizeSortOrder();
    }

    private void NormalizeSortOrder()
    {
        _file.Blueprints = _file.Blueprints
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.Favorite)
            .ThenBy(item => item.CreatedAtUtcTicks)
            .ToList();
        for (var i = 0; i < _file.Blueprints.Count; i++)
        {
            _file.Blueprints[i].SortOrder = i;
        }
    }
}
