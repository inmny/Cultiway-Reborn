using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物信息窗口中的妖兽详情页：境界、妖力、身体、血脉与消化情况。</summary>
public sealed class YaoPage : MonoBehaviour
{
    private Text summary;
    private Text bodyText;
    private Text bloodlineText;
    private Text digestionText;

    /// <summary>用纵向布局创建详情页，文本自上而下排列，不做手工定位。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<YaoPage>();
        GameObject root = UiLayout.Create(
            page.transform, "Yao Content", false,
            XianRealmPagePresentation.PageWidth, XianRealmPagePresentation.PageHeight, 4f);

        component.summary = UiElements.CreateText(
            root.transform, "YaoSummary", string.Empty,
            XianRealmPagePresentation.PageWidth, 32f, 7, TextAnchor.UpperLeft);
        component.summary.verticalOverflow = VerticalWrapMode.Overflow;

        component.bodyText = UiElements.CreateText(
            root.transform, "YaoBody", string.Empty,
            XianRealmPagePresentation.PageWidth, 76f, 6, TextAnchor.UpperLeft);
        component.bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        component.bloodlineText = UiElements.CreateText(
            root.transform, "YaoBloodline", string.Empty,
            XianRealmPagePresentation.PageWidth, 42f, 6, TextAnchor.UpperLeft);
        component.bloodlineText.verticalOverflow = VerticalWrapMode.Overflow;

        component.digestionText = UiElements.CreateText(
            root.transform, "YaoDigestion", string.Empty,
            XianRealmPagePresentation.PageWidth, 42f, 6, TextAnchor.UpperLeft);
        component.digestionText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>按角色当前妖修数据刷新全部区域。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        YaoPage component = page.GetComponent<YaoPage>();
        ActorExtend extend = actor.GetExtend();
        if (!extend.HasCultisys<Yao>())
        {
            component.summary.text = "Cultiway.YaoPage.NotYao".Localize();
            component.bodyText.text = string.Empty;
            component.bloodlineText.text = string.Empty;
            component.digestionText.text = string.Empty;
            return;
        }

        ref Yao yao = ref extend.GetCultisys<Yao>();
        float maxPower = actor.stats[BaseStatses.MaxYaoPower.id];
        component.summary.text = string.Format("Cultiway.YaoPage.Summary".Localize(),
            Cultisyses.Yao.GetLevelName(yao.CurrLevel),
            $"{yao.yao_power:0}/{maxPower:0}",
            yao.BodyStability.ToString("0"),
            yao.OrganCapacityBonus);

        component.bodyText.text = BuildBodyText(extend);
        component.bloodlineText.text = BuildBloodlineText(extend);
        component.digestionText.text = BuildDigestionText(extend);
    }

    /// <summary>按身体位置列出当前形态的器官、等级与来源。</summary>
    private static string BuildBodyText(ActorExtend extend)
    {
        if (!extend.E.TryGetComponent(out YaoBody body) ||
            !body.TryGetActiveForm(out YaoFormRecord form) ||
            form.Organs == null || form.Organs.Length == 0)
        {
            return "Cultiway.YaoPage.BodyEmpty".Localize();
        }

        var text = new System.Text.StringBuilder();
        text.AppendLine("Cultiway.YaoPage.BodyHeader".Localize());
        foreach (YaoOrganRecord organ in form.Organs)
        {
            text.AppendLine($"· {YaoOrganName(organ.OrganId)} x{organ.Rank}");
        }

        return text.ToString();
    }

    /// <summary>汇总主血脉、隐性血脉、纯度与代数。</summary>
    private static string BuildBloodlineText(ActorExtend extend)
    {
        if (!extend.E.TryGetComponent(out YaoGenome genome) ||
            string.IsNullOrEmpty(genome.PrimaryBloodlineId))
        {
            return "Cultiway.YaoPage.BloodlineEmpty".Localize();
        }

        string hidden = string.IsNullOrEmpty(genome.HiddenBloodlineId)
            ? "--"
            : YaoBloodlineName(genome.HiddenBloodlineId);
        return string.Format("Cultiway.YaoPage.BloodlineFormat".Localize(),
            YaoBloodlineName(genome.PrimaryBloodlineId),
            $"{genome.PrimaryPurity:P0}",
            hidden,
            genome.GenomeGeneration);
    }

    /// <summary>汇总消化队列与妖丹状态。</summary>
    private static string BuildDigestionText(ActorExtend extend)
    {
        var text = new System.Text.StringBuilder();
        if (extend.E.TryGetComponent(out YaoDigestion digestion))
        {
            digestion.EnsureInitialized();
            int active = 0;
            foreach (YaoDigestionEntry entry in digestion.Queue)
            {
                if (entry.IsEmpty || entry.Phase is YaoDigestionPhase.Resolved or YaoDigestionPhase.Rejected) continue;
                active++;
            }

            text.AppendLine(string.Format("Cultiway.YaoPage.DigestionFormat".Localize(),
                active, YaoDigestion.QueueSize));
        }

        if (extend.E.TryGetComponent(out YaoCore core))
        {
            text.AppendLine(string.Format("Cultiway.YaoPage.CoreFormat".Localize(),
                YaoCoreService.GetPatternName(core.CorePatternId),
                core.Quality.ToString("0"),
                core.Cracks));
        }

        return text.ToString();
    }

    private static string YaoOrganName(string organId)
    {
        return $"Cultiway.CreatureOrgan.{organId}".Localize();
    }

    private static string YaoBloodlineName(string bloodlineId)
    {
        return YaoBloodlines.TryGet(bloodlineId, out YaoBloodlineAsset bloodline)
            ? bloodline.NameKey.Localize()
            : bloodlineId;
    }
}
