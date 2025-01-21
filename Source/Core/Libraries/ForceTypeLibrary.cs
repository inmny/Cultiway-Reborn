namespace Cultiway.Core.Libraries;

public class ForceTypeLibrary : AssetLibrary<ForceTypeAsset>
{
    public static ForceTypeAsset City { get; private set; }
    public override void init()
    {
        base.init();
        City = add(new()
        {
            id = nameof(City)
        });
    }
}