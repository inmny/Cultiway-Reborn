namespace Cultiway.Utils.Extension;

public static class BookManagerTools
{
    public static Book GenerateNewBook(this BookManager manager, Actor actor, BookTypeAsset book_type)
    {
        City city = actor.getCity();
        Building building = city.getBuildingWithBookSlot();
        if (building == null)
        {
            return null;
        }

        Book book = manager.NewBook(actor, book_type);
        if (book == null)
        {
            return null;
        }

        World.world.game_stats.data.booksWritten += 1L;
        World.world.map_stats.booksWritten += 1L;
        actor.changeHappiness("wrote_book", 0);
        building.addBook(book);
        city.setStatusDirty();
        return book;
    }

    public static Book NewBook(this BookManager manager, Actor actor, BookTypeAsset book_type)
    {
        Book book = manager.newObject();
        ActorTrait t_trait_actor = manager.getBookTrait(actor);
        LanguageTrait t_trait_language = actor.language?.getTraitForBook();
        ReligionTrait t_trait_religion = actor.religion?.getTraitForBook();
        CultureTrait t_trait_culture = actor.culture?.getTraitForBook();
        book.newBook(actor, book_type, t_trait_actor, t_trait_culture, t_trait_language, t_trait_religion);
        return book;
    }
}