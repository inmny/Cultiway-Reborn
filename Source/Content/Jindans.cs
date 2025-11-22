using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;
[Dependency(typeof(SkillEntities))]
public class Jindans : ExtendLibrary<JindanAsset, Jindans>
{
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

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Jindan");
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

        Condensed.Group = JindanGroups.Special;
        Condensed.composition = new ElementComposition(15, 15, 15, 15, 15, 5, 5, 15);

        Phantom.Group = JindanGroups.Special;
        Phantom.composition = new ElementComposition(5, 5, 10, 5, 5, 40, 5, 25);

        Dragon.Group = JindanGroups.External;
        Dragon.composition = new ElementComposition(20, 5, 10, 20, 15, 5, 20, 5);

        AddEffects();
    }
    [Hotfixable]
    private void AddEffects()
    {
        foreach (var jindan in assets_added)
        {
            var skill_similarities = new Dictionary<SkillEntityAsset, float>();
            var composition_array = jindan.composition.AsArray();
            foreach (var skill_entity in ModClass.I.SkillV3.SkillLib.list)
            {
                skill_similarities[skill_entity] =
                    MathUtils.CosineSimilarity(composition_array, skill_entity.Element.AsArray());
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