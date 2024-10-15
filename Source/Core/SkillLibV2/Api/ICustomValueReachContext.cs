using System;

namespace Cultiway.Core.SkillLibV2.Api;

public interface ICustomValueReachContext<TValue> : IEventContext where TValue : IComparable<TValue>
{
    public TValue Value { get; }
}