using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class ElixirEffectJsonGenerator : PromptNameGenerator<ElixirEffectJsonGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_ElixirEffect.json");
    protected override string GetDefaultName(string[] param)
    {
        return string.Empty;
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
        sb.Append("为由");
        for (int i=0;i<param.Length;i++)
        {
            sb.Append('\"');
            sb.Append(param[i]);
            sb.Append('\"');
            if (i < param.Length - 1)
            {
                sb.Append(',');
            }
        }

        sb.Append("制成的丹药生成药效，");
        // TODO: 填写更完善的prompt使得其
        return sb.ToString();
    }
}