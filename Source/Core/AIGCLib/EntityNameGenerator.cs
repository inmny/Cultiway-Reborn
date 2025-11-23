using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Friflo.Engine.ECS;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.AIGCLib;

public abstract class EntityNameGenerator<T> : BaseNameGenerator<T> where T : EntityNameGenerator<T>
{
    public void NewNameGenerateRequest(string[] param_list, Entity target)
    {
        _ = GenerateAsync(param_list, target);
    }

    private async Task
        GenerateAsync(string[] param_list, Entity target)
    {
        if (target.IsNull)
        {
            return;
        }
        var name = GetDefaultName(param_list);
        var key = GetStoreKey(param_list);
        if (RequestNewName(key))
        {
            try
            {
                var prompt = GetPrompt(param_list);
                var system_prompt = GetSystemPrompt();
                var response = Manager.RequestResponseContent(prompt, system_prompt, temperature: 0.7f);
                var res = await response;
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
                    name = res;
                }
            }
            catch (Exception e)
            {
                ModClass.LogErrorConcurrent(e.ToString());
            }
        }
        else
        {
            lock (NameDict)
            {
                if (NameDict.TryGetValue(key, out var names) && names.Count > 0)
                {
                    name = names.GetRandom();
                }
            }
        }
        EventSystemHub.Publish(new EntityNameGeneratedEvent()
        {
            Target = target,
            Name = name
        });
    }
}
