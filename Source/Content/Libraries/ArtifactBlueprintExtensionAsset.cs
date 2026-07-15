using System;
using Friflo.Engine.ECS;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 为专属法宝在百宝阁蓝图中保存额外组件数据。
/// 基础法宝组件由蓝图编解码器统一处理，扩展只负责自己拥有的数据。
/// </summary>
public sealed class ArtifactBlueprintExtensionAsset : Asset
{
    /// <summary>
    /// 多个扩展恢复时的稳定顺序。
    /// </summary>
    public int order;

    /// <summary>
    /// 判断指定法宝是否需要写入该扩展。
    /// </summary>
    public Func<Entity, bool> CanCapture;

    /// <summary>
    /// 从法宝本体提取可持久化数据。
    /// </summary>
    public Func<Entity, JToken> Capture;

    /// <summary>
    /// 将扩展数据恢复到新制造的法宝实体。该回调只应附加组件，不应改变库存或世界状态。
    /// </summary>
    public Action<Entity, JToken> Restore;

    /// <summary>
    /// 校验已经持久化的数据；返回空字符串表示有效。
    /// </summary>
    public Func<JToken, string> Validate;
}
