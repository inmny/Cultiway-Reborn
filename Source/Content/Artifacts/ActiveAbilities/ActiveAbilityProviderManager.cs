using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Content.Artifacts.ActiveAbilities;

/// <summary>
/// 在 Content 资产完成初始化后，将具体能力来源注册到 Core 的统一主动能力服务。
/// </summary>
[Dependency(typeof(ArtifactAbilities))]
internal sealed class ActiveAbilityProviderManager : ICanInit
{
    public void Init()
    {
        ActiveAbilityService.Register(new ArtifactActiveAbilityProvider());
        ActiveAbilityService.Register(new TalismanActiveAbilityProvider());
        ActiveAbilityService.Register(new MagicScrollActiveAbilityProvider());
    }
}
