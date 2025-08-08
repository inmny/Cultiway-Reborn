using Cultiway.Abstract;
using strings;

namespace Cultiway.Content;

public class CitizenJobs : ExtendLibrary<CitizenJobAsset, CitizenJobs>
{
    [CloneSource(nameof(S_ActorJob.gatherer_herbs))]
    public static CitizenJobAsset HerbCollector { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();
    }

    protected override void PostInit(CitizenJobAsset asset)
    {
        var library = (CitizenJobLibrary)cached_library;
        if (asset.common_job)
        {
            if (asset.priority_no_food > 0) library.list_priority_high_food.Add(asset);

            if (asset.priority > 0)
                library.list_priority_high.Add(asset);
            else
                library.list_priority_normal.Add(asset);

            asset.unit_job_default = asset.id;
        }
    }
}