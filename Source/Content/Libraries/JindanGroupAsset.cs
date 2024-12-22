using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;

namespace Cultiway.Content.Libraries;

public delegate bool JindanGroupCheck(ActorExtend ae, ref XianBase xian_base);

public class JindanGroupAsset : Asset
{
    public JindanGroupCheck  check;
    public List<JindanAsset> jindans = new();
    public int               prior;
}