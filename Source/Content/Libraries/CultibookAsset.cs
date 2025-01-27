using System;
using System.Collections.Generic;
using Cultiway.Content.Components;

namespace Cultiway.Content.Libraries;

public class CultibookAsset : Asset
{
    public List<string> Contributors = new();

    public Cultibook GetEntity()
    {
        throw new NotImplementedException();
    }
}