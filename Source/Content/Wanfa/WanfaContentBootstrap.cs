using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.UI;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.General;

namespace Cultiway.Content.Wanfa;

public static class WanfaContentBootstrap
{
    public static PowerButton GrantButton { get; private set; }

    public static void Initialize(WanfaPavilionService service)
    {
        service.GrantRequested += WanfaDropExportSession.Enter;
        service.TestCastRequested += WanfaTestCastSession.Enter;
        service.WorldStateClearing += ClearWorldState;

        GrantButton = PowerButtonCreator.CreateGodPowerButton(
            GodPowers.WanfaGrant.id,
            SpriteTextureLoader.getSprite("cultiway/icons/iconMagic"));
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD, GrantButton);
        ModClass.I.GeneralLogicSystems.Add(new WanfaSessionUpdateSystem());
    }

    private static void ClearWorldState()
    {
        WanfaDropExportSession.Clear();
        WanfaTestCastSession.Clear(false);
    }
}

internal sealed class WanfaSessionUpdateSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        WanfaDropExportSession.Tick();
        WanfaTestCastSession.Tick();
    }
}
