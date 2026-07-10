using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Extensions;

public static class BookManagerTools
{
    private static readonly CultibookLibrary CultibookLibrary = Libraries.Manager.CultibookLibrary;

    public static Book WriteCultibookBook(this BookManager manager, Actor creator, CultibookAsset cultibook,
        float mastery)
    {
        var book = manager.NewBook(creator, BookTypes.Cultibook);
        if (book == null) return null;

        var bookExtend = book.GetExtend();
        bookExtend.AddComponent(new Cultibook(cultibook.id));
        bookExtend.AddComponent(cultibook.Level);
        bookExtend.Master(cultibook, mastery);
        book.data.name = cultibook.Name;
        SectVerifyLog.Log("WriteScriptureBook",
            $"type=cultibook creator={SectVerifyLog.Actor(creator)} book={SectVerifyLog.Book(book)} cultibook={cultibook.id} mastery={mastery:F1}");
        return book;
    }

    public static Book WriteElixirRecipeBook(this BookManager manager, Actor creator, ElixirAsset elixir,
        float mastery)
    {
        var book = manager.NewBook(creator, BookTypes.Elixirbook);
        if (book == null) return null;

        var bookExtend = book.GetExtend();
        bookExtend.AddComponent(new Elixirbook(elixir.id));
        bookExtend.Master(elixir, mastery);
        book.data.name = elixir.GetName() + "丹方";
        SectVerifyLog.Log("WriteScriptureBook",
            $"type=elixir creator={SectVerifyLog.Actor(creator)} book={SectVerifyLog.Book(book)} elixir={elixir.id} mastery={mastery:F1}");
        return book;
    }

    public static Book WriteSkillbookBook(this BookManager manager, Actor creator, Entity skillContainer)
    {
        var book = manager.NewBook(creator, BookTypes.Skillbook);
        if (book == null) return null;

        var bookExtend = book.GetExtend();
        var clonedSkillContainer = skillContainer.Store.CloneEntity(skillContainer);
        bookExtend.AddComponent(new Skillbook
        {
            SkillContainer = clonedSkillContainer
        });
        bookExtend.E.AddRelation(new SkillMasterRelation
        {
            SkillContainer = clonedSkillContainer
        });
        SectVerifyLog.Log("WriteScriptureBook",
            $"type=skill creator={SectVerifyLog.Actor(creator)} book={SectVerifyLog.Book(book)} skill={clonedSkillContainer.Id}");
        return book;
    }

    public static Book CreateNewSkillbook(this BookManager manager, Actor creator, Entity skillContainer)
    {
        return manager.WriteSkillbookBook(creator, skillContainer);
    }

    public static Book CreateCultibookFromDraft(this BookManager manager, Actor creator, CultibookAsset draftAsset)
    {
        var actorExtend = creator.GetExtend();
        draftAsset = CultibookRuleComposer.NormalizeDraft(draftAsset, actorExtend);
        var rawCultibook = manager.NewBook(creator, BookTypes.Cultibook);
        if (rawCultibook == null)
        {
            foreach (var entry in draftAsset.SkillPool)
            {
                if (entry?.SkillContainer.IsNull == false) entry.SkillContainer.RemoveTag<TagOccupied>();
            }
            return null;
        }

        var bookExtend = rawCultibook.GetExtend();
        var cultibook = CultibookLibrary.AddDynamic(draftAsset);
        bookExtend.AddComponent(new Cultibook(cultibook.id));
        bookExtend.AddComponent(cultibook.Level);
        bookExtend.Master(cultibook, 100);
        actorExtend.Master(cultibook, 100);
        rawCultibook.data.name = cultibook.Name;
        SectVerifyLog.Log("CreateCultibookDraft",
            $"creator={SectVerifyLog.Actor(creator)} book={SectVerifyLog.Book(rawCultibook)} cultibook={cultibook.id}");
        return rawCultibook;
    }

    public static Book CreateNewCultibook(this BookManager manager, Actor creator)
    {
        var actorExtend = creator.GetExtend();
        var draft = CultibookRuleComposer.CreateDraft(actorExtend);
        return manager.CreateCultibookFromDraft(creator, draft);
    }

    public static CultibookAsset CreateImprovedCultibook(CultibookAsset originalCultibook, ActorExtend creator)
    {
        return CultibookRuleComposer.CreateImprovedDraft(originalCultibook, creator);
    }
}
