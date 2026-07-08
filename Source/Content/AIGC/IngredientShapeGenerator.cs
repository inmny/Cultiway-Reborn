using System;
using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
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
        return ResolveShapeId(param);
    }

    public static string ResolveShapeId(Actor actor, ActorExtend ae, string[] param)
    {
        if (actor == null || ae == null) return ResolveShapeId(param);
        var seed = NamingRuleUtils.StableHash($"{actor.asset?.id}|{actor.data?.id}|{string.Join("|", param ?? Array.Empty<string>())}");
        return ItemShapes.PickDropShape(actor, ae, seed).id;
    }

    public static string ResolveShapeId(string[] param)
    {
        if (param == null || param.Length == 0) return ItemShapes.Blood.id;
        var joined = string.Join("|", param);
        return joined.ContainsAny("金丹", "妖丹", "内丹") ? ItemShapes.Ball.id : ItemShapes.Blood.id;
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
