using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public class BookTypes : ExtendLibrary<BookTypeAsset, BookTypes>
{
    public static BookTypeAsset Cultibook { get; private set; }
    public static BookTypeAsset Skillbook { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        Cultibook.requirement_check = (actor, _) =>
        {
            return actor.GetExtend().HasCultisys<Xian>();
        };
        Cultibook.name_template = WorldboxGame.NameGenerators.Cultibook.id;
        Cultibook.path_icons = "cultibook/";
        Cultibook.GetExtend<BookTypeAssetExtend>().custom_cover_name = "cultibook";
        Skillbook.requirement_check = (_, _) => false;
    }
}