using System;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Wanfa;

internal static class WanfaTestCastSession
{
    private enum SessionState
    {
        None,
        SelectingCaster,
        WaitingForExecution
    }

    private static readonly SkillBlueprintCompiler Compiler = new();
    private static SkillBlueprint _draft;
    private static Func<bool> _isExecutionActive;
    private static SessionState _state;
    public static bool IsActive => _state == SessionState.SelectingCaster;

    public static void Enter(SkillBlueprint draft, PowerButton grantButton)
    {
        WanfaGrantSession.Clear();
        _draft = draft.DeepClone();
        _isExecutionActive = null;
        _state = SessionState.SelectingCaster;
        PowerButtonSelector.instance.unselectAll();
        PowerButtonSelector.instance.clickPowerButton(grantButton);
        ScrollWindow.hideAllEvent(false);
        WorldTip.showNow("Cultiway.Wanfa.UI.Tip.SelectTestActor".Localize(), false, "top", 3f);
    }

    public static bool TryCast(WorldTile tile, string powerId)
    {
        var actor = ActionLibrary.getActorFromTile(tile);
        if (actor == null || !actor.isAlive()) return false;

        var target = FindRandomEnemy(actor);
        if (target == null)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.NoTestEnemy".Localize(), false, "top", 3f);
            FinishSelection();
            return false;
        }

        var compiled = Compiler.Compile(_draft, SkillBlueprintCompileMode.Preview);
        if (!compiled.Success)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.TestUnavailable".Localize(), false, "top", 3f);
            FinishSelection();
            return false;
        }

        float power = actor.GetExtend().GetPowerLevel();
        BaseSimObject carrier = Manager.ResolveSpatialSource(actor);
        Func<bool> active = null;
        bool accepted = ModClass.I.SkillV3.EmitSkill(new SkillEmission
        {
            SkillContainer = compiled.Container,
            Source = actor,
            SpatialSource = carrier,
            SourcePos = carrier.GetSimPos(),
            Target = target,
            TargetPos = target.GetSimPos(),
            Strength = SkillContext.DefaultStrength,
            PowerLevel = power,
            RuntimeData = new SkillCastRuntimeData { Origin = ActiveAbilityUseOrigin.Player },
            ReportPreview = isActive => active = isActive
        });
        if (!accepted || active == null)
        {
            SkillBlueprintCompiler.Recycle(compiled.Container);
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.TestUnavailable".Localize(), false, "top", 3f);
            FinishSelection();
            return false;
        }

        _isExecutionActive = active;
        WanfaPavilionService.Instance.TrackTestContainer(compiled.Container);
        _draft = null;
        _state = SessionState.WaitingForExecution;
        PowerButtonSelector.instance.unselectAll();
        FocusCamera(target);
        return true;
    }

    public static void Tick()
    {
        if (_state == SessionState.SelectingCaster && !WorldboxGame.GodPowers.WanfaGrant.isSelected())
        {
            FinishSelection(false);
            return;
        }

        if (_state == SessionState.WaitingForExecution && !_isExecutionActive())
        {
            _isExecutionActive = null;
            _state = SessionState.None;
            WanfaPavilionService.Instance.CompleteTestCast();
        }
    }

    public static void Clear(bool resumeEditor)
    {
        _draft = null;
        _isExecutionActive = null;
        _state = SessionState.None;
        if (resumeEditor) WanfaPavilionService.Instance.CompleteTestCast();
    }

    private static Actor FindRandomEnemy(Actor caster)
    {
        using var enemies = new ListPool<Actor>();
        foreach (var candidate in World.world.units.units_only_alive)
        {
            if (candidate == caster || !caster.kingdom.isEnemy(candidate.kingdom)) continue;
            enemies.Add(candidate);
        }
        return enemies.Count == 0 ? null : enemies.GetRandom();
    }

    private static void FocusCamera(Actor target)
    {
        var position = World.world.camera.transform.position;
        position.x = target.current_position.x;
        position.y = target.current_position.y;
        World.world.camera.transform.position = position;
    }

    private static void FinishSelection(bool unselectPower = true)
    {
        _draft = null;
        _isExecutionActive = null;
        _state = SessionState.None;
        if (unselectPower) PowerButtonSelector.instance.unselectAll();
        WanfaPavilionService.Instance.CompleteTestCast();
    }
}
