using System;

namespace Cultiway.Core.ControlledTasks;

internal static class ControlledTaskHandoffScope
{
    private static Actor actor;
    private static bool active;

    internal static IDisposable Enter(Actor target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (active) throw new InvalidOperationException("A controlled task handoff is already active.");
        actor = target;
        active = true;
        return new Scope();
    }

    internal static bool SuppressesReleaseEffect(Actor candidate)
    {
        return active && ReferenceEquals(actor, candidate);
    }

    private sealed class Scope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            actor = null;
            active = false;
        }
    }
}

internal static class ControlledTaskCancellationScope
{
    private static long actorId;
    private static bool active;

    internal static IDisposable Enter(long targetActorId)
    {
        if (active) throw new InvalidOperationException("A controlled task cancellation is already active.");
        actorId = targetActorId;
        active = true;
        return new Scope();
    }

    internal static bool Contains(long candidateActorId)
    {
        return active && actorId == candidateActorId;
    }

    private sealed class Scope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            actorId = 0;
            active = false;
        }
    }
}
