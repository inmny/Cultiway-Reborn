using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public sealed class LogicSkillVfxFlushSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        ModClass.I.SkillV3.Vfx.Flush();
    }
}
