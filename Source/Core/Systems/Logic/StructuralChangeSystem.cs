using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class StructuralChangeSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        ModClass.I.CommandBuffer.Playback();
    }
}