using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

public class CultibookAsset : Asset
{
    public Entity CultibookEntity;
    public CultibookBaseAsset BaseAsset;
    public BaseStats FinalStats = new();
    public List<string> Contributors = new();
}