using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.UI.CreatureInfoPages;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.UI.Prefab;

/// <summary>展示功法静态能力，并在人物上下文中追加实际掌握与契合数据。</summary>
public class CultibookTooltip : APrefabPreview<CultibookTooltip>
{
    private static CultibookAsset pendingAsset;
    private static ActorExtend pendingActor;
    private static float pendingMastery;

    public Tooltip Tooltip { get; private set; }

    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
    }

    /// <summary>在指定控件旁显示一部人物已掌握或了解的功法。</summary>
    public static void Show(GameObject source, CultibookAsset asset, ActorExtend actor, float mastery)
    {
        if (source == null || asset == null || actor == null) return;
        pendingAsset = asset;
        pendingActor = actor;
        pendingMastery = Mathf.Clamp(mastery, 0f, 100f);
        try
        {
            global::Tooltip.show(source, Tooltips.Cultibook.id, new TooltipData());
        }
        finally
        {
            pendingAsset = null;
            pendingActor = null;
            pendingMastery = 0f;
        }
    }

    /// <summary>由 Tooltip 回调读取同步人物上下文；没有待展示内容时返回 false。</summary>
    internal bool SetupPending()
    {
        if (pendingAsset == null || pendingActor == null) return false;
        Setup(pendingAsset, pendingActor, pendingMastery);
        return true;
    }

    /// <summary>以物理功法书上下文显示静态功法信息。</summary>
    [Hotfixable]
    public void Setup(Book book)
    {
        Init();
        BookExtend bookExtend = book?.GetExtend();
        if (bookExtend == null || !bookExtend.HasComponent<Cultibook>()) return;
        CultibookAsset asset = bookExtend.GetComponent<Cultibook>().Asset;
        if (asset == null) return;
        Setup(asset, null, 100f);
    }

    private void Setup(CultibookAsset asset, ActorExtend actor, float mastery)
    {
        Init();
        Tooltip.name.text = string.Format(
            "Cultiway.CultibookTooltip.Format.Title".Localize(),
            asset.Name);
        AppendDescription(asset);
        AppendItemLevel(asset.Level);
        if (actor != null) AppendMastery(mastery);
        AppendCultivateMethod(asset);
        AppendLevelRequirements(asset);
        if (actor != null) AppendAffinity(actor, asset);
        AppendStats(asset.FinalStats, actor == null ? 1f : mastery / 100f);
        AppendSkillPool(asset);
    }

    private void AppendDescription(CultibookAsset asset)
    {
        if (!string.IsNullOrEmpty(asset.Description)) Tooltip.addDescription(asset.Description);
    }

    private void AppendItemLevel(ItemLevel level)
    {
        Tooltip.addLineText(
            "Cultiway.CultibookTooltip.Label.ItemLevel".Localize(),
            level.GetName(),
            pLocalize: false,
            pLimitValue: int.MaxValue);
    }

    private void AppendMastery(float mastery)
    {
        Tooltip.addLineText(
            "Cultiway.CultibookTooltip.Label.Mastery".Localize(),
            $"{Mathf.Clamp(mastery, 0f, 100f):0.#}%",
            pLocalize: false,
            pLimitValue: int.MaxValue);
    }

    private void AppendCultivateMethod(CultibookAsset asset)
    {
        CultivateMethodAsset method = asset.GetCultivateMethod();
        if (method == null) return;
        Tooltip.addLineText(
            "Cultiway.CultibookTooltip.Label.Method".Localize(),
            method.id.Localize(),
            pLocalize: false,
            pLimitValue: int.MaxValue);
    }

    private void AppendLevelRequirements(CultibookAsset asset)
    {
        string value;
        if (asset.MinLevel <= 0 && asset.MaxLevel >= 20)
        {
            value = "Cultiway.CultibookTooltip.Realm.All".Localize();
        }
        else if (asset.MinLevel <= 0)
        {
            value = string.Format(
                "Cultiway.CultibookTooltip.Realm.AtMost".Localize(),
                Cultisyses.Xian.GetLevelName(asset.MaxLevel));
        }
        else if (asset.MaxLevel >= 20)
        {
            value = string.Format(
                "Cultiway.CultibookTooltip.Realm.AtLeast".Localize(),
                Cultisyses.Xian.GetLevelName(asset.MinLevel));
        }
        else
        {
            value = string.Format(
                "Cultiway.CultibookTooltip.Realm.Range".Localize(),
                Cultisyses.Xian.GetLevelName(asset.MinLevel),
                Cultisyses.Xian.GetLevelName(asset.MaxLevel));
        }

        Tooltip.addLineText(
            "Cultiway.CultibookTooltip.Label.Realm".Localize(),
            value,
            pLocalize: false,
            pLimitValue: int.MaxValue);
    }

    private void AppendAffinity(ActorExtend actor, CultibookAsset asset)
    {
        string value = CultibookPagePresentation.TryResolveAffinity(actor, asset, out float affinity)
            ? CultibookPagePresentation.FormatPercent(affinity)
            : "Cultiway.CultibookPage.Value.NoRoot".Localize();
        Tooltip.addLineText(
            "Cultiway.CultibookTooltip.Label.Affinity".Localize(),
            value,
            pLocalize: false,
            pLimitValue: int.MaxValue);
    }

    private void AppendStats(BaseStats finalStats, float ratio)
    {
        List<string> lines = BuildStatLines(finalStats, Mathf.Clamp01(ratio));
        AddLineGroup("Cultiway.CultibookTooltip.Section.Attributes".Localize(), lines);
    }

    private void AppendSkillPool(CultibookAsset asset)
    {
        if (asset.SkillPool == null || asset.SkillPool.Count == 0) return;
        var lines = new List<string>();
        foreach (SkillPoolEntry entry in asset.SkillPool)
        {
            if (entry.SkillContainer.IsNull || !entry.SkillContainer.HasComponent<SkillContainer>()) continue;
            ref SkillContainer container = ref entry.SkillContainer.GetComponent<SkillContainer>();
            string skillId = container.SkillEntityAssetID;
            if (string.IsNullOrEmpty(skillId)) continue;
            string skillName = entry.SkillContainer.HasName
                ? entry.SkillContainer.Name.value
                : skillId.Localize();
            var requirements = new List<string>();
            if (entry.MasteryThreshold > 0f)
            {
                requirements.Add(string.Format(
                    "Cultiway.CultibookTooltip.Skill.MasteryRequirement".Localize(),
                    entry.MasteryThreshold));
            }
            if (entry.LevelRequirement > 0)
            {
                requirements.Add(string.Format(
                    "Cultiway.CultibookTooltip.Skill.RealmRequirement".Localize(),
                    Cultisyses.Xian.GetLevelName(entry.LevelRequirement)));
            }
            lines.Add(requirements.Count == 0
                ? skillName
                : string.Format(
                    "Cultiway.CultibookTooltip.Format.SkillWithRequirements".Localize(),
                    skillName,
                    string.Join(
                        "Cultiway.CultibookTooltip.Format.RequirementSeparator".Localize(),
                        requirements)));
        }
        AddLineGroup("Cultiway.CultibookTooltip.Section.Skills".Localize(), lines);
    }

    private static List<string> BuildStatLines(BaseStats finalStats, float ratio)
    {
        var lines = new List<string>();
        foreach (CultisysPresentation.StatBonus stat in
                 CultisysPresentation.BuildStatBonuses(finalStats, ratio))
            lines.Add(string.Format(
                "Cultiway.CultibookTooltip.Format.Stat".Localize(),
                stat.Name,
                stat.Value));
        return lines;
    }

    private void AddLineGroup(string title, List<string> lines)
    {
        if (lines == null || lines.Count == 0) return;
        Tooltip.addLineText(title, lines[0], pLocalize: false, pLimitValue: int.MaxValue);
        for (var i = 1; i < lines.Count; i++)
            Tooltip.addLineText(string.Empty, lines[i], pLocalize: false, pLimitValue: int.MaxValue);
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);
        Prefab = obj.AddComponent<CultibookTooltip>();
    }
}
