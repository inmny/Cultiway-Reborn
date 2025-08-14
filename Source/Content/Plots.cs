using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content;

public class Plots : ExtendLibrary<PlotAsset, Plots>
{
    [CloneSource(S_Plot.new_book)]
    public static PlotAsset NewCultibook { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        
        NewCultibook.path_icon = "books/custom_book_covers/cultibook/31";
        NewCultibook.check_is_possible = (Actor actor) => actor.hasCity() && actor.hasCulture() && actor.hasLanguage() && actor.city.hasBookSlots() && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.check_should_continue = (Actor actor) => actor.hasCity() && actor.hasCulture() && actor.hasLanguage() && actor.city.hasBookSlots() && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.action = (Actor actor) =>
        {
            if (!actor.hasCity())
            {
                return false;
            }

            if (!actor.city.hasBookSlots())
            {
                return false;
            }

            World.world.books.GenerateNewBook(actor, BookTypes.Cultibook);
            return true;
        };
    }
}