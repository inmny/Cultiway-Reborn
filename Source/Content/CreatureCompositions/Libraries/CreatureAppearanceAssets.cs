using System;
using System.Collections.Generic;

namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>一个物种的外观资源组；帧位置已在制作时对齐原版身体。</summary>
public sealed class CreatureVisualRigAsset : Asset
{
    /// <summary>可以使用这组外观的单位模板。</summary>
    public string[] CompatibleActorAssetIds = Array.Empty<string>();

    /// <summary>检查当前单位是否仍是资源对应的物种。</summary>
    public bool SupportsActor(string actorAssetId) => Array.IndexOf(CompatibleActorAssetIds, actorAssetId) >= 0;
}

/// <summary>部件相对身体的合成顺序。</summary>
public enum CreatureLayerDepth : byte
{
    /// <summary>尾和远侧翼先画，身体随后盖住根部。</summary>
    BehindBody,
    /// <summary>体表花纹画在身体上、角和眼之下。</summary>
    Surface,
    /// <summary>角、眼与背甲覆盖在身体正面。</summary>
    AboveBody
}

/// <summary>已经按原版动画帧对齐的部件图片。</summary>
public sealed class CreatureLayerFrame
{
    /// <summary>图片资源路径，相对 GameResources。</summary>
    public string SpritePath;
    /// <summary>对应成体身体帧的宽度；防止把成体部件画到幼体上。</summary>
    public int BodyWidth;
    /// <summary>对应成体身体帧的高度。</summary>
    public int BodyHeight;
    /// <summary>部件图片左边距原身体图片左边的像素距离。</summary>
    public int X;
    /// <summary>部件图片底边距原身体图片底边的像素距离。</summary>
    public int Y;
}

/// <summary>一个器官的外观：位置竞争、遮挡规则及逐物种逐帧图片。</summary>
public sealed class CreatureVisualLayerAsset : Asset
{
    /// <summary>实际占据的位置，例如角、眼、背部、尾或体表；同处只选一种造型。</summary>
    public string Channel;
    /// <summary>争用位置和外观名额时的优先顺序。</summary>
    public int Priority;
    /// <summary>本层占据后不能再画的位置，例如背甲盖住体表纹样。</summary>
    public string[] OccludesChannels = Array.Empty<string>();
    /// <summary>相对身体的合成顺序。</summary>
    public CreatureLayerDepth Depth;
    /// <summary>是否只在原身体有像素的位置着色。</summary>
    public bool ClipToBody;
    /// <summary>界面展示用的小图标，与世界动画图片分开。</summary>
    public string IconPath;
    /// <summary>物种资源组编号 → 原版动画帧名 → 对齐好的部件。</summary>
    public Dictionary<string, Dictionary<string, CreatureLayerFrame>> FramesByRig = new(StringComparer.Ordinal);
}

/// <summary>由离线制作工具生成的唯一外观目录；游戏与预览共用。</summary>
public sealed class CreatureAppearanceCatalog
{
    /// <summary>物种资源组。</summary>
    public CreatureVisualRigAsset[] Rigs = Array.Empty<CreatureVisualRigAsset>();
    /// <summary>器官外观与组合规则。</summary>
    public CreatureVisualLayerAsset[] Layers = Array.Empty<CreatureVisualLayerAsset>();
}

/// <summary>物种外观资源组库。</summary>
public sealed class CreatureVisualRigLibrary : CreatureCompositionAssetLibrary<CreatureVisualRigAsset> { }

/// <summary>器官外观库。</summary>
public sealed class CreatureVisualLayerLibrary : CreatureCompositionAssetLibrary<CreatureVisualLayerAsset> { }
