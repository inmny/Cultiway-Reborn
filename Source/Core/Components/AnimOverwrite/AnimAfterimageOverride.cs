using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components.AnimOverwrite;

/// <summary>覆盖运动配置解析出的残影，用于同一技能内随动作切换残影几何。</summary>
public struct AnimAfterimageOverride : IComponent
{
    /// <summary>本次执行体采用的完整残影参数。</summary>
    public AnimAfterimage Value;
}
