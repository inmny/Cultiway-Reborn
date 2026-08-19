using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.SpiritVeins;
using Cultiway.Content.UI.CreatureInfoPages;
using Cultiway.Content.UI.Prefab;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using strings;

namespace Cultiway.Content;

public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
{
    [CloneSource(S_Tooltip.book)]
    public static TooltipAsset Cultibook { get; private set; }

    [CloneSource(S_Tooltip.tip)]
    public static TooltipAsset CoreFormationEffect { get; private set; }

    [CloneSource(S_Tooltip.tip)]
    public static TooltipAsset SpiritVein { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        Cultibook.prefab_id = "tooltips/tooltip_cultiway_cultibook";
        Cultibook.callback = ShowCultibookTooltip;
        CultibookTooltip.PatchTo<Tooltip>(Cultibook.prefab_id);
        CoreFormationEffect.callback = ShowCoreFormationEffect;
        SpiritVein.callback = ShowSpiritVein;
        WorldboxGame.Tooltips.Actor.callback += ShowActorCultiwayInfo;
        WorldboxGame.Tooltips.ActorKing.callback += ShowActorCultiwayInfo;
        WorldboxGame.Tooltips.ActorLeader.callback += ShowActorCultiwayInfo;
    }

    private static void ShowActorCultiwayInfo(Tooltip tooltip, string type, TooltipData data)
    {
        var ae = data.actor.GetExtend();
        InsertSectAndMasterInfo(tooltip, ae);
        if (ae.HasElementRoot())
        {
            var er = ae.GetElementRoot();
            var cultisys = Cultisyses.GetDisplayCultisys(ae);
            var style = cultisys?.DisplayStyle;
            var label = style != null ? LM.Get(style.category_label_key) : "灵根";
            tooltip.addLineText(label, er.Type.GetName(cultisys), pLocalize: false);
        }
        if (ae.HasComponent<Jindan>())
        {
            ref Jindan jindan = ref ae.GetComponent<Jindan>();
            tooltip.addLineText("Cultiway.CoreFormation.Label.Jindan".Localize(), jindan.GetName(), pLocalize: false);
        }
        if (ae.HasComponent<Yuanying>())
        {
            ref Yuanying yuanying = ref ae.GetComponent<Yuanying>();
            tooltip.addLineText("Cultiway.CoreFormation.Label.Yuanying".Localize(), yuanying.GetName(), pLocalize: false);
        }
    }

    private static void InsertSectAndMasterInfo(Tooltip tooltip, ActorExtend ae)
    {
        string insertAfter = "kingdom";
        if (ae.sect != null && !ae.sect.isRekt())
        {
            tooltip.InsertLineAfter(insertAfter, "Sect", ae.sect.name, ae.sect.getColor().color_text);
            insertAfter = "Sect";
        }
        Actor master = ae.GetMaster();
        if (master != null && !master.isRekt())
            tooltip.InsertLineAfter(insertAfter, "Masters", master.getName(), master.kingdom?.getColor()?.color_text);
    }

    private static void ShowCultibookTooltip(Tooltip tooltip, string type, TooltipData data)
    {
        var cultibookTooltip = tooltip.GetComponent<CultibookTooltip>();
        if (cultibookTooltip?.SetupPending() == true) return;
        var book = data.book;
        if (book == null || book.getAsset() != BookTypes.Cultibook) return;
        cultibookTooltip?.Setup(book);
    }

    private static void ShowSpiritVein(Tooltip tooltip, string type, TooltipData data)
    {
        string value = data?.tip_name;
        int separator = value?.IndexOf(':') ?? -1;
        if (separator <= 0 || !int.TryParse(value.Substring(separator + 1), out int id))
        {
            tooltip.setTitle("Cultiway.SpiritVein.Type.Vein", string.Empty);
            return;
        }

        SpiritVeinManager manager = WorldboxGame.I?.SpiritVeins;
        string target = value.Substring(0, separator);
        switch (target)
        {
            case "vein":
                ShowSpiritVeinObject(tooltip, manager, manager?.GetVeinByTopologyId(id));
                break;
            case "eye":
                ShowSpiritVeinEye(tooltip, manager, manager?.GetEye(id));
                break;
            case "ground":
                ShowGatheringGround(tooltip, manager, manager?.GetGround(id));
                break;
            default:
                ShowVeinSection(tooltip, manager, manager?.GetSection(id));
                break;
        }
    }

    private static void ShowSpiritVeinObject(
        Tooltip tooltip,
        SpiritVeinManager manager,
        SpiritVein vein)
    {
        if (vein == null)
        {
            tooltip.setTitle("Cultiway.SpiritVein.Type.Vein", string.Empty);
            return;
        }

        tooltip.setTitle(vein.Name, "Cultiway.SpiritVein.Type.Vein", vein.Composition.HexColor());
        tooltip.addLineText("Cultiway.SpiritVein.Scale", ResolveVeinScale(vein.Scale));
        tooltip.addLineText("Cultiway.SpiritVein.Source", EmptyAsDash(vein.SourceRegionName));
        tooltip.addLineText("Cultiway.SpiritVein.Outlet", EmptyAsDash(vein.OutletRegionName));
        tooltip.addLineText("Cultiway.SpiritVein.Elements", ResolveElementSummary(vein.Composition));
        tooltip.addLineText("Cultiway.SpiritVein.List.Branches", vein.BranchIds.Count.ToString());
        tooltip.addLineText("Cultiway.SpiritVein.List.Sections", vein.SectionIds.Count.ToString());
        tooltip.addLineText("Cultiway.SpiritVein.List.Grounds", vein.GroundIds.Count.ToString());
        tooltip.addLineText("Cultiway.SpiritVein.List.Eyes", vein.EyeIds.Count.ToString());
    }

    private static void ShowVeinSection(
        Tooltip tooltip,
        SpiritVeinManager manager,
        SpiritVeinSection section)
    {
        if (section == null)
        {
            tooltip.setTitle("Cultiway.SpiritVein.Type.Vein", string.Empty);
            return;
        }
        SpiritVein vein = manager?.GetVeinByTopologyId(section.VeinId);
        SpiritVeinBranch branch = manager?.GetBranch(section.BranchId);
        string title = branch?.Name ?? vein?.Name ?? "-";
        string subtitle = branch == null ? "Cultiway.SpiritVein.Type.Vein" : "Cultiway.SpiritVein.Type.BranchDragon";
        tooltip.setTitle(title, subtitle, section.Composition.HexColor());
        tooltip.addLineText("Cultiway.SpiritVein.Location", ResolveSectionKind(section.Kind));
        tooltip.addLineText("Cultiway.SpiritVein.Region", EmptyAsDash(section.RegionName));
        tooltip.addLineText("Cultiway.SpiritVein.Scale", branch == null
            ? ResolveVeinScale(vein?.Scale ?? DragonVeinScale.Micro)
            : ResolveBranchScale(branch.Scale));
        tooltip.addLineText("Cultiway.SpiritVein.Elements", ResolveElementSummary(section.Composition));
        tooltip.addLineText("Cultiway.SpiritVein.Status", ResolveSectionStatus(section.Status));
        tooltip.addLineText("Cultiway.SpiritVein.Fill", $"{section.FillRatio * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Patency", $"{section.Patency * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Supply", $"{section.EffectiveSupply:0.#}");
        tooltip.addLineText("Cultiway.SpiritVein.Purity", $"{section.Purity * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Pollution", ResolvePollutionName(section.Purity));
        if (vein != null)
        {
            tooltip.addLineText("Cultiway.SpiritVein.Source", EmptyAsDash(vein.SourceRegionName));
            tooltip.addLineText("Cultiway.SpiritVein.Outlet", EmptyAsDash(vein.OutletRegionName));
        }
    }

    private static void ShowGatheringGround(
        Tooltip tooltip,
        SpiritVeinManager manager,
        GatheringGround ground)
    {
        if (ground == null)
        {
            tooltip.setTitle("Cultiway.SpiritVein.Type.Ground", string.Empty);
            return;
        }
        SpiritVein vein = manager?.GetVeinByTopologyId(ground.PrimaryVeinId);
        SpiritVein guest = manager?.GetVeinByTopologyId(ground.GuestVeinId);
        SpiritVeinEye eye = manager?.GetEye(ground.EyeId);
        SpiritVeinSection section = manager?.GetSection(ground.SectionId);
        tooltip.setTitle(ground.Name, ResolveGroundKindKey(ground.Kind), eye?.Composition.HexColor());
        tooltip.addLineText("Cultiway.SpiritVein.Network", vein?.Name ?? "-");
        if (guest != null) tooltip.addLineText("Cultiway.SpiritVein.GuestVein", guest.Name);
        tooltip.addLineText("Cultiway.SpiritVein.Quality", ResolveGroundQuality(ground.Quality));
        tooltip.addLineText("Cultiway.SpiritVein.Eye", eye?.Name ?? "-");
        tooltip.addLineText("Cultiway.SpiritVein.Region", EmptyAsDash(ground.RegionName));
        tooltip.addLineText("Cultiway.SpiritVein.Elements", ResolveElementSummary(eye?.Composition ?? section?.Composition ?? default));
        tooltip.addLineText("Cultiway.SpiritVein.HallArea", ground.HallTileIds.Length.ToString());
        tooltip.addLineText("Cultiway.SpiritVein.Convergence", $"{ground.Convergence * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Shelter", $"{ground.Shelter * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Leakage", $"{ground.Leakage * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Fill", $"{ground.FillRatio * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Purity", $"{ground.Purity * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Pollution", ResolvePollutionName(ground.Purity));
    }

    private static void ShowSpiritVeinEye(
        Tooltip tooltip,
        SpiritVeinManager manager,
        SpiritVeinEye eye)
    {
        if (eye == null)
        {
            tooltip.setTitle("Cultiway.SpiritVein.Type.Eye", string.Empty);
            return;
        }
        SpiritVein vein = manager?.GetVeinByTopologyId(eye.VeinId);
        GatheringGround ground = manager?.GetGround(eye.GroundId);
        tooltip.setTitle(eye.Name, "Cultiway.SpiritVein.Type.Eye", eye.Composition.HexColor());
        tooltip.addLineText("Cultiway.SpiritVein.Manifestation", ResolveManifestation(eye.Manifestation));
        tooltip.addLineText("Cultiway.SpiritVein.Network", vein?.Name ?? "-");
        tooltip.addLineText("Cultiway.SpiritVein.Ground", ground?.Name ?? "-");
        tooltip.addLineText("Cultiway.SpiritVein.Concentration", ResolveConcentration(eye.Concentration));
        tooltip.addLineText("Cultiway.SpiritVein.Elements", ResolveElementSummary(eye.Composition));
        tooltip.addLineText("Cultiway.SpiritVein.Fill", $"{eye.FillRatio * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Purity", $"{eye.Purity * 100f:0.#}%");
        tooltip.addLineText("Cultiway.SpiritVein.Pollution", ResolvePollutionName(eye.Purity));
    }

    private static string ResolveElementSummary(ElementComposition composition)
    {
        int first = 0;
        int second = 1;
        if (composition[second] > composition[first]) (first, second) = (second, first);
        for (int i = 2; i < ElementIndex.Count; i++)
        {
            if (composition[i] <= composition[second]) continue;
            second = i;
            if (composition[second] > composition[first]) (first, second) = (second, first);
        }
        return $"{ElementIndex.ElementNames[first].Localize()} {composition[first] * 100f:0.#}%、" +
               $"{ElementIndex.ElementNames[second].Localize()} {composition[second] * 100f:0.#}%";
    }

    private static string ResolveVeinScale(DragonVeinScale scale)
    {
        return ("Cultiway.SpiritVein.Scale." + scale).Localize();
    }

    private static string ResolveBranchScale(SpiritBranchScale scale)
    {
        return ("Cultiway.SpiritVein.BranchScale." + scale).Localize();
    }

    private static string ResolveGroundQuality(GatheringGroundQuality quality)
    {
        return ("Cultiway.SpiritVein.Quality." + quality).Localize();
    }

    private static string ResolveGroundKindKey(GatheringGroundKind kind)
    {
        return "Cultiway.SpiritVein.GroundKind." + kind;
    }

    private static string ResolveSectionKind(VeinSectionKind kind)
    {
        return ("Cultiway.SpiritVein.SectionKind." + kind).Localize();
    }

    private static string ResolveSectionStatus(VeinSectionStatus status)
    {
        return ("Cultiway.SpiritVein.Status." + status).Localize();
    }

    private static string ResolveManifestation(SpiritEyeManifestation manifestation)
    {
        return ("Cultiway.SpiritVein.Manifestation." + manifestation).Localize();
    }

    private static string ResolveConcentration(SpiritEyeConcentration concentration)
    {
        return ("Cultiway.SpiritVein.Concentration." + concentration).Localize();
    }

    private static string ResolvePollutionName(float purity)
    {
        return purity >= 0.85f
            ? "Cultiway.SpiritVein.Pollution.Clean".Localize()
            : purity >= 0.6f
                ? "Cultiway.SpiritVein.Pollution.Light".Localize()
                : purity >= 0.3f
                    ? "Cultiway.SpiritVein.Pollution.Polluted".Localize()
                    : "Cultiway.SpiritVein.Pollution.Heavy".Localize();
    }

    private static string EmptyAsDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static void ShowCoreFormationEffect(Tooltip tooltip, string type, TooltipData data)
    {
        CoreFormationEffectTooltip.SetupPending(tooltip);
    }
}
