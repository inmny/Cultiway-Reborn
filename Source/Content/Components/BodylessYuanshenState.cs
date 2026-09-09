using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>原人物失去肉身后，以元神本体继续存在的运行状态。</summary>
public struct BodylessYuanshenState : IComponent
{
    /// <summary>元神失去肉身的世界时间；本相肉身保存在元神成果中。</summary>
    public double body_lost_at;
}
