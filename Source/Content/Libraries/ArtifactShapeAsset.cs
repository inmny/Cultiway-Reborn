using System;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 法器器形。器形只描述法器的物理外观与默认世界表现，不决定其用途或能力。
/// </summary>
public class ArtifactShapeAsset : ItemShapeAsset
{
    /// <summary>器形本身稳定表达的语义，不包含材料和能力带来的语义。</summary>
    public SemanticDescriptor semantics = new();

    /// <summary>
    /// 外观目录中用于筛选组合模板的器形族。
    /// </summary>
    public string appearance_family;

    /// <summary>
    /// 法器装备后采用的默认世界表现方案。
    /// </summary>
    public ArtifactPresentationAsset presentation;

    /// <summary>
    /// 获取世界中的法器贴图。专属器形可以覆盖默认的组合外观渲染器。
    /// </summary>
    public Func<Entity, Sprite> GetWorldSprite;

    /// <summary>
    /// 获取能力施展、独立运动或部署期间的法器贴图；为空时沿用普通世界贴图。
    /// </summary>
    public Func<Entity, Sprite> GetActiveWorldSprite;

    /// <summary>
    /// 获取支持本体、辉光和阴影分层的世界表现；专属单张贴图无需实现此接口。
    /// </summary>
    public Func<Entity, bool, ArtifactWorldSpriteSet> GetWorldSprites;
}

/// <summary>同一法器世界贴图的语义分层，以及供旧调用路径使用的扁平合成结果。</summary>
public readonly struct ArtifactWorldSpriteSet
{
    /// <summary>参与世界光照和控制状态染色的法器本体。</summary>
    public readonly Sprite Body;

    /// <summary>不受昼夜压暗的自发光像素层。</summary>
    public readonly Sprite Emission;

    /// <summary>位于法器本体下方的接地阴影。</summary>
    public readonly Sprite Shadow;

    /// <summary>本体与辉光合成后的兼容贴图，不包含阴影。</summary>
    public readonly Sprite Composite;

    /// <summary>优先返回完整合成贴图，否则退回本体贴图。</summary>
    public Sprite Main => Composite != null ? Composite : Body;

    /// <summary>世界尺寸与碰撞换算使用本体像素范围，不计外围辉光和阴影。</summary>
    public Sprite ScaleReference => Body != null ? Body : Main;

    public ArtifactWorldSpriteSet(Sprite body, Sprite emission, Sprite shadow, Sprite composite)
    {
        Body = body;
        Emission = emission;
        Shadow = shadow;
        Composite = composite;
    }

    /// <summary>将专属单张世界贴图包装为兼容的单层表现。</summary>
    public static ArtifactWorldSpriteSet Single(Sprite sprite)
    {
        return new ArtifactWorldSpriteSet(sprite, null, null, sprite);
    }
}
