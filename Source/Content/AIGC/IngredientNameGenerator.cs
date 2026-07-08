using System;
using System.IO;
using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class IngredientNameGenerator : PromptNameGenerator<IngredientNameGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_IngredientNameDict.json");

    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你需要为用户给出的材料命名，并且要符合材料来源的特性，不要有任何符号，不要给出思考过程，仅给出一个答案。\\nInput example:\\n为拥有火灵根，金煌金丹的龙掉落的材料命名。\\nOutput example:\\n赤金龙鳞";
    }

    protected override string GetDefaultName(string[] param)
    {
        return GenerateDefaultName(param);
    }

    public static IngredientNamingContext CreateContext(Actor actor, ActorExtend ae, ItemShapeAsset shape)
    {
        return CreateContext(actor, ae, shape?.id);
    }

    public static IngredientNamingContext CreateContext(Actor actor, ActorExtend ae, string shapeId)
    {
        var context = new IngredientNamingContext
        {
            SourceAssetId = actor?.asset?.id ?? string.Empty,
            SourceName = NamingRuleUtils.GetSourceDisplayName(actor),
            ShapeId = shapeId
        };

        if (ae == null) return context;

        context.PowerLevel = ae.GetPowerLevel();
        if (ae.TryGetComponent(out Xian xian))
        {
            context.XianLevel = xian.CurrLevel;
        }

        if (ae.TryGetComponent(out ElementRoot root))
        {
            NamingRuleUtils.ApplyElementRoot(context, root);
        }

        if (ae.TryGetComponent(out Jindan jindan))
        {
            NamingRuleUtils.ApplyJindan(context, jindan);
        }

        var quality = NamingRuleUtils.CalculateQuality(context.PowerLevel, context.XianLevel, context.ElementStrength, context.JindanStrength);
        context.QualityStage = quality.Stage;
        context.QualityLevel = quality.Level;
        return context;
    }

    public static IngredientNamingContext CreateContext(Entity ingredient)
    {
        var context = new IngredientNamingContext();
        if (ingredient.IsNull) return context;

        if (ingredient.TryGetComponent(out ItemCreation creation))
        {
            context.SourceAssetId = creation.creator_asset_id ?? string.Empty;
            context.SourceName = NamingRuleUtils.GetSourceDisplayName(creation.creator_asset_id, creation.creator);
        }
        if (ingredient.TryGetComponent(out ItemShape shape))
        {
            context.ShapeId = shape.shape_id;
        }
        if (ingredient.TryGetComponent(out ElementRoot root))
        {
            NamingRuleUtils.ApplyElementRoot(context, root);
        }
        if (ingredient.TryGetComponent(out Jindan jindan))
        {
            NamingRuleUtils.ApplyJindan(context, jindan);
        }
        if (ingredient.TryGetComponent(out ItemLevel level))
        {
            context.QualityStage = level.Stage;
            context.QualityLevel = level.Level;
        }
        else
        {
            var quality = NamingRuleUtils.CalculateQuality(context.PowerLevel, context.XianLevel, context.ElementStrength, context.JindanStrength);
            context.QualityStage = quality.Stage;
            context.QualityLevel = quality.Level;
        }

        return context;
    }

    public static string GenerateDefaultName(IngredientNamingContext context, string[] legacyParam = null)
    {
        if (context == null || string.IsNullOrEmpty(context.ShapeId))
        {
            return GenerateDefaultName(legacyParam);
        }

        var seed = NamingRuleUtils.StableHash($"{context.SourceAssetId}|{context.SourceName}|{context.ShapeId}|{context.ElementRootId}|{context.JindanId}|{context.QualityStage}|{context.QualityLevel}");
        var descriptor = PickIngredientDescriptor(context, seed);
        var source = string.Empty;
        var shape = ItemShapes.PickIngredientNameCandidate(context.ShapeId, seed);

        if (context.ShapeId == ItemShapes.Ball.id && !string.IsNullOrEmpty(context.JindanId))
        {
            source = NamingRuleUtils.TrimKnownSuffix(NamingRuleUtils.Localize(context.JindanId), "金丹", "丹");
            shape = seed % 2 == 0 ? "丹核" : "丹珠";
        }

        var name = NamingRuleUtils.NormalizeName($"{descriptor}{source}{shape}");
        if (name.Length < 2 && legacyParam != null && legacyParam.Length > 0)
        {
            name = NamingRuleUtils.NormalizeName($"{PickLegacyDescriptor(legacyParam)}{shape}");
        }
        return NamingRuleUtils.LimitNameLength(name, 9);
    }

    public static string GenerateDefaultName(string[] param)
    {
        if (param == null || param.Length == 0) return "灵材";
        var shape = param.Length > 0 ? param[param.Length - 1] : "材";
        var descriptor = PickLegacyDescriptor(param);
        var seed = NamingRuleUtils.StableHash(string.Join("|", param));
        var noun = ItemShapes.PickIngredientNameCandidate(shape, seed);
        return NamingRuleUtils.LimitNameLength(NamingRuleUtils.NormalizeName($"{descriptor}{noun}"), 9);
    }

    public static string LocalizeElement(int elementIndex)
    {
        return NamingRuleUtils.LocalizeElement(elementIndex);
    }

    protected override bool IsValid(string name)
    {
        return name.Length is > 1 and < 10;
    }

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();

        sb.Append("为拥有");
        for (int i = 1; i < param.Length; i++)
        {
            if (i > 1)
            {
                sb.Append('，');
            }
            sb.Append('“');
            sb.Append(param[i]);
            sb.Append('”');
        }

        sb.Append("的");
        sb.Append(param[0]);
        sb.Append("掉落的材料命名");

        return sb.ToString();
    }

    private static string PickIngredientDescriptor(IngredientNamingContext context, int seed)
    {
        if (!string.IsNullOrEmpty(context.JindanId) && seed % 4 == 0)
        {
            var name = NamingRuleUtils.TrimKnownSuffix(NamingRuleUtils.Localize(context.JindanId), "金丹", "丹");
            if (!string.IsNullOrEmpty(name)) return NamingRuleUtils.LimitNameLength(name, 3);
        }

        if (context.QualityStage >= 3) return NamingRuleUtils.Pick(seed, "仙", "太清", "无垢");
        if (context.QualityStage == 2) return NamingRuleUtils.Pick(seed, "天", "玄", "九转");
        if (context.QualityStage == 1 && seed % 3 == 0) return NamingRuleUtils.Pick(seed, "玄", "灵", "凝");

        return ElementDescriptor(context.PrimaryElementIndex, seed);
    }

    private static string PickLegacyDescriptor(string[] param)
    {
        var joined = string.Join("|", param ?? Array.Empty<string>());
        var seed = NamingRuleUtils.StableHash(joined);
        if (joined.ContainsAny("金煌", "金灵根", "金")) return NamingRuleUtils.Pick(seed, "庚金", "金", "玄金");
        if (joined.ContainsAny("木灵根", "青木", "木")) return NamingRuleUtils.Pick(seed, "青木", "青", "生");
        if (joined.ContainsAny("水灵根", "寒霜", "冰", "水")) return NamingRuleUtils.Pick(seed, "玄水", "冰", "寒");
        if (joined.ContainsAny("火灵根", "烈火", "火", "炎")) return NamingRuleUtils.Pick(seed, "赤火", "炎", "赤");
        if (joined.ContainsAny("土灵根", "润土", "土")) return NamingRuleUtils.Pick(seed, "厚土", "黄", "地");
        if (joined.ContainsAny("阴", "幽")) return NamingRuleUtils.Pick(seed, "幽阴", "幽", "玄");
        if (joined.ContainsAny("阳", "曜")) return NamingRuleUtils.Pick(seed, "阳华", "曜", "明");
        if (joined.ContainsAny("混沌", "熵")) return NamingRuleUtils.Pick(seed, "混沌", "浊", "玄");
        return NamingRuleUtils.Pick(seed, "灵", "玄", "凝");
    }

    private static string ElementDescriptor(int elementIndex, int seed)
    {
        return elementIndex switch
        {
            ElementIndex.Iron => NamingRuleUtils.Pick(seed, "庚金", "金", "玄金"),
            ElementIndex.Wood => NamingRuleUtils.Pick(seed, "青木", "青", "生"),
            ElementIndex.Water => NamingRuleUtils.Pick(seed, "玄水", "冰", "寒"),
            ElementIndex.Fire => NamingRuleUtils.Pick(seed, "赤火", "炎", "赤"),
            ElementIndex.Earth => NamingRuleUtils.Pick(seed, "厚土", "黄", "地"),
            ElementIndex.Neg => NamingRuleUtils.Pick(seed, "幽阴", "幽", "玄"),
            ElementIndex.Pos => NamingRuleUtils.Pick(seed, "阳华", "曜", "明"),
            ElementIndex.Entropy => NamingRuleUtils.Pick(seed, "混沌", "浊", "玄"),
            _ => NamingRuleUtils.Pick(seed, "灵", "玄", "凝")
        };
    }
}
