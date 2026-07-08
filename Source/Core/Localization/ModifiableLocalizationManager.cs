using System;
using System.Collections.Generic;
using System.IO;
using NeoModLoader.General;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Localization;

public static class ModifiableLocalizationManager
{
    private const int FileVersion = 1;
    private const string ModConfigDirectoryName = "mods_config";
    private const string FileNameSuffix = "_modifiable_localization.config";
    private const string RealmLevelNamesSectionId = "cultisys_level_names";

    private static readonly Dictionary<string, Dictionary<string, int>> ResetGroups = new()
    {
        [RealmLevelNamesSectionId] = new Dictionary<string, int>
        {
            ["Xian"] = 20,
            ["Magic"] = 10
        }
    };

    private static string _configPath;
    private static ModifiableLocalizationFile _data = new();

    public static string ConfigPath => _configPath;

    public static void Initialize(string modFolderPath)
    {
        try
        {
            string modId = ResolveModId(modFolderPath);
            string configDirectory = Path.Combine(Application.persistentDataPath, ModConfigDirectoryName);
            Directory.CreateDirectory(configDirectory);
            _configPath = Path.Combine(configDirectory, $"{SanitizeFileName(modId)}{FileNameSuffix}");
            Load();
        }
        catch (Exception e)
        {
            ModClass.LogWarning($"Failed to initialize modifiable localization config: {e.Message}");
            _configPath = null;
            _data = new ModifiableLocalizationFile();
        }
    }

    public static string GetText(string sectionId, string key)
    {
        if (TryGetStoredText(sectionId, key, out string text))
        {
            return text;
        }

        return GetDefaultText(sectionId, key);
    }

    public static void UpdateText(string sectionId, string key, string text)
    {
        if (string.IsNullOrEmpty(sectionId) || string.IsNullOrEmpty(key))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            ResetText(sectionId, key);
            return;
        }

        Dictionary<string, string> section = GetOrCreateSection(sectionId);
        section[key] = text.Trim();
        Save();
    }

    public static void ResetText(string sectionId, string key)
    {
        if (string.IsNullOrEmpty(sectionId) || string.IsNullOrEmpty(key))
        {
            return;
        }

        Dictionary<string, string> section = GetOrCreateSection(sectionId);
        section[key] = GetDefaultText(sectionId, key);
        Save();
    }

    public static void ResetCultiLevelGroup(string sectionId, string groupId)
    {
        if (!TryGetGroupKeys(sectionId, groupId, out List<string> keys))
        {
            return;
        }

        Dictionary<string, string> section = GetOrCreateSection(sectionId);
        foreach (string key in keys)
        {
            section[key] = GetDefaultText(sectionId, key);
        }

        Save();
    }

    private static bool TryGetStoredText(string sectionId, string key, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrEmpty(sectionId) || string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (_data.sections.TryGetValue(sectionId, out Dictionary<string, string> section)
            && section != null
            && section.TryGetValue(key, out string stored)
            && !string.IsNullOrWhiteSpace(stored))
        {
            text = stored;
            return true;
        }

        return false;
    }

    private static string GetDefaultText(string sectionId, string key)
    {
        return TryGetDefaultLocaleKey(sectionId, key, out string localeKey) ? LM.Get(localeKey) : string.Empty;
    }

    private static bool TryGetDefaultLocaleKey(string sectionId, string key, out string localeKey)
    {
        localeKey = null;
        if (sectionId != RealmLevelNamesSectionId)
        {
            return false;
        }

        if (!TryParseGroupLevelKey(key, out string groupId, out int level))
        {
            return false;
        }

        localeKey = $"cultisys_{groupId}_{level}";
        return true;
    }

    private static bool TryGetGroupKeys(string sectionId, string groupId, out List<string> keys)
    {
        keys = null;
        if (!ResetGroups.TryGetValue(sectionId, out Dictionary<string, int> groups))
        {
            return false;
        }

        if (!groups.TryGetValue(groupId, out int levelCount))
        {
            return false;
        }

        keys = new List<string>(levelCount);
        for (int i = 0; i < levelCount; i++)
        {
            keys.Add(GetGroupLevelKey(groupId, i));
        }

        return true;
    }

    private static bool TryParseGroupLevelKey(string key, out string groupId, out int level)
    {
        groupId = null;
        level = 0;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        int split = key.LastIndexOf('.');
        if (split <= 0 || split >= key.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(key[(split + 1)..], out level))
        {
            return false;
        }

        groupId = key[..split];
        return true;
    }

    private static Dictionary<string, string> GetOrCreateSection(string sectionId)
    {
        _data.sections ??= new Dictionary<string, Dictionary<string, string>>();
        if (!_data.sections.TryGetValue(sectionId, out Dictionary<string, string> section) || section == null)
        {
            section = new Dictionary<string, string>();
            _data.sections[sectionId] = section;
        }

        return section;
    }

    private static string GetGroupLevelKey(string groupId, int level)
    {
        return $"{groupId}.{level}";
    }

    private static void Save()
    {
        if (string.IsNullOrEmpty(_configPath))
        {
            return;
        }

        try
        {
            _data.version = FileVersion;
            string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception e)
        {
            ModClass.LogWarning($"Failed to save modifiable localization config: {e.Message}");
        }
    }

    private static void Load()
    {
        if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
        {
            _data = new ModifiableLocalizationFile();
            return;
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            _data = JsonConvert.DeserializeObject<ModifiableLocalizationFile>(json) ?? new ModifiableLocalizationFile();
            _data.sections ??= new Dictionary<string, Dictionary<string, string>>();
        }
        catch (Exception e)
        {
            ModClass.LogWarning($"Failed to load modifiable localization config: {e.Message}");
            _data = new ModifiableLocalizationFile();
        }
    }

    private static string ResolveModId(string modFolderPath)
    {
        try
        {
            string manifestPath = Path.Combine(modFolderPath, "mod.json");
            if (File.Exists(manifestPath))
            {
                string json = File.ReadAllText(manifestPath);
                ModManifest manifest = JsonConvert.DeserializeObject<ModManifest>(json);
                if (!string.IsNullOrWhiteSpace(manifest?.GUID))
                {
                    return manifest.GUID.Trim();
                }
            }
        }
        catch (Exception e)
        {
            ModClass.LogWarning($"Failed to resolve mod id for modifiable localization config: {e.Message}");
        }

        return new DirectoryInfo(modFolderPath).Name;
    }

    private static string SanitizeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "mod" : value.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(c, '_');
        }

        return result;
    }

    private sealed class ModifiableLocalizationFile
    {
        public int version = FileVersion;
        public Dictionary<string, Dictionary<string, string>> sections = new();
    }

    private sealed class ModManifest
    {
        public string GUID { get; set; }
    }
}
