using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>声明小世界实体使用的原版单位视觉资产。</summary>
internal struct SubWorldUnitVisual : IComponent
{
    internal SubWorldUnitVisual(string actorAssetId)
    {
        this.actorAssetId = actorAssetId;
    }

    internal string actorAssetId;
}
