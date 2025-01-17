using System.Collections.Generic;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public interface IHasForce
{
    public bool HasRelatedForce<TRelation>() where TRelation : struct, IForceRelation;
    public IEnumerable<Entity> GetForces<TRelation>() where TRelation : struct, IForceRelation;
    public void JoinForce<TRelation>(Entity force)  where TRelation : struct, IForceRelation;
}