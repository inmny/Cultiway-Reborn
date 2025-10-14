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
        Elixirbook.name_template = WorldboxGame.NameGenerators.Cultibook.id;
        Elixirbook.path_icons = "cultibook/";
        Elixirbook.GetExtend<BookTypeAssetExtend>().custom_cover_name = "cultibook";
        Elixirbook.GetExtend<BookTypeAssetExtend>().instance_read_action = LearnElixirbook;
        
        
        Skillbook.requirement_check = (actor, _) =>
        {
            return actor.GetExtend().HasCultisys<Xian>();
        };
        Skillbook.name_template = WorldboxGame.NameGenerators.Cultibook.id;
        Skillbook.path_icons = "cultibook/";
        Skillbook.GetExtend<BookTypeAssetExtend>().custom_cover_name = "cultibook";
        Skillbook.GetExtend<BookTypeAssetExtend>().instance_read_action = LearnSkillbook;
    }

    private static void LearnSkillbook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;
        var be = book.GetExtend();
        ae.LearnSkillV3(be.GetComponent<Skillbook>().SkillContainer, true);
    }
    private static void LearnElixirbook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;
        var be = book.GetExtend();
        var elixir_asset = be.GetComponent<Elixirbook>().Asset;
        var master = ae.GetMaster(elixir_asset);
        ae.Master(elixir_asset, master + 1);
    }
    private static void LearnCultibook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;
        var be = book.GetExtend();
        var cultibook_asset = be.GetComponent<Cultibook>().Asset;
        var master = ae.GetMaster(cultibook_asset);
        ae.Master(cultibook_asset, master + 1);
    }
}