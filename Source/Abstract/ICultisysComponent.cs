using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public interface ICultisysComponent : IComponent
{
    public BaseCultisysAsset Asset     { get; }
    public int               CurrLevel { get; set; }
}