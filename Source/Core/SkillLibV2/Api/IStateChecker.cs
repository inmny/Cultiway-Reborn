using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Api;

public interface IStateChecker
{
    public bool IsInverted { get; }
    public bool Satisfy(ref Entity check_target);
}