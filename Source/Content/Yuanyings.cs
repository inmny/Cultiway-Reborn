using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;
[Dependency(typeof(Jindans), typeof(SkillEntities), typeof(CultivationSemantics))]
public class Yuanyings : ExtendLibrary<YuanyingAsset, Yuanyings>
{
    public static YuanyingAsset Common { get; private set; }
    /// <summary>
    /// 金煌元婴
    /// </summary>
    public static YuanyingAsset JinHwang  { get; private set; }
    /// <summary>
    /// 剑煌元婴
    /// </summary>
    public static YuanyingAsset SwordHwang { get; private set; }
    /// <summary>
    /// 青木元婴
    /// </summary>
    public static YuanyingAsset Aoki  { get; private set; }
    /// <summary>
    /// 寒霜元婴
    /// </summary>
    public static YuanyingAsset Frost { get; private set; }
    /// <summary>
    /// 烈火元婴
    /// </summary>
    public static YuanyingAsset Blaze  { get; private set; }
    /// <summary>
    ///     润土元婴
    /// </summary>
    public static YuanyingAsset Bentonite { get; private set; }

    /// <summary>
    ///     凝元元婴
    /// </summary>
    public static YuanyingAsset Condensed { get; private set; }
    /// <summary>
    ///     幻影元婴
    /// </summary>
    public static YuanyingAsset Phantom { get; private set; }
    
    /// <summary>
    ///     恶龙元婴
    /// </summary>
    public static YuanyingAsset Dragon { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Yuanying";
    protected override void OnInit()
    {
        var props = typeof(Yuanyings).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var prop in props)
        {
            if (prop.PropertyType != typeof(YuanyingAsset))
            {
                continue;
            }
            var yuanying_asset = prop.GetValue(null) as YuanyingAsset;
                
            if (yuanying_asset == null)
            {
                continue;
            }
            var jindan_prop = typeof(Jindans).GetProperty(prop.Name);
            if (jindan_prop == null || jindan_prop.PropertyType != typeof(JindanAsset))
            {
                continue;
            }
            var jindan_asset = jindan_prop.GetValue(null) as JindanAsset;
            if (jindan_asset == null)
            {
                continue;
            }
            // 从对应的金丹复制元素组成
            yuanying_asset.composition = jindan_asset.composition;
            yuanying_asset.Semantics = ComposeSemantics(jindan_asset);
            Map(yuanying_asset, jindan_asset);
            Map(yuanying_asset, Jindans.Common);
        }
        
        AddEffects();
    }

    private static SemanticDescriptor ComposeSemantics(JindanAsset jindan)
    {
        var builder = new SemanticDescriptorBuilder()
            .Add(CultivationSemantics.Realm.Yuanying)
            .Add(CultivationSemantics.Role.Cultivation);
        for (var i = 0; i < jindan.Semantics.contributions.Length; i++)
        {
            var contribution = jindan.Semantics.contributions[i];
            if (contribution.semantic_id == CultivationSemantics.Realm.Jindan.id ||
                contribution.semantic_id == CultivationSemantics.Role.Cultivation.id) continue;
            builder.Add(contribution);
        }
        return builder.Build();
    }

    private void Map(YuanyingAsset yuanying, JindanAsset jindan)
    {
        var library = cached_library as YuanyingLibrary;
        library?.Map(yuanying, jindan);
    }

    [Hotfixable]
    private void AddEffects()
    {
        foreach (var yuanying in assets_added)
        {
            yuanying.skills.Clear();
            yuanying.skill_acc_weight.Clear();
            var skill_similarities = new Dictionary<SkillEntityAsset, float>();
            var composition_array = yuanying.composition.AsArray();
            foreach (var skill_entity in ModClass.I.SkillV3.SkillLib.list)
            {
                if (!skill_entity.CanBeLearned) continue;
                var skill_composition = skill_entity.Element.AsArray();
                var similarity = MathUtils.CosineSimilarity(composition_array, skill_composition);
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
                yuanying.skill_acc_weight.Add(acc);
                yuanying.skills.Add(pair.Key);
            }
        }
    }

    public override void OnReload()
    {
        AddEffects();
    }
}
