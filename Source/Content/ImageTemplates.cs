using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class ImageTemplates : ExtendLibrary<ImageTemplateAsset, ImageTemplates>
{
    protected override void OnInit()
    {
        RegisterAssets();
    }
}