using Cultiway.Abstract;
using Cultiway.Content.Libraries;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;
[Dependency(typeof(WrappedSkills))]
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

        JinHwang.Group = JindanGroups.Element;
        SwordHwang.Group = JindanGroups.Element;
        Aoki.Group = JindanGroups.Element;
        Frost.Group = JindanGroups.Element;
        Blaze.Group = JindanGroups.Element;
        Bentonite.Group = JindanGroups.Element;

        Condensed.Group = JindanGroups.Special;
        Phantom.Group = JindanGroups.Special;

        Dragon.Group = JindanGroups.External;

        AddEffects();
    }
    [Hotfixable]
    private void AddEffects()
    {
        JinHwang.wrapped_skill_id = WrappedSkills.StartAllGoldBlade.id;
        SwordHwang.wrapped_skill_id = WrappedSkills.StartAllGoldSword.id;
        Aoki.wrapped_skill_id = WrappedSkills.StartAllWindBlade.id;
        Frost.wrapped_skill_id = WrappedSkills.StartAllWaterBlade.id;
        Blaze.wrapped_skill_id = WrappedSkills.StartAllFireBlade.id;
        Bentonite.wrapped_skill_id = WrappedSkills.StartAllGroundThorn.id;
    }

    public override void OnReload()
    {
        AddEffects();
    }
}