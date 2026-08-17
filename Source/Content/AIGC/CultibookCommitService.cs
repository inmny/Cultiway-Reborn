using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.AIGC;

internal readonly struct CultibookCommitResult
{
    public bool Success { get; }
    public string ReasonLocaleKey { get; }
    public CultibookAsset Asset { get; }

    private CultibookCommitResult(bool success, string reasonLocaleKey, CultibookAsset asset)
    {
        Success = success;
        ReasonLocaleKey = reasonLocaleKey ?? string.Empty;
        Asset = asset;
    }

    internal static CultibookCommitResult Succeeded(CultibookAsset asset) =>
        new(true, string.Empty, asset);

    internal static CultibookCommitResult Failed(string reasonLocaleKey) =>
        new(false, reasonLocaleKey, null);
}

/// <summary>在主线程中验证、注册、入库并切换主修；任一步失败都会回滚临时资产。</summary>
internal static class CultibookCommitService
{
    internal static CultibookCommitResult TryCommit(CultibookRequestRecord request,
        CultibookDraftDto draftDto, bool useFallback)
    {
        if (!CultibookRequestService.IsCurrentOwner(request))
            return CultibookCommitResult.Failed(
                "Cultiway.ControlledTask.Reason.CultibookRequestCancelled");

        Actor actor = World.world?.units?.get(request.ActorId);
        if (actor == null || actor.isRekt() || !actor.isAlive())
            return CultibookCommitResult.Failed("Cultiway.ControlledTask.Reason.ActorLost");
        if (!actor.hasHouse() || !actor.hasCity() || !actor.hasLanguage() || !actor.hasCulture())
            return CultibookCommitResult.Failed("Cultiway.ControlledTask.Reason.RequiresWritingPlace");
        City city = actor.getCity();
        Building destination = city?.getBuildingWithBookSlot();
        if (destination == null)
            return CultibookCommitResult.Failed("Cultiway.ControlledTask.Reason.BookDestinationUnavailable");

        ActorExtend extend = actor.GetExtend();
        if (!CapturedSkillsRemainOwned(request, extend))
            return CultibookCommitResult.Failed(
                "Cultiway.ControlledTask.Reason.CultibookSkillSourceChanged");

        CultibookAsset original = null;
        if (request.Kind == CultibookRequestKind.Create)
        {
            if (extend.GetMainCultibook() != null)
                return CultibookCommitResult.Failed(
                    "Cultiway.ControlledTask.Reason.AlreadyHasMainCultibook");
        }
        else
        {
            original = extend.GetMainCultibook();
            if (original == null || original.id != request.OriginalCultibookId ||
                extend.GetMainCultibookMastery() < 100f)
                return CultibookCommitResult.Failed(
                    "Cultiway.ControlledTask.Reason.MainCultibookChanged");
            if (original.Level.Stage >= 3 && original.Level.Level >= 8)
                return CultibookCommitResult.Failed(
                    "Cultiway.ControlledTask.Reason.CultibookAtMaximum");

            float intelligence = extend.GetStat(S.intelligence);
            float successRate = Mathf.Clamp(0.5f + intelligence / 10f * 0.01f, 0.5f, 0.9f);
            if (!Randy.randomChance(successRate))
                return CultibookCommitResult.Failed(
                    "Cultiway.ControlledTask.Reason.CultibookImprovementFailed");
        }

        CultibookAsset draft = null;
        CultibookAsset registered = null;
        Book book = null;
        bool stored = false;
        bool actorMastered = false;
        bool hadState = extend.TryGetComponent(out ActorCultibookState previousState);
        try
        {
            if (useFallback || draftDto == null)
            {
                draft = request.Kind == CultibookRequestKind.Create
                    ? CultibookRuleComposer.CreateDraft(extend)
                    : CultibookRuleComposer.CreateImprovedDraft(original, extend);
            }
            else
            {
                draft = BuildDraft(extend, original, draftDto, request.SkillHandles,
                    out string draftReasonLocaleKey);
                if (draft == null) return CultibookCommitResult.Failed(draftReasonLocaleKey);
                draft = CultibookRuleComposer.NormalizeDraft(draft, extend, original);
            }
            if (draft == null)
                return CultibookCommitResult.Failed(
                    "Cultiway.ControlledTask.Reason.CultibookDraftInvalid");

            book = World.world.books.NewBook(actor, BookTypes.Cultibook);
            if (book == null)
                throw new InvalidOperationException("Cultibook book creation returned null.");

            registered = Libraries.Manager.CultibookLibrary.AddDynamic(draft);
            if (registered == null)
                throw new InvalidOperationException("Dynamic cultibook registration returned null.");

            BookExtend bookExtend = book.GetExtend();
            bookExtend.AddComponent(new Cultibook(registered.id));
            bookExtend.AddComponent(registered.Level);
            book.data.name = registered.Name;

            stored = World.world.books.TryStoreBookInCity(city, actor, book);
            if (!stored)
                throw new InvalidOperationException("Cultibook destination rejected the book.");

            extend.Master(registered, 100f);
            actorMastered = true;
            extend.SetMainCultibook(registered);
            extend.AddMainCultibookMastery(100f);
            bookExtend.Master(registered, 100f);
            return CultibookCommitResult.Succeeded(registered);
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[CultibookCommit] actor={actor.getID()} kind={request.Kind}: {exception}");
            if (actorMastered && registered != null) extend.DeMaster(registered);
            RestoreActorState(extend, hadState, previousState);
            if (stored) RemoveStoredBook(city, destination, book);
            if (book != null && !book.isRekt()) World.world.books.DisposeUncommittedBook(book);
            if (registered != null)
                Libraries.Manager.CultibookLibrary.RemoveAll(new[] { registered.id });
            else
                DeleteDraftSkills(draft);
            return CultibookCommitResult.Failed(
                "Cultiway.ControlledTask.Reason.CultibookCommitFailed");
        }
    }

    private static bool CapturedSkillsRemainOwned(CultibookRequestRecord request, ActorExtend extend)
    {
        if (request?.SkillHandles == null) return true;
        foreach (Entity skill in request.SkillHandles.Values)
            if (skill.IsNull || !extend.OwnsLearnedSkill(skill)) return false;
        return true;
    }

    private static CultibookAsset BuildDraft(ActorExtend extend, CultibookAsset original,
        CultibookDraftDto dto, IReadOnlyDictionary<int, Entity> capturedSkills,
        out string reasonLocaleKey)
    {
        reasonLocaleKey = string.Empty;
        if (dto == null)
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.CultibookDraftInvalid";
            return null;
        }

        Dictionary<int, Entity> actorSkills = (capturedSkills ??
                new Dictionary<int, Entity>())
            .Where(pair => pair.Value.Id == pair.Key && !pair.Value.IsNull &&
                           pair.Value.HasComponent<SkillContainer>() &&
                           extend.OwnsLearnedSkill(pair.Value) &&
                           !string.IsNullOrEmpty(
                               pair.Value.GetComponent<SkillContainer>().SkillEntityAssetID))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var skillPool = new List<SkillPoolEntry>();
        var selected = new HashSet<int>();
        try
        {
            foreach (CultibookSkillDraftDto entry in dto.SkillPool ?? new List<CultibookSkillDraftDto>())
            {
                if (entry == null || entry.EntityId <= 0 || !selected.Add(entry.EntityId)) continue;
                if (!actorSkills.TryGetValue(entry.EntityId, out Entity source))
                {
                    reasonLocaleKey = "Cultiway.ControlledTask.Reason.CultibookSkillSourceChanged";
                    DeleteSkillPool(skillPool);
                    return null;
                }
                Entity clone = source.Store.CloneEntity(source);
                clone.AddTag<TagOccupied>();
                skillPool.Add(new SkillPoolEntry
                {
                    SkillContainer = clone,
                    BaseChance = entry.BaseChance,
                    MasteryThreshold = entry.MasteryThreshold,
                    LevelRequirement = entry.LevelRequirement,
                });
            }

            return new CultibookAsset
            {
                id = Guid.NewGuid().ToString("N"),
                Name = dto.Name,
                Description = dto.Description,
                ElementReq = dto.ElementRequirement,
                ElementAffinityThreshold = dto.ElementAffinityThreshold,
                MinLevel = dto.MinLevel,
                MaxLevel = dto.MaxLevel,
                CultivateMethodId = dto.CultivateMethodId,
                SkillPool = skillPool,
                ConflictConditions = original?.ConflictConditions?.ToArray() ??
                                     Array.Empty<Core.Semantics.SemanticQueryExpression>(),
                SynergyConditions = original?.SynergyConditions?.ToArray() ??
                                    Array.Empty<Core.Semantics.SemanticQueryExpression>(),
            };
        }
        catch
        {
            DeleteSkillPool(skillPool);
            throw;
        }
    }

    private static void RestoreActorState(ActorExtend extend, bool hadState,
        ActorCultibookState previousState)
    {
        if (hadState)
        {
            if (!extend.HasComponent<ActorCultibookState>()) extend.AddComponent(previousState);
            else extend.GetComponent<ActorCultibookState>() = previousState;
            CultibookAsset previous = string.IsNullOrEmpty(previousState.MainCultibookId)
                ? null
                : Libraries.Manager.CultibookLibrary.get(previousState.MainCultibookId);
            if (previous?.GetCultivateMethod()?.Handles(CultivationTriggerKind.TimedTick) == true)
                extend.E.AddTag<TimedCultivationTag>();
            else
                extend.E.RemoveTag<TimedCultivationTag>();
        }
        else
        {
            if (extend.HasComponent<ActorCultibookState>())
                extend.E.RemoveComponent<ActorCultibookState>();
            extend.E.RemoveTag<TimedCultivationTag>();
        }
        extend.MarkCultiwayStatsDirty();
    }

    private static void RemoveStoredBook(City city, Building destination, Book book)
    {
        if (book == null) return;
        destination?.data?.books?.list_books?.Remove(book.id);
        if (World.world.game_stats.data.booksWritten > 0) World.world.game_stats.data.booksWritten--;
        if (World.world.map_stats.booksWritten > 0) World.world.map_stats.booksWritten--;
        city?.setStatusDirty();
    }

    private static void DeleteDraftSkills(CultibookAsset draft)
    {
        if (draft?.SkillPool == null) return;
        DeleteSkillPool(draft.SkillPool);
    }

    private static void DeleteSkillPool(IEnumerable<SkillPoolEntry> skillPool)
    {
        foreach (SkillPoolEntry entry in skillPool)
            if (entry?.SkillContainer.IsNull == false) entry.SkillContainer.DeleteEntity();
    }
}
