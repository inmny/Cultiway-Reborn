using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class IngredientShapeGenerator : PromptNameGenerator<IngredientShapeGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_IngredientShapeDict.json");

    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你是修仙素材形状判定器，需要根据素材来源（修士类别、灵根、金丹等）选择最合适的形状。" +
               $"你只能返回一个形状的英文代号，且必须从给定列表中挑选: {string.Join(", ", ModClass.L.ItemShapeLibrary.list.Select(asset => $"{asset.id}({asset.id.Localize()})"))}。";
    }

    protected override string GetDefaultName(string[] param)
    {
        return ItemShapes.Ball.id.Localize();
    }

    protected override bool IsValid(string name)
    {
        return ModClass.L.ItemShapeLibrary.has(name);
    }

    protected override string PostProcess(string name)
    {
        return name.Trim();
    }

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();
        sb.Append("素材来源：");
        if (param.Length > 0)
        {
            sb.Append(string.Join("，", param));
        }
        else
        {
            sb.Append("未知");
        }

        sb.Append("。请判断其掉落素材的形状。");
        return sb.ToString();
    }
}
