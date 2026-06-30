using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Extensions;

public readonly struct ScriptureBookTarget
{
    private readonly City _city;
    private readonly Sect _sect;

    private ScriptureBookTarget(City city, Sect sect)
    {
        _city = city;
        _sect = sect;
    }

    public static ScriptureBookTarget ForCity(City city)
    {
        return new ScriptureBookTarget(city, null);
    }

    public static ScriptureBookTarget ForSect(Sect sect)
    {
        return new ScriptureBookTarget(null, sect);
    }

    public bool StoreBook(Actor contributor, Book book)
    {
        if (_sect != null)
        {
            bool result = World.world.books.TryStoreBookInSect(_sect, book, contributor, SectConst.ContributionWriteScriptureBook);
            SectVerifyLog.Log("StoreScriptureBook", $"target=sect sect={SectVerifyLog.Sect(_sect)} contributor={SectVerifyLog.Actor(contributor)} book={SectVerifyLog.Book(book)} result={result}");
            return result;
        }

        if (_city != null)
        {
            bool result = World.world.books.TryStoreBookInCity(_city, contributor, book);
            SectVerifyLog.Log("StoreScriptureBook", $"target=city city={_city.name}#{_city.data.id} contributor={SectVerifyLog.Actor(contributor)} book={SectVerifyLog.Book(book)} result={result}");
            return result;
        }

        return false;
    }
}

public static class ScriptureBookStorageTools
{
    public static bool TryPickCultibookTarget(
        this Actor actor,
        out ScriptureBookTarget target,
        out CultibookAsset cultibook,
        out float mastery)
    {
        target = default;
        cultibook = null;
        mastery = 0f;

        if (!CanWriteBook(actor)) return false;

        var candidates = actor.GetExtend().GetAllMaster<CultibookAsset>().ToList();
        Sect sect = actor.GetExtend().sect;
        if (actor.CanContributeSectScripture(sect))
        {
            var sectCandidates = candidates
                .Where(item => sect.CanAcceptCultibook(item.Item1))
                .ToList();
            if (sectCandidates.Count > 0)
            {
                var sectCandidate = sectCandidates.GetRandom();
                target = ScriptureBookTarget.ForSect(sect);
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

        target = ScriptureBookTarget.ForCity(city);
        cultibook = candidate.Item1;
        mastery = candidate.Item2;
        SectVerifyLog.Log("PickScriptureTarget", $"type=cultibook target=city city={city.name}#{city.data.id} actor={SectVerifyLog.Actor(actor)} cultibook={cultibook.id} mastery={mastery:F1}");
        return true;
    }

    public static bool TryPickElixirRecipeTarget(
        this Actor actor,
        out ScriptureBookTarget target,
        out ElixirAsset elixir,
        out float mastery)
    {
        target = default;
        elixir = null;
        mastery = 0f;

        if (!CanWriteBook(actor)) return false;

        var candidates = actor.GetExtend().GetAllMaster<ElixirAsset>().ToList();
        Sect sect = actor.GetExtend().sect;
        if (actor.CanContributeSectScripture(sect))
        {
            var sectCandidates = candidates
                .Where(item => sect.CanAcceptElixirRecipe(item.Item1))
                .ToList();
            if (sectCandidates.Count > 0)
            {
                var sectCandidate = sectCandidates.GetRandom();
                target = ScriptureBookTarget.ForSect(sect);
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
        target = ScriptureBookTarget.ForCity(city);
        elixir = cityCandidate.Item1;
        mastery = cityCandidate.Item2;
        SectVerifyLog.Log("PickScriptureTarget", $"type=elixir target=city city={city.name}#{city.data.id} actor={SectVerifyLog.Actor(actor)} elixir={elixir.id} mastery={mastery:F1}");
        return true;
    }

    public static bool TryPickSkillbookTarget(this Actor actor, out ScriptureBookTarget target, out Entity skillContainer)
    {
        target = default;
        skillContainer = default;

        if (!CanWriteBook(actor)) return false;

        var candidates = actor.GetExtend().all_skills.ToList();
        Sect sect = actor.GetExtend().sect;
        if (actor.CanContributeSectScripture(sect))
        {
            var sectCandidates = candidates
                .Where(skill => sect.CanAcceptSkillbook(skill))
                .ToList();
            if (sectCandidates.Count > 0)
            {
                Entity sectCandidate = sectCandidates.GetRandom();
                target = ScriptureBookTarget.ForSect(sect);
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
        target = ScriptureBookTarget.ForCity(city);
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

    public static bool CanAcceptCultibook(this Sect sect, CultibookAsset cultibook)
    {
        return sect != null
               && !sect.isRekt()
               && cultibook != null
               && !sect.HasScriptureCultibook(cultibook);
    }

    public static bool CanAcceptElixirRecipe(this Sect sect, ElixirAsset elixir)
    {
        return sect != null
               && !sect.isRekt()
               && elixir != null
               && !sect.HasScriptureElixirRecipe(elixir);
    }

    public static bool CanAcceptSkillbook(this Sect sect, Entity skillContainer)
    {
        return sect != null
               && !sect.isRekt()
               && !skillContainer.IsNull
               && !sect.HasScriptureSkillbook(skillContainer);
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
