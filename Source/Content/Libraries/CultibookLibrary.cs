using System;
using Cultiway.Abstract;

namespace Cultiway.Content.Libraries;

public class CultibookLibrary : DynamicAssetLibrary<CultibookAsset>
{
    public CultibookAsset NewCopy(CultibookAsset asset, string contributor)
    {
        throw new NotImplementedException();
    }
}