namespace Cultiway.Utils.Extension;

public static class BookManagerTools
{
    public static Book GenerateNewBookForCity(this BookManager manager, Actor actor, BookTypeAsset book_type)
    {
        if (actor == null || actor.isRekt()) return null;
        if (!actor.hasCity()) return null;
        if (actor.getCity().getBuildingWithBookSlot() == null) return null;

        Book book = manager.NewBook(actor, book_type);
        if (book == null)
        {
            return null;
        }

        return manager.TryStoreBookInCity(actor, book) ? book : null;
    }

    public static bool TryStoreBookInCity(this BookManager manager, Actor actor, Book book)
    {
        if (actor == null || actor.isRekt()) return false;
        if (!actor.hasCity()) return false;

        return manager.TryStoreBookInCity(actor.getCity(), actor, book);
    }

    public static bool TryStoreBookInCity(this BookManager manager, City city, Actor actor, Book book)
    {
        if (city == null || city.isRekt()) return false;
        if (actor == null || actor.isRekt()) return false;
        if (book == null || book.isRekt()) return false;

        Building building = city.getBuildingWithBookSlot();
        if (building == null) return false;

        World.world.game_stats.data.booksWritten += 1L;
        World.world.map_stats.booksWritten += 1L;
        actor.changeHappiness("wrote_book", 0);
        building.addBook(book);
        city.setStatusDirty();
        return true;
    }

    public static Book NewBook(this BookManager manager, Actor actor, BookTypeAsset book_type)
    {
        if (actor == null || actor.isRekt()) return null;
        if (book_type == null) return null;
        if (!actor.hasLanguage() || !actor.hasCulture()) return null;

        Book book = manager.newObject();
        ActorTrait t_trait_actor = manager.getBookTrait(actor);
        LanguageTrait t_trait_language = actor.language?.getTraitForBook();
        ReligionTrait t_trait_religion = actor.religion?.getTraitForBook();
        CultureTrait t_trait_culture = actor.culture?.getTraitForBook();
        book.newBook(actor, book_type, t_trait_actor, t_trait_culture, t_trait_language, t_trait_religion);
        return book;
    }
}
