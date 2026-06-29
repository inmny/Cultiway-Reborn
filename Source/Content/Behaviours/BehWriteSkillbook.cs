using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehWriteSkillbook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        if (ae.all_skills.Count == 0) return BehResult.Stop;
        var skill_to_share = ae.all_skills.GetRandom();
        if (!pObject.hasCity() || !pObject.city.hasBookSlots()) return BehResult.Continue;

        var city = pObject.getCity();
        foreach (var book_id in city.getBooks())
        {
            var book = World.world.books.get(book_id);
            var be = book.GetExtend();
            if (be.HasComponent<Skillbook>() && SkillContainerUtils.IsSimilar(be.GetComponent<Skillbook>().SkillContainer, skill_to_share))
            {
                return BehResult.Continue;
            }
        }
        var raw_cultibook = World.world.books.WriteSkillbookBook(pObject, skill_to_share);
        if (raw_cultibook == null)
        {
            return BehResult.Stop;
        }
        World.world.books.TryStoreBookInCity(pObject, raw_cultibook);
        pObject.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}
