using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content;

public class Jindans : ExtendLibrary<JindanAsset, Jindans>
{
    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Jindan");
    }
}