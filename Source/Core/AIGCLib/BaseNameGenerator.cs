using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.AIGCLib;

public abstract class BaseNameGenerator<T> where T : BaseNameGenerator<T>
{
    protected BaseNameGenerator()
    {
        Load();
    }
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = System.Activator.CreateInstance<T>();
            }

            return _instance;
        }
    }

    private static T _instance;
    protected abstract string NameDictPath { get; }
    protected Dictionary<string, List<string>> NameDict { get; set; } = new();
    protected abstract string GetSystemPrompt();

    protected abstract string GetDefaultName(string[] param);

    protected virtual bool IsValid(string name)
    {
        return true;
    }
    protected virtual bool RequestNewName(string key)
    {
        return !NameDict.TryGetValue(key, out var names) || Randy.randomChance(1 / Mathf.Exp(names.Count));
    }

    protected virtual string GetStoreKey(string[] param)
    {
        return param.Join();
    }

    protected virtual string PostProcess(string name)
    {
        return name;
    }
    protected abstract string GetPrompt(string[] param);
    public void Save()
    {
        File.WriteAllText(NameDictPath, JsonConvert.SerializeObject(NameDict));
    }

    public void Load()
    {
        var path = NameDictPath;
        if (!File.Exists(path))
        {
            path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content/PreparedLLMResults",
                Path.GetFileName(path));
        }
        if (File.Exists(path))
        {
            try
            {
                NameDict =
                    JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText(path)) ??
                    new();
            }
            catch
            {
                NameDict = new();
            }
        }
    }
}