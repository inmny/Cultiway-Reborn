using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.WorldTools;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.SkillLibV3.Wanfa;

internal static class WanfaGrantSession
{
    private sealed class GrantPayload
    {
        public SkillBlueprint Snapshot;
        public int Revision;
        public int SessionGeneration;
        public float ExpiresAt;
    }

    private const float PayloadLifetime = 30f;
    private static readonly Dictionary<long, GrantPayload> Payloads = new();
    private static readonly SkillBlueprintCompiler Compiler = new();
    private static long _nextToken = DateTime.UtcNow.Ticks;
    private static bool _modeWasActive;
    private static int _sessionGeneration;

    public static void Initialize(WanfaPavilionService service)
    {
        service.WorldStateClearing += ClearWorldState;

        var grantPower = WorldboxGame.GodPowers.WanfaGrant;
        grantPower.click_action = TrySpawn;
        grantPower.click_brush_action = TrySpawnBrush;
        foreach (var drop in WorldboxGame.Drops.WanfaDrops)
        {
            drop.action_landed_drop = OnDropLanded;
        }

        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    [ClickActionCaller]
    public static bool TrySpawnBrush(WorldTile tile, string powerId)
    {
        if (WanfaTestCastSession.IsActive) return WanfaTestCastSession.TryCast(tile, powerId);
        if (WanfaPavilionService.Instance.SelectedBlueprintCount == 0)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.SelectGrantBlueprint".Localize(), false, "top", 3f);
            return false;
        }
        World.world.loopWithBrush(tile, Config.current_brush_data, TrySpawn, powerId);
        return true;
    }

    public static bool TrySpawn(WorldTile tile, string powerId)
    {
        if (WanfaTestCastSession.IsActive) return WanfaTestCastSession.TryCast(tile, powerId);

        var service = WanfaPavilionService.Instance;
        var blueprints = service.GetSelectedBlueprints();
        if (blueprints.Count == 0)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.SelectGrantBlueprint".Localize(), false, "top", 3f);
            return false;
        }

        var actors = WorldToolDropTargets.SnapshotAliveActors(tile);
        if (actors.Count == 0) return false;

        string validationError = null;
        var spawned = 0;
        foreach (var blueprint in blueprints)
        {
            var hasCompatibleActor = false;
            for (var i = 0; i < actors.Count; i++)
            {
                var validation = service.ValidateGrant(actors[i], blueprint);
                if (validation.IsCompatible)
                {
                    hasCompatibleActor = true;
                    break;
                }
                validationError ??= GetValidationError(validation);
            }
            if (!hasCompatibleActor) continue;

            var token = Interlocked.Increment(ref _nextToken);
            Payloads[token] = new GrantPayload
            {
                Snapshot = blueprint.DeepClone(),
                Revision = blueprint.Revision,
                SessionGeneration = _sessionGeneration,
                ExpiresAt = Time.realtimeSinceStartup + PayloadLifetime
            };

            var drop = service.ResolveVfxElement(blueprint).GrantDrop;
            World.world.drop_manager.spawn(tile, drop, pCasterId: token);
            spawned++;
        }

        if (spawned == 0 && validationError != null)
        {
            WorldTip.showNow(validationError, false, "top", 3f);
        }
        _modeWasActive = spawned > 0;
        return spawned > 0;
    }

    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId)
    {
        var token = drop.getCasterId();
        if (!Payloads.TryGetValue(token, out var payload)) return;
        Payloads.Remove(token);
        if (payload.SessionGeneration != _sessionGeneration || payload.ExpiresAt < Time.realtimeSinceStartup) return;

        var actors = WorldToolDropTargets.SnapshotAliveActors(tile);
        string validationError = null;
        var compatibleActors = 0;
        for (var i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (!actor.isAlive()) continue;
            var validation = WanfaPavilionService.Instance.ValidateGrant(actor, payload.Snapshot);
            if (!validation.IsCompatible)
            {
                validationError ??= GetValidationError(validation);
                continue;
            }

            compatibleActors++;
            GrantOrPrompt(actor, payload);
        }

        if (compatibleActors == 0 && validationError != null)
            WorldTip.showNow(validationError, false, "top", 3f);
    }

    public static void Tick()
    {
        if (_modeWasActive && !WorldboxGame.GodPowers.WanfaGrant.isSelected())
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
        _modeWasActive = false;
        WanfaPavilionService.Instance.ClearGrantConflicts();
    }

    private static void ResolveConflict(long targetActorId, GrantPayload payload, Entity oldContainer,
        bool overwrite)
    {
        if (!overwrite) return;
        if (payload.SessionGeneration != _sessionGeneration) return;
        var actor = World.world.units.get(targetActorId);
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

    private static void GrantOrPrompt(Actor actor, GrantPayload payload)
    {
        if (payload.SessionGeneration != _sessionGeneration) return;
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
            var targetActorId = actor.data.id;
            WanfaPavilionService.Instance.RequestGrantConflict(actor.getName(), payload.Revision,
                overwrite => ResolveConflict(targetActorId, payload, oldRevision, overwrite));
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

    private static string GetValidationError(SkillCompatibilityResult validation)
    {
        return validation.Issues.First(issue =>
            issue.Severity == Core.SkillLibV3.Editor.SkillValidationSeverity.Error).Message;
    }

    private static void ClearWorldState()
    {
        Clear();
        WanfaTestCastSession.Clear(false);
    }

    private sealed class UpdateSystem : BaseSystem
    {
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Tick();
            WanfaTestCastSession.Tick();
        }
    }
}
