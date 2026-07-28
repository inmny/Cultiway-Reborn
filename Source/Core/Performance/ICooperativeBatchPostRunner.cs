using System.Collections.Generic;

namespace Cultiway.Core.Performance;

internal interface ICooperativeBatchPostRunner<TBatch, TObject>
    where TBatch : Batch<TObject>, new()
{
    void Start(
        List<TBatch> activeBatches,
        float elapsed);

    string GetNextPhaseName(string phasePrefix);

    bool WaitingForBackgroundWork { get; }

    bool IsBackgroundWorkCompleted { get; }

    bool TryJoinBackgroundWork(double maximumMilliseconds);

    void WaitForBackgroundWork();

    bool Step();

    void Abort();
}
