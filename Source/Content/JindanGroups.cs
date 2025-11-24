using System;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content;

public class JindanGroups : ExtendLibrary<JindanGroupAsset, JindanGroups>
{
    class PriorAttribute(int value) : Attribute
    {
        public readonly int value = value;
    }
    [Prior(0)]
    public static JindanGroupAsset Common   { get; private set; }
    [Prior(1)]
    public static JindanGroupAsset Element  { get; private set; }
    [Prior(2)]
    public static JindanGroupAsset Special  { get; private set; }
    [Prior(3)]
    public static JindanGroupAsset External { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.JindanGroup";
    protected override void OnInit()
    {
        Common.check = (ActorExtend ae, ref XianBase @base) => true;
        Element.check = (ActorExtend ae, ref XianBase @base) => Randy.randomChance(@base.GetFiveQiStrength());
        Special.check = (ActorExtend ae, ref XianBase @base) =>
        {
            // 需要五气与三花都较为均衡才有更高概率进入特殊金丹池
            var balanced = Mathf.Min(@base.GetThreeHuaStrength(), @base.GetFiveQiStrength());
            var chance = balanced / 10f;
            if (chance < 0) chance = 0;
            if (chance > 1) chance = 1;
            return Randy.randomChance(chance);
        };
    }

    protected override void ActionAfterCreation(PropertyInfo prop, JindanGroupAsset asset)
    {
        var prior = prop.GetCustomAttribute<PriorAttribute>();
        if (prior != null) asset.prior = prior.value;
    }
}
