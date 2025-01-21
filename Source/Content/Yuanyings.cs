using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content;
[Dependency(typeof(Jindans))]
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
    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Yuanying");

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
            Map(yuanying_asset, jindan_asset);
            Map(yuanying_asset, Jindans.Common);
        }
    }

    private void Map(YuanyingAsset yuanying, JindanAsset jindan)
    {
        var library = cached_library as YuanyingLibrary;
        library?.Map(yuanying, jindan);
    }
}