using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public class BookTypes : ExtendLibrary<BookTypeAsset, BookTypes>
{
    public static BookTypeAsset Cultibook { get; private set; }
    public static BookTypeAsset Elixirbook { get; private set; }
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
        Cultibook.GetExtend<BookTypeAssetExtend>().instance_read_action = LearnCultibook;
        Elixirbook.requirement_check = (actor, _) =>
        {
            return false;
        };
        Skillbook.requirement_check = (_, _) => false;
    }

    private static void LearnCultibook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        var be = book.GetExtend();
        var cultibook_asset = be.GetComponent<Cultibook>().Asset;
        if (!ae.HasCultibook())
        {
            ae.Master(cultibook_asset, 1);
        }
        else
        {
            var master = ae.GetMaster(cultibook_asset);
            ae.Master(cultibook_asset, master + 1);
        }
    }
}