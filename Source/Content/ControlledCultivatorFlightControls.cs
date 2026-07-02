using Cultiway.Content.Const;
using Cultiway.Content.Patch;
using Cultiway.Core;
using strings;

namespace Cultiway.Content;

internal static class ControlledCultivatorFlightControls
{
    private static bool _possessedActionWrapped;
    private static WorldAction _vanillaPossessedAction;

    internal static void Init()
    {
        WrapPossessedAction();
    }

    internal static bool ToggleManualFlight()
    {
        if (!ControlledCultivatorSkillControls.TryGetControlledActor(out var actor)) return false;

        if (actor.data.hasFlag(ContentActorDataKeys.ManualControlledFlight_flag))
        {
            StopManualFlight(actor);
            ShowTip("飞行关闭");
            return true;
        }

        if (!PatchAboutFly.StartCultiwayFlight(actor))
        {
            ShowTip("当前修为无法飞行");
            return false;
        }

        actor.data.addFlag(ContentActorDataKeys.ManualControlledFlight_flag);
        ShowTip("飞行开启");
        return true;
    }

    internal static ControlledFlightControlState GetState()
    {
        if (!ControlledCultivatorSkillControls.TryGetControlledActor(out var actor))
        {
            return ControlledFlightControlState.Inactive;
        }

        var manualFlight = actor.data.hasFlag(ContentActorDataKeys.ManualControlledFlight_flag);
        var canToggleFlight = manualFlight || PatchAboutFly.CanStartCultiwayFlight(actor);
        return new ControlledFlightControlState(actor, canToggleFlight, manualFlight);
    }

    private static void WrapPossessedAction()
    {
        if (_possessedActionWrapped) return;

        var possessed = AssetManager.status.get(S_Status.possessed);
        if (possessed == null) return;

        _vanillaPossessedAction = possessed.action;
        possessed.action = (target, tile) =>
        {
            var result = _vanillaPossessedAction?.Invoke(target, tile) ?? true;
            if (target != null && target.isActor())
            {
                ValidateManualFlight(target.a);
            }
            return result;
        };
        _possessedActionWrapped = true;
    }

    private static void ValidateManualFlight(Actor actor)
    {
        if (actor == null || !actor.data.hasFlag(ContentActorDataKeys.ManualControlledFlight_flag)) return;
        if (ControllableUnit.isControllingUnit(actor) && PatchAboutFly.CanStartCultiwayFlight(actor)) return;
        StopManualFlight(actor);
    }

    private static void StopManualFlight(Actor actor)
    {
        actor.data.removeFlag(ContentActorDataKeys.ManualControlledFlight_flag);
        PatchAboutFly.StopCultiwayFlight(actor, false);
    }

    private static void ShowTip(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        WorldTip.showNow(text, false, "top", 2.5f);
    }
}

internal readonly struct ControlledFlightControlState
{
    public static readonly ControlledFlightControlState Inactive = new();

    public readonly Actor Actor;
    public readonly bool CanToggleFlight;
    public readonly bool IsManualFlight;

    public bool Active => Actor != null;

    public ControlledFlightControlState(Actor actor, bool canToggleFlight, bool isManualFlight)
    {
        Actor = actor;
        CanToggleFlight = canToggleFlight;
        IsManualFlight = isManualFlight;
    }
}
