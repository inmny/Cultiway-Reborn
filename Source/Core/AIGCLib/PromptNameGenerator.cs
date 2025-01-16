using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cultiway.Utils;
using NeoModLoader.api.attributes;
using NeoModLoader.services;
using Newtonsoft.Json;

namespace Cultiway.Core.AIGCLib;

public abstract class PromptNameGenerator<T> where T : PromptNameGenerator<T>
{
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
    protected PromptNameGenerator()
    {
        Load();
    }

    protected abstract string NameDictPath { get; }
    protected Dictionary<string, List<string>> NameDict { get; set; } = new();

    protected abstract string GetDefaultName(string[] param);
    protected abstract bool RequestNewName(string key);
    protected abstract string GetStoreKey(string[] param);
    protected abstract string GetPrompt(string[] param);
    protected virtual float Temperature { get; } = 2;
    
    private bool _isRequestingNewName = false;
    [Hotfixable]
    public string GenerateName(string[] param)
    {
        var key = GetStoreKey(param);
        if (!_isRequestingNewName && RequestNewName(key))
        {
            _isRequestingNewName = true;
            Task.Run(() =>
            {
                var prompt = GetPrompt(param);
                var res = Manager.RequestResponseContent(prompt, temperature: Temperature).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(res))
                    lock (NameDict)
                    {
                        if (NameDict.TryGetValue(key, out var names))
                        {
                            names.Add(res);
                        }
                        else
                        {
                            NameDict[key] = new List<string> { res };
                        }
                        Save();
                    }
                _isRequestingNewName = false;
            });
        }

        lock (NameDict)
        {
            if (NameDict.TryGetValue(key, out var names) && names.Count > 0)
            {
                return names.GetRandom();
            }
        }

        return GetDefaultName(param);
    }

    public void Save()
    {
        File.WriteAllText(NameDictPath, JsonConvert.SerializeObject(NameDict));
    }

    public void Load()
    {
        if (File.Exists(NameDictPath))
        {
            try
            {
                NameDict =
                    JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText(NameDictPath)) ??
                    new();
            }
            catch
            {
                NameDict = new();
            }
        }
    }
}