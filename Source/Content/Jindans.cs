using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;
[Dependency(typeof(SkillEntities))]
public class Jindans : ExtendLibrary<JindanAsset, Jindans>
{
    private const float ElementJindanFiveQiWeight = 0.7f;
    private const float ElementJindanRootWeight = 0.3f;
    private const float ElementJindanMinimumScore = 0.01f;

    public static JindanAsset Common { get; private set; }
    /// <summary>
    /// 金煌金丹
    /// </summary>
    public static JindanAsset JinHwang  { get; private set; }
    /// <summary>
    /// 剑煌金丹
    /// </summary>
    public static JindanAsset SwordHwang { get; private set; }
    /// <summary>
    /// 青木金丹
    /// </summary>
    public static JindanAsset Aoki  { get; private set; }
    /// <summary>
    /// 寒霜金丹
    /// </summary>
    public static JindanAsset Frost { get; private set; }
    /// <summary>
    /// 烈火金丹
    /// </summary>
    public static JindanAsset Blaze  { get; private set; }
    /// <summary>
    ///     润土金丹
    /// </summary>
    public static JindanAsset Bentonite { get; private set; }

    /// <summary>
    ///     凝元金丹
    /// </summary>
    public static JindanAsset Condensed { get; private set; }
    /// <summary>
    ///     幻影金丹
    /// </summary>
    public static JindanAsset Phantom { get; private set; }
    
    /// <summary>
    ///     恶龙金丹
    /// </summary>
    public static JindanAsset Dragon { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Jindan";
    protected override void OnInit()
    {
        Common.Group = JindanGroups.Common;
        Common.composition = new ElementComposition(20, 20, 20, 20, 20, 0, 0, 0);

        JinHwang.Group = JindanGroups.Element;
        JinHwang.composition = new ElementComposition(60, 5, 5, 10, 10, 3, 3, 4);

        SwordHwang.Group = JindanGroups.Element;
        SwordHwang.composition = new ElementComposition(50, 5, 5, 15, 10, 2, 10, 3);

        Aoki.Group = JindanGroups.Element;
        Aoki.composition = new ElementComposition(5, 60, 10, 5, 10, 3, 3, 4);

        Frost.Group = JindanGroups.Element;
        Frost.composition = new ElementComposition(5, 5, 60, 5, 5, 15, 2, 3);

        Blaze.Group = JindanGroups.Element;
        Blaze.composition = new ElementComposition(5, 5, 5, 60, 10, 2, 10, 3);

        Bentonite.Group = JindanGroups.Element;
        Bentonite.composition = new ElementComposition(10, 5, 10, 5, 60, 3, 3, 4);

        SetupElementJindanScore(JinHwang);
        SetupElementJindanScore(SwordHwang);
        SetupElementJindanScore(Aoki);
        SetupElementJindanScore(Frost);
        SetupElementJindanScore(Blaze);
        SetupElementJindanScore(Bentonite);

        Condensed.Group = JindanGroups.Special;
        Condensed.composition = new ElementComposition(15, 15, 15, 15, 15, 5, 5, 15);

        Phantom.Group = JindanGroups.Special;
        Phantom.composition = new ElementComposition(5, 5, 10, 5, 5, 40, 5, 25);

        Dragon.Group = JindanGroups.External;
        Dragon.composition = new ElementComposition(20, 5, 10, 20, 15, 5, 20, 5);

        AddEffects();
    }

    /// <summary>
    /// 为元素金丹设置基于筑基五气与灵根组成的相似度权重。
    /// </summary>
    private static void SetupElementJindanScore(JindanAsset jindan)
    {
        jindan.score = (ActorExtend ae, ref XianBase xianBase) => GetElementJindanScore(jindan, ae, ref xianBase);
    }

    /// <summary>
    /// 计算元素金丹与当前修士筑基五气、先天灵根的综合相似度。
    /// </summary>
    private static float GetElementJindanScore(JindanAsset jindan, ActorExtend ae, ref XianBase xianBase)
    {
        var jindanComposition = jindan.composition.AsArray();
        var fiveQiComposition = GetFiveQiComposition(ref xianBase);
        var fiveQiSimilarity = SafeCosineSimilarity(jindanComposition, fiveQiComposition, ElementIndex.Neg);
        var rootSimilarity = fiveQiSimilarity;

        if (ae.HasElementRoot())
        {
            rootSimilarity = SafeCosineSimilarity(jindanComposition, GetRootComposition(ae), ElementIndex.Neg);
        }

        var score = fiveQiSimilarity * ElementJindanFiveQiWeight + rootSimilarity * ElementJindanRootWeight;
        return Mathf.Max(score, ElementJindanMinimumScore);
    }

    /// <summary>
    /// 把筑基五气转成与元素组成同顺序的向量。
    /// </summary>
    private static float[] GetFiveQiComposition(ref XianBase xianBase)
    {
        return
        [
            xianBase.iron,
            xianBase.wood,
            xianBase.water,
            xianBase.fire,
            xianBase.earth,
            0f,
            0f,
            0f
        ];
    }

    /// <summary>
    /// 把灵根组成转成与金丹组成同顺序的向量。
    /// </summary>
    private static float[] GetRootComposition(ActorExtend ae)
    {
        ref var root = ref ae.GetElementRoot();
        return
        [
            root.Iron,
            root.Wood,
            root.Water,
            root.Fire,
            root.Earth,
            root.Neg,
            root.Pos,
            root.Entropy
        ];
    }

    /// <summary>
    /// 计算安全的余弦相似度，避免异常数据导致权重为 NaN。
    /// </summary>
    private static float SafeCosineSimilarity(float[] a, float[] b, int len)
    {
        var similarity = MathUtils.CosineSimilarity(a, b, len);
        if (float.IsNaN(similarity) || float.IsInfinity(similarity)) return 0f;
        return Mathf.Max(0f, similarity);
    }

    [Hotfixable]
    private void AddEffects()
    {
        foreach (var jindan in assets_added)
        {
            jindan.skills.Clear();
            jindan.skill_acc_weight.Clear();
            var skill_similarities = new Dictionary<SkillEntityAsset, float>();
            var composition_array = jindan.composition.AsArray();
            foreach (var skill_entity in ModClass.I.SkillV3.SkillLib.list)
            {
                if (!skill_entity.CanBeLearned) continue;
                var skill_composition = skill_entity.Element.AsArray();
                var similarity = MathUtils.CosineSimilarity(composition_array, skill_composition);
                if (jindan.Group == JindanGroups.Special)
                {
                    // 特殊金丹压低多属性法术权重，避免过于偏向风刃等组合技能
                    var non_zero = 0;
                    for (var i = 0; i < skill_composition.Length; i++)
                    {
                        if (skill_composition[i] > 0) non_zero++;
                    }
                    if (non_zero > 1) similarity *= 0.7f;
                }
                if (float.IsNaN(similarity) || float.IsInfinity(similarity) || similarity <= 0f) continue;
                skill_similarities[skill_entity] = similarity;
            }

            var sorted = skill_similarities
                .OrderByDescending(pair => pair.Value)
                .ToList();
            var acc = 0f;
            foreach (var pair in sorted)
            {
                acc += pair.Value;
                jindan.skill_acc_weight.Add(acc);
                jindan.skills.Add(pair.Key);
            }
        }
    }

    public override void OnReload()
    {
        AddEffects();
    }
}
