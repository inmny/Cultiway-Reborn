using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;

namespace Cultiway.Core.AIGCLib;

/// <summary>
/// 面向 Actor 的异步命名生成器，生成结果通过事件系统回写。
/// </summary>
public abstract class ActorNameGenerator<T> : BaseNameGenerator<T>
    where T : ActorNameGenerator<T>
{
    protected virtual float Temperature { get; } = 0.7f;

    public void NewNameGenerateRequest(string[] paramList, Actor target)
    {
        if (target.isRekt())
            return;
        _ = GenerateAsync(paramList, target.data.id);
    }

    private async Task GenerateAsync(string[] paramList, long actorId)
    {
        var name = GetDefaultName(paramList);
        var key = GetStoreKey(paramList);

        var generated_name = string.Empty;
        if (RequestNewName(key))
        {
            try
            {
                var prompt = GetPrompt(paramList);
                var systemPrompt = GetSystemPrompt();
                var response = Manager.RequestResponseContent(
                    prompt,
                    systemPrompt,
                    temperature: Temperature
                );
                generated_name = await response;
                generated_name = PostProcess(generated_name);
                if (!string.IsNullOrEmpty(generated_name) && IsValid(generated_name))
                {
                    lock (NameDict)
                    {
                        if (NameDict.TryGetValue(key, out var names))
                        {
                            names.Add(generated_name);
                        }
                        else
                        {
                            NameDict[key] = new List<string> { generated_name };
                        }
                        Save();
                    }
                    name = generated_name;
                }
            }
            catch (Exception e)
            {
                ModClass.LogErrorConcurrent(e.ToString());
            }
        }
        if (string.IsNullOrEmpty(generated_name))
        {
            lock (NameDict)
            {
                if (NameDict.TryGetValue(key, out var names) && names.Count > 0)
                {
                    name = names.GetRandom();
                }
            }
        }
        if (World.world.units.get(actorId) == null)
            return;
        EventSystemHub.Publish(new ActorNameGeneratedEvent() { ID = actorId, Name = name });
    }
}
