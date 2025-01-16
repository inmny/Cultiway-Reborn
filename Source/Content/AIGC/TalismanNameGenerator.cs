using System.IO;
using Cultiway.Core.AIGCLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class TalismanNameGenerator : PromptNameGenerator<TalismanNameGenerator>
{
    protected override string NameDictPath { get; } = Path.Combine(Application.persistentDataPath, "Cultiway_TalismanNameDict.json");

    protected override string GetDefaultName(string[] param)
    {
        return param[0] + "符";
    }
    [Hotfixable]
    protected override bool RequestNewName(string key)
    {
        return !NameDict.TryGetValue(key, out var names) || Toolbox.randomChance(1 / Mathf.Exp(names.Count));
    }

    protected override string GetStoreKey(string[] param)
    {
        return param[0];
    }

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        return $"为能够释放名为“{param[0]}”的法术的符箓命名，仅给出一个答案(比如落雷符)，不要有任何符号";
    }
}