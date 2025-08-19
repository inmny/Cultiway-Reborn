using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

public static class BookManagerTools
{
    public static Book CreateNewCultibook(this BookManager manager, Actor creator)
    {
        var ae = creator.GetExtend();
        var raw_cultibook = manager.GenerateNewBook(creator, BookTypes.Cultibook);
        if (raw_cultibook == null)
        {
            return null;
        }
        var be = raw_cultibook.GetExtend();
        var stats = new BaseStats();
        foreach (var stat in WorldboxGame.BaseStats.ArmorStats)
        {
            stats[stat] = creator.stats[stat];
        }

        foreach (var stat in WorldboxGame.BaseStats.MasterStats)
        {
            stats[stat] = creator.stats[stat];
        }
        be.AddComponent(new Cultibook()
        {
            FinalStats = stats
        });
        be.AddComponent(new ItemLevel()
        {
            
        });
        ae.SetCultibookMasterRelation(be.E, 100);
        return raw_cultibook;
    }
}