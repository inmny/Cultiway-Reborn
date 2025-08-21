using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content;
[Dependency(typeof(PlotCategories))]
public class Plots : ExtendLibrary<PlotAsset, Plots>
{
    [CloneSource(S_Plot.new_book)]
    public static PlotAsset NewCultibook { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset NewSect { get; private set; }
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

            var book = World.world.books.CreateNewCultibook(actor);
            return book != null;
        };


        NewSect.path_icon = "books/custom_book_covers/cultibook/31";
        NewSect.group_id = PlotCategories.Sect.id;
        NewSect.check_is_possible = a => a.GetExtend().HasCultibook() && !a.HasSect();
        NewSect.check_should_continue = a => a.GetExtend().HasCultibook() && !a.HasSect();
        NewSect.action = a =>
        {
            if (!a.GetExtend().HasCultibook()) return false;
            if (a.HasSect()) return false;

            a.GetExtend().SetSect(WorldboxGame.I.Sects.BuildSect(a));
            return true;
        };
    }
}