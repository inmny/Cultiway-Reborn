using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cultiway.Core.Performance;

internal interface ICooperativeBatchPostRunner<TBatch, TObject>
    where TBatch : Batch<TObject>, new()
{
    void Start(
        List<TBatch> activeBatches,
        float elapsed,
        ParallelOptions parallelOptions);

    string GetNextPhaseName(string phasePrefix);

    bool Step();

    void Abort();
}
