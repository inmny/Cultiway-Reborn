using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.WorldTools;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api;

namespace Cultiway.Content.Artifacts.Baibao;

/// <summary>
/// 百宝阁的地图交互入口。世界工具只选择目标，法宝目录与制造规则仍由服务负责。
/// </summary>
internal static class BaibaoWorldToolSession
{
    private static readonly HashSet<long> GrantedActorIds = new();
    private static ArtifactBlueprint[] _sessionBlueprints = [];
    private static bool _grantModeWasActive;
    private static bool _grantingBrush;
    private static bool _brushFoundNewRecipient;
    private static int _brushRecipients;
    private static int _brushGranted;

    public static void Initialize()
    {
        WorldboxGame.GodPowers.BaibaoArchive.click_action = TryOpenArchive;
        WorldboxGame.GodPowers.BaibaoArchive.click_brush_action = TryOpenArchive;
        WorldboxGame.GodPowers.BaibaoGrant.click_action = TryGrant;
        WorldboxGame.GodPowers.BaibaoGrant.click_brush_action = TryGrantBrush;
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    /// <summary>
    /// 固定本轮要赠送的整套蓝图，并清空已经领取过的生物名单。
    /// </summary>
    public static void BeginGrantSession()
    {
        _sessionBlueprints = BaibaoPavilionService.Instance.GetSelectedBlueprints()
            .Select(blueprint => blueprint.DeepClone())
            .ToArray();
        GrantedActorIds.Clear();
        _grantModeWasActive = true;
    }

    [ClickActionCaller]
    public static bool TryGrantBrush(WorldTile tile, string powerId)
    {
        EnsureGrantSession();
        if (_sessionBlueprints.Length == 0)
        {
            ShowTip("Cultiway.Baibao.UI.Tip.SelectBlueprint".Localize());
            return false;
        }

        _grantingBrush = true;
        _brushFoundNewRecipient = false;
        _brushRecipients = 0;
        _brushGranted = 0;
        try
        {
            World.world.loopWithBrush(tile, Config.current_brush_data, TryGrant, powerId);
        }
        finally
        {
            _grantingBrush = false;
        }

        if (_brushGranted > 0)
        {
            ShowTip(string.Format("Cultiway.Baibao.UI.Format.GrantSuccess".Localize(), _brushRecipients,
                _brushGranted));
        }
        else if (_brushFoundNewRecipient)
        {
            ShowTip("Cultiway.Baibao.UI.Tip.NoValidBlueprint".Localize());
        }
        return _brushGranted > 0;
    }

    public static bool TryGrant(WorldTile tile, string powerId)
    {
        EnsureGrantSession();
        if (_sessionBlueprints.Length == 0)
        {
            ShowTip("Cultiway.Baibao.UI.Tip.SelectBlueprint".Localize());
            return false;
        }

        var actors = WorldToolDropTargets.SnapshotAliveActors(tile);
        if (actors.Count == 0) return false;

        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        int granted = 0;
        int recipients = 0;
        bool foundNewRecipient = false;
        bool foundPreviousRecipient = false;
        for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
        {
            Actor actor = actors[actorIndex];
            long actorId = actor.data.id;
            if (GrantedActorIds.Contains(actorId))
            {
                foundPreviousRecipient = true;
                continue;
            }

            foundNewRecipient = true;
            int actorGranted = 0;
            for (int blueprintIndex = 0; blueprintIndex < _sessionBlueprints.Length; blueprintIndex++)
            {
                if (!service.TryMaterialize(_sessionBlueprints[blueprintIndex], "百宝阁", out var artifact)) continue;
                actor.GetExtend().AddSpecialItem(artifact);
                actorGranted++;
            }

            if (actorGranted == 0) continue;
            GrantedActorIds.Add(actorId);
            recipients++;
            granted += actorGranted;
        }
        if (_grantingBrush && foundNewRecipient) _brushFoundNewRecipient = true;

        if (granted > 0)
        {
            if (_grantingBrush)
            {
                _brushRecipients += recipients;
                _brushGranted += granted;
            }
            else
            {
                ShowTip(string.Format("Cultiway.Baibao.UI.Format.GrantSuccess".Localize(), recipients, granted));
            }
        }
        else if (!_grantingBrush)
        {
            ShowTip(foundPreviousRecipient && !foundNewRecipient
                ? "Cultiway.Baibao.UI.Tip.AlreadyGrantedThisSession".Localize()
                : "Cultiway.Baibao.UI.Tip.NoValidBlueprint".Localize());
        }
        return granted > 0;
    }

    public static bool TryOpenArchive(WorldTile tile, string powerId)
    {
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        var actors = WorldToolDropTargets.SnapshotAliveActors(tile)
            .OrderBy(candidate => candidate.data.id)
            .ToArray();
        if (actors.Length == 0) return false;
        Actor actor = actors.FirstOrDefault(candidate => service.GetArchivableArtifacts(candidate).Count > 0);
        if (actor == null)
        {
            ShowTip(string.Format("Cultiway.Baibao.UI.Format.NoArtifact".Localize(), actors[0].getName()));
            return false;
        }

        service.RequestArchive(actor);
        return true;
    }

    private static void ShowTip(string text)
    {
        WorldTip.showNow(text, false, "top", 3f);
    }

    private static void EnsureGrantSession()
    {
        if (!_grantModeWasActive) BeginGrantSession();
    }

    private static void Tick()
    {
        bool active = WorldboxGame.GodPowers.BaibaoGrant.isSelected();
        if (active)
        {
            if (!_grantModeWasActive) BeginGrantSession();
            return;
        }
        if (_grantModeWasActive) EndGrantSession();
    }

    private static void EndGrantSession()
    {
        _sessionBlueprints = [];
        GrantedActorIds.Clear();
        _grantModeWasActive = false;
    }

    private sealed class UpdateSystem : BaseSystem
    {
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Tick();
        }
    }
}
