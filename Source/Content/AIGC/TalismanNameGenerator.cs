using System.IO;
using Cultiway.Core.AIGCLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class TalismanNameGenerator : PromptNameGenerator<TalismanNameGenerator>
{
    protected override string NameDictPath { get; } = Path.Combine(Application.persistentDataPath, "Cultiway_TalismanNameDict.json");

    protected override string GetSystemPrompt()
    {
        return "为用户提供的法术生成符箓名称，仅给出一个答案，不要有任何符号。\nInput example:\n雷击术\nOutput example:\n落雷符";
    }

    protected override string GetDefaultName(string[] param)
    {
        return param[0] + "符";
    }

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        return param[0];
    }
}