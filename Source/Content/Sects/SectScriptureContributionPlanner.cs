using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Sects;

public static class SectScriptureContributionPlanner
{
    public static bool TryPickCultibookTarget(
        Actor actor,
        out ScriptureBookDestination target,
        out CultibookAsset cultibook,
        out float mastery)
    {
        target = default;
        cultibook = null;
        mastery = 0f;

        if (!CanWriteBook(actor)) return false;

        var candidates = actor.GetExtend().GetAllMaster<CultibookAsset>().ToList();
        Sect sect = actor.GetExtend().sect;
        if (SectScripturePolicy.CanContribute(actor, sect))
        {
            var sectCandidates = candidates
                .Where(item => SectScripturePolicy.CanAccept(sect, item.Item1))
                .ToList();
            if (sectCandidates.Count > 0)
            {
                var sectCandidate = sectCandidates.GetRandom();
                target = ScriptureBookDestination.ForSect(sect);
                cultibook = sectCandidate.Item1;
                mastery = sectCandidate.Item2;
                SectVerifyLog.Log("PickScriptureTarget", $"type=cultibook target=sect sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} cultibook={cultibook.id} mastery={mastery:F1}");
                return true;
            }
        }

        City city = actor.getCity();
        var cityCandidates = candidates
            .Where(item => city.CanAcceptCultibook(item.Item1))
            .ToList();

        if (cityCandidates.Count == 0) return false;
        var candidate = cityCandidates.GetRandom();

        target = ScriptureBookDestination.ForCity(city);
        cultibook = candidate.Item1;
        mastery = candidate.Item2;
        SectVerifyLog.Log("PickScriptureTarget", $"type=cultibook target=city city={city.name}#{city.data.id} actor={SectVerifyLog.Actor(actor)} cultibook={cultibook.id} mastery={mastery:F1}");
        return true;
    }

    public static bool TryPickElixirRecipeTarget(
        this Actor actor,
        out ScriptureBookDestination target,
        out ElixirAsset elixir,
        out float mastery)
    {
        target = default;
        elixir = null;
        mastery = 0f;

        if (!CanWriteBook(actor)) return false;

        var candidates = actor.GetExtend().GetAllMaster<ElixirAsset>().ToList();
        Sect sect = actor.GetExtend().sect;
        if (SectScripturePolicy.CanContribute(actor, sect))
        {
            var sectCandidates = candidates
                .Where(item => SectScripturePolicy.CanAccept(sect, item.Item1))
                .ToList();
            if (sectCandidates.Count > 0)
            {
                var sectCandidate = sectCandidates.GetRandom();
                target = ScriptureBookDestination.ForSect(sect);
                elixir = sectCandidate.Item1;
                mastery = sectCandidate.Item2;
                SectVerifyLog.Log("PickScriptureTarget", $"type=elixir target=sect sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} elixir={elixir.id} mastery={mastery:F1}");
                return true;
            }
        }

        City city = actor.getCity();
        var cityCandidates = candidates
            .Where(item => city.CanAcceptElixirRecipe(item.Item1))
            .ToList();
        if (cityCandidates.Count == 0) return false;

        var cityCandidate = cityCandidates.GetRandom();
        target = ScriptureBookDestination.ForCity(city);
        elixir = cityCandidate.Item1;
        mastery = cityCandidate.Item2;
        SectVerifyLog.Log("PickScriptureTarget", $"type=elixir target=city city={city.name}#{city.data.id} actor={SectVerifyLog.Actor(actor)} elixir={elixir.id} mastery={mastery:F1}");
        return true;
    }

    public static bool TryPickSkillbookTarget(Actor actor, out ScriptureBookDestination target, out Entity skillContainer)
    {
        target = default;
        skillContainer = default;

        if (!CanWriteBook(actor)) return false;

        var candidates = actor.GetExtend().all_skills.ToList();
        Sect sect = actor.GetExtend().sect;
        if (SectScripturePolicy.CanContribute(actor, sect))
        {
            var sectCandidates = candidates
                .Where(skill => SectScripturePolicy.CanAccept(sect, skill))
                .ToList();
            if (sectCandidates.Count > 0)
            {
                Entity sectCandidate = sectCandidates.GetRandom();
                target = ScriptureBookDestination.ForSect(sect);
                skillContainer = sectCandidate;
                SectVerifyLog.Log("PickScriptureTarget", $"type=skill target=sect sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} skill={skillContainer.Id}");
                return true;
            }
        }

        City city = actor.getCity();
        var cityCandidates = candidates
            .Where(skill => city.CanAcceptSkillbook(skill))
            .ToList();
        if (cityCandidates.Count == 0) return false;

        Entity cityCandidate = cityCandidates.GetRandom();
        target = ScriptureBookDestination.ForCity(city);
        skillContainer = cityCandidate;
        SectVerifyLog.Log("PickScriptureTarget", $"type=skill target=city city={city.name}#{city.data.id} actor={SectVerifyLog.Actor(actor)} skill={skillContainer.Id}");
        return true;
    }

    public static bool CanAcceptCultibook(this City city, CultibookAsset cultibook)
    {
        return city != null
               && !city.isRekt()
               && cultibook != null
               && city.hasBookSlots()
               && !city.HasCultibook(cultibook);
    }

    public static bool CanAcceptElixirRecipe(this City city, ElixirAsset elixir)
    {
        return city != null
               && !city.isRekt()
               && elixir != null
               && city.hasBookSlots()
               && !city.HasElixirRecipe(elixir);
    }

    public static bool CanAcceptSkillbook(this City city, Entity skillContainer)
    {
        return city != null
               && !city.isRekt()
               && !skillContainer.IsNull
               && city.hasBookSlots()
               && !city.HasSkillbook(skillContainer);
    }

    private static bool CanWriteBook(Actor actor)
    {
        return actor != null
               && !actor.isRekt()
               && actor.hasCity()
               && actor.hasLanguage();
    }

    private static bool HasCultibook(this City city, CultibookAsset cultibook)
    {
        foreach (long bookId in city.getBooks())
        {
            Book book = World.world.books.get(bookId);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Cultibook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Cultibook>()) continue;
            CultibookAsset existing = bookExtend.GetComponent<Cultibook>().Asset;
            if (existing != null && existing.id == cultibook.id) return true;
        }

        return false;
    }

    private static bool HasElixirRecipe(this City city, ElixirAsset elixir)
    {
        foreach (long bookId in city.getBooks())
        {
            Book book = World.world.books.get(bookId);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Elixirbook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Elixirbook>()) continue;
            ElixirAsset existing = bookExtend.GetComponent<Elixirbook>().Asset;
            if (existing != null && existing.id == elixir.id) return true;
        }

        return false;
    }

    private static bool HasSkillbook(this City city, Entity skillContainer)
    {
        foreach (long bookId in city.getBooks())
        {
            Book book = World.world.books.get(bookId);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Skillbook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Skillbook>()) continue;
            if (SkillContainerUtils.IsSimilar(bookExtend.GetComponent<Skillbook>().SkillContainer, skillContainer)) return true;
        }

        return false;
    }
}
