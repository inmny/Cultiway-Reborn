using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

internal enum ControlledScriptureKind
{
    Cultibook,
    ElixirRecipe,
    Skill,
}

internal sealed class ControlledScriptureWriteContext : IControlledTaskExecutionContext
{
    public ControlledScriptureKind Kind { get; }
    public string SourceKey { get; }
    public string DestinationKey { get; }
    public Entity SkillHandle { get; }

    public ControlledScriptureWriteContext(ControlledScriptureKind kind, string sourceKey,
        string destinationKey, Entity skillHandle)
    {
        Kind = kind;
        SourceKey = sourceKey;
        DestinationKey = destinationKey;
        SkillHandle = skillHandle;
    }

    public void OnOrderFinished(ControlledTaskOrderState state, string reasonLocaleKey)
    {
        // 书本只在写入行为内创建；未消费的上下文不持有世界资源。
    }
}

internal sealed class ScriptureCommandConfigurator : IControlledTaskCommandConfigurator
{
    private const string SourceKey = "source";
    private const string DestinationKey = "destination";
    private const string CityPrefix = "city:";
    private const string SectPrefix = "sect:";

    private static readonly IReadOnlyList<ControlledTaskParameterDefinition> ParameterDefinitions =
        new[]
        {
            new ControlledTaskParameterDefinition(
                SourceKey,
                ControlledTaskParameterMode.SingleChoice,
                true,
                1,
                1,
                "Cultiway.ControlledTask.Parameter.Source",
                "Cultiway.ControlledTask.Parameter.Source.Description"),
            new ControlledTaskParameterDefinition(
                DestinationKey,
                ControlledTaskParameterMode.SingleChoice,
                true,
                1,
                1,
                "Cultiway.ControlledTask.Parameter.Destination",
                "Cultiway.ControlledTask.Parameter.Destination.Description"),
        };

    private readonly ControlledScriptureKind kind;

    internal ScriptureCommandConfigurator(ControlledScriptureKind targetKind)
    {
        kind = targetKind;
    }

    public IReadOnlyList<ControlledTaskParameterDefinition> Parameters => ParameterDefinitions;

    public IReadOnlyList<ControlledTaskOption> GetOptions(
        Actor actor,
        string parameterKey,
        ControlledTaskInvocation invocation)
    {
        if (!CanWrite(actor)) return Array.Empty<ControlledTaskOption>();
        if (parameterKey == SourceKey) return QuerySources(actor);
        if (parameterKey == DestinationKey)
        {
            string sourceKey = invocation.GetSelections(SourceKey).FirstOrDefault();
            return string.IsNullOrEmpty(sourceKey)
                ? Array.Empty<ControlledTaskOption>()
                : QueryDestinations(actor, sourceKey);
        }
        return Array.Empty<ControlledTaskOption>();
    }

    public ControlledTaskAvailability Validate(Actor actor, ControlledTaskInvocation invocation)
    {
        if (!CanWrite(actor))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresCity");

        string sourceKey = invocation.GetSelections(SourceKey).FirstOrDefault();
        string destinationKey = invocation.GetSelections(DestinationKey).FirstOrDefault();
        if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(destinationKey))
            return ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.ParameterSelectionRequired");

        if (!TryResolveSource(actor, sourceKey, out Entity skill, out CultibookAsset cultibook,
                out ElixirAsset elixir))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.SourceInvalid");
        if (!TryResolveDestination(actor, destinationKey, cultibook, elixir, skill,
                out _))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.DestinationInvalid");
        return ControlledTaskAvailability.Available;
    }

    public IControlledTaskExecutionContext Prepare(Actor actor, ControlledTaskInvocation invocation)
    {
        ControlledTaskAvailability availability = Validate(actor, invocation);
        if (!availability.Enabled) throw new InvalidOperationException(availability.ReasonLocaleKey);
        string sourceKey = invocation.GetSelections(SourceKey)[0];
        string destinationKey = invocation.GetSelections(DestinationKey)[0];
        if (!TryResolveSource(actor, sourceKey, out Entity skill, out _, out _))
            throw new InvalidOperationException("Controlled scripture source disappeared.");
        return new ControlledScriptureWriteContext(
            kind, sourceKey, destinationKey, skill);
    }

    internal static bool TryWrite(Actor actor, ControlledScriptureWriteContext context,
        out string reasonLocaleKey)
    {
        reasonLocaleKey = string.Empty;
        if (actor == null || actor.isRekt() || context == null)
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.ActorLost";
            return false;
        }

        ScriptureCommandConfigurator resolver = new(context.Kind);
        Entity skill = context.SkillHandle;
        CultibookAsset cultibook = null;
        ElixirAsset elixir = null;
        if (context.Kind == ControlledScriptureKind.Skill)
        {
            if (!resolver.IsOwnedSkill(actor, skill))
            {
                reasonLocaleKey = "Cultiway.ControlledTask.Reason.SourceOrDestinationInvalid";
                return false;
            }
        }
        else if (!resolver.TryResolveSource(actor, context.SourceKey, out skill,
                     out cultibook, out elixir))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.SourceOrDestinationInvalid";
            return false;
        }

        if (!resolver.TryResolveDestination(actor, context.DestinationKey, cultibook, elixir, skill,
                out ScriptureBookDestination destination))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.SourceOrDestinationInvalid";
            return false;
        }

        return context.Kind switch
        {
            ControlledScriptureKind.Cultibook => ScriptureWritingService.TryWriteCultibook(
                actor, destination, cultibook, actor.GetExtend().GetMaster(cultibook), out reasonLocaleKey),
            ControlledScriptureKind.ElixirRecipe => ScriptureWritingService.TryWriteElixirRecipe(
                actor, destination, elixir, actor.GetExtend().GetMaster(elixir), out reasonLocaleKey),
            ControlledScriptureKind.Skill => ScriptureWritingService.TryWriteSkill(
                actor, destination, skill, out reasonLocaleKey),
            _ => false,
        };
    }

    private IReadOnlyList<ControlledTaskOption> QuerySources(Actor actor)
    {
        var result = new List<ControlledTaskOption>();
        ActorExtend extend = actor.GetExtend();
        switch (kind)
        {
            case ControlledScriptureKind.Cultibook:
                foreach ((CultibookAsset asset, float mastery) in extend.GetAllMaster<CultibookAsset>()
                             .OrderBy(item => item.Item1?.id, StringComparer.Ordinal))
                {
                    if (asset == null || QueryDestinations(actor, "asset:" + asset.id).Count == 0) continue;
                    result.Add(new ControlledTaskOption(
                        "asset:" + asset.id,
                        asset.Name,
                        $"{"Cultiway.ControlledTask.Parameter.Mastery".Localize()}: {mastery:F0}%",
                        "cultiway/icons/iconCultivation"));
                }
                break;
            case ControlledScriptureKind.ElixirRecipe:
                foreach ((ElixirAsset asset, float mastery) in extend.GetAllMaster<ElixirAsset>()
                             .OrderBy(item => item.Item1?.id, StringComparer.Ordinal))
                {
                    if (asset == null || QueryDestinations(actor, "asset:" + asset.id).Count == 0) continue;
                    result.Add(new ControlledTaskOption(
                        "asset:" + asset.id,
                        asset.GetName() + "丹方",
                        $"{"Cultiway.ControlledTask.Parameter.Mastery".Localize()}: {mastery:F0}%",
                        "cultiway/icons/iconElixirCauldron"));
                }
                break;
            case ControlledScriptureKind.Skill:
                if (extend.all_skills == null) break;
                foreach (Entity skill in extend.all_skills.OrderBy(item => item.Id))
                {
                    if (!IsValidSkill(skill) || QueryDestinations(actor, "entity:" + skill.Id).Count == 0)
                        continue;
                    string label = skill.HasName
                        ? skill.Name.value
                        : skill.GetComponent<SkillContainer>().SkillEntityAssetID.Localize();
                    result.Add(new ControlledTaskOption(
                        "entity:" + skill.Id,
                        label,
                        "Cultiway.ControlledTask.Parameter.SkillSource".Localize(),
                        "cultiway/icons/iconWriting"));
                }
                break;
        }
        return result;
    }

    private IReadOnlyList<ControlledTaskOption> QueryDestinations(Actor actor, string sourceKey)
    {
        if (!TryResolveSource(actor, sourceKey, out Entity skill, out CultibookAsset cultibook,
                out ElixirAsset elixir)) return Array.Empty<ControlledTaskOption>();
        return QueryDestinations(actor, cultibook, elixir, skill);
    }

    private IReadOnlyList<ControlledTaskOption> QueryDestinations(Actor actor,
        CultibookAsset cultibook = null, ElixirAsset elixir = null, Entity skill = default)
    {
        var result = new List<ControlledTaskOption>();
        Sect sect = actor.GetExtend().sect;
        if (sect != null && SectScripturePolicy.CanContribute(actor, sect) &&
            ((cultibook != null && SectScripturePolicy.CanAccept(sect, cultibook)) ||
             (elixir != null && SectScripturePolicy.CanAccept(sect, elixir)) ||
             (kind == ControlledScriptureKind.Skill && SectScripturePolicy.CanAccept(sect, skill))))
        {
            result.Add(new ControlledTaskOption(
                SectPrefix + sect.getID(),
                "Cultiway.ControlledTask.Parameter.SectDestination".Localize() + ": " + sect.name,
                "Cultiway.ControlledTask.Parameter.DestinationSectDescription".Localize(),
                "ui/icons/iconKingdom"));
        }

        City city = actor.getCity();
        bool cityAccepts = cultibook != null
            ? city.CanAcceptCultibook(cultibook)
            : elixir != null
                ? city.CanAcceptElixirRecipe(elixir)
                : city.CanAcceptSkillbook(skill);
        if (cityAccepts)
        {
            result.Add(new ControlledTaskOption(
                CityPrefix + city.data.id.ToString(CultureInfo.InvariantCulture),
                "Cultiway.ControlledTask.Parameter.CityDestination".Localize() + ": " + city.name,
                "Cultiway.ControlledTask.Parameter.DestinationCityDescription".Localize(),
                "ui/icons/iconBooks"));
        }
        return result;
    }

    private bool TryResolveSource(Actor actor, string sourceKey, out Entity skill,
        out CultibookAsset cultibook, out ElixirAsset elixir)
    {
        skill = default;
        cultibook = null;
        elixir = null;
        if (string.IsNullOrEmpty(sourceKey)) return false;

        if (kind == ControlledScriptureKind.Skill)
        {
            if (!sourceKey.StartsWith("entity:", StringComparison.Ordinal) ||
                !long.TryParse(sourceKey.Substring("entity:".Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long skillId)) return false;
            HashSet<Entity> skills = actor.GetExtend().all_skills;
            if (skills == null) return false;
            foreach (Entity candidate in skills)
            {
                if (candidate.Id == skillId && IsValidSkill(candidate) &&
                    actor.GetExtend().OwnsLearnedSkill(candidate))
                {
                    skill = candidate;
                    return true;
                }
            }
            return false;
        }

        if (!sourceKey.StartsWith("asset:", StringComparison.Ordinal)) return false;
        string assetId = sourceKey.Substring("asset:".Length);
        if (kind == ControlledScriptureKind.Cultibook)
        {
            cultibook = Libraries.Manager.CultibookLibrary.get(assetId);
            return cultibook != null && actor.GetExtend().GetMaster(cultibook) > 0f;
        }

        elixir = Libraries.Manager.ElixirLibrary.get(assetId);
        return elixir != null && actor.GetExtend().GetMaster(elixir) > 0f;
    }

    private bool TryResolveDestination(Actor actor, string destinationKey, CultibookAsset cultibook,
        ElixirAsset elixir, Entity skill, out ScriptureBookDestination destination)
    {
        destination = default;
        if (string.IsNullOrEmpty(destinationKey)) return false;
        if (destinationKey.StartsWith(SectPrefix, StringComparison.Ordinal))
        {
            Sect sect = actor.GetExtend().sect;
            if (sect == null || !long.TryParse(destinationKey.Substring(SectPrefix.Length),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out long sectId) ||
                sect.getID() != sectId || !SectScripturePolicy.CanContribute(actor, sect)) return false;
            bool accepts = cultibook != null
                ? SectScripturePolicy.CanAccept(sect, cultibook)
                : elixir != null
                    ? SectScripturePolicy.CanAccept(sect, elixir)
                    : SectScripturePolicy.CanAccept(sect, skill);
            if (!accepts) return false;
            destination = ScriptureBookDestination.ForSect(sect);
            return true;
        }

        if (!destinationKey.StartsWith(CityPrefix, StringComparison.Ordinal) || !actor.hasCity() ||
            !long.TryParse(destinationKey.Substring(CityPrefix.Length), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long cityId)) return false;
        City city = actor.getCity();
        if (city == null || city.data.id != cityId) return false;
        bool cityAccepts = cultibook != null
            ? city.CanAcceptCultibook(cultibook)
            : elixir != null
                ? city.CanAcceptElixirRecipe(elixir)
                : city.CanAcceptSkillbook(skill);
        if (!cityAccepts) return false;
        destination = ScriptureBookDestination.ForCity(city);
        return true;
    }

    private bool IsOwnedSkill(Actor actor, Entity skill)
    {
        return IsValidSkill(skill) && actor?.GetExtend()?.OwnsLearnedSkill(skill) == true;
    }

    private static bool CanWrite(Actor actor)
    {
        return actor != null && !actor.isRekt() && actor.hasCity() && actor.hasLanguage();
    }

    private static bool IsValidSkill(Entity skill)
    {
        return !skill.IsNull && skill.HasComponent<SkillContainer>() &&
               !string.IsNullOrEmpty(skill.GetComponent<SkillContainer>().SkillEntityAssetID) &&
               !skill.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied, TagRecycle>());
    }
}

internal static class ScriptureWritingService
{
    internal static bool TryWriteCultibook(Actor actor, ScriptureBookDestination destination,
        CultibookAsset cultibook, float mastery, out string reasonLocaleKey)
    {
        Book book = World.world.books.WriteCultibookBook(actor, cultibook, mastery);
        return TryStore(actor, destination, book, out reasonLocaleKey);
    }

    internal static bool TryWriteElixirRecipe(Actor actor, ScriptureBookDestination destination,
        ElixirAsset elixir, float mastery, out string reasonLocaleKey)
    {
        Book book = World.world.books.WriteElixirRecipeBook(actor, elixir, mastery);
        return TryStore(actor, destination, book, out reasonLocaleKey);
    }

    internal static bool TryWriteSkill(Actor actor, ScriptureBookDestination destination,
        Entity skill, out string reasonLocaleKey)
    {
        Book book = World.world.books.WriteSkillbookBook(actor, skill);
        return TryStore(actor, destination, book, out reasonLocaleKey);
    }

    internal static bool TryWrite(Actor actor, ControlledScriptureWriteContext context,
        out string reasonLocaleKey)
    {
        return ScriptureCommandConfigurator.TryWrite(actor, context, out reasonLocaleKey);
    }

    private static bool TryStore(Actor actor, ScriptureBookDestination destination, Book book,
        out string reasonLocaleKey)
    {
        if (book == null)
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.BookCreationFailed";
            return false;
        }

        try
        {
            if (destination.StoreBook(actor, book))
            {
                reasonLocaleKey = string.Empty;
                return true;
            }
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ScriptureWriting] store failed actor={actor?.getID()}: {exception}");
        }

        CleanupBook(book);
        reasonLocaleKey = "Cultiway.ControlledTask.Reason.DestinationUnavailable";
        return false;
    }

    internal static void CleanupBook(Book book)
    {
        if (book == null || book.isRekt()) return;
        BookExtend extend = book.GetExtend();
        if (extend.HasComponent<Skillbook>())
        {
            Entity skill = extend.GetComponent<Skillbook>().SkillContainer;
            if (!skill.IsNull) skill.DeleteEntity();
        }
        World.world.books.DisposeUncommittedBook(book);
    }
}
