using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 更新法术掠过后留在地面的短时粒子发射源。
/// </summary>
public class RenderSkillFlyOverParticleSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        SkillFlyOverParticleEmitter.Update(Tick.deltaTime);
    }
}
