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
}
