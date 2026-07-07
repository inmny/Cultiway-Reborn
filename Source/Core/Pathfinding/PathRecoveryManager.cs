using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Cultiway.Core.Pathfinding;

/// <summary>
/// 路径自动纠错上下文，记录失败次数并按退避策略重新发起寻路。
/// </summary>
internal static class PathRecoveryManager
{
    private sealed class State
    {
        public int Failures;
        public float NextRetryTime;
        public PathFailureReason LastReason;
    }

    private static readonly Dictionary<long, State> States = new();
    private static int _stateCount;

    public static void Clear()
    {
        lock (States)
        {
            States.Clear();
            Volatile.Write(ref _stateCount, 0);
        }
    }

    public static void OnProgress(Actor actor)
    {
        if (actor?.data == null) return;
        if (Volatile.Read(ref _stateCount) == 0) return;
        lock (States)
        {
            if (States.Remove(actor.data.id))
            {
                Volatile.Write(ref _stateCount, States.Count);
            }
        }
    }

    public static bool OnFailureAndRecover(Actor actor, PathFailureReason reason)
    {
        if (actor?.data == null) return false;
        if (!CanRecover(reason))
        {
            Clear(actor);
            return false;
        }

        State state;
        lock (States)
        {
            if (!States.TryGetValue(actor.data.id, out state))
            {
                state = new State();
                States.Add(actor.data.id, state);
                Volatile.Write(ref _stateCount, States.Count);
            }

            if (state.LastReason != reason)
            {
                state.Failures = 0;
                state.LastReason = reason;
            }

            state.Failures++;
            if (state.Failures > MaxRetriesFor(reason))
            {
                States.Remove(actor.data.id);
                Volatile.Write(ref _stateCount, States.Count);
                return false;
            }

            var delay = Mathf.Clamp(0.3f * Mathf.Pow(2, state.Failures - 1), 0.3f, 2f);
            state.NextRetryTime = Time.time + delay;
        }

        return TryRequest(actor);
    }

    public static bool TryRequest(Actor actor)
    {
        if (actor == null) return false;
        if (Volatile.Read(ref _stateCount) == 0) return false;
        State state = null;
        lock (States)
        {
            if (actor.data != null)
            {
                States.TryGetValue(actor.data.id, out state);
            }
        }

        if (state == null)
        {
            return false;
        }

        if (state != null && Time.time < state.NextRetryTime)
        {
            var wait = state.NextRetryTime - Time.time;
            actor.timer_action = Mathf.Max(actor.timer_action, wait);
            actor.setNotMoving();
            return true;
        }

        if (!PathFinder.Instance.TryRequestRecover(actor))
        {
            return false;
        }

        return true;
    }

    public static void Clear(Actor actor)
    {
        if (actor?.data == null) return;
        if (Volatile.Read(ref _stateCount) == 0) return;
        lock (States)
        {
            if (States.Remove(actor.data.id))
            {
                Volatile.Write(ref _stateCount, States.Count);
            }
        }
    }

    private static bool CanRecover(PathFailureReason reason)
    {
        return reason switch
        {
            PathFailureReason.StepBlocked => true,
            PathFailureReason.UnsafeStep => true,
            PathFailureReason.PortalUnavailable => true,
            PathFailureReason.TransportFailed => true,
            PathFailureReason.Timeout => true,
            PathFailureReason.GeneratorException => true,
            _ => false
        };
    }

    private static int MaxRetriesFor(PathFailureReason reason)
    {
        return reason switch
        {
            PathFailureReason.PortalUnavailable => 2,
            PathFailureReason.TransportFailed => 2,
            PathFailureReason.GeneratorException => 1,
            PathFailureReason.Timeout => 2,
            _ => 4
        };
    }
}
