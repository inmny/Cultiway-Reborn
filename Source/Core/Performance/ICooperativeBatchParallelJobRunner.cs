using System.Collections.Generic;

namespace Cultiway.Core.Performance;

internal interface ICooperativeBatchParallelJobRunner<TBatch, TObject>
    where TBatch : Batch<TObject>, new()
{
    bool TrySkipAllBatches(
        Job<TObject> job,
        int batchCount,
        float elapsed);

    bool TryRunGroup(
        IReadOnlyList<TBatch> batches,
        int jobIndex,
        int[] activeBatchIndices,
        int activeBatchCount,
        float elapsed);

    bool TryRun(
        TBatch batch,
        Job<TObject> job,
        float elapsed);
}
