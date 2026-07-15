using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Wanfa;

internal static class WanfaTestCastSession
{
    private enum SessionState
    {
        None,
        SelectingCaster,
        WaitingForRecycle
    }

    private static readonly SkillBlueprintCompiler Compiler = new();
    private static SkillBlueprint _draft;
    private static Entity _trackedSkillEntity;
    private static SessionState _state;
    public static bool IsActive => _state == SessionState.SelectingCaster;

    public static void Enter(SkillBlueprint draft, PowerButton grantButton)
    {
        WanfaGrantSession.Clear();
        _draft = draft.DeepClone();
        _trackedSkillEntity = default;
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

        _trackedSkillEntity = ModClass.I.SkillV3.SpawnSkill(compiled.Container, actor, target,
            SkillContext.DefaultStrength,
            power_level: actor.GetExtend().GetPowerLevel());
        WanfaPavilionService.Instance.TrackTestContainer(compiled.Container);
        _draft = null;
        _state = SessionState.WaitingForRecycle;
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

        if (_state == SessionState.WaitingForRecycle && _trackedSkillEntity.IsNull)
        {
            _trackedSkillEntity = default;
            _state = SessionState.None;
            WanfaPavilionService.Instance.CompleteTestCast();
        }
    }

    public static void Clear(bool resumeEditor)
    {
        _draft = null;
        _trackedSkillEntity = default;
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
        _trackedSkillEntity = default;
        _state = SessionState.None;
        if (unselectPower) PowerButtonSelector.instance.unselectAll();
        WanfaPavilionService.Instance.CompleteTestCast();
    }
}
