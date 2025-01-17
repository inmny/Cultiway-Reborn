using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class ElixirNameGenerator : PromptNameGenerator<ElixirNameGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_ElixirNameDict.json");
    protected override string GetDefaultName(string[] param)
    {
        return "未名丹药";
    }

    protected override bool RequestNewName(string key)
    {
        return !NameDict.TryGetValue(key, out var names) || Toolbox.randomChance(1 / Mathf.Exp(names.Count));
    }

    protected override string GetStoreKey(string[] param)
    {
        return param.Join();
    }

    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();

        sb.Append("为具有\"");
        sb.Append(param[0]);
        sb.Append("\"效果的丹药命名，该丹药由");
        for (int i = 1; i < param.Length; i++)
        {
            sb.Append('\"');
            sb.Append(param[i]);
            sb.Append('\"');
            if (i < param.Length - 1)
            {
                sb.Append('，');
            }
        }

        sb.Append("炼制得到，仅给出一个答案(比如凝元丹)，不要有任何符号");
        
        return sb.ToString();
    }
}