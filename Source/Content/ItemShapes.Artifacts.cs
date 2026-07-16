using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 法器器形。注册为 <see cref="ArtifactShapeAsset"/>（继承自 ItemShapeAsset），
/// 既保留视觉形状的纹理/命名能力，又引用可独立扩展的组合外观与世界表现方案。
/// 因 <see cref="Abstract.ExtendLibrary{TAsset, T}"/> 的自动注册按属性精确类型匹配，
/// ArtifactShapeAsset 类型的属性不会被 <see cref="ItemShapes"/>(ExtendLibrary&lt;ItemShapeAsset&gt;) 自动注册，
/// 故在此手动 <c>Add</c> 进 ItemShapeLibrary，与普通 ItemShapeAsset 共库、共反查路径。
/// </summary>
[Dependency(typeof(ArtifactPresentations))]
public partial class ItemShapes
{
    /// <summary>剑形法器；以剑尖为前向锚点，适合飞行穿刺、剑阵和近身护主能力。</summary>
    public static ArtifactShapeAsset Sword  { get; private set; }
    /// <summary>印形法器；以印面和底座展开作用，适合坠击、镇压、封禁和法域能力。</summary>
    public static ArtifactShapeAsset Seal   { get; private set; }
    /// <summary>袍形法器；贴附或环绕持有者，适合护盾、隐匿、卸力和持续防护。</summary>
    public static ArtifactShapeAsset Robe   { get; private set; }
    /// <summary>镜形法器；以镜面为作用中心，适合反射、洞察、破妄和摄魂。</summary>
    public static ArtifactShapeAsset Mirror { get; private set; }
    /// <summary>鼎形法器；可作为炉鼎实体部署，适合炼制辅助、火炼、吞噬和转化。</summary>
    public static ArtifactShapeAsset Ding   { get; private set; }
    /// <summary>旗幡形法器；以旗面和旗杆维持法域，适合号令、聚魂和召役。</summary>
    public static ArtifactShapeAsset Banner { get; private set; }
    /// <summary>钟铃形法器；通过声波与共鸣作用于范围目标，适合震魂、净化和护罩。</summary>
    public static ArtifactShapeAsset Bell   { get; private set; }
    /// <summary>葫芦形法器；以内腔储存并吸纳或释放资源，适合吞摄、蓄灵和倾泻。</summary>
    public static ArtifactShapeAsset Gourd  { get; private set; }
    /// <summary>扇形法器；沿扇面朝向释放扇形效果，适合风火横扫、击退和净化。</summary>
    public static ArtifactShapeAsset Fan    { get; private set; }
    /// <summary>塔形法器；投射多层镇压或防护法域，适合囚禁、结界和塔影攻击。</summary>
    public static ArtifactShapeAsset Tower  { get; private set; }
    /// <summary>珠形法器；围绕持有者悬浮共鸣，适合护体、脉冲攻击和灵力联结。</summary>
    public static ArtifactShapeAsset Pearl  { get; private set; }

    private void SetupArtifactShapes()
    {
        Sword = AddArtifactShape(nameof(Sword), ["剑"], "sword", ArtifactPresentations.Sword);
        Seal = AddArtifactShape(nameof(Seal), ["印"], "seal", ArtifactPresentations.Seal);
        Robe = AddArtifactShape(nameof(Robe), ["袍"], "robe", ArtifactPresentations.Robe);
        Mirror = AddArtifactShape(nameof(Mirror), ["镜"], "mirror", ArtifactPresentations.Mirror);
        Ding = AddArtifactShape(nameof(Ding), ["鼎"], "ding", ArtifactPresentations.Ding);
        Banner = AddArtifactShape(nameof(Banner), ["旗", "幡"], "banner", ArtifactPresentations.Banner);
        Bell = AddArtifactShape(nameof(Bell), ["钟", "铃", "铎"], "bell", ArtifactPresentations.Bell);
        Gourd = AddArtifactShape(nameof(Gourd), ["葫芦"], "gourd", ArtifactPresentations.Gourd);
        Fan = AddArtifactShape(nameof(Fan), ["扇"], "fan", ArtifactPresentations.Fan);
        Tower = AddArtifactShape(nameof(Tower), ["塔"], "tower", ArtifactPresentations.Tower);
        Pearl = AddArtifactShape(nameof(Pearl), ["珠", "宝珠"], "pearl", ArtifactPresentations.Pearl);
    }

    private ArtifactShapeAsset AddArtifactShape(string id_suffix, string[] nameCandidates,
        string appearanceFamily, ArtifactPresentationAsset presentation)
    {
        var asset = new ArtifactShapeAsset
        {
            id = $"{Prefix()}.{id_suffix}",
            ingredient_name_candidates = nameCandidates,
            appearance_family = appearanceFamily,
            presentation = presentation,
            GetIcon = ArtifactAppearanceRenderer.GetIconSprite,
            GetWorldSprite = ArtifactAppearanceRenderer.GetWorldSprite,
        };
        return (ArtifactShapeAsset)Add(asset);
    }
}
