using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using UnityEngine;

namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>附加图层随来源器官消失时的着色方式。</summary>
public enum CreatureLayerTintPolicy : byte
{
    /// <summary>使用图层图片自身颜色。</summary>
    None,

    /// <summary>使用生物所属势力的颜色，适合鳞片、羽毛等表被图层。</summary>
    KingdomColor,

    /// <summary>在图片颜色上整体提亮，适合眼部、灵光等发光图层。</summary>
    Glow,

    /// <summary>使用图层定义中的固定颜色，适合火羽、月华等固定色调。</summary>
    FixedColor
}

/// <summary>附加图层在主体某一动画帧上的对应图片。</summary>
[Serializable]
public struct CreatureLayerFrame
{
    /// <summary>主体动画帧的图片名称。</summary>
    public string BaseFrameName;

    /// <summary>图层帧图片的资源路径，相对 GameResources。</summary>
    public string SpritePath;
}

/// <summary>一类生物共用的外观骨架：声明命名锚点与图层前后顺序。</summary>
public sealed class CreatureVisualRigAsset : Asset
{
    /// <summary>骨架适用的生物单位模板编号；为空时对全部模板生效。</summary>
    public string[] CompatibleActorAssetIds = Array.Empty<string>();

    /// <summary>
    ///     命名锚点表；数值是主体精灵包围盒的比例：x 沿朝向正方向（头为正、尾为负），y 向上。
    /// </summary>
    public Dictionary<string, Vector2> Anchors = new(StringComparer.Ordinal);

    /// <summary>外观通道从后到前的绘制顺序。</summary>
    public string[] LayerOrder = Array.Empty<string>();

    /// <summary>判断骨架是否适用于指定生物单位模板。</summary>
    public bool SupportsActor(string actorAssetId)
    {
        if (CompatibleActorAssetIds == null || CompatibleActorAssetIds.Length == 0) return true;
        foreach (string compatible in CompatibleActorAssetIds)
        {
            if (string.Equals(compatible, actorAssetId, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>按名称读取锚点偏移；未登记的锚点回到原点。</summary>
    public Vector2 ResolveAnchor(string anchorId)
    {
        return !string.IsNullOrEmpty(anchorId) && Anchors != null && Anchors.TryGetValue(anchorId, out Vector2 anchor)
            ? anchor
            : Vector2.zero;
    }
}

/// <summary>一个器官的附加图层定义：按主体动画帧预先对应图层帧。</summary>
public sealed class CreatureVisualLayerAsset : Asset
{
    /// <summary>图层兼容的外观骨架编号；为空时对全部骨架生效。</summary>
    public string[] RigCompatibility = Array.Empty<string>();

    /// <summary>外观通道；同一通道只显示一个胜出图层。</summary>
    public string Channel;

    /// <summary>主体动画帧到图层帧的预先对应表。</summary>
    public CreatureLayerFrame[] FramesByBaseFrame = Array.Empty<CreatureLayerFrame>();

    /// <summary>整动画通配贴图；逐帧对照未命中时使用，适合不随动画变形的部件。</summary>
    public string WildcardSpritePath;

    /// <summary>
    ///     是否按主体轮廓遮罩。开启后图层纹理与主体精灵的像素轮廓相乘，只显示身体范围内的部分，
    ///     适合鳞纹、羽纹这类跟随体表的淡染；轮廓部件（角、甲、翼）保持关闭。
    /// </summary>
    public bool MaskToBody;

    /// <summary>挂点名称，引用外观骨架的命名锚点。</summary>
    public string Anchor;

    /// <summary>在锚点基础上的额外偏移（x 沿朝向，y 向上）。</summary>
    public Vector2 Offset;

    /// <summary>图层相对主体大小的缩放。</summary>
    public float Scale = 1f;

    /// <summary>图层着色方式。</summary>
    public CreatureLayerTintPolicy TintPolicy = CreatureLayerTintPolicy.None;

    /// <summary>固定着色颜色；仅 <see cref="CreatureLayerTintPolicy.FixedColor" /> 时使用。</summary>
    public Color TintColor = Color.white;

    /// <summary>判断图层是否兼容指定外观骨架。</summary>
    public bool SupportsRig(string rigId)
    {
        if (RigCompatibility == null || RigCompatibility.Length == 0) return true;
        foreach (string compatible in RigCompatibility)
        {
            if (string.Equals(compatible, rigId, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    ///     按主体动画帧名称读取图层帧路径；逐帧对照优先，未命中时回退通配贴图。
    /// </summary>
    public bool TryGetFramePath(string baseFrameName, out string spritePath)
    {
        if (!string.IsNullOrEmpty(baseFrameName))
        {
            foreach (CreatureLayerFrame frame in FramesByBaseFrame)
            {
                if (string.Equals(frame.BaseFrameName, baseFrameName, StringComparison.Ordinal))
                {
                    spritePath = frame.SpritePath;
                    return !string.IsNullOrEmpty(spritePath);
                }
            }
        }

        spritePath = WildcardSpritePath;
        return !string.IsNullOrEmpty(spritePath);
    }
}

/// <summary>外观骨架定义库。</summary>
public sealed class CreatureVisualRigLibrary : CreatureCompositionAssetLibrary<CreatureVisualRigAsset>
{
}

/// <summary>附加图层定义库。</summary>
public sealed class CreatureVisualLayerLibrary : CreatureCompositionAssetLibrary<CreatureVisualLayerAsset>
{
}
