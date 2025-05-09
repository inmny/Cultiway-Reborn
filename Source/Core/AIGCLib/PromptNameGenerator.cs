using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cultiway.Utils;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.services;
using Newtonsoft.Json;
using UnityEngine;

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
                try
                {
                    var prompt = GetPrompt(param);
                    var res = Manager.RequestResponseContent(prompt, GetSystemPrompt(), temperature: Temperature).GetAwaiter().GetResult();
                    res = PostProcess(res);
                    if (!string.IsNullOrEmpty(res) && IsValid(res))
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
                }
                catch (Exception e)
                {
                    LogService.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
                }
                finally
                {
                    _isRequestingNewName = false;
                }
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