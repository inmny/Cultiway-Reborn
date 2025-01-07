using System;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using NeoModLoader.api.attributes;

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

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.JindanGroup");
        Common.check = (ActorExtend ae, ref XianBase @base) => true;
        Element.check = (ActorExtend ae, ref XianBase @base) => Toolbox.randomChance(@base.GetFiveQiStrength());
        Special.check = (ActorExtend ae, ref XianBase @base) => Toolbox.randomChance(@base.GetStrength() / 20);
    }

    protected override void ActionAfterCreation(PropertyInfo prop, JindanGroupAsset asset)
    {
        var prior = prop.GetCustomAttribute<PriorAttribute>();
        if (prior != null) asset.prior = prior.value;
    }
}