using System;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>法器内部一个可持久化的命名资源通道。</summary>
public struct ArtifactStoredResource
{
    public string key;
    public float amount;
    public float capacity;
}

/// <summary>
/// 法器通用储藏状态。资源含义由能力约定，组件本身不区分灵力、魂魄或物质。
/// </summary>
public struct ArtifactStorageState : IComponent
{
    public ArtifactStoredResource[] resources = [];

    public ArtifactStorageState()
    {
    }
}
