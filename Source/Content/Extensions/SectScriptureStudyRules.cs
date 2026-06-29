using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class SectScriptureStudyRules
{
    public static bool CanStudySectScripture(Actor actor)
    {
        return TryPickStudyBook(actor, out _);
    }

    public static bool TryPickStudyBook(Actor actor, out Book book)
    {
        book = null;

        if (!CanStartStudy(actor)) return false;

        ActorExtend ae = actor.GetExtend();
        Sect sect = ae.sect;
        var candidates = new List<StudyCandidate>();
        IReadOnlyList<long> bookIds = sect.GetScriptureBookIds();
        for (int i = 0; i < bookIds.Count; i++)
        {
            Book candidate = World.world.books.get(bookIds[i]);
            if (!CanReadBook(actor, candidate)) continue;

            float score = GetStudyScore(actor, ae, sect, candidate);
            if (score <= 0f) continue;

            candidates.Add(new StudyCandidate(candidate, score));
        }

        if (candidates.Count == 0) return false;

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        book = PickWeighted(candidates);
        return book != null;
    }

    private static bool CanStartStudy(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        if (!actor.hasCity()) return false;
        if (!actor.hasLanguage() && !actor.hasTag("can_read_any_book")) return false;

        ActorExtend ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return false;

        Sect sect = ae.sect;
        return sect != null && !sect.isRekt() && sect.GetScriptureBookIds().Count > 0;
    }

    private static bool CanReadBook(Actor actor, Book book)
    {
        if (book == null || book.isRekt()) return false;
        if (!book.isReadyToBeRead()) return false;
        if (actor.hasTag("can_read_any_book")) return true;

        return actor.hasLanguage() && book.data.language_id == actor.language.id;
    }

    private static float GetStudyScore(Actor actor, ActorExtend ae, Sect sect, Book book)
    {
        BookTypeAsset bookType = book.getAsset();
        BookExtend bookExtend = book.GetExtend();

        if (bookType == BookTypes.Cultibook)
        {
            return GetCultibookScore(actor, ae, sect, bookExtend);
        }

        if (bookType == BookTypes.Skillbook)
        {
            return GetSkillbookScore(ae, bookExtend);
        }

        if (bookType == BookTypes.Elixirbook)
        {
            return GetElixirbookScore(ae, bookExtend);
        }

        return 0f;
    }

    private static float GetCultibookScore(Actor actor, ActorExtend ae, Sect sect, BookExtend bookExtend)
    {
        if (!bookExtend.HasComponent<Cultibook>()) return 0f;

        CultibookAsset cultibook = bookExtend.GetComponent<Cultibook>().Asset;
        if (cultibook == null) return 0f;

        CultibookAsset mainCultibook = ae.GetMainCultibook();
        float score = 0f;
        if (mainCultibook == cultibook)
        {
            float missingMastery = 100f - ae.GetMainCultibookMastery();
            if (missingMastery <= 0f) return 0f;
            score = 350f + missingMastery;
        }
        else if (mainCultibook == null)
        {
            score = 300f;
        }
        else
        {
            float knownMastery = ae.GetMaster(cultibook);
            if (knownMastery < SectConst.ScriptureStudyKnownCultibookCap)
            {
                score = 120f + SectConst.ScriptureStudyKnownCultibookCap - knownMastery;
            }

            float valueDelta = ae.EvaluateCultibookValue(cultibook) - ae.EvaluateCultibookValue(mainCultibook);
            if (valueDelta > 0f)
            {
                score += 80f + valueDelta * 4f;
            }
        }

        if (score <= 0f) return 0f;

        if (sect.GetDoctrineCultibook() == cultibook)
        {
            score += 120f;
        }

        if (ae.HasElementRoot())
        {
            score += cultibook.ElementReq.GetAffinity(ae.GetElementRoot()) * 80f;
        }

        if (ae.TryGetComponent(out Xian xian))
        {
            score = ApplyCultibookLevelFit(score, cultibook, xian.CurrLevel);
        }

        return score + GetItemLevelScore(bookExtend) * 3f;
    }

    private static float ApplyCultibookLevelFit(float score, CultibookAsset cultibook, int actorLevel)
    {
        if (actorLevel < cultibook.MinLevel)
        {
            int gap = cultibook.MinLevel - actorLevel;
            return gap > 2 ? 0f : score * 0.35f;
        }

        if (actorLevel > cultibook.MaxLevel)
        {
            return score * 0.6f;
        }

        return score + 50f;
    }

    private static float GetSkillbookScore(ActorExtend ae, BookExtend bookExtend)
    {
        if (!bookExtend.HasComponent<Skillbook>()) return 0f;

        Entity skillContainer = bookExtend.GetComponent<Skillbook>().SkillContainer;
        if (skillContainer.IsNull || !skillContainer.HasComponent<SkillContainer>()) return 0f;
        if (ae.HasSimilarSkill(skillContainer)) return 0f;

        float score = 160f + GetItemLevelScore(bookExtend) * 2f;
        if (IsMainCultibookSkill(ae, skillContainer, out int levelRequirement))
        {
            score += 120f;
            if (ae.TryGetComponent(out Xian xian) && xian.CurrLevel >= levelRequirement)
            {
                score += 60f;
            }
        }

        return score;
    }

    private static bool IsMainCultibookSkill(ActorExtend ae, Entity skillContainer, out int levelRequirement)
    {
        levelRequirement = 0;

        CultibookAsset mainCultibook = ae.GetMainCultibook();
        if (mainCultibook?.SkillPool == null) return false;

        for (int i = 0; i < mainCultibook.SkillPool.Count; i++)
        {
            SkillPoolEntry entry = mainCultibook.SkillPool[i];
            if (entry.SkillContainer.IsNull || !entry.SkillContainer.HasComponent<SkillContainer>()) continue;
            if (!SkillContainerUtils.IsSimilar(entry.SkillContainer, skillContainer)) continue;

            levelRequirement = entry.LevelRequirement;
            return true;
        }

        return false;
    }

    private static float GetElixirbookScore(ActorExtend ae, BookExtend bookExtend)
    {
        if (!bookExtend.HasComponent<Elixirbook>()) return 0f;

        ElixirAsset elixir = bookExtend.GetComponent<Elixirbook>().Asset;
        if (elixir == null) return 0f;

        float mastery = ae.GetMaster(elixir);
        if (mastery >= SectConst.ScriptureStudyElixirMasteryCap) return 0f;

        return 120f + SectConst.ScriptureStudyElixirMasteryCap - mastery + GetItemLevelScore(bookExtend) * 2f;
    }

    private static int GetItemLevelScore(BookExtend bookExtend)
    {
        if (!bookExtend.HasComponent<ItemLevel>()) return 0;

        return bookExtend.GetComponent<ItemLevel>();
    }

    private static Book PickWeighted(List<StudyCandidate> candidates)
    {
        int count = Mathf.Min(SectConst.ScriptureStudyTopCandidateCount, candidates.Count);
        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
        {
            totalWeight += candidates[i].Score;
        }

        if (totalWeight <= 0f) return candidates[0].Book;

        float roll = Randy.randomFloat(0f, totalWeight);
        for (int i = 0; i < count; i++)
        {
            roll -= candidates[i].Score;
            if (roll <= 0f) return candidates[i].Book;
        }

        return candidates[count - 1].Book;
    }

    private readonly struct StudyCandidate
    {
        public StudyCandidate(Book book, float score)
        {
            Book = book;
            Score = score;
        }

        public Book Book { get; }
        public float Score { get; }
    }
}
