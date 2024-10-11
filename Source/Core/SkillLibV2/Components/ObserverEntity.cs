using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Components;

public struct ObserverEntity : IComponent
{
    public List<Entity> LinkedTriggerEntities { get; }
    public int          LinkedCount           { get; internal set; }
}