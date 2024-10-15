using System;

namespace Cultiway.Core.SkillLibV2.Api;

public enum CompareResult
{
    LessThanTarget,
    EqualToTarget,
    GreaterThanTarget
}

public interface ICustomValueReachTrigger<TTrigger, TContext, TValue> : IEventTrigger<TTrigger, TContext>
    where TValue : IComparable<TValue>
    where TContext : struct, ICustomValueReachContext<TValue>
    where TTrigger : struct, ICustomValueReachTrigger<TTrigger, TContext, TValue>
{
    public CompareResult ExpectedResult { get; }
    public TValue        TargetValue    { get; }
}