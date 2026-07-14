using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 标记法器本体已经显化在世界中。位置和朝向分别存放在通用的 Position 与 Rotation 组件中。
/// </summary>
public struct ArtifactManifestation : IComponent
{
    /// <summary>
    /// 当前装备调度状态，供表现和能力判断使用。
    /// </summary>
    public ArtifactControlState control_state;

    /// <summary>
    /// 世界贴图最长边应占据的世界尺寸。
    /// </summary>
    public float world_size;

    /// <summary>
    /// 是否水平翻转世界贴图。
    /// </summary>
    public bool flip_x;

    /// <summary>
    /// 当前法器是否应在正常世界视图中显示。
    /// </summary>
    public bool visible;

    /// <summary>
    /// 在法器渲染层内的排序序号。
    /// </summary>
    public int sorting_order;
}

/// <summary>
/// 法器显化后的空间本体参数，为后续碰撞、选取、拦截和破坏提供统一入口。
/// </summary>
public struct ArtifactBody : IComponent
{
    public float radius;
    public bool targetable;
    public bool collidable;
}

/// <summary>
/// 标记法器的位置正由能力或部署系统控制，默认装备跟随系统不应覆盖其运动。
/// </summary>
public struct ArtifactIndependentMotion : IComponent
{
}
