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

public abstract class PromptNameGenerator<T> : BaseNameGenerator<T> where T : PromptNameGenerator<T>
{
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
    public async Task<string> GenerateNameAsync(string[] param)
    {
        var key = GetStoreKey(param);
        if (!_isRequestingNewName && RequestNewName(key))
        {
            _isRequestingNewName = true;
            try
            {
                var prompt = GetPrompt(param);
                var res = await Manager.RequestResponseContent(prompt, GetSystemPrompt(), temperature: Temperature);
                res = PostProcess(res);
                if (!string.IsNullOrEmpty(res) && IsValid(res))
                {
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

                    return res;
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
}