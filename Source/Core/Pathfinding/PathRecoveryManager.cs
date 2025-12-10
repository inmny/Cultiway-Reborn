using System.Collections.Generic;
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
    }

    private static readonly Dictionary<long, State> States = new();

    public static void Clear()
    {
        lock (States)
        {
            States.Clear();
        }
    }

    public static void OnProgress(Actor actor)
    {
        if (actor?.data == null) return;
        lock (States)
        {
            States.Remove(actor.data.id);
        }
    }

    public static bool OnFailureAndRecover(Actor actor)
    {
        if (actor?.data == null) return false;

        State state;
        lock (States)
        {
            if (!States.TryGetValue(actor.data.id, out state))
            {
                state = new State();
                States.Add(actor.data.id, state);
            }

            state.Failures++;
            var delay = Mathf.Clamp(0.3f * Mathf.Pow(2, state.Failures - 1), 0.3f, 2f);
            state.NextRetryTime = Time.time + delay;
        }

        return TryRequest(actor);
    }

    public static bool TryRequest(Actor actor)
    {
        if (actor == null) return false;
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
}
