using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;
using Cultiway.Utils.Extension;
using Cultiway.Core.SkillLibV3.Wanfa;

namespace Cultiway.Content.Wanfa;

internal sealed class WanfaGrantPayload
{
    public long Token;
    public long TargetActorId;
    public SkillBlueprint Snapshot;
    public int Revision;
    public int SessionGeneration;
    public float ExpiresAt;
}

public static class WanfaDropExportSession
{
    private const float PayloadLifetime = 30f;
    private static readonly Dictionary<long, WanfaGrantPayload> Payloads = new();
    private static readonly SkillBlueprintCompiler Compiler = new();
    private static long _nextToken = DateTime.UtcNow.Ticks;
    private static string _selectedBlueprintId;
    private static bool _modeWasActive;
    private static int _sessionGeneration;

    public static void Enter(string blueprintId)
    {
        WanfaTestCastSession.Clear(false);
        _sessionGeneration++;
        Payloads.Clear();
        WanfaPavilionService.Instance.ClearGrantConflicts();
        _selectedBlueprintId = blueprintId;
        PowerButtonSelector.instance.unselectAll();
        PowerButtonSelector.instance.clickPowerButton(WanfaContentBootstrap.GrantButton);
        ScrollWindow.hideAllEvent(false);
        WorldTip.showNow("Cultiway.Wanfa.UI.Tip.SelectGrantActor".Localize(), false, "top", 3f);
        _modeWasActive = true;
    }

    public static bool TrySpawn(WorldTile tile, string powerId)
    {
        if (WanfaTestCastSession.IsActive) return WanfaTestCastSession.TryCast(tile, powerId);

        if (string.IsNullOrWhiteSpace(_selectedBlueprintId))
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.SelectGrantBlueprint".Localize(), false, "top", 3f);
            return false;
        }

        var actor = ActionLibrary.getActorFromTile(tile);
        if (actor == null || !actor.isAlive()) return false;

        var blueprint = WanfaPavilionService.Instance.Get(_selectedBlueprintId);
        if (blueprint == null) return false;
        var validation = WanfaPavilionService.Instance.ValidateGrant(actor, blueprint);
        if (!validation.IsCompatible)
        {
            var error = validation.Issues.First(issue =>
                issue.Severity == Core.SkillLibV3.Editor.SkillValidationSeverity.Error);
            WorldTip.showNow(error.Message, false, "top", 3f);
            return false;
        }

        var token = Interlocked.Increment(ref _nextToken);
        Payloads[token] = new WanfaGrantPayload
        {
            Token = token,
            TargetActorId = actor.data.id,
            Snapshot = blueprint.DeepClone(),
            Revision = blueprint.Revision,
            SessionGeneration = _sessionGeneration,
            ExpiresAt = Time.realtimeSinceStartup + PayloadLifetime
        };

        var drop = ResolveDrop(blueprint);
        World.world.drop_manager.spawn(tile, drop, pCasterId: token);
        return true;
    }

    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId)
    {
        var token = drop.getCasterId();
        if (!Payloads.TryGetValue(token, out var payload)) return;
        Payloads.Remove(token);
        if (payload.SessionGeneration != _sessionGeneration || payload.ExpiresAt < Time.realtimeSinceStartup) return;

        var actor = World.world.units.get(payload.TargetActorId);
        if (actor == null || !actor.isAlive()) return;
        GrantOrPrompt(actor, payload);
    }

    public static void Tick()
    {
        if (_modeWasActive && !GodPowers.WanfaGrant.isSelected())
        {
            Clear();
            return;
        }

        var now = Time.realtimeSinceStartup;
        var expired = Payloads.Where(item => item.Value.ExpiresAt < now).Select(item => item.Key).ToArray();
        foreach (var token in expired) Payloads.Remove(token);
    }

    public static void Clear()
    {
        _sessionGeneration++;
        Payloads.Clear();
        _selectedBlueprintId = null;
        _modeWasActive = false;
        WanfaPavilionService.Instance.ClearGrantConflicts();
    }

    private static void ResolveConflict(WanfaGrantPayload payload, Entity oldContainer, bool overwrite)
    {
        if (!overwrite) return;
        if (payload.SessionGeneration != _sessionGeneration ||
            payload.ExpiresAt < Time.realtimeSinceStartup) return;
        var actor = World.world.units.get(payload.TargetActorId);
        if (actor == null || !actor.isAlive()) return;
        var validation = WanfaPavilionService.Instance.ValidateGrant(actor, payload.Snapshot);
        if (!validation.IsCompatible)
        {
            var error = validation.Issues.First(issue =>
                issue.Severity == Core.SkillLibV3.Editor.SkillValidationSeverity.Error);
            WorldTip.showNow(error.Message, false, "top", 3f);
            return;
        }

        var compiled = Compiler.Compile(payload.Snapshot, SkillBlueprintCompileMode.Runtime);
        if (!compiled.Success)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.DamagedBlueprint".Localize(), false, "top", 3f);
            return;
        }

        var result = SkillOwnershipService.Replace(actor.GetExtend(), oldContainer, compiled.Container);
        if (result == SkillOwnershipResult.Replaced)
        {
            WanfaPavilionService.Instance.CompleteGrant(actor, payload.Snapshot);
            WorldTip.showNow(string.Format("Cultiway.Wanfa.UI.Format.GrantUpdated".Localize(),
                payload.Revision), false, "top", 3f);
        }
        else
        {
            SkillBlueprintCompiler.Recycle(compiled.Container);
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.ReplaceFailed".Localize(), false, "top", 3f);
        }
    }

    private static void GrantOrPrompt(Actor actor, WanfaGrantPayload payload)
    {
        if (payload.SessionGeneration != _sessionGeneration) return;
        var validation = WanfaPavilionService.Instance.ValidateGrant(actor, payload.Snapshot);
        if (!validation.IsCompatible)
        {
            var error = validation.Issues.First(issue =>
                issue.Severity == Core.SkillLibV3.Editor.SkillValidationSeverity.Error);
            WorldTip.showNow(error.Message, false, "top", 3f);
            return;
        }

        var signature = SkillBlueprintSignature.Build(payload.Snapshot);
        Entity oldRevision = default;
        var oldRevisionNumber = 0;
        var sameOrNewerRevision = 0;
        foreach (var container in actor.GetExtend().GetLearnedSkillsInOrder())
        {
            if (signature == SkillContainerSignature.Build(container))
            {
                WorldTip.showNow(string.Format("Cultiway.Wanfa.UI.Format.AlreadyKnown".Localize(),
                    actor.getName()), false, "top", 3f);
                return;
            }
            if (!container.TryGetComponent(out SkillBlueprintOrigin origin)) continue;
            if (origin.BlueprintId != payload.Snapshot.Id) continue;
            if (origin.Revision >= payload.Revision)
            {
                sameOrNewerRevision = Math.Max(sameOrNewerRevision, origin.Revision);
                continue;
            }
            if (origin.Revision <= oldRevisionNumber) continue;
            oldRevision = container;
            oldRevisionNumber = origin.Revision;
        }

        if (sameOrNewerRevision > 0)
        {
            WorldTip.showNow(string.Format("Cultiway.Wanfa.UI.Format.RejectDowngrade".Localize(),
                actor.getName(), sameOrNewerRevision), false, "top", 3f);
            return;
        }

        if (!oldRevision.IsNull)
        {
            WanfaPavilionService.Instance.RequestGrantConflict(actor.getName(), payload.Revision,
                overwrite => ResolveConflict(payload, oldRevision, overwrite));
            return;
        }

        var compiled = Compiler.Compile(payload.Snapshot, SkillBlueprintCompileMode.Runtime);
        if (!compiled.Success)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.DamagedBlueprint".Localize(), false, "top", 3f);
            return;
        }

        var result = SkillOwnershipService.Learn(actor.GetExtend(), compiled.Container);
        if (result == SkillOwnershipResult.Added)
        {
            WanfaPavilionService.Instance.CompleteGrant(actor, payload.Snapshot);
            WorldTip.showNow(string.Format("Cultiway.Wanfa.UI.Format.GrantLearned".Localize(), actor.getName(),
                WanfaPavilionService.Instance.GetDisplayName(payload.Snapshot)), false, "top", 3f);
        }
        else
        {
            SkillBlueprintCompiler.Recycle(compiled.Container);
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.GrantFailed".Localize(), false, "top", 3f);
        }
    }

    private static DropAsset ResolveDrop(SkillBlueprint blueprint)
    {
        var element = WanfaPavilionService.Instance.ResolveVfxElement(blueprint);
        if (element == SkillVfxElements.Metal) return Drops.WanfaMetal;
        if (element == SkillVfxElements.Wood) return Drops.WanfaWood;
        if (element == SkillVfxElements.Water) return Drops.WanfaWater;
        if (element == SkillVfxElements.Ice) return Drops.WanfaIce;
        if (element == SkillVfxElements.Fire) return Drops.WanfaFire;
        if (element == SkillVfxElements.Earth) return Drops.WanfaEarth;
        if (element == SkillVfxElements.Neg) return Drops.WanfaNeg;
        if (element == SkillVfxElements.Pos) return Drops.WanfaPos;
        if (element == SkillVfxElements.Wind) return Drops.WanfaWind;
        if (element == SkillVfxElements.Lightning) return Drops.WanfaLightning;
        if (element == SkillVfxElements.Poison) return Drops.WanfaPoison;
        if (element == SkillVfxElements.Explosion) return Drops.WanfaExplosion;
        if (element == SkillVfxElements.Burnout) return Drops.WanfaBurnout;
        if (element == SkillVfxElements.Gravity) return Drops.WanfaGravity;
        if (element == SkillVfxElements.Curse) return Drops.WanfaCurse;
        return Drops.WanfaEntropy;
    }
}
