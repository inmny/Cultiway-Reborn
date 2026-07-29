using System;
using System.Globalization;
using System.Threading;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 跳过没有活动对象且没有自定义生成逻辑的原版特效控制器。
/// BaseEffectController.spawn 为空实现，因此这类控制器的空转 update
/// 只会推进一个不可观察的内部计时器。
/// </summary>
internal static class ActiveStackEffectsUpdater
{
    private static long updatePasses;
    private static long controllersUpdated;
    private static long inactiveControllersSkipped;
    private static long initializationFallbacks;

    internal static void Update(
        StackEffects effects,
        float elapsed)
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler)
        {
            effects.update(elapsed);
            return;
        }

        if (AssetManager.effects_library.list.Count >
            effects.list.Count)
        {
            effects.update(elapsed);
            Interlocked.Increment(
                ref initializationFallbacks);
            return;
        }

        Bench.bench(
            "stack_effects",
            "game_total");
        int updated = 0;
        int skipped = 0;
        try
        {
            for (int i = 0;
                 i < effects.list.Count;
                 i++)
            {
                BaseEffectController controller =
                    effects.list[i];
                if (controller.getActiveIndex() == 0 &&
                    controller.GetType() ==
                    typeof(BaseEffectController))
                {
                    skipped++;
                    continue;
                }

                controller.update(elapsed);
                updated++;
            }
        }
        finally
        {
            Bench.benchEnd(
                "stack_effects",
                "game_total",
                pSaveCounter: false,
                0L);
        }

        if (Bench.bench_enabled)
        {
            Interlocked.Increment(
                ref updatePasses);
            Interlocked.Add(
                ref controllersUpdated,
                updated);
            Interlocked.Add(
                ref inactiveControllersSkipped,
                skipped);
        }
    }

    internal static string GetDiagnostics()
    {
        long passes =
            Interlocked.Read(ref updatePasses);
        long updated =
            Interlocked.Read(ref controllersUpdated);
        long skipped =
            Interlocked.Read(
                ref inactiveControllersSkipped);
        long total = updated + skipped;
        return string.Format(
            CultureInfo.InvariantCulture,
            "passes={0} updated={1:0.0}(avg) " +
            "skipped={2:0.0}(avg)/{3:0.0}% " +
            "fallbacks={4}",
            passes,
            passes == 0L
                ? 0.0
                : updated / (double)passes,
            passes == 0L
                ? 0.0
                : skipped / (double)passes,
            total == 0L
                ? 0.0
                : skipped * 100.0 / total,
            Interlocked.Read(
                ref initializationFallbacks));
    }
}
