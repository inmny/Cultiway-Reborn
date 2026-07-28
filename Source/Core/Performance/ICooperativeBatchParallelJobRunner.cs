namespace Cultiway.Core.Performance;

internal interface ICooperativeBatchParallelJobRunner<TBatch, TObject>
    where TBatch : Batch<TObject>, new()
{
    bool TryRun(
        TBatch batch,
        Job<TObject> job,
        float elapsed);
}
