using Cultiway.Abstract;
using Cultiway.Content.CreatureCompositions.Services;

namespace Cultiway.Content.CreatureCompositions;

/// <summary>登记组合生灵共用身体框架的世界生命周期。</summary>
public sealed class CreatureCompositionModule : ICanInit
{
    public void Init()
    {
        CreaturePhenotypeCompiler.Initialize();
    }
}
